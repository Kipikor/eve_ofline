using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EveOffline
{
    public static class SaveService
    {
        public const int CurrentVersion = 14;
        public const double StartingIsk = 1_000_000d;
        public const int StartingCharacterCount = 10;
        public const int StartingShipCount = 9;
        public const string SaveFileName = "eve-offline-mining-command-v5.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public static string BackupPath => SavePath + ".bak";

        public static GameSave NewGame()
        {
            var save = new GameSave
            {
                Version = CurrentVersion,
                Isk = StartingIsk,
                Characters = new List<CharacterSave>(StartingCharacterCount),
                Ships = new List<ShipSave>(StartingShipCount),
                StationInventory = new List<InventoryStack>(),
                StationItemInstances = new List<ItemInstanceSave>(),
                PriceCache = new MarketPriceCache(),
                Operation = new OperationSave { LocationId = "uitra-belt-1" },
                MiningSites = new List<MiningSiteStateSave>(),
                LastSaveUnix = UnixNow(),
                NextShipSerial = 10,
                NextItemSerial = 1
            };
            var starterPackage = Catalog.GetPackage("venture-ore-t0")
                ?? throw new InvalidOperationException("Missing canonical starter package venture-ore-t0.");

            for (var i = 0; i < StartingCharacterCount; i++)
            {
                var number = i + 1;
                var pilot = new CharacterSave
                {
                    Id = $"pilot-{number:00}",
                    Name = $"Пилот {number:00}",
                    DeployOnLaunch = i < StartingShipCount
                };
                AddStartingSkill(pilot, "spaceship-command", 1);
                AddStartingSkill(pilot, "mining", 1);
                AddStartingSkill(pilot, "mining-frigate", 1);
                AddStartingSkill(pilot, "drones", 1);
                AddStartingSkill(pilot, "light-drone-operation", 1);
                foreach (var requirement in PreparedPackageService.RequiredSkills(starterPackage))
                    AddStartingSkillWithPrerequisites(pilot, requirement.SkillId, requirement.Level, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

                if (i < StartingShipCount)
                {
                    var uid = $"venture-{number:00}";
                    pilot.AssignedShipUid = uid;
                    var ship = CreateShip(uid, "venture");
                    PreparedPackageService.ApplyLockedFit(ship, starterPackage);
                    save.Ships.Add(ship);
                }
                save.Characters.Add(pilot);
            }
            return save;
        }

        public static ShipSave CreateShip(string uid, string hullId, bool starterFit = false)
        {
            var hull = Catalog.GetShip(hullId) ?? throw new ArgumentException("Unknown hull " + hullId);
            var ship = new ShipSave
            {
                Uid = uid,
                HullId = hullId,
                Location = ShipLocation.Station,
                ShieldHp = hull.ShieldHp,
                ArmorHp = hull.ArmorHp,
                StructureHp = hull.StructureHp
            };
            if (starterFit)
            {
                // Compatibility for older callers: starterFit now means the
                // canonical ready-made package, never a hand-built slot fit.
                if (!string.Equals(hullId, "venture", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("starterFit is only valid for the canonical Venture package.", nameof(hullId));
                var starterPackage = Catalog.GetPackage("venture-ore-t0")
                    ?? throw new InvalidOperationException("Missing canonical starter package venture-ore-t0.");
                PreparedPackageService.ApplyLockedFit(ship, starterPackage);
            }
            return ship;
        }

        public static GameSave LoadOrCreate()
        {
            if (TryLoad(out var save, out _))
            {
                // Commit the complete offline-simulation checkpoint immediately.
                // Otherwise an abrupt exit before the first autosave could award
                // the same elapsed interval again on every subsequent launch.
                Save(save);
                return save;
            }
            else
            {
                save = NewGame();
                Save(save);
            }
            return save;
        }

        public static bool TryLoad(out GameSave save, out string error)
        {
            if (!TryReadCompatible(SavePath, out save, out error))
            {
                if (!TryReadCompatible(BackupPath, out save, out var backupError))
                {
                    error = string.IsNullOrWhiteSpace(error) ? backupError : error;
                    return false;
                }

                // Restore the known-good backup before LoadOrCreate checkpoints the
                // loaded state, so Save() cannot copy a corrupt primary over it.
                try { File.Copy(BackupPath, SavePath, true); }
                catch (Exception exception) { error = exception.Message; return false; }
            }

            var nowUnix = UnixNow();
            var historicalUnix = save.LastSaveUnix > 0 ? Math.Min(save.LastSaveUnix, nowUnix) : nowUnix;
            MigrateToCurrentVersion(save);
            // Normalize against the persisted checkpoint first. Advancing the
            // site world to real now before catch-up would refresh an 11:00 UTC
            // belt before the interval immediately preceding downtime was mined.
            Normalize(save, historicalUnix);
            OfflineSimulationService.CatchUp(save, nowUnix);
            return true;
        }

        public static void Save(GameSave save)
        {
            if (save == null) return;
            MigrateToCurrentVersion(save);
            Normalize(save);
            save.Version = CurrentVersion;
            save.LastSaveUnix = UnixNow();
            Directory.CreateDirectory(Application.persistentDataPath);
            var temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(save, true), new UTF8Encoding(false));
            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
            File.Copy(temp, SavePath, true);
            File.Delete(temp);
        }

        public static void DeleteSave()
        {
            foreach (var path in new[] { SavePath, SavePath + ".tmp", BackupPath }) if (File.Exists(path)) File.Delete(path);
        }

        static bool TryRead(string path, out GameSave save, out string error)
        {
            save = null;
            if (!File.Exists(path)) { error = "Файл сохранения не найден."; return false; }
            try { save = JsonUtility.FromJson<GameSave>(File.ReadAllText(path, Encoding.UTF8)); error = save == null ? "Пустое сохранение." : string.Empty; return save != null; }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        static bool TryReadCompatible(string path, out GameSave save, out string error)
        {
            if (!TryRead(path, out save, out error)) return false;
            if (save.Version is 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or CurrentVersion) return true;
            save = null;
            error = "Несовместимая версия сохранения.";
            return false;
        }

        static void AddStartingSkill(CharacterSave pilot, string skillId, int level)
        {
            var state = pilot.Skills.Find(candidate => string.Equals(candidate.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
            if (state == null)
            {
                state = new CharacterSkillSave { SkillId = skillId };
                pilot.Skills.Add(state);
            }
            state.BookOwned = true;
            state.SkillPoints = Math.Max(state.SkillPoints, SkillService.RequiredSp(skillId, level));
        }

        static void AddStartingSkillWithPrerequisites(CharacterSave pilot, string skillId, int level, HashSet<string> visiting)
        {
            if (pilot == null || string.IsNullOrWhiteSpace(skillId) || level <= 0 || !visiting.Add(skillId)) return;
            var definition = Catalog.GetSkill(skillId);
            foreach (var prerequisite in definition?.Prerequisites ?? Array.Empty<SkillRequirement>())
                AddStartingSkillWithPrerequisites(pilot, prerequisite.SkillId, prerequisite.Level, visiting);
            AddStartingSkill(pilot, skillId, level);
            visiting.Remove(skillId);
        }

        /// <summary>
        /// Upgrades an already deserialized compatible save without touching
        /// the filesystem. Exposed so import tools and regression checks can
        /// prove that a migration preserves the player's runtime state.
        /// </summary>
        public static void MigrateToCurrentVersion(GameSave save)
        {
            if (save == null || save.Version >= CurrentVersion) return;

            // v9 and earlier stored either manual fits or the short-lived first
            // package schema. Apart from the explicitly authorized conversion
            // of the nine original starter Ventures below, attach identity only
            // when the existing fit has a safe catalog match. Deliberately do
            // not apply/refit arbitrary ships: modules, drones, current HP,
            // holds, wallet and every active-operation state must remain exact.
            if (save.Version <= 9)
            {
                foreach (var ship in save.Ships ?? Enumerable.Empty<ShipSave>())
                {
                    if (IsMigratingStarterVenture(ship))
                    {
                        ApplyStarterVentureT0Identity(ship);
                        continue;
                    }
                    if (ship == null) continue;

                    // Never trust a short-lived v9 package identity by name
                    // alone: current packages contain a fuller locked fit. Match
                    // the materialized modules and drones again, while preserving
                    // any legacy tank scalar if the ship remains custom.
                    var legacyTankPresetId = ship.TankPresetId ?? string.Empty;
                    ship.PackageId = string.Empty;
                    ship.TankPresetId = string.Empty;
                    var closest = PreparedPackageService.FindClosestPackage(ship);
                    if (closest != null)
                    {
                        ship.PackageId = closest.Id;
                        ship.TankPresetId = closest.TankPresetId ?? string.Empty;
                    }
                    else
                    {
                        ship.TankPresetId = legacyTankPresetId;
                    }
                }
            }

            if (save.Version <= 10)
            {
                // v10 only serialized the currently open belt. Seed the new
                // world state from that exact snapshot so migration preserves
                // partial depletion, active fleet, holds, HP and timers.
                save.MiningSites ??= new List<MiningSiteStateSave>();
                var migrationNow = save.LastSaveUnix > 0 ? save.LastSaveUnix : UnixNow();
                MiningSiteService.Normalize(save, migrationNow);
                MiningSiteService.SnapshotActiveOperation(save, migrationNow);
            }

            if (save.Version <= 11)
            {
                // Package variants can be retired when the real fitting matrix
                // changes. Preserve the materialized legacy ship exactly, but
                // remove an unknown identity so it remains deployable as
                // LEGACY CUSTOM rather than being locked behind a missing plan.
                foreach (var ship in save.Ships ?? Enumerable.Empty<ShipSave>())
                    if (ship != null && !string.IsNullOrWhiteSpace(ship.PackageId) && Catalog.GetPackage(ship.PackageId) == null)
                        ship.PackageId = string.Empty;

                MiningSiteService.MigrateV11ClearIcicleCooldown(save);
            }

            if (save.Version <= 12)
            {
                save.Operation ??= new OperationSave();
                save.Operation.TravelSourceLocationId ??= string.Empty;
                save.Operation.AutoRestartIndustrialCoreShipUid ??= string.Empty;
                // A completed legacy operation is the best available evidence of
                // the system the player manually reached. An in-flight legacy
                // journey deliberately remains uncommitted until it arrives.
                if (save.Operation.Active && !save.Operation.TravelActive)
                {
                    var current = Catalog.GetLocation(save.Operation.LocationId);
                    if (current != null)
                    {
                        save.Operation.AutoSecurityFloorTenths = MiningSiteService.SecurityTierTenths(current);
                        save.Operation.AutoSecurityFloorInitialized = true;
                    }
                }
                MiningSiteService.MigrateV13CompactPristineStaticStates(save);
            }

            if (save.Version <= 13)
            {
                MigrateLegacyMercoxit(save);
            }

            save.Version = CurrentVersion;
        }

        static void MigrateLegacyMercoxit(GameSave save)
        {
            // Prepared Mercoxit fits became ordinary Ore fits. Rebuild only
            // ships carrying the retired package identity; custom legacy fits
            // and inventory items stay intact and readable.
            foreach (var ship in save.Ships ?? Enumerable.Empty<ShipSave>())
            {
                if (ship == null || string.IsNullOrWhiteSpace(ship.PackageId)) continue;
                const string marker = "-mercoxit-";
                var markerIndex = ship.PackageId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) continue;
                var replacementId = ship.PackageId.Substring(0, markerIndex) + "-ore-" + ship.PackageId.Substring(markerIndex + marker.Length);
                var replacement = Catalog.GetPackage(replacementId);
                if (replacement == null || !string.Equals(replacement.HullId, ship.HullId, StringComparison.OrdinalIgnoreCase))
                {
                    // Never leave a retired identity that would make an otherwise
                    // readable legacy ship impossible to deploy.
                    ship.PackageId = string.Empty;
                    continue;
                }

                var shieldHp = ship.ShieldHp;
                var armorHp = ship.ArmorHp;
                var structureHp = ship.StructureHp;
                PreparedPackageService.ApplyLockedFit(ship, replacement, true);
                // ApplyLockedFit intentionally clamps against an unpiloted max;
                // migration must preserve the exact live damage/skill-scaled HP.
                ship.ShieldHp = shieldHp;
                ship.ArmorHp = armorHp;
                ship.StructureHp = structureHp;
            }

            var retiredSkills = new HashSet<string>(new[] { "deep-core-mining", "mercoxit-ore-processing" }, StringComparer.OrdinalIgnoreCase);
            foreach (var pilot in save.Characters ?? Enumerable.Empty<CharacterSave>())
            {
                if (pilot == null) continue;
                pilot.Skills ??= new List<CharacterSkillSave>();
                pilot.TrainingQueue ??= new List<SkillQueueEntrySave>();
                var refund = pilot.Skills
                    .Where(state => state != null && retiredSkills.Contains(state.SkillId))
                    .Sum(state => Math.Max(0d, state.SkillPoints));
                pilot.UnallocatedSkillPoints += refund;
                pilot.Skills.RemoveAll(state => state == null || retiredSkills.Contains(state.SkillId));
                pilot.TrainingQueue.RemoveAll(entry => entry == null || retiredSkills.Contains(entry.SkillId));
                if (retiredSkills.Contains(pilot.TrainingSkillId))
                {
                    pilot.TrainingSkillId = string.Empty;
                    pilot.TrainingTargetLevel = 0;
                }
                SkillService.NormalizeQueue(pilot);
            }
        }

        static bool IsMigratingStarterVenture(ShipSave ship)
        {
            if (ship == null || !string.Equals(ship.HullId, "venture", StringComparison.OrdinalIgnoreCase)) return false;
            var starterUid = Enumerable.Range(1, StartingShipCount)
                .Any(number => string.Equals(ship.Uid, $"venture-{number:00}", StringComparison.OrdinalIgnoreCase));
            if (!starterUid) return false;
            if (string.Equals(ship.PackageId, "venture-ore-t0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ship.PackageId, "venture-ore-t1", StringComparison.OrdinalIgnoreCase)) return true;
            var modules = ship.Modules ?? new List<FittedModuleSave>();
            return modules.Count == 2 &&
                   modules.Any(module => module != null && module.Slot == 0 && string.Equals(module.ModuleId, "miner-i", StringComparison.OrdinalIgnoreCase)) &&
                   modules.Any(module => module != null && module.Slot == 1 && string.Equals(module.ModuleId, "miner-i", StringComparison.OrdinalIgnoreCase));
        }

        static void ApplyStarterVentureT0Identity(ShipSave ship)
        {
            // The v10 design explicitly converts the nine original free ships
            // to T0. Preserve all simulation state and damage while removing the
            // interim automatic low-slot/tank/drone equipment.
            ship.PackageId = "venture-ore-t0";
            ship.TankPresetId = string.Empty;
            ship.Modules ??= new List<FittedModuleSave>();
            ship.Modules.Clear();
            ship.Modules.Add(new FittedModuleSave { Slot = 0, ModuleId = "miner-i", Active = true });
            ship.Modules.Add(new FittedModuleSave { Slot = 1, ModuleId = "miner-i", Active = true });
            ship.CombatDroneId = string.Empty;
            ship.CombatDroneCount = 0;
            ship.MiningDroneId = string.Empty;
            ship.MiningDroneCount = 0;
        }

        static void Normalize(GameSave save, long nowUnix = 0)
        {
            save.Characters ??= new(); save.Ships ??= new(); save.StationInventory ??= new(); save.StationItemInstances ??= new(); save.PriceCache ??= new(); save.PriceCache.Entries ??= new(); save.Operation ??= new(); save.MiningSites ??= new();
            save.Operation.Asteroids ??= new(); save.Operation.Fleet ??= new(); save.Operation.Enemies ??= new(); save.Operation.Log ??= new();
            save.Operation.BurstEffects ??= new(); save.Operation.TravelShipUids ??= new();
            save.Operation.TravelSourceLocationId ??= string.Empty;
            save.Operation.AutoRestartIndustrialCoreShipUid ??= string.Empty;
            save.Operation.AutoSecurityFloorTenths = Math.Clamp(save.Operation.AutoSecurityFloorTenths, -10, 10);
            if (!save.Operation.AutoSecurityFloorInitialized) save.Operation.AutoSecurityFloorTenths = 0;
            if (!save.Operation.AutoRestartIndustrialCore) save.Operation.AutoRestartIndustrialCoreShipUid = string.Empty;
            if (!save.Operation.TravelActive)
            {
                save.Operation.TravelSourceLocationId = string.Empty;
                save.Operation.TravelIsAutomatic = false;
            }
            if (!save.Operation.BeltWarpActive) save.Operation.BeltWarpIsAutomatic = false;
            nowUnix = nowUnix > 0 ? nowUnix : UnixNow();
            MiningSiteService.SnapshotActiveOperation(save, nowUnix);
            MiningSiteService.Tick(save, nowUnix);
            save.Operation.BurstEffects.RemoveAll(effect => effect == null || string.IsNullOrWhiteSpace(effect.ShipUid) || !Catalog.PreparedBurstProfileIds.Contains(effect.ChargeId, StringComparer.OrdinalIgnoreCase) || effect.SecondsLeft <= 0 || effect.Strength <= 0);
            foreach (var effect in save.Operation.BurstEffects)
            {
                effect.SecondsLeft = Math.Max(0, effect.SecondsLeft);
                effect.Strength = Math.Max(0, effect.Strength);
            }
            NormalizeReturnWarp(save);
            foreach (var member in save.Operation.Fleet) member.MiningCycles ??= new();
            foreach (var pilot in save.Characters)
            {
                pilot.Skills ??= new();
                SkillService.NormalizeQueue(pilot);
            }
            save.NextItemSerial = Math.Max(1, save.NextItemSerial);
            foreach (var instance in save.StationItemInstances)
            {
                if (string.IsNullOrWhiteSpace(instance.Uid)) instance.Uid = NextItemUid(save);
                instance.Damage = Math.Clamp(instance.Damage, 0f, .999f);
            }
            foreach (var ship in save.Ships)
            {
                ship.Modules ??= new(); ship.MiningHold ??= new(); ship.CargoHold ??= new(); ship.FuelHold ??= new();
                ship.PackageId ??= string.Empty;
                ship.TankPresetId ??= string.Empty;
                var hull = Catalog.GetShip(ship.HullId);
                var overflowUpgrades = ship.Modules
                    .Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningUpgrade)
                    .OrderBy(fitted => fitted.Slot)
                    .Skip(Math.Max(0, hull?.LowSlots ?? 0))
                    .ToArray();
                foreach (var overflow in overflowUpgrades)
                {
                    OperationService.AddItem(save.StationInventory, overflow.ModuleId, 1);
                    ship.Modules.Remove(overflow);
                }
                var preparedPackage = Catalog.GetPackage(ship.PackageId);
                var burstModules = ship.Modules
                    .Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningBurst)
                    .OrderBy(fitted => fitted.Slot)
                    .ToList();
                if (preparedPackage != null && !string.IsNullOrWhiteSpace(preparedPackage.BurstModuleId))
                {
                    var desiredProfiles = (preparedPackage.BurstChargeIds ?? Array.Empty<string>())
                        .Take(PreparedPackageService.BurstModuleCount(preparedPackage))
                        .ToArray();
                    var lockedBursts = burstModules
                        .Where(fitted => string.Equals(fitted.ModuleId, preparedPackage.BurstModuleId, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    for (var burstIndex = 0; burstIndex < Math.Min(lockedBursts.Count, desiredProfiles.Length); burstIndex++)
                        lockedBursts[burstIndex].ChargeId = desiredProfiles[burstIndex];
                    foreach (var obsoleteBurst in lockedBursts.Skip(desiredProfiles.Length).ToArray())
                    {
                        // Old Rorqual packages paid for a fourth module. Return it
                        // to legacy station inventory instead of deleting its value.
                        OperationService.AddItem(save.StationInventory, obsoleteBurst.ModuleId, 1);
                        ship.Modules.Remove(obsoleteBurst);
                    }
                    save.Operation.BurstEffects.RemoveAll(effect => string.Equals(effect?.SourceShipUid, ship.Uid, StringComparison.Ordinal) && !desiredProfiles.Contains(effect.ChargeId, StringComparer.OrdinalIgnoreCase));
                    burstModules = ship.Modules
                        .Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningBurst)
                        .OrderBy(fitted => fitted.Slot)
                        .ToList();
                }
                for (var burstIndex = 0; burstIndex < burstModules.Count; burstIndex++)
                {
                    var fitted = burstModules[burstIndex];
                    if (!string.IsNullOrWhiteSpace(fitted.ChargeId) && Catalog.PreparedBurstProfileIds.Contains(fitted.ChargeId, StringComparer.OrdinalIgnoreCase)) continue;
                    fitted.ChargeId = preparedPackage?.BurstChargeIds?.ElementAtOrDefault(burstIndex)
                        ?? string.Empty;
                }
                foreach (var fitted in ship.Modules)
                {
                    var moduleKind = Catalog.GetModule(fitted.ModuleId)?.Kind;
                    if (string.IsNullOrWhiteSpace(fitted.ChargeId))
                    {
                        fitted.ChargeId = string.Empty;
                        fitted.ChargeUid = string.Empty;
                        fitted.ChargeDamage = 0;
                        fitted.ChargeQuantity = 0;
                        fitted.ChargeQuantityInitialized = moduleKind == ModuleKind.MiningBurst;
                        fitted.BurstCycleSecondsLeft = 0;
                        if (moduleKind is ModuleKind.MiningBurst or ModuleKind.IndustrialCore) fitted.Active = false;
                        continue;
                    }
                    if (moduleKind == ModuleKind.MiningBurst && !Catalog.PreparedBurstProfileIds.Contains(fitted.ChargeId, StringComparer.OrdinalIgnoreCase))
                    {
                        fitted.ChargeId = string.Empty;
                        fitted.ChargeUid = string.Empty;
                        fitted.ChargeDamage = 0;
                        fitted.ChargeQuantity = 0;
                        fitted.ChargeQuantityInitialized = true;
                        fitted.BurstCycleSecondsLeft = 0;
                        fitted.Active = false;
                        continue;
                    }
                    if (moduleKind == ModuleKind.MiningBurst)
                    {
                        // Quantity is legacy data only. ChargeId now names the
                        // built-in effect profile and no physical ammo is used.
                        fitted.ChargeQuantityInitialized = true;
                        fitted.ChargeQuantity = Math.Max(0, fitted.ChargeQuantity);
                        fitted.BurstCycleSecondsLeft = Math.Max(0, fitted.BurstCycleSecondsLeft);
                        fitted.ChargeUid = string.Empty;
                        fitted.ChargeDamage = 0;
                        var automaticRoamingTransit =
                            (save.Operation.TravelActive && save.Operation.TravelIsAutomatic) ||
                            (save.Operation.BeltWarpActive && save.Operation.BeltWarpIsAutomatic);
                        fitted.Active = save.Operation.BurstsActive && save.Operation.BurstShipUid == ship.Uid &&
                                        (ship.Location == ShipLocation.Belt ||
                                         (automaticRoamingTransit && save.Operation.Fleet.Any(member => member.ShipUid == ship.Uid)));
                        continue;
                    }
                    fitted.ChargeDamage = Math.Clamp(fitted.ChargeDamage, 0f, .999f);
                    if (Catalog.GetCrystal(fitted.ChargeId) != null && string.IsNullOrWhiteSpace(fitted.ChargeUid))
                        fitted.ChargeUid = NextItemUid(save);
                }
                foreach (var core in ship.Modules.Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.IndustrialCore))
                    core.Active = ship.Location == ShipLocation.Belt && save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == ship.Uid;
            }
            if (!save.Ships.SelectMany(ship => ship.Modules).Any(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningBurst && fitted.Active && Catalog.PreparedBurstProfileIds.Contains(fitted.ChargeId, StringComparer.OrdinalIgnoreCase)))
            {
                save.Operation.BurstsActive = false;
                save.Operation.BurstShipUid = string.Empty;
            }
        }

        static void NormalizeReturnWarp(GameSave save)
        {
            var op = save.Operation;
            if (!op.WarpPointInitialized || !Finite(op.WarpPointX) || !Finite(op.WarpPointY) || !Finite(op.WarpPointZ))
            {
                var angleDegrees = (op.BeltSeed * 17 + op.VisitNumber * 61) % 360;
                var radians = angleDegrees * Mathf.Deg2Rad;
                op.WarpPointX = Mathf.Cos(radians) * 260f;
                op.WarpPointY = 18f;
                op.WarpPointZ = Mathf.Sin(radians) * 260f;
                op.WarpPointInitialized = true;
            }

            foreach (var member in op.Fleet)
            {
                if (member == null) continue;
                var returning = member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay;
                if (!returning)
                {
                    member.WarpPhase = FleetWarpPhase.None;
                    member.WarpPhaseSecondsLeft = 0;
                    continue;
                }

                member.ReturnAfterUnload = member.Order == FleetOrder.UnloadAndReturn;
                if (member.WarpPhase == FleetWarpPhase.None)
                {
                    // v5-v7 transit had no visual phases. Preserve its remaining
                    // away time, then give returning ships the new warp-in pass.
                    member.WarpOriginX = member.X;
                    member.WarpOriginY = member.Y;
                    member.WarpOriginZ = member.Z;
                    member.ResumeTargetAsteroidId = member.TargetAsteroidId ?? string.Empty;
                    member.ResumeOrder = string.IsNullOrWhiteSpace(member.ResumeTargetAsteroidId) ? FleetOrder.Idle : FleetOrder.Approaching;
                    if (member.ReturnAfterUnload)
                    {
                        member.WarpPhase = FleetWarpPhase.InTransit;
                        member.WarpPhaseSecondsLeft = Math.Clamp(member.TransitSecondsLeft > 0 ? member.TransitSecondsLeft : OperationService.WarpTransitSeconds, 0f, OperationService.WarpTransitSeconds);
                    }
                    else
                    {
                        member.WarpPhase = FleetWarpPhase.AligningOut;
                        member.WarpPhaseSecondsLeft = Math.Clamp(member.TransitSecondsLeft > 0 ? member.TransitSecondsLeft : OperationService.WarpAlignSeconds, 0f, OperationService.WarpAlignSeconds);
                    }
                }

                member.WarpPhaseSecondsLeft = Math.Max(0, member.WarpPhaseSecondsLeft);
                member.TransitSecondsLeft = member.WarpPhase switch
                {
                    FleetWarpPhase.AligningOut => member.WarpPhaseSecondsLeft + (member.ReturnAfterUnload ? OperationService.WarpTransitSeconds + OperationService.WarpInSeconds : 0),
                    FleetWarpPhase.InTransit => member.WarpPhaseSecondsLeft + (member.ReturnAfterUnload ? OperationService.WarpInSeconds : 0),
                    FleetWarpPhase.WarpingIn => member.WarpPhaseSecondsLeft,
                    _ => member.TransitSecondsLeft
                };
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                if (ship != null) ship.Location = ShipLocation.Transit;
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static string NextItemUid(GameSave save) => $"item-{save.NextItemSerial++:000000}";

        static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
