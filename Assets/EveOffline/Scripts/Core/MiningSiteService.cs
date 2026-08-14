using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EveOffline
{
    /// <summary>
    /// Owns the persistent mining-site world. Static belts keep their mined
    /// state until the next daily 11:00 UTC downtime. Dynamic anomalies never
    /// reset at downtime; after depletion they receive their own data-driven
    /// respawn timer. Exact asteroid layouts and unpublished anomaly odds are
    /// authored approximations, while the lifecycle rules remain explicit.
    /// </summary>
    public static class MiningSiteService
    {
        const int DowntimeHourUtc = 11;
        const int MaxOverdueAnomalyAttemptsPerTick = 4096;

        sealed class SiteIndex
        {
            public List<MiningSiteStateSave> Source;
            public int Count = -1;
            public long LastTickUnix = long.MinValue;
            public bool Normalized;
            public bool DeadlinesDirty = true;
            public long DeadlineCacheUnix = long.MinValue;
            public long NextEventUnix = long.MaxValue;
            public List<AsteroidSave> ActiveAsteroidSource;
            public string ActiveGradeLocationId = string.Empty;
            public int ActiveGradeInstanceSerial;
            public readonly Dictionary<string, MiningSiteStateSave> ByLocationId = new(StringComparer.OrdinalIgnoreCase);
        }

        static readonly ConditionalWeakTable<GameSave, SiteIndex> Indexes = new();
        static readonly LocationDefinition[] DynamicLocations = Catalog.Locations
            .Where(location => location.SiteKind == MiningSiteKind.DynamicAnomaly)
            .ToArray();
        static readonly LocationDefinition[] OrderedStaticBelts = Catalog.Locations
            .Where(location => location.SiteKind == MiningSiteKind.StaticBelt)
            .OrderBy(location => SecurityTierTenths(location))
            .ThenBy(location => location.SystemName, StringComparer.Ordinal)
            .ThenBy(location => location.SystemId)
            .ThenBy(location => location.BeltName, StringComparer.Ordinal)
            .ThenBy(location => location.BeltId)
            .ThenBy(location => location.Id, StringComparer.Ordinal)
            .ToArray();

        /// <summary>Displayed security rounded to tenths, represented as an integer.</summary>
        public static int SecurityTierTenths(LocationDefinition location) => location == null ? int.MinValue : SecurityTierTenths(location.Security);
        public static int SecurityTierTenths(float security) => (int)Math.Round(security * 10d, MidpointRounding.AwayFromZero);

        public static void Normalize(GameSave save, long nowUnix = 0)
        {
            if (save == null) return;
            nowUnix = ResolveNow(nowUnix);
            save.MiningSites ??= new List<MiningSiteStateSave>();
            save.MiningSites.RemoveAll(state => state == null || Catalog.GetLocation(state.LocationId) == null);
            RebuildIndex(save);

            // Static belts are pristine by default and become persisted only when
            // opened/mined. Dynamic anomalies need eager state because their
            // availability and independent cooldown advance while unobserved.
            foreach (var state in save.MiningSites)
            {
                var location = Catalog.GetLocation(state.LocationId);
                if (location != null) NormalizeState(state, location, nowUnix);
            }
            foreach (var location in DynamicLocations)
            {
                var state = FindState(save, location.Id);
                if (state == null)
                {
                    state = CreateInitialState(location, nowUnix);
                    AddState(save, state);
                }
            }

            RebuildIndex(save);
            var index = GetIndex(save);
            index.Normalized = true;
            MarkDeadlinesDirty(index);
        }

        public static void Tick(GameSave save, long nowUnix = 0)
        {
            if (save == null) return;
            nowUnix = ResolveNow(nowUnix);
            var index = EnsureNormalized(save, nowUnix);
            if (index.LastTickUnix > nowUnix)
            {
                // Explicit simulated-time callers may rewind between independent
                // audits. Rebuild only the deadline cache; serialized state is
                // never rewound.
                index.LastTickUnix = long.MinValue;
                MarkDeadlinesDirty(index);
            }

            if (index.LastTickUnix != nowUnix || index.DeadlinesDirty)
            {
                EnsureDeadlineCache(save, index, nowUnix);
                if (index.NextEventUnix <= nowUnix)
                {
                    foreach (var state in save.MiningSites)
                    {
                        var location = Catalog.GetLocation(state?.LocationId);
                        if (location == null) continue;
                        if (location.SiteKind == MiningSiteKind.StaticBelt)
                        {
                            if (state.NextRefreshUnix <= nowUnix)
                                RefreshStatic(state, location, nowUnix);
                            continue;
                        }
                        var attempts = 0;
                        while (state.Lifecycle == MiningSiteLifecycle.Cooldown &&
                               state.RespawnUnix > 0 && state.RespawnUnix <= nowUnix &&
                               attempts++ < MaxOverdueAnomalyAttemptsPerTick)
                        {
                            var eventUnix = state.RespawnUnix;
                            RespawnAnomaly(state, location, eventUnix);
                            if (state.Lifecycle == MiningSiteLifecycle.Cooldown && state.RespawnUnix <= eventUnix)
                            {
                                // A malformed or future data rule must never
                                // leave Tick spinning on a non-forward deadline.
                                StartAnomalyCooldown(state, location, nowUnix);
                                break;
                            }
                        }
                        if (state.Lifecycle == MiningSiteLifecycle.Cooldown &&
                            state.RespawnUnix > 0 && state.RespawnUnix <= nowUnix)
                        {
                            // Extremely long gaps or pathological bad-luck
                            // streaks are bounded. Keep the serialized serial and
                            // move its next attempt safely beyond this Tick.
                            StartAnomalyCooldown(state, location, nowUnix);
                        }
                    }
                    MarkDeadlinesDirty(index);
                    EnsureDeadlineCache(save, index, nowUnix);
                }
                index.LastTickUnix = nowUnix;
            }

            // A downtime/cooldown may have changed the current site's backing
            // instance while the simulator was open or offline.
            var operation = save.Operation;
            if (operation?.Active == true && !operation.BeltWarpActive && !operation.TravelActive)
            {
                var location = Catalog.GetLocation(operation.LocationId);
                var active = location == null ? null : EnsureState(save, location, nowUnix);
                if (active != null && operation.SiteInstanceSerial != active.InstanceSerial)
                {
                    // If downtime refreshed the exhausted endpoint of an
                    // automatic route, honour the global ordering again. Load
                    // in place only when this same belt is now the first eligible
                    // destination; otherwise keep the depleted operation serial
                    // until OperationService starts the route to the earlier belt.
                    var firstAfterRefresh = operation.AutoNextBelt &&
                                            location.SiteKind == MiningSiteKind.StaticBelt &&
                                            RemainingM3(operation.Asteroids) <= 0
                        ? FirstAutomaticBelt(save, operation, location, true)
                        : null;
                    var deferToAutomaticRoute = firstAfterRefresh != null &&
                                                !string.Equals(firstAfterRefresh.Id, location.Id, StringComparison.OrdinalIgnoreCase);
                    if (!deferToAutomaticRoute)
                    {
                        CopyAsteroids(active.Asteroids, operation.Asteroids);
                        operation.SiteInstanceSerial = active.InstanceSerial;
                        ResetTargets(operation);
                    }
                }
                EnsureActiveOreGrades(index, operation, location);
            }
        }

        /// <summary>
        /// Earliest serialized world event after processing everything due at
        /// <paramref name="nowUnix"/>. The cache is invalidated only when a site
        /// is added or its lifecycle deadline changes, so active mining can ask
        /// every few seconds without walking hundreds of persisted belts.
        /// </summary>
        public static long NextWorldEventUnix(GameSave save, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            if (save == null) return NextStaticRefreshUnix(nowUnix);
            Tick(save, nowUnix);
            var index = EnsureNormalized(save, nowUnix);
            EnsureDeadlineCache(save, index, nowUnix);
            return index.NextEventUnix;
        }

        public static MiningSiteStateSave GetState(GameSave save, string locationId)
        {
            if (save == null || string.IsNullOrWhiteSpace(locationId)) return null;
            Tick(save);
            var location = Catalog.GetLocation(locationId);
            return location == null ? null : EnsureState(save, location, ResolveNow(0));
        }

        public static bool IsAvailable(GameSave save, string locationId, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            Tick(save, nowUnix);
            var location = Catalog.GetLocation(locationId);
            if (location == null) return false;
            var state = FindState(save, locationId);
            return IsAvailableWithoutTick(location, state);
        }

        public static string AvailabilityText(GameSave save, string locationId, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            Tick(save, nowUnix);
            var location = Catalog.GetLocation(locationId);
            var state = FindState(save, locationId);
            if (location == null) return "недоступно";
            if (IsAvailableWithoutTick(location, state))
                return location.SiteKind == MiningSiteKind.StaticBelt ? "доступен" : "аномалия доступна";
            var seconds = SecondsUntilAvailable(save, locationId, nowUnix);
            var eta = seconds < 0 ? "время неизвестно" : FormatDuration(seconds);
            return location.SiteKind == MiningSiteKind.StaticBelt
                ? $"исчерпан • восстановление через {eta} (11:00 UTC)"
                : $"новая аномалия примерно через {eta}";
        }

        public static long NextStaticRefreshUnix(long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            var now = DateTimeOffset.FromUnixTimeSeconds(nowUnix).UtcDateTime;
            var candidate = new DateTime(now.Year, now.Month, now.Day, DowntimeHourUtc, 0, 0, DateTimeKind.Utc);
            if (candidate <= now) candidate = candidate.AddDays(1);
            return new DateTimeOffset(candidate).ToUnixTimeSeconds();
        }

        public static long SecondsUntilAvailable(GameSave save, string locationId, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            Tick(save, nowUnix);
            var location = Catalog.GetLocation(locationId);
            var state = FindState(save, locationId);
            if (location == null) return -1;
            if (IsAvailableWithoutTick(location, state)) return 0;
            if (state == null) return -1;
            var target = location.SiteKind == MiningSiteKind.StaticBelt ? state.NextRefreshUnix : state.RespawnUnix;
            return target <= 0 ? -1 : Math.Max(0, target - nowUnix);
        }

        public static bool TryGetNextBeltInSystem(GameSave save, string currentLocationId, out LocationDefinition next)
        {
            next = null;
            var current = Catalog.GetLocation(currentLocationId);
            if (save == null || current == null) return false;
            Tick(save);
            var belts = OrderedStaticBelts.Where(location => location.SystemId == current.SystemId).ToList();
            var currentIndex = belts.FindIndex(location => string.Equals(location.Id, current.Id, StringComparison.OrdinalIgnoreCase));
            for (var offset = 1; offset <= belts.Count; offset++)
            {
                var candidate = belts[(Math.Max(-1, currentIndex) + offset) % belts.Count];
                if (string.Equals(candidate.Id, current.Id, StringComparison.OrdinalIgnoreCase)) continue;
                var state = FindState(save, candidate.Id);
                if (IsAvailableWithoutTick(candidate, state))
                {
                    next = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Chooses the next ordinary belt for roaming automation. Anomalies are
        /// intentionally excluded. Selection is global, deterministic, honours
        /// the player's manually committed security floor and requires every
        /// member of the active fleet to support the destination.
        /// </summary>
        public static bool TryGetNextAutomaticBelt(GameSave save, out LocationDefinition next, long nowUnix = 0)
        {
            next = null;
            var operation = save?.Operation;
            var current = Catalog.GetLocation(operation?.LocationId);
            if (operation?.Active != true || current == null || operation.Fleet == null || operation.Fleet.Count == 0) return false;
            nowUnix = ResolveNow(nowUnix);
            Tick(save, nowUnix);
            next = FirstAutomaticBelt(save, operation, current, false);
            return next != null;
        }

        public static void SnapshotActiveOperation(GameSave save, long nowUnix = 0)
        {
            var operation = save?.Operation;
            if (operation?.Active != true || string.IsNullOrWhiteSpace(operation.LocationId)) return;
            nowUnix = ResolveNow(nowUnix);
            EnsureNormalized(save, nowUnix);
            var location = Catalog.GetLocation(operation.LocationId);
            var state = location == null ? null : EnsureState(save, location, nowUnix);
            SnapshotActiveOperation(save, operation, location, state, nowUnix);
        }

        /// <summary>
        /// Persists the exact depletion event for the currently open dynamic
        /// anomaly. Offline replay calls this at the end of the mining step that
        /// removed the last resource, so its independent respawn timer starts at
        /// simulated event time rather than at the next real save. Ordinary
        /// belts and anomaly routing are deliberately unaffected.
        /// </summary>
        public static bool TrySnapshotActiveDynamicDepletion(GameSave save, long nowUnix = 0)
        {
            var operation = save?.Operation;
            var location = Catalog.GetLocation(operation?.LocationId);
            if (operation?.Active != true || operation.TravelActive || operation.BeltWarpActive ||
                location?.SiteKind != MiningSiteKind.DynamicAnomaly || RemainingM3(operation.Asteroids) > 0) return false;

            nowUnix = ResolveNow(nowUnix);
            EnsureNormalized(save, nowUnix);
            var state = EnsureState(save, location, nowUnix);
            if (state == null ||
                (state.Lifecycle == MiningSiteLifecycle.Cooldown && RemainingM3(state.Asteroids) <= 0))
                return false;
            return SnapshotActiveOperation(save, operation, location, state, nowUnix);
        }

        static bool SnapshotActiveOperation(
            GameSave save,
            OperationSave operation,
            LocationDefinition location,
            MiningSiteStateSave state,
            long nowUnix)
        {
            if (save == null || operation == null || location == null || state == null) return false;
            if (operation.SiteInstanceSerial <= 0)
                operation.SiteInstanceSerial = state.InstanceSerial;
            else if (operation.SiteInstanceSerial != state.InstanceSerial)
                return false;
            ApplyOreGrades(operation.Asteroids, location, operation.SiteInstanceSerial);
            var operationRemaining = RemainingM3(operation.Asteroids);
            if (operationRemaining <= 0 && state.Lifecycle == MiningSiteLifecycle.Cooldown &&
                RemainingM3(state.Asteroids) <= 0) return false;
            CopyAsteroids(operation.Asteroids, state.Asteroids);
            if (operationRemaining > 0)
            {
                state.Lifecycle = MiningSiteLifecycle.Available;
                if (location.SiteKind == MiningSiteKind.DynamicAnomaly) state.RespawnUnix = 0;
                MarkDeadlinesDirty(save);
                return true;
            }
            if (location.SiteKind == MiningSiteKind.StaticBelt)
            {
                state.Lifecycle = MiningSiteLifecycle.Cooldown;
                // Keep an already persisted boundary, including an overdue one:
                // Tick must still observe it and perform the missed 11:00 UTC
                // refresh instead of Snapshot postponing it to tomorrow.
                if (state.NextRefreshUnix <= 0)
                    state.NextRefreshUnix = NextStaticRefreshUnix(nowUnix);
                MarkDeadlinesDirty(save);
                return true;
            }
            if (state.Lifecycle != MiningSiteLifecycle.Cooldown)
                StartAnomalyCooldown(state, location, nowUnix);
            MarkDeadlinesDirty(save);
            return true;
        }

        public static void LoadIntoOperation(GameSave save, LocationDefinition location, long nowUnix = 0)
        {
            if (save?.Operation == null || location == null) return;
            nowUnix = ResolveNow(nowUnix);
            Tick(save, nowUnix);
            var state = EnsureState(save, location, nowUnix);
            save.Operation.Asteroids ??= new List<AsteroidSave>();
            CopyAsteroids(state?.Asteroids, save.Operation.Asteroids);
            save.Operation.SiteInstanceSerial = state?.InstanceSerial ?? 0;
        }

        /// <summary>
        /// v12 eagerly serialized every untouched static belt. Removing only an
        /// fully deterministic pristine layout keeps mined,
        /// depleted, refreshed and active site state exact while shrinking the
        /// v13 save to the sites that actually carry information.
        /// </summary>
        public static int MigrateV13CompactPristineStaticStates(GameSave save)
        {
            if (save?.MiningSites == null || save.MiningSites.Count == 0) return 0;
            var protectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var operation = save.Operation;
            if (!string.IsNullOrWhiteSpace(operation?.LocationId)) protectedIds.Add(operation.LocationId);
            if (!string.IsNullOrWhiteSpace(operation?.TravelSourceLocationId)) protectedIds.Add(operation.TravelSourceLocationId);
            if (!string.IsNullOrWhiteSpace(operation?.TravelDestinationLocationId)) protectedIds.Add(operation.TravelDestinationLocationId);
            if (!string.IsNullOrWhiteSpace(operation?.BeltWarpDestinationLocationId)) protectedIds.Add(operation.BeltWarpDestinationLocationId);

            var removed = 0;
            for (var index = save.MiningSites.Count - 1; index >= 0; index--)
            {
                var state = save.MiningSites[index];
                var location = Catalog.GetLocation(state?.LocationId);
                if (location?.SiteKind != MiningSiteKind.StaticBelt || protectedIds.Contains(location.Id) ||
                    !IsPristineStaticState(state, location)) continue;
                save.MiningSites.RemoveAt(index);
                removed++;
            }
            RebuildIndex(save);
            return removed;
        }

        public static double RemainingM3(IEnumerable<AsteroidSave> asteroids)
        {
            return asteroids?.Sum(asteroid => OperationService.AsteroidVolumeM3(asteroid)) ?? 0d;
        }

        /// <summary>
        /// v11 used an authored deterministic 6-8 hour Clear Icicle cooldown.
        /// Recover its original depletion time from that deterministic duration,
        /// then move the pending respawn to the current exact six-hour rule.
        /// Available sites and all asteroid/runtime state remain untouched.
        /// </summary>
        public static void MigrateV11ClearIcicleCooldown(GameSave save)
        {
            var state = save?.MiningSites?.FirstOrDefault(candidate =>
                string.Equals(candidate?.LocationId, "manatirid-clear-icicle", StringComparison.OrdinalIgnoreCase));
            if (state == null || state.Lifecycle != MiningSiteLifecycle.Cooldown || state.RespawnUnix <= 0) return;
            var oldDuration = DeterministicRangeSeconds(state.LocationId, state.InstanceSerial, 6 * 60 * 60, 8 * 60 * 60);
            var depletedUnix = state.RespawnUnix - oldDuration;
            state.RespawnUnix = depletedUnix + 6 * 60 * 60;
            MarkDeadlinesDirty(save);
        }

        static MiningSiteStateSave CreateInitialState(LocationDefinition location, long nowUnix)
        {
            var state = new MiningSiteStateSave
            {
                LocationId = location.Id,
                Lifecycle = MiningSiteLifecycle.Available,
                InstanceSerial = 1,
                LastRefreshUnix = PreviousStaticRefreshUnix(nowUnix),
                NextRefreshUnix = location.SiteKind == MiningSiteKind.StaticBelt ? NextStaticRefreshUnix(nowUnix) : 0,
                Asteroids = new List<AsteroidSave>()
            };
            if (location.SiteKind == MiningSiteKind.DynamicAnomaly && !InitialAnomalyAvailable(location, nowUnix))
                StartAnomalyCooldown(state, location, nowUnix);
            else
                Generate(state, location);
            return state;
        }

        static void NormalizeState(MiningSiteStateSave state, LocationDefinition location, long nowUnix)
        {
            state.LocationId = location.Id;
            state.Asteroids ??= new List<AsteroidSave>();
            state.InstanceSerial = Math.Max(1, state.InstanceSerial);
            if (location.SiteKind == MiningSiteKind.StaticBelt)
            {
                state.RespawnUnix = 0;
                if (state.NextRefreshUnix <= 0) state.NextRefreshUnix = NextStaticRefreshUnix(nowUnix);
                if (state.LastRefreshUnix <= 0) state.LastRefreshUnix = PreviousStaticRefreshUnix(nowUnix);
                if (state.Asteroids.Count == 0 && state.Lifecycle == MiningSiteLifecycle.Available) Generate(state, location);
                ApplyOreGrades(state.Asteroids, location, state.InstanceSerial);
                return;
            }
            state.NextRefreshUnix = 0;
            if (state.Lifecycle == MiningSiteLifecycle.Available && state.Asteroids.Count == 0) Generate(state, location);
            if (state.Lifecycle == MiningSiteLifecycle.Cooldown && state.RespawnUnix <= 0) StartAnomalyCooldown(state, location, nowUnix);
            ApplyOreGrades(state.Asteroids, location, state.InstanceSerial);
        }

        static void RefreshStatic(MiningSiteStateSave state, LocationDefinition location, long nowUnix)
        {
            var refresh = state.NextRefreshUnix;
            while (refresh <= nowUnix) refresh += 24 * 60 * 60;
            state.InstanceSerial++;
            state.LastRefreshUnix = refresh - 24 * 60 * 60;
            state.NextRefreshUnix = refresh;
            state.RespawnUnix = 0;
            state.Lifecycle = MiningSiteLifecycle.Available;
            Generate(state, location);
        }

        static void RespawnAnomaly(MiningSiteStateSave state, LocationDefinition location, long nowUnix)
        {
            state.InstanceSerial++;
            if (!AnomalyAttemptSucceeds(location, state.InstanceSerial, nowUnix))
            {
                StartAnomalyCooldown(state, location, nowUnix);
                return;
            }
            state.LastRefreshUnix = nowUnix;
            state.RespawnUnix = 0;
            state.Lifecycle = MiningSiteLifecycle.Available;
            Generate(state, location);
        }

        static void StartAnomalyCooldown(MiningSiteStateSave state, LocationDefinition location, long nowUnix)
        {
            state.Lifecycle = MiningSiteLifecycle.Cooldown;
            state.Asteroids?.Clear();
            state.RespawnUnix = nowUnix + DeterministicRangeSeconds(location, state.InstanceSerial);
            state.NextRefreshUnix = 0;
        }

        static void Generate(MiningSiteStateSave state, LocationDefinition location)
        {
            state.Asteroids ??= new List<AsteroidSave>();
            state.Asteroids.Clear();
            var seed = StableHash(location.Id) ^ (state.InstanceSerial * 7919);
            var random = new Random(seed);
            if (location.OreIds?.Length > 0 && location.OreIds.All(id => Catalog.GetOre(id)?.Kind == ResourceKind.Gas))
            {
                for (var i = 0; i < location.OreIds.Length; i++)
                {
                    var resource = Catalog.GetOre(location.OreIds[i]);
                    if (resource == null) continue;
                    state.Asteroids.Add(new AsteroidSave { Id = $"G-{state.InstanceSerial}-{i:00}", OreId = resource.Id, RemainingUnits = resource.DefaultAsteroidUnits, X = -16 + i * 32, Y = 0, Z = 28 + i * 8, Scale = 5 + i });
                }
                return;
            }
            if (location.OreIds == null || location.OreIds.Length == 0) return;
            var asteroidCount = random.Next(location.AuthoredAsteroidCountMin, location.AuthoredAsteroidCountMax + 1);
            for (var i = 0; i < asteroidCount; i++)
            {
                var oreId = i < location.OreIds.Length ? location.OreIds[i] : location.OreIds[random.Next(location.OreIds.Length)];
                var ore = Catalog.GetOre(oreId);
                if (ore == null) continue;
                var asteroidId = $"A-{state.InstanceSerial}-{i:00}";
                ore = SelectSpawnVariant(ore, location, state.InstanceSerial, asteroidId);
                var amountScale = location.ResourceLayoutApproximate ? .8 + random.NextDouble() * .4 : .35 + random.NextDouble() * .9;
                state.Asteroids.Add(new AsteroidSave { Id = asteroidId, OreId = ore.Id, RemainingUnits = ore.DefaultAsteroidUnits * amountScale, X = -35 + (float)random.NextDouble() * 70, Y = -6 + (float)random.NextDouble() * 12, Z = 8 + (float)random.NextDouble() * 62, Scale = 2.2f + (float)random.NextDouble() * 3.8f });
            }
        }

        static void ApplyOreGrades(IEnumerable<AsteroidSave> asteroids, LocationDefinition location, int instanceSerial)
        {
            if (asteroids == null || location == null) return;
            foreach (var asteroid in asteroids.Where(item => item != null))
            {
                var source = Catalog.GetOre(asteroid.OreId);
                var variant = SelectSpawnVariant(source, location, instanceSerial, asteroid.Id);
                if (variant != null) asteroid.OreId = variant.Id;
            }
        }

        static OreDefinition SelectSpawnVariant(OreDefinition source, LocationDefinition location, int instanceSerial, string asteroidId)
        {
            // Ice and gas do not use the asteroid-ore grade family. The managed
            // rookie site is also deliberately base-only; Grade 0 is reserved
            // catalog data and is never selected by the current world generator.
            if (source == null || source.Kind != ResourceKind.Ore) return source;
            if (string.Equals(location?.Id, "uitra-managed-mining-site", StringComparison.OrdinalIgnoreCase))
                return Catalog.GetOreVariant(Catalog.GetOreFamilyId(source), 1) ?? source;

            var rollHash = unchecked(StableHash(location?.Id) ^ instanceSerial * 15485863 ^ StableHash(asteroidId) * 32452843);
            var roll = (uint)rollHash % 1_000_000 / 1_000_000d;
            var grade = location?.Band switch
            {
                SecurityBand.HighSec => roll < .70 ? 1 : roll < .95 ? 2 : 3,
                SecurityBand.LowSec => roll < .35 ? 1 : roll < .70 ? 2 : roll < .95 ? 3 : 4,
                _ => roll < .10 ? 1 : roll < .35 ? 2 : roll < .75 ? 3 : 4
            };
            if (location?.SiteKind == MiningSiteKind.DynamicAnomaly) grade = Math.Min(4, grade + 1);
            if (Catalog.IsMercoxitFamily(source)) grade = Math.Min(3, grade);
            return Catalog.GetOreVariant(Catalog.GetOreFamilyId(source), Math.Max(1, grade)) ?? source;
        }

        static bool InitialAnomalyAvailable(LocationDefinition location, long nowUnix)
        {
            if (location.AnomalyInitialSpawnChance >= 1f) return true;
            var day = nowUnix / (24 * 60 * 60);
            var roll = (uint)(StableHash(location.Id) ^ (int)day) % 10_000 / 10_000f;
            return roll < location.AnomalyInitialSpawnChance;
        }

        static bool AnomalyAttemptSucceeds(LocationDefinition location, int serial, long nowUnix)
        {
            if (location.AnomalyInitialSpawnChance >= 1f) return true;
            var roll = (uint)(StableHash(location.Id) ^ serial * 104729 ^ (int)(nowUnix / 60)) % 10_000 / 10_000f;
            return roll < location.AnomalyInitialSpawnChance;
        }

        static int DeterministicRangeSeconds(LocationDefinition location, int serial)
        {
            var min = Math.Max(1, (int)Math.Round(location.AnomalyRespawnMinSeconds));
            var max = Math.Max(min, (int)Math.Round(location.AnomalyRespawnMaxSeconds));
            return DeterministicRangeSeconds(location.Id, serial, min, max);
        }

        static int DeterministicRangeSeconds(string locationId, int serial, int min, int max)
        {
            var span = max - min + 1;
            return min + (int)((uint)(StableHash(locationId) ^ serial * 48611) % span);
        }

        static bool IsAvailableWithoutTick(LocationDefinition location, MiningSiteStateSave state)
        {
            if (location == null) return false;
            if (state == null)
                return location.SiteKind == MiningSiteKind.StaticBelt && PristineStaticHasResources(location);
            return state.Lifecycle == MiningSiteLifecycle.Available && RemainingM3(state.Asteroids) > 0;
        }

        static bool PristineStaticHasResources(LocationDefinition location)
        {
            return location?.SiteKind == MiningSiteKind.StaticBelt &&
                   location.AuthoredAsteroidCountMax > 0 &&
                   location.OreIds?.Any(oreId =>
                   {
                       var ore = Catalog.GetOre(oreId);
                       return ore != null && ore.DefaultAsteroidUnits > 0 && ore.UnitVolumeM3 > 0;
                   }) == true;
        }

        static bool FleetSupportsLocation(GameSave save, OperationSave operation, LocationDefinition location)
        {
            if (save == null || operation?.Fleet == null || operation.Fleet.Count == 0 || location == null) return false;
            foreach (var member in operation.Fleet)
            {
                var ship = save.Ships?.Find(candidate => string.Equals(candidate?.Uid, member?.ShipUid, StringComparison.Ordinal));
                var pilot = save.Characters?.Find(candidate => string.Equals(candidate?.Id, member?.PilotId, StringComparison.Ordinal));
                var hull = Catalog.GetShip(ship?.HullId);
                if (ship == null || pilot == null || !OperationService.CanFly(pilot, ship) ||
                    !OperationService.LocationSupportsShip(location, ship, hull)) return false;
            }
            return true;
        }

        static LocationDefinition FirstAutomaticBelt(GameSave save, OperationSave operation, LocationDefinition current, bool includeCurrent)
        {
            if (save == null || operation == null || current == null) return null;
            var floor = operation.AutoSecurityFloorInitialized
                ? operation.AutoSecurityFloorTenths
                : SecurityTierTenths(current);
            foreach (var candidate in OrderedStaticBelts)
            {
                if ((!includeCurrent && string.Equals(candidate.Id, current.Id, StringComparison.OrdinalIgnoreCase)) ||
                    SecurityTierTenths(candidate) < floor ||
                    !IsAvailableWithoutTick(candidate, FindState(save, candidate.Id)) ||
                    !FleetSupportsLocation(save, operation, candidate)) continue;
                return candidate;
            }
            return null;
        }

        static bool IsPristineStaticState(MiningSiteStateSave state, LocationDefinition location)
        {
            if (state == null || location == null || state.InstanceSerial < 1 ||
                state.Lifecycle != MiningSiteLifecycle.Available || state.RespawnUnix != 0 ||
                state.Asteroids == null || state.Asteroids.Count == 0) return false;
            var expected = new MiningSiteStateSave
            {
                LocationId = location.Id,
                Lifecycle = MiningSiteLifecycle.Available,
                InstanceSerial = state.InstanceSerial,
                Asteroids = new List<AsteroidSave>()
            };
            Generate(expected, location);
            return AsteroidsEqual(state.Asteroids, expected.Asteroids);
        }

        static bool AsteroidsEqual(IReadOnlyList<AsteroidSave> left, IReadOnlyList<AsteroidSave> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var index = 0; index < left.Count; index++)
            {
                var a = left[index];
                var b = right[index];
                if (a == null || b == null ||
                    !string.Equals(a.Id, b.Id, StringComparison.Ordinal) ||
                    !string.Equals(a.OreId, b.OreId, StringComparison.OrdinalIgnoreCase) ||
                    Math.Abs(a.RemainingUnits - b.RemainingUnits) > 1e-6 ||
                    Math.Abs(a.X - b.X) > 1e-6f || Math.Abs(a.Y - b.Y) > 1e-6f ||
                    Math.Abs(a.Z - b.Z) > 1e-6f || Math.Abs(a.Scale - b.Scale) > 1e-6f) return false;
            }
            return true;
        }

        static MiningSiteStateSave EnsureState(GameSave save, LocationDefinition location, long nowUnix)
        {
            if (save == null || location == null) return null;
            var state = FindState(save, location.Id);
            if (state != null) return state;
            state = CreateInitialState(location, ResolveNow(nowUnix));
            AddState(save, state);
            return state;
        }

        static void AddState(GameSave save, MiningSiteStateSave state)
        {
            if (save == null || state == null) return;
            save.MiningSites ??= new List<MiningSiteStateSave>();
            var index = GetIndex(save);
            save.MiningSites.Add(state);
            index.ByLocationId[state.LocationId] = state;
            index.Count = save.MiningSites.Count;
            MarkDeadlinesDirty(index);
        }

        static SiteIndex EnsureNormalized(GameSave save, long nowUnix)
        {
            var index = GetIndex(save);
            if (index.Normalized) return index;
            Normalize(save, nowUnix);
            return GetIndex(save);
        }

        static void EnsureDeadlineCache(GameSave save, SiteIndex index, long nowUnix)
        {
            if (save == null || index == null) return;
            if (!index.DeadlinesDirty && nowUnix >= index.DeadlineCacheUnix) return;

            // Downtime remains a global event even if every untouched static
            // belt is still lazy and therefore has no serialized state yet.
            var next = NextStaticRefreshUnix(nowUnix);
            foreach (var state in save.MiningSites ?? Enumerable.Empty<MiningSiteStateSave>())
            {
                if (state == null) continue;
                var location = Catalog.GetLocation(state.LocationId);
                var candidate = location?.SiteKind == MiningSiteKind.StaticBelt
                    ? state.NextRefreshUnix
                    : state.Lifecycle == MiningSiteLifecycle.Cooldown ? state.RespawnUnix : 0;
                if (candidate > 0 && candidate < next) next = candidate;
            }
            index.NextEventUnix = next;
            index.DeadlineCacheUnix = nowUnix;
            index.DeadlinesDirty = false;
        }

        static void MarkDeadlinesDirty(GameSave save)
        {
            if (save == null) return;
            MarkDeadlinesDirty(GetIndex(save));
        }

        static void MarkDeadlinesDirty(SiteIndex index)
        {
            if (index == null) return;
            index.DeadlinesDirty = true;
            index.DeadlineCacheUnix = long.MinValue;
            index.NextEventUnix = long.MaxValue;
        }

        static void EnsureActiveOreGrades(SiteIndex index, OperationSave operation, LocationDefinition location)
        {
            if (index == null || operation == null || location == null) return;
            if (ReferenceEquals(index.ActiveAsteroidSource, operation.Asteroids) &&
                index.ActiveGradeInstanceSerial == operation.SiteInstanceSerial &&
                string.Equals(index.ActiveGradeLocationId, location.Id, StringComparison.OrdinalIgnoreCase)) return;
            ApplyOreGrades(operation.Asteroids, location, operation.SiteInstanceSerial);
            index.ActiveAsteroidSource = operation.Asteroids;
            index.ActiveGradeLocationId = location.Id ?? string.Empty;
            index.ActiveGradeInstanceSerial = operation.SiteInstanceSerial;
        }

        static SiteIndex GetIndex(GameSave save)
        {
            if (save == null) return null;
            save.MiningSites ??= new List<MiningSiteStateSave>();
            var index = Indexes.GetValue(save, _ => new SiteIndex());
            if (!ReferenceEquals(index.Source, save.MiningSites) || index.Count != save.MiningSites.Count)
                RebuildIndex(save, index);
            return index;
        }

        static void RebuildIndex(GameSave save)
        {
            if (save == null) return;
            RebuildIndex(save, Indexes.GetValue(save, _ => new SiteIndex()));
        }

        static void RebuildIndex(GameSave save, SiteIndex index)
        {
            save.MiningSites ??= new List<MiningSiteStateSave>();
            index.Source = save.MiningSites;
            index.Count = save.MiningSites.Count;
            index.Normalized = false;
            index.LastTickUnix = long.MinValue;
            index.ActiveAsteroidSource = null;
            index.ActiveGradeLocationId = string.Empty;
            index.ActiveGradeInstanceSerial = 0;
            MarkDeadlinesDirty(index);
            index.ByLocationId.Clear();
            foreach (var state in save.MiningSites)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.LocationId) || index.ByLocationId.ContainsKey(state.LocationId)) continue;
                index.ByLocationId.Add(state.LocationId, state);
            }
        }

        static void CopyAsteroids(IEnumerable<AsteroidSave> source, List<AsteroidSave> destination)
        {
            destination ??= new List<AsteroidSave>();
            destination.Clear();
            if (source == null) return;
            foreach (var asteroid in source.Where(item => item != null))
                destination.Add(new AsteroidSave { Id = asteroid.Id, OreId = asteroid.OreId, RemainingUnits = asteroid.RemainingUnits, X = asteroid.X, Y = asteroid.Y, Z = asteroid.Z, Scale = asteroid.Scale });
        }

        static void ResetTargets(OperationSave operation)
        {
            operation.Enemies?.Clear();
            foreach (var member in operation.Fleet ?? Enumerable.Empty<FleetMemberSave>())
            {
                // A downtime refresh may happen while a ship is already in its
                // unload/dock warp. That state has no asteroid target and owns a
                // persisted phase, timer and return origin; replacing only the
                // belt instance must not strand the Transit ship as Idle.
                if (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay) continue;
                member.TargetAsteroidId = string.Empty;
                member.Order = FleetOrder.Idle;
                member.CycleProgressSeconds = 0;
                member.DroneCycleProgressSeconds = 0;
                member.MiningCycles?.Clear();
            }
        }

        static MiningSiteStateSave FindState(GameSave save, string locationId)
        {
            if (save == null || string.IsNullOrWhiteSpace(locationId)) return null;
            return GetIndex(save).ByLocationId.TryGetValue(locationId, out var state) ? state : null;
        }
        static long ResolveNow(long value) => value > 0 ? value : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static long PreviousStaticRefreshUnix(long nowUnix) => NextStaticRefreshUnix(nowUnix) - 24 * 60 * 60;
        static int StableHash(string value) { unchecked { var hash = 17; foreach (var c in value ?? string.Empty) hash = hash * 31 + c; return hash; } }
        static string FormatDuration(long seconds) { var span = TimeSpan.FromSeconds(Math.Max(0, seconds)); return span.TotalDays >= 1 ? $"{(int)span.TotalDays}д {span.Hours}ч" : span.TotalHours >= 1 ? $"{(int)span.TotalHours}ч {span.Minutes}м" : $"{Math.Max(1, span.Minutes)}м"; }
    }
}
