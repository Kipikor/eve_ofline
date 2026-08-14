using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    public sealed class OfflineSimulationReport
    {
        public long FromUnix;
        public long ToUnix;
        public double SimulatedSeconds;
        public double IgnoredSeconds;
        public int SimulationSteps;
        public bool Truncated;
        public double OreAddedM3;
        public int CompletedTrainingEntries;
        public int RouteLegs;
        public int SystemsVisited;
        public int ShipsLost;
        public bool IskWasCorrected;
        public string ErrorMessage = string.Empty;

        public bool HasElapsedTime => ToUnix > FromUnix;
        public bool Failed => !string.IsNullOrWhiteSpace(ErrorMessage);

        public string Summary
        {
            get
            {
                if (!HasElapsedTime) return "Офлайн-прогресс: перерыва не было.";
                var parts = new List<string>
                {
                    $"Офлайн {FormatDuration(ToUnix - FromUnix)}",
                    $"ресурсы +{Math.Max(0, OreAddedM3):N0} м³"
                };
                if (CompletedTrainingEntries > 0) parts.Add($"обучено: {CompletedTrainingEntries}");
                if (RouteLegs > 0) parts.Add($"переходов: {RouteLegs}");
                if (SystemsVisited > 1) parts.Add($"систем: {SystemsVisited}");
                if (ShipsLost > 0) parts.Add($"потеряно: {ShipsLost}");
                if (Truncated) parts.Add($"не просчитано: {FormatDuration(IgnoredSeconds)}");
                if (IskWasCorrected) parts.Add("ISK восстановлены: офлайн-продажи запрещены");
                if (Failed) parts.Add($"ошибка: {Compact(ErrorMessage, 120)}");
                return string.Join(" • ", parts);
            }
        }

        static string FormatDuration(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}д {span.Hours}ч";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}ч {span.Minutes}м";
            if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}м";
            return $"{Math.Max(1, (int)Math.Ceiling(span.TotalSeconds))}с";
        }

        static string Compact(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var oneLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return oneLine.Length <= maxLength ? oneLine : oneLine.Substring(0, Math.Max(0, maxLength - 1)) + "…";
        }
    }

    /// <summary>
    /// Replays the persisted simulation between the last durable checkpoint and
    /// this launch. Active gameplay never receives a single giant delta: travel
    /// and warp stop exactly at their serialized deadlines, while mining,
    /// approach and combat use steps no larger than five seconds. An otherwise
    /// idle Industrial Core jumps to its exact cycle boundary; completely idle
    /// time jumps directly to training or site events.
    /// </summary>
    public static class OfflineSimulationService
    {
        const double ActiveStepSeconds = 5d;
        const double MinimumStepSeconds = .001d;
        const long MaximumCatchUpSeconds = 30L * 24L * 60L * 60L;
        const int MaximumSteps = 750_000;

        public static OfflineSimulationReport LastOfflineReport { get; private set; } = new();

        public static OfflineSimulationReport CatchUp(GameSave save, long nowUnix = 0)
        {
            nowUnix = nowUnix > 0 ? nowUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var report = new OfflineSimulationReport
            {
                FromUnix = save?.LastSaveUnix ?? nowUnix,
                ToUnix = nowUnix
            };
            LastOfflineReport = report;
            if (save == null) return report;

            var checkpointUnix = save.LastSaveUnix;
            if (checkpointUnix <= 0 || checkpointUnix >= nowUnix)
            {
                if (checkpointUnix <= 0) report.FromUnix = nowUnix;
                save.LastSaveUnix = nowUnix;
                MiningSiteService.Tick(save, nowUnix);
                return report;
            }

            var iskBefore = save.Isk;
            var oreBeforeM3 = TotalOwnedOreVolumeM3(save);
            var shipsBefore = save.Ships?.Count ?? 0;
            var queuedAtStart = CaptureQueuedTraining(save);
            var visitedSystems = new HashSet<int>();
            RememberCurrentSystem(save, visitedSystems);

            var requestedSeconds = (double)(nowUnix - checkpointUnix);
            var simulationEndUnix = Math.Min(nowUnix, checkpointUnix + MaximumCatchUpSeconds);
            var cursorUnix = (double)checkpointUnix;
            var trainingCursorUnix = cursorUnix;
            var previousLocationId = save.Operation?.LocationId ?? string.Empty;
            var steps = 0;

            try
            {
                MiningSiteService.Tick(save, checkpointUnix);
                while (cursorUnix + MinimumStepSeconds < simulationEndUnix)
                {
                    if (steps >= MaximumSteps)
                    {
                        report.Truncated = true;
                        break;
                    }
                    steps++;

                    var simulatedNowUnix = ToSimulationSecond(cursorUnix);
                    MiningSiteService.Tick(save, simulatedNowUnix);
                    PrepareImmediateAutomation(save, simulatedNowUnix);

                    var remainingSeconds = simulationEndUnix - cursorUnix;
                    var stepSeconds = NextStepSeconds(save, cursorUnix, remainingSeconds);
                    if (stepSeconds <= 0 || double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds))
                        stepSeconds = Math.Min(MinimumStepSeconds, remainingSeconds);
                    stepSeconds = Math.Min(stepSeconds, remainingSeconds);

                    var operation = save.Operation;
                    if (operation?.TravelActive == true || operation?.BeltWarpActive == true)
                    {
                        var arrivalUnix = ToSimulationSecond(cursorUnix + stepSeconds);
                        TravelService.Tick(save, (float)stepSeconds, out _, arrivalUnix);
                    }
                    else if (operation?.Active == true && ShouldTickOperation(save))
                    {
                        OperationService.Tick(save, (float)stepSeconds, null, simulatedNowUnix);
                        MiningSiteService.TrySnapshotActiveDynamicDepletion(
                            save,
                            ToSimulationSecond(cursorUnix + stepSeconds));
                    }

                    // Mining during this interval uses the skills that existed at
                    // its start. A training deadline is itself a step boundary, so
                    // newly completed levels affect the following interval.
                    SkillService.TickAll(save, stepSeconds);
                    trainingCursorUnix += stepSeconds;
                    cursorUnix += stepSeconds;

                    TrackRouteProgress(save, ref previousLocationId, report, visitedSystems);
                }

                report.SimulatedSeconds = Math.Max(0, cursorUnix - checkpointUnix);
                report.IgnoredSeconds = Math.Max(0, requestedSeconds - report.SimulatedSeconds);
                report.Truncated |= report.IgnoredSeconds >= 1d;

                // Skill queues are inexpensive and have always been real-time.
                // Even when the safety cap truncates fleet simulation, no SP is
                // discarded from the un-simulated remainder.
                var remainingTrainingSeconds = Math.Max(0, nowUnix - trainingCursorUnix);
                if (remainingTrainingSeconds > 0) SkillService.TickAll(save, remainingTrainingSeconds);

                // The world clock itself must finish at the actual launch time.
                // If fleet catch-up was capped, this refreshes sites without
                // granting any mining, travel, sales or unloading for that gap.
                MiningSiteService.Tick(save, nowUnix);
            }
            catch (Exception exception)
            {
                report.ErrorMessage = exception.Message;
                report.SimulatedSeconds = Math.Max(0, cursorUnix - checkpointUnix);
                report.IgnoredSeconds = Math.Max(0, requestedSeconds - report.SimulatedSeconds);
                report.Truncated = report.IgnoredSeconds >= 1d;

                // Training remains monotonic even if an unrelated fleet state in
                // an old save cannot be replayed.
                var remainingTrainingSeconds = Math.Max(0, nowUnix - trainingCursorUnix);
                if (remainingTrainingSeconds > 0) SkillService.TickAll(save, remainingTrainingSeconds);
                try { MiningSiteService.Tick(save, nowUnix); }
                catch { /* The report retains the original actionable failure. */ }
            }
            finally
            {
                report.SimulationSteps = steps;
                report.IskWasCorrected = double.IsNaN(save.Isk) || double.IsInfinity(save.Isk) ||
                                         Math.Abs(save.Isk - iskBefore) > .000001d;
                // Assignment is unconditional: even a sub-cent floating-point
                // drift is outside the contract of an offline path with no sale.
                save.Isk = iskBefore;

                // This in-memory checkpoint prevents a second CatchUp call from
                // awarding the same interval. LoadOrCreate persists it immediately.
                save.LastSaveUnix = nowUnix;
            }

            report.OreAddedM3 = Math.Max(0, TotalOwnedOreVolumeM3(save) - oreBeforeM3);
            report.CompletedTrainingEntries = queuedAtStart.Count(entry => EntryWasCompleted(save, entry));
            report.ShipsLost = Math.Max(0, shipsBefore - (save.Ships?.Count ?? 0));
            report.SystemsVisited = visitedSystems.Count;
            LastOfflineReport = report;
            return report;
        }

        static void PrepareImmediateAutomation(GameSave save, long nowUnix)
        {
            var operation = save?.Operation;
            if (operation?.Active != true || operation.TravelActive || operation.BeltWarpActive) return;

            // A save may have been closed on the exact depletion frame, before
            // its dynamic anomaly was snapshotted. Persist that event at the
            // historical cursor before considering targets. Anomalies remain
            // outside ordinary-belt roaming.
            MiningSiteService.TrySnapshotActiveDynamicDepletion(save, nowUnix);

            if (operation.AutoRetarget)
            {
                foreach (var member in (operation.Fleet ?? new List<FleetMemberSave>())
                             .Where(candidate => candidate != null && candidate.Order == FleetOrder.Idle)
                             .ToArray())
                    OperationService.TryAssignAutomaticTarget(save, member);
            }

            var location = Catalog.GetLocation(operation.LocationId);
            if (!operation.AutoNextBelt || location?.SiteKind != MiningSiteKind.StaticBelt ||
                MiningSiteService.RemainingM3(operation.Asteroids) > 0 || operation.IndustrialCoreActive ||
                operation.Fleet?.Any(member => member != null &&
                    (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay)) == true) return;

            if (MiningSiteService.TryGetNextAutomaticBelt(save, out var destination, nowUnix))
                TravelService.TryStartAutomaticBeltTravel(save, destination.Id, out _, nowUnix);
        }

        static double NextStepSeconds(GameSave save, double cursorUnix, double remainingSeconds)
        {
            var operation = save?.Operation;
            var fineStepRequired = RequiresFineStep(save);
            var step = fineStepRequired ? Math.Min(ActiveStepSeconds, remainingSeconds) : remainingSeconds;

            if (operation?.TravelActive == true)
                ConsiderImmediateOrDeadline(ref step, operation.TravelSecondsLeft);
            if (operation?.BeltWarpActive == true)
                ConsiderImmediateOrDeadline(ref step, operation.BeltWarpSecondsLeft);

            if (operation?.Active == true && !operation.TravelActive && !operation.BeltWarpActive)
            {
                var currentLocation = Catalog.GetLocation(operation.LocationId);
                if (operation.StopAfterFleetWarp && (operation.Fleet?.Count ?? 0) == 0)
                    step = Math.Min(step, MinimumStepSeconds);
                if (operation.IndustrialCoreActive)
                    ConsiderImmediateOrDeadline(ref step, operation.IndustrialCoreSecondsLeft, operation.IndustrialCoreStopRequested);
                if (currentLocation != null && currentLocation.Threat > 0 && currentLocation.MaxNpcCount > 0 &&
                    HasBeltShip(save, operation) && operation.RaidTimerSeconds < 99_000f)
                {
                    if (operation.RaidTimerSeconds > 0)
                        ConsiderDeadline(ref step, operation.RaidTimerSeconds, true);
                    else
                        step = Math.Min(step, MinimumStepSeconds);
                }

                // While somebody is actually mining, burst pulse/expiry is a
                // yield boundary. A lingering effect whose source was switched
                // off also gets its exact expiry event. If active no-charge
                // bursts merely wait for downtime, OperationService can advance
                // all pulse cycles in one jump instead of scanning every belt 5s.
                if (fineStepRequired || !operation.BurstsActive)
                {
                    foreach (var effect in operation.BurstEffects ?? new List<BurstRecipientEffectSave>())
                        ConsiderImmediateOrDeadline(ref step, effect?.SecondsLeft ?? 0, true);
                }
                if (fineStepRequired)
                {
                    foreach (var ship in save.Ships ?? new List<ShipSave>())
                        foreach (var module in ship?.Modules ?? new List<FittedModuleSave>())
                            if (module?.Active == true && Catalog.GetModule(module.ModuleId)?.Kind == ModuleKind.MiningBurst)
                                ConsiderImmediateOrDeadline(ref step, module.BurstCycleSecondsLeft, true);
                }

                foreach (var member in operation.Fleet ?? new List<FleetMemberSave>())
                {
                    if (member == null) continue;
                    if (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay)
                    {
                        ConsiderImmediateOrDeadline(ref step, member.WarpPhaseSecondsLeft);
                        continue;
                    }
                    if (RequiresImmediateMemberStep(save, member))
                    {
                        step = Math.Min(step, MinimumStepSeconds);
                        continue;
                    }
                    if (member.Order == FleetOrder.Approaching)
                        ConsiderImmediateOrDeadline(ref step, ApproachSecondsLeft(save, member));
                    if (member.Order == FleetOrder.Mining)
                        ConsiderMiningDeadlines(save, member, ref step);
                }
            }

            ConsiderDeadline(ref step, NextTrainingDeadlineSeconds(save));
            ConsiderDeadline(ref step, NextWorldDeadlineSeconds(save, cursorUnix));
            return Math.Max(MinimumStepSeconds, Math.Min(step, remainingSeconds));
        }

        static bool RequiresFineStep(GameSave save)
        {
            var operation = save?.Operation;
            if (operation == null) return false;
            if (operation.TravelActive || operation.BeltWarpActive) return false;
            if (!operation.Active) return false;
            // Approach, extraction, unload warp, core, burst and raid-spawn
            // boundaries are all represented explicitly in NextStepSeconds.
            // Only live NPC combat still needs a bounded integration step for
            // movement, incoming damage and drone damage.
            return HasBeltShip(save, operation) &&
                   operation.Enemies?.Any(enemy => enemy != null && enemy.StructureHp > 0) == true;
        }

        static bool ShouldTickOperation(GameSave save)
        {
            var operation = save?.Operation;
            if (operation?.Active != true || operation.TravelActive || operation.BeltWarpActive) return false;
            if (RequiresFineStep(save) || operation.IndustrialCoreActive || operation.BurstsActive || operation.StopAfterFleetWarp ||
                operation.BurstEffects?.Any(effect => effect != null && effect.SecondsLeft > 0) == true) return true;
            if (operation.AutoRestartIndustrialCore && HasBeltShip(save, operation) &&
                MiningSiteService.RemainingM3(operation.Asteroids) > 0) return true;
            if (operation.Fleet?.Any(member => member != null &&
                    (member.Order is FleetOrder.Approaching or FleetOrder.Mining ||
                     operation.AutoUnload && member.Order == FleetOrder.Idle &&
                     !string.IsNullOrWhiteSpace(member.TargetAsteroidId))) == true) return true;
            if (operation.Fleet?.Any(member => member != null &&
                    (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay)) == true) return true;
            var location = Catalog.GetLocation(operation.LocationId);
            return location != null && location.Threat > 0 && location.MaxNpcCount > 0 && HasBeltShip(save, operation) &&
                   operation.RaidTimerSeconds < 99_000f;
        }

        static bool HasBeltShip(GameSave save, OperationSave operation)
        {
            return operation?.Fleet?.Any(member => member != null &&
                save?.Ships?.Find(ship => ship != null && ship.Uid == member.ShipUid)?.Location == ShipLocation.Belt) == true;
        }

        static void ConsiderMiningDeadlines(GameSave save, FleetMemberSave member, ref double step)
        {
            var operation = save?.Operation;
            var ship = save?.Ships?.Find(candidate => candidate != null && candidate.Uid == member.ShipUid);
            var target = operation?.Asteroids?.Find(candidate => candidate != null && candidate.Id == member.TargetAsteroidId && candidate.RemainingUnits > 0);
            var resource = Catalog.GetOre(target?.OreId);
            if (ship == null || resource == null)
            {
                step = Math.Min(step, MinimumStepSeconds);
                return;
            }

            foreach (var fitted in (ship.Modules ?? new List<FittedModuleSave>())
                         .Where(module => module != null && OperationService.CanMineResource(module, resource)))
            {
                var cycleSeconds = OperationService.MiningCycleSecondsForSlot(save, member, fitted.Slot);
                if (cycleSeconds <= 0) continue;
                var state = member.MiningCycles?.Find(candidate => candidate.Slot == fitted.Slot);
                var progress = state?.ProgressSeconds ?? Math.Max(0, member.CycleProgressSeconds);
                ConsiderImmediateOrDeadline(ref step, cycleSeconds - progress);
            }

            var pilot = save?.Characters?.Find(candidate => candidate != null && candidate.Id == member.PilotId);
            var miningDrone = Catalog.GetDrone(ship.MiningDroneId);
            var hasMiningDrones = resource.Kind == ResourceKind.Ore && !Catalog.IsMercoxitFamily(resource) &&
                                  miningDrone?.Mining == true && ship.MiningDroneCount > 0 &&
                                  SkillService.GetLevel(pilot, "drones") > 0;
            if ((operation.Enemies?.Count ?? 0) == 0 && hasMiningDrones)
                ConsiderImmediateOrDeadline(ref step, 60d - Math.Max(0, member.DroneCycleProgressSeconds));
        }

        static bool RequiresImmediateMemberStep(GameSave save, FleetMemberSave member)
        {
            if (save?.Operation == null || member == null) return false;
            var isExtractorOrder = member.Order is FleetOrder.Approaching or FleetOrder.Mining;
            var pendingAutoUnload = save.Operation.AutoUnload && member.Order == FleetOrder.Idle &&
                                    !string.IsNullOrWhiteSpace(member.TargetAsteroidId);
            if (pendingAutoUnload) return true;
            if (!isExtractorOrder) return false;

            var ship = save.Ships?.Find(candidate => candidate != null && candidate.Uid == member.ShipUid);
            var pilot = save.Characters?.Find(candidate => candidate != null && candidate.Id == member.PilotId);
            var hull = Catalog.GetShip(ship?.HullId);
            var target = save.Operation.Asteroids?.Find(candidate => candidate != null &&
                candidate.Id == member.TargetAsteroidId && candidate.RemainingUnits > 0);
            var resource = Catalog.GetOre(target?.OreId);
            if (ship == null || pilot == null || hull == null || resource == null) return true;

            var hasExtractor = (ship.Modules ?? new List<FittedModuleSave>())
                .Any(fitted => fitted != null && OperationService.CanMineResource(fitted, resource));
            var miningDrone = Catalog.GetDrone(ship.MiningDroneId);
            var hasMiningDrones = resource.Kind == ResourceKind.Ore && !Catalog.IsMercoxitFamily(resource) &&
                                  miningDrone?.Mining == true && ship.MiningDroneCount > 0 &&
                                  SkillService.GetLevel(pilot, "drones") > 0;
            if (!hasExtractor && !hasMiningDrones) return true;

            var freeHoldM3 = OperationService.MiningHoldCapacity(pilot, hull) -
                             OperationService.HoldVolume(ship.MiningHold);
            return freeHoldM3 + 1e-6d < resource.UnitVolumeM3;
        }

        static double ApproachSecondsLeft(GameSave save, FleetMemberSave member)
        {
            var ship = save?.Ships?.Find(candidate => candidate != null && candidate.Uid == member.ShipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            var target = save?.Operation?.Asteroids?.Find(candidate => candidate != null && candidate.Id == member.TargetAsteroidId && candidate.RemainingUnits > 0);
            if (ship == null || hull == null || target == null || hull.SpeedMps <= 0) return 0;
            if (save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == ship.Uid)
                return double.PositiveInfinity;

            var dx = target.X - member.X;
            var dy = target.Y - member.Y;
            var dz = target.Z - member.Z;
            var distanceKm = Math.Sqrt(dx * dx + dy * dy + dz * dz) * OperationService.KmPerWorldUnit;
            var preferredKm = Math.Max(0, member.PreferredRangeKm);
            if (preferredKm <= 0)
            {
                var pilot = save.Characters?.Find(candidate => candidate != null && candidate.Id == member.PilotId);
                var ranges = (ship.Modules ?? new List<FittedModuleSave>())
                    .Where(fitted => fitted != null && OperationService.CanMineResource(fitted, Catalog.GetOre(target.OreId)))
                    .Select(fitted => OperationService.MiningRangeKm(pilot, hull, Catalog.GetModule(fitted.ModuleId)))
                    .Where(value => value > 0)
                    .ToArray();
                preferredKm = ranges.Length > 0 ? ranges.Min() * .9f : 54f;
            }
            return Math.Max(0, distanceKm - preferredKm) / (hull.SpeedMps / 1000d);
        }

        static double NextTrainingDeadlineSeconds(GameSave save)
        {
            var next = double.PositiveInfinity;
            foreach (var pilot in save?.Characters ?? new List<CharacterSave>())
            {
                var seconds = SkillService.TrainingSecondsLeft(pilot);
                if (seconds > 0 && seconds < next) next = seconds;
            }
            return double.IsPositiveInfinity(next) ? 0 : next;
        }

        static double NextWorldDeadlineSeconds(GameSave save, double cursorUnix)
        {
            var nextUnix = MiningSiteService.NextWorldEventUnix(save, ToSimulationSecond(cursorUnix));
            return nextUnix <= cursorUnix + 1e-7d ? 0 : nextUnix - cursorUnix;
        }

        static void ConsiderDeadline(ref double step, double seconds, bool stopJustBefore = false)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            var deadline = stopJustBefore && seconds > MinimumStepSeconds * 2d
                ? seconds - MinimumStepSeconds
                : Math.Max(MinimumStepSeconds, seconds);
            if (deadline < step) step = deadline;
        }

        static void ConsiderImmediateOrDeadline(ref double step, double seconds, bool stopJustBefore = false)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (seconds <= MinimumStepSeconds)
            {
                step = Math.Min(step, MinimumStepSeconds);
                return;
            }
            ConsiderDeadline(ref step, seconds, stopJustBefore);
        }

        static void TrackRouteProgress(
            GameSave save,
            ref string previousLocationId,
            OfflineSimulationReport report,
            HashSet<int> visitedSystems)
        {
            var currentLocationId = save?.Operation?.LocationId ?? string.Empty;
            if (!string.Equals(previousLocationId, currentLocationId, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(previousLocationId) || !string.IsNullOrWhiteSpace(currentLocationId))
                    report.RouteLegs++;
                previousLocationId = currentLocationId;
            }
            RememberCurrentSystem(save, visitedSystems);
        }

        static void RememberCurrentSystem(GameSave save, HashSet<int> systems)
        {
            var operation = save?.Operation;
            var location = Catalog.GetLocation(operation?.LocationId);
            if (location != null && location.SystemId != 0) systems.Add(location.SystemId);
            var destination = Catalog.GetLocation(operation?.TravelDestinationLocationId ?? operation?.BeltWarpDestinationLocationId);
            if (destination != null && destination.SystemId != 0) systems.Add(destination.SystemId);
        }

        static List<QueuedTrainingSnapshot> CaptureQueuedTraining(GameSave save)
        {
            var result = new List<QueuedTrainingSnapshot>();
            foreach (var pilot in save?.Characters ?? new List<CharacterSave>())
            {
                if (pilot == null) continue;
                SkillService.NormalizeQueue(pilot);
                foreach (var entry in pilot.TrainingQueue ?? new List<SkillQueueEntrySave>())
                    if (entry != null)
                        result.Add(new QueuedTrainingSnapshot(pilot.Id, entry.SkillId, entry.TargetLevel));
            }
            return result;
        }

        static bool EntryWasCompleted(GameSave save, QueuedTrainingSnapshot entry)
        {
            var pilot = save?.Characters?.Find(candidate => candidate != null &&
                string.Equals(candidate.Id, entry.PilotId, StringComparison.Ordinal));
            return SkillService.GetLevel(pilot, entry.SkillId) >= entry.TargetLevel;
        }

        static double TotalOwnedOreVolumeM3(GameSave save)
        {
            var total = StackOreVolumeM3(save?.StationInventory);
            foreach (var ship in save?.Ships ?? new List<ShipSave>())
            {
                total += StackOreVolumeM3(ship?.MiningHold);
                total += StackOreVolumeM3(ship?.CargoHold);
                total += StackOreVolumeM3(ship?.FuelHold);
            }
            return total;
        }

        static double StackOreVolumeM3(IEnumerable<InventoryStack> stacks)
        {
            if (stacks == null) return 0;
            var total = 0d;
            foreach (var stack in stacks)
            {
                var resource = Catalog.GetOre(stack?.ItemId);
                if (resource != null) total += Math.Max(0, stack.Quantity) * resource.UnitVolumeM3;
            }
            return total;
        }

        static long ToSimulationSecond(double unixSeconds) => Math.Max(1, (long)Math.Floor(unixSeconds + 1e-7d));

        readonly struct QueuedTrainingSnapshot
        {
            public readonly string PilotId;
            public readonly string SkillId;
            public readonly int TargetLevel;

            public QueuedTrainingSnapshot(string pilotId, string skillId, int targetLevel)
            {
                PilotId = pilotId;
                SkillId = skillId;
                TargetLevel = targetLevel;
            }
        }
    }
}
