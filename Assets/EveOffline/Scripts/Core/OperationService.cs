using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace EveOffline
{
    public sealed class CompressionResult
    {
        public int ShipsAffected;
        public int StacksCompressed;
        public double UnitsCompressed;
        public double RawVolumeM3;
        public double CompressedVolumeM3;
        public double SavedVolumeM3 => Math.Max(0, RawVolumeM3 - CompressedVolumeM3);
    }

    public static class OperationService
    {
        public const float KmPerWorldUnit = 2f;
        public const float WarpAlignSeconds = 5f;
        public const float WarpTransitSeconds = 10f;
        public const float WarpInSeconds = 5f;
        public const float CombatDroneLaunchSeconds = 2f;
        public const float CombatDroneSpeedKmPerSecond = 4f;
        // Position and target coordinates are serialized as floats. A ship can
        // otherwise stop at the requested range while the recomputed distance is
        // a few millimetres larger, leaving it permanently in Approaching.
        const float MiningRangeArrivalToleranceKm = .001f;
        static readonly System.Random Random = new(1977);
        static readonly ConditionalWeakTable<GameSave, AutomaticRouteAttemptState> AutomaticRouteAttempts = new();

        sealed class AutomaticRouteAttemptState
        {
            public long Unix = long.MinValue;
            public string LocationId = string.Empty;
            public string SnapshotLocationId = string.Empty;
            public int SnapshotInstanceSerial;
        }

        [Flags]
        enum BurstEffect
        {
            None = 0,
            Range = 1,
            Cycle = 2,
            Efficiency = 4,
            All = Range | Cycle | Efficiency
        }

        public static void Start(GameSave save, string locationId, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            var location = Catalog.GetLocation(locationId) ?? Catalog.Locations[0];
            var op = save.Operation ??= new OperationSave();
            MiningSiteService.Tick(save, nowUnix);
            if (!MiningSiteService.IsAvailable(save, location.Id, nowUnix))
            {
                Log(op, $"{location.DisplayName} недоступен: {MiningSiteService.AvailabilityText(save, location.Id, nowUnix)}.");
                return;
            }
            if (op.IndustrialCoreActive && (!op.Active || !string.Equals(op.LocationId, location.Id, StringComparison.Ordinal)))
            {
                Log(op, $"Переход в {location.DisplayName} заблокирован до окончания цикла Industrial Core.");
                return;
            }
            if (!op.Active || op.LocationId != location.Id)
            {
                MiningSiteService.SnapshotActiveOperation(save, nowUnix);
                DockAndUnloadPreviousOperation(save, op);
                op.Active = true; op.LocationId = location.Id; op.VisitNumber++; op.ElapsedSeconds = 0; op.RaidTimerSeconds = NextRaid(location); op.StopAfterFleetWarp = false; op.BurstsActive = false; op.BurstShipUid = string.Empty; op.IndustrialCoreActive = false; op.IndustrialCoreStopRequested = false; op.IndustrialCoreSecondsLeft = 0; op.IndustrialCoreShipUid = string.Empty; ClearAutomaticCoreRestartIntent(op);
                op.WarpPointInitialized = false;
                EnsureSharedWarpPoint(op);
                op.Asteroids.Clear(); op.Fleet.Clear(); op.Enemies.Clear(); op.Log.Clear();
                MiningSiteService.LoadIntoOperation(save, location, nowUnix);
            }

            foreach (var pilot in save.Characters)
            {
                var ship = FindShip(save, pilot.AssignedShipUid);
                if (!pilot.DeployOnLaunch || ship == null || ship.Location != ShipLocation.Station || op.Fleet.Count >= Catalog.FleetCapacity) continue;
                Join(save, pilot.Id);
            }
            Log(op, $"Флот прибыл: {location.DisplayName}, security {location.Security:0.0}.");
        }

        public static bool Join(GameSave save, string pilotId)
        {
            var op = save.Operation; if (op == null || !op.Active || op.Fleet.Count >= Catalog.FleetCapacity) return false;
            var pilot = FindPilot(save, pilotId); var ship = FindShip(save, pilot?.AssignedShipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            var location = Catalog.GetLocation(op.LocationId);
            if (pilot == null || ship == null || ship.Location != ShipLocation.Station || !CanFly(pilot, ship) || !LocationSupportsShip(location, ship, hull)) return false;
            var index = op.Fleet.Count;
            ship.Location = ShipLocation.Belt;
            var member = new FleetMemberSave { PilotId = pilot.Id, ShipUid = ship.Uid, Order = FleetOrder.Idle, PreferredRangeKm = 0, X = -22 + (index % 3) * 3.2f, Y = -2 + (index / 3) * 2.2f, Z = -4 + (index % 2) * 2 };
            op.Fleet.Add(member);
            if (op.AutoRetarget) TryAssignAutomaticTarget(save, member);
            Log(op, $"{pilot.Name} вошёл в операцию на {Catalog.GetShip(ship.HullId).DisplayName}.");
            return true;
        }

        public static bool AssignTarget(GameSave save, string shipUid, string asteroidId)
        {
            var member = save.Operation?.Fleet.Find(item => item.ShipUid == shipUid);
            var asteroid = save.Operation?.Asteroids.Find(item => item.Id == asteroidId && item.RemainingUnits > 0);
            var ship = FindShip(save, shipUid);
            if (member == null || asteroid == null || ship == null || !CanMineResource(ship, asteroid.OreId)) return false;
            member.TargetAsteroidId = asteroidId; member.Order = FleetOrder.Approaching; member.CycleProgressSeconds = 0; member.DroneCycleProgressSeconds=0; member.MiningCycles?.Clear();
            return true;
        }

        public static void ReturnShip(GameSave save, string shipUid, bool returnAfterUnload)
        {
            ReturnShipWithResult(save, shipUid, returnAfterUnload);
        }

        public static FleetCommandResult ReturnShipWithResult(GameSave save, string shipUid, bool returnAfterUnload)
        {
            var result = new FleetCommandResult { RequestedCount = 1 };
            var member = save.Operation?.Fleet.Find(item => item.ShipUid == shipUid); var ship = FindShip(save, shipUid);
            if (member == null || ship == null)
            {
                result.FailedCount = 1; result.Messages.Add("Корабль не найден в активном флоте."); return result;
            }
            if (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay)
            {
                result.Success = true; result.ImmediateCount = 1;
                result.Messages.Add($"{Catalog.GetShip(ship.HullId)?.DisplayName}: возврат уже выполняется.");
                return result;
            }
            if (save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == shipUid)
            {
                member.CoreReturnQueued = true;
                member.CoreReturnAfterUnload = returnAfterUnload;
                save.Operation.IndustrialCoreStopRequested = true;
                result.Success = true; result.QueuedCount = 1;
                var message = $"{Catalog.GetShip(ship.HullId)?.DisplayName}: возврат поставлен в очередь до границы текущего цикла Industrial Core ({save.Operation.IndustrialCoreSecondsLeft:0} сек.).";
                result.Messages.Add(message); Log(save.Operation, message); return result;
            }
            DisableBursts(save, shipUid);
            BeginReturn(save, member, ship, returnAfterUnload);
            result.Success = true; result.ImmediateCount = 1; result.Messages.Add($"{Catalog.GetShip(ship.HullId)?.DisplayName}: возвращается на Jita 4-4."); return result;
        }

        public static void ReturnFleet(GameSave save, bool returnAfterUnload)
        {
            ReturnFleetWithResult(save, returnAfterUnload);
        }

        public static FleetCommandResult ReturnFleetWithResult(GameSave save, bool returnAfterUnload)
        {
            var combined = new FleetCommandResult();
            var uids = save?.Operation?.Fleet?.Select(item => item.ShipUid).ToArray() ?? Array.Empty<string>();
            foreach (var uid in uids)
            {
                var one = ReturnShipWithResult(save, uid, returnAfterUnload);
                combined.RequestedCount += one.RequestedCount; combined.ImmediateCount += one.ImmediateCount;
                combined.QueuedCount += one.QueuedCount; combined.FailedCount += one.FailedCount;
                combined.Messages.AddRange(one.Messages);
            }
            combined.Success = combined.RequestedCount > 0 && combined.FailedCount < combined.RequestedCount;
            return combined;
        }

        public static void Stop(GameSave save)
        {
            TryStop(save, out _);
        }

        public static bool TryStop(GameSave save, out string message, long nowUnix = 0)
        {
            if (save?.Operation == null) { message = "Операция не активна."; return false; }
            if (save.Operation.IndustrialCoreActive)
            {
                message = $"Нельзя завершить операцию: Industrial Core закончит цикл через {save.Operation.IndustrialCoreSecondsLeft:0} сек.";
                return false;
            }
            MiningSiteService.SnapshotActiveOperation(save, ResolveNow(nowUnix));
            foreach (var member in save.Operation.Fleet.ToArray()) Dock(save, member, false);
            save.Operation.Active = false; save.Operation.StopAfterFleetWarp = false; save.Operation.Enemies.Clear(); save.Operation.BurstEffects?.Clear(); save.Operation.IndustrialCoreActive = false; save.Operation.IndustrialCoreStopRequested = false; save.Operation.IndustrialCoreShipUid = string.Empty; ClearAutomaticCoreRestartIntent(save.Operation); save.Operation.BurstsActive = false; save.Operation.BurstShipUid = string.Empty;
            message = "Операция завершена, флот вернулся на Jita 4-4.";
            return true;
        }

        public static bool RequestStopWithWarp(GameSave save, out string message)
        {
            var op = save?.Operation;
            if (op?.Active != true) { message = "Операция не активна."; return false; }
            if (op.Fleet.Count == 0)
            {
                FinalizeStoppedOperation(save);
                message = "Операция завершена.";
                return true;
            }

            op.StopAfterFleetWarp = true;
            var result = ReturnFleetWithResult(save, false);
            if (!result.Success)
            {
                op.StopAfterFleetWarp = false;
                message = result.Summary;
                return false;
            }
            message = result.QueuedCount > 0
                ? $"Флот отварпывает домой; {result.QueuedCount} корабл. ждёт окончания цикла Industrial Core."
                : "Флот разворачивается и отварпывает домой. Операция завершится после ухода последнего корабля.";
            return true;
        }

        public static void Tick(GameSave save, float dt, Action<string> notify = null, long nowUnix = 0)
        {
            var op = save?.Operation; if (op == null || !op.Active || op.BeltWarpActive || op.TravelActive || dt <= 0) return;
            nowUnix = ResolveNow(nowUnix);
            MiningSiteService.Tick(save, nowUnix);
            if (!op.IndustrialCoreActive && op.AutoRestartIndustrialCore && MiningSiteService.RemainingM3(op.Asteroids) > 0)
                RestartAutomaticCoreAfterArrival(save, notify);
            op.ElapsedSeconds += dt;
            TickBurstEffects(op, dt);
            TickBurstModules(save, dt, notify);
            var location = Catalog.GetLocation(op.LocationId);
            if (op.IndustrialCoreActive)
            {
                var remainingCoreDt = dt;
                while (op.IndustrialCoreActive && remainingCoreDt > 0)
                {
                    if (remainingCoreDt + .0001f < op.IndustrialCoreSecondsLeft)
                    {
                        op.IndustrialCoreSecondsLeft -= remainingCoreDt;
                        remainingCoreDt = 0;
                    }
                    else
                    {
                        remainingCoreDt -= Math.Max(0, op.IndustrialCoreSecondsLeft);
                        op.IndustrialCoreSecondsLeft = 0;
                        FinishCoreCycle(save, notify);
                    }
                }
            }
            op.RaidTimerSeconds -= dt;
            if (location.Threat > 0 && op.RaidTimerSeconds <= 0) { SpawnRaid(save, location, notify); op.RaidTimerSeconds = NextRaid(location); }

            foreach (var member in op.Fleet.ToArray()) TickMember(save, member, dt, notify);
            if (op.StopAfterFleetWarp && op.Fleet.Count == 0)
            {
                FinalizeStoppedOperation(save);
                notify?.Invoke("Флот ушёл в варп. Операция завершена.");
                return;
            }
            TickEnemies(save, dt, notify);
            var currentSite = Catalog.GetLocation(op.LocationId);
            var depletedStaticBelt = op.AutoNextBelt && currentSite?.SiteKind == MiningSiteKind.StaticBelt &&
                                     MiningSiteService.RemainingM3(op.Asteroids) <= 0;
            if (depletedStaticBelt && ShouldSnapshotDepletion(save, op))
                MiningSiteService.SnapshotActiveOperation(save, nowUnix);
            if (depletedStaticBelt && op.IndustrialCoreActive)
            {
                if (!op.IndustrialCoreStopRequested)
                {
                    op.AutoRestartIndustrialCore = true;
                    op.AutoRestartIndustrialCoreShipUid = op.IndustrialCoreShipUid ?? string.Empty;
                    op.IndustrialCoreStopRequested = true;
                    notify?.Invoke("Белт исчерпан: Industrial Core остановится на границе цикла, затем флот отварпает дальше.");
                }
                return;
            }
            if (depletedStaticBelt && CanAttemptAutomaticRoute(save, op.LocationId, nowUnix) &&
                MiningSiteService.TryGetNextAutomaticBelt(save, out var nextBelt, nowUnix))
            {
                if (TravelService.TryStartAutomaticBeltTravel(save, nextBelt.Id, out var warpMessage, nowUnix)) notify?.Invoke(warpMessage);
            }
        }

        public static void CompleteSameSystemBeltWarp(GameSave save, LocationDefinition destination, long nowUnix = 0)
        {
            var op = save?.Operation;
            if (op?.Active != true || destination == null) return;
            nowUnix = ResolveNow(nowUnix);
            var preserveAutomaticBurstIntent = (op.BeltWarpIsAutomatic || op.TravelIsAutomatic) &&
                                               op.BurstsActive && !string.IsNullOrWhiteSpace(op.BurstShipUid);
            op.LocationId = destination.Id;
            op.VisitNumber++;
            op.ElapsedSeconds = 0;
            op.RaidTimerSeconds = NextRaid(destination);
            op.Enemies.Clear();
            op.Log.Clear();
            op.BurstEffects?.Clear();
            if (!preserveAutomaticBurstIntent)
            {
                SetAllBurstModulesActive(save, false);
                op.BurstsActive = false;
                op.BurstShipUid = string.Empty;
            }
            op.WarpPointInitialized = false;
            EnsureSharedWarpPoint(op);
            MiningSiteService.LoadIntoOperation(save, destination, nowUnix);
            for (var index = 0; index < op.Fleet.Count; index++)
            {
                var member = op.Fleet[index];
                var ship = FindShip(save, member.ShipUid);
                if (ship != null) ship.Location = ShipLocation.Belt;
                member.Order = FleetOrder.Idle;
                member.TargetAsteroidId = string.Empty;
                member.X = -22 + (index % 3) * 3.2f;
                member.Y = -2 + (index / 3) * 2.2f;
                member.Z = -4 + (index % 2) * 2;
                member.CycleProgressSeconds = 0;
                member.DroneCycleProgressSeconds = 0;
                ClearCombatDroneEngagement(member);
                member.MiningCycles?.Clear();
                ClearWarpState(member);
                if (op.AutoRetarget) TryAssignAutomaticTarget(save, member);
            }
            if (op.AutoRestartIndustrialCore) RestartAutomaticCoreAfterArrival(save, null);
            if (preserveAutomaticBurstIntent) RestartAutomaticBurstsAfterArrival(save);
            Log(op, $"Флот прибыл: {destination.DisplayName}, security {destination.Security:0.0}.");
        }

        static void FinalizeStoppedOperation(GameSave save)
        {
            var op = save?.Operation;if(op==null)return;
            op.Active=false;op.StopAfterFleetWarp=false;op.Enemies.Clear();op.BurstEffects?.Clear();
            op.IndustrialCoreActive=false;op.IndustrialCoreStopRequested=false;op.IndustrialCoreShipUid=string.Empty;
            ClearAutomaticCoreRestartIntent(op);
            op.BurstsActive=false;op.BurstShipUid=string.Empty;
        }

        /// <summary>
        /// Returns the serialized simulation-time countdown to the next NPC raid.
        /// A raid is not promised while the location already holds its NPC cap.
        /// </summary>
        public static bool TryGetNextNpcRaidEta(GameSave save,out float seconds)
        {
            seconds=0;
            var operation=save?.Operation;var location=Catalog.GetLocation(operation?.LocationId);
            if(operation?.Active!=true||location==null||location.Threat<=0||location.MaxNpcCount<=0)return false;
            if((operation.Enemies?.Count??0)>=location.MaxNpcCount)return false;
            if(float.IsNaN(operation.RaidTimerSeconds)||float.IsInfinity(operation.RaidTimerSeconds)||operation.RaidTimerSeconds>=99_000f)return false;
            seconds=Math.Max(0,operation.RaidTimerSeconds);
            return true;
        }

        public static bool ToggleBursts(GameSave save, out string message)
        {
            var shipUid = save?.Operation?.BurstsActive == true && !string.IsNullOrWhiteSpace(save.Operation.BurstShipUid)
                ? save.Operation.BurstShipUid
                : ResolveDefaultBurstShipUid(save);
            if (save?.Operation?.BurstsActive == true && string.IsNullOrWhiteSpace(save.Operation.BurstShipUid)) save.Operation.BurstShipUid = shipUid;
            return ToggleBursts(save, shipUid, out message);
        }

        public static bool ToggleBursts(GameSave save, string shipUid, out string message)
        {
            var op = save?.Operation;
            if (op == null || !op.Active) { message = "Операция не активна."; return false; }
            var command = GetFleetMember(save, shipUid);
            var ship = FindShip(save, shipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            var fittedBursts = GetInstalledBurstModules(ship, hull).ToArray();
            if (command == null || ship?.Location != ShipLocation.Belt || hull?.IsCommandShip != true || fittedBursts.Length == 0)
            {
                message = "Выбранный корабль не находится в зоне добычи или на нём нет Mining Foreman Burst.";
                return false;
            }
            if (!fittedBursts.Any(fitted => BurstEffectForCharge(fitted.ChargeId) != BurstEffect.None))
            {
                message = "Для Mining Foreman Burst не настроен встроенный эффект.";
                return false;
            }
            if (op.BurstsActive && string.Equals(op.BurstShipUid, shipUid, StringComparison.Ordinal))
            {
                DisableBursts(save, shipUid);
                message = $"Mining Foreman Burst выключен на {hull.DisplayName}.";
                return true;
            }

            SetAllBurstModulesActive(save, false);
            foreach (var fitted in fittedBursts)
            {
                fitted.Active = BurstEffectForCharge(fitted.ChargeId) != BurstEffect.None;
                fitted.BurstCycleSecondsLeft = 0;
            }
            op.BurstShipUid = shipUid;
            op.BurstsActive = fittedBursts.Any(fitted => fitted.Active);
            TickBurstModules(save, 0f, null);
            message = $"Mining Foreman Burst включён на {hull.DisplayName}; первый бесплатный импульс отправлен всему флоту.";
            return true;
        }

        public static bool ToggleCore(GameSave save, out string message)
        {
            var shipUid = save?.Operation?.IndustrialCoreActive == true && !string.IsNullOrWhiteSpace(save.Operation.IndustrialCoreShipUid)
                ? save.Operation.IndustrialCoreShipUid
                : ResolveDefaultCoreShipUid(save);
            return ToggleCore(save, shipUid, out message);
        }

        public static bool ToggleCore(GameSave save, string shipUid, out string message)
        {
            var op = save?.Operation;
            if (op == null || !op.Active) { message = "Операция не активна."; return false; }
            var command = GetFleetMember(save, shipUid);
            var ship = FindShip(save, shipUid); var hull = Catalog.GetShip(ship?.HullId);
            var fittedCore = GetCompatibleCoreFitting(ship, hull);
            var core = Catalog.GetModule(fittedCore?.ModuleId);
            if (command == null || ship?.Location != ShipLocation.Belt || hull?.SupportsIndustrialCore != true || core == null)
            {
                message = "Выбранный корабль не находится в зоне добычи или на нём нет подходящего Industrial Core.";
                return false;
            }
            if (op.IndustrialCoreActive)
            {
                if (!string.Equals(op.IndustrialCoreShipUid, shipUid, StringComparison.Ordinal))
                {
                    message = "Industrial Core уже активен на другом корабле.";
                    return false;
                }
                ClearAutomaticCoreRestartIntent(op);
                op.IndustrialCoreStopRequested = true;
                message = $"Ядро остановится после текущего цикла ({op.IndustrialCoreSecondsLeft:0} сек.).";
                return true;
            }
            ClearAutomaticCoreRestartIntent(op);
            var cycle = CoreCycleSeconds(hull, core);
            foreach (var other in save.Ships.SelectMany(candidate => candidate.Modules).Where(candidate => Catalog.GetModule(candidate.ModuleId)?.Kind == ModuleKind.IndustrialCore)) other.Active = false;
            fittedCore.Active = true;
            op.IndustrialCoreActive = true; op.IndustrialCoreStopRequested = false; op.IndustrialCoreSecondsLeft = cycle; op.IndustrialCoreShipUid = ship.Uid; message = $"{core.DisplayName} активирован на {cycle:0} сек.; топливо не требуется."; return true;
        }

        public static bool TryCompressFleetMiningHolds(GameSave save,string sourceShipUid,out CompressionResult result,out string message)
        {
            result=new CompressionResult();
            var operation=save?.Operation;
            if(operation?.Active!=true){message="Сжатие доступно только в активной операции.";return false;}
            if(string.IsNullOrWhiteSpace(sourceShipUid)){message="Корабль-сжималка не выбран.";return false;}
            var sourceMember=operation.Fleet?.Find(member=>member!=null&&string.Equals(member.ShipUid,sourceShipUid,StringComparison.Ordinal));
            var sourceShip=FindShip(save,sourceShipUid);var sourceHull=Catalog.GetShip(sourceShip?.HullId);var package=Catalog.GetPackage(sourceShip?.PackageId);
            if(sourceMember==null||sourceShip?.Location!=ShipLocation.Belt||sourceHull==null){message="Корабль-сжималка должен находиться в зоне добычи активного флота.";return false;}
            if(package?.Role!=PreparedPackageRole.Booster||!string.Equals(package.HullId,sourceShip.HullId,StringComparison.OrdinalIgnoreCase))
            {message="Сжатие доступно только штатному Booster-комплекту.";return false;}
            var sourcePilot=FindPilot(save,sourceMember.PilotId);
            if(!PreparedPackageService.CanUsePackage(sourcePilot,package)){message="Пилоту не хватает навыков полного Booster-комплекта со сжималками.";return false;}
            if(operation.IndustrialCoreActive!=true||!string.Equals(operation.IndustrialCoreShipUid,sourceShipUid,StringComparison.Ordinal)||GetCompatibleCoreFitting(sourceShip,sourceHull)==null)
            {message="Для сжатия включи Industrial Core на выбранном Booster-корабле.";return false;}

            var supported=(package.CompressorModuleIds??Array.Empty<string>())
                .Select(Catalog.GetModule)
                .Where(module=>module?.Kind==ModuleKind.Compressor)
                .Aggregate(CompressionKind.None,(combined,module)=>combined|module.CompressionKind);
            if(supported==CompressionKind.None){message="Этот Booster-комплект не умеет сжимать ресурсы.";return false;}

            var ships=(operation.Fleet??new List<FleetMemberSave>())
                .Select(member=>FindShip(save,member?.ShipUid))
                .Where(ship=>ship?.Location==ShipLocation.Belt)
                .GroupBy(ship=>ship.Uid,StringComparer.Ordinal)
                .Select(group=>group.First())
                .ToArray();
            var replacements=new Dictionary<ShipSave,List<InventoryStack>>();
            foreach(var ship in ships)
            {
                var replacement=new List<InventoryStack>();var affected=false;
                void Add(string itemId,double quantity)
                {
                    var existing=replacement.Find(stack=>stack!=null&&string.Equals(stack.ItemId,itemId,StringComparison.OrdinalIgnoreCase));
                    if(existing==null)replacement.Add(new InventoryStack{ItemId=itemId,Quantity=quantity});else existing.Quantity+=quantity;
                }
                foreach(var stack in ship.MiningHold??new List<InventoryStack>())
                {
                    if(stack==null)continue;
                    var resource=stack.Quantity>0?Catalog.GetOre(stack.ItemId):null;
                    var kind=Catalog.CompressionKindFor(resource);
                    if(resource==null||kind==CompressionKind.None||(supported&kind)==0)
                    {
                        if(Catalog.TryGetCompressedSource(stack.ItemId,out _))Add(stack.ItemId,stack.Quantity);
                        else replacement.Add(new InventoryStack{ItemId=stack.ItemId,Quantity=stack.Quantity});
                        continue;
                    }
                    var compressedId=Catalog.CompressedItemId(resource.Id);
                    if(string.IsNullOrEmpty(compressedId)||!Catalog.TryGetItemVolumeM3(compressedId,out var compressedUnitVolume))
                    {message=$"Не удалось определить объём сжатого ресурса {resource.DisplayName}.";return false;}
                    Add(compressedId,stack.Quantity);affected=true;result.StacksCompressed++;result.UnitsCompressed+=stack.Quantity;
                    result.RawVolumeM3+=stack.Quantity*resource.UnitVolumeM3;result.CompressedVolumeM3+=stack.Quantity*compressedUnitVolume;
                }
                if(affected)result.ShipsAffected++;
                replacements.Add(ship,replacement);
            }
            if(result.UnitsCompressed<=0){message="В mining hold флота нет несжатых ресурсов подходящего типа.";return false;}
            foreach(var pair in replacements){pair.Key.MiningHold.Clear();pair.Key.MiningHold.AddRange(pair.Value);}
            message=$"Сжато {result.UnitsCompressed:N0} ед. в {result.ShipsAffected} корабл.: {result.RawVolumeM3:N1} → {result.CompressedVolumeM3:N1} м³.";
            return true;
        }

        public static float CoreFuelPerCycle(CharacterSave pilot, ShipDefinition hull, MiningModuleDefinition core)
        {
            // Deliberate offline-simulator rule: Industrial Core never consumes fuel.
            return 0;
        }

        public static float CoreCycleSeconds(ShipDefinition hull, MiningModuleDefinition core)
        {
            return core?.CycleSeconds > 0 ? core.CycleSeconds : 300f;
        }

        static void TickMember(GameSave save, FleetMemberSave member, float dt, Action<string> notify)
        {
            var ship = FindShip(save, member.ShipUid); var pilot = FindPilot(save, member.PilotId); var hull = Catalog.GetShip(ship?.HullId);
            if (ship == null || pilot == null || hull == null) { save.Operation.Fleet.Remove(member); return; }
            if (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay)
            {
                TickReturnWarp(save, member, ship, dt, notify);
                return;
            }
            if (save.Operation.AutoRetarget && member.Order == FleetOrder.Idle && !member.CoreReturnQueued)
            {
                // Assignment happens at this tick boundary. Never grant the new
                // target the dt that elapsed before it was selected.
                if (TryAssignAutomaticTarget(save, member, notify)) return;
                if (member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay || string.IsNullOrEmpty(member.TargetAsteroidId)) return;
            }
            var canStartPendingAutoUnload=member.Order==FleetOrder.Idle&&save.Operation.AutoUnload&&!member.CoreReturnQueued&&!string.IsNullOrEmpty(member.TargetAsteroidId);
            if (member.Order is not (FleetOrder.Approaching or FleetOrder.Mining)&&!canStartPendingAutoUnload) return;
            var asteroid = save.Operation.Asteroids.Find(item => item.Id == member.TargetAsteroidId && item.RemainingUnits > 0);
            if (asteroid == null)
            {
                member.Order = FleetOrder.Idle; member.TargetAsteroidId = string.Empty;
                // Another fleet member may have depleted the old target at the
                // end of this same interval. Retarget now, but start moving or
                // mining only in the next tick so the old dt is never reused.
                if(save.Operation.AutoRetarget)TryAssignAutomaticTarget(save,member,notify);
                return;
            }
            var resource = Catalog.GetOre(asteroid.OreId);
            if(resource==null){member.Order=FleetOrder.Idle;member.TargetAsteroidId=string.Empty;return;}
            var miningModules=ship.Modules.Where(fitted=>CanMineResource(fitted,resource)).OrderBy(fitted=>fitted.Slot).ToArray();
            var miningDrone = Catalog.GetDrone(ship.MiningDroneId);
            var hasMiningDrones = resource?.Kind == ResourceKind.Ore && miningDrone?.Mining == true && ship.MiningDroneCount > 0 && SkillService.GetLevel(pilot, "drones") > 0;
            if (miningModules.Length == 0 && !hasMiningDrones) { member.Order = FleetOrder.Idle; member.TargetAsteroidId = string.Empty; return; }
            var heldVolumeM3=HoldVolume(ship.MiningHold);
            var freeHoldM3=MiningHoldCapacity(pilot,hull)-heldVolumeM3;
            if(freeHoldM3<resource.UnitVolumeM3)
            {
                if(save.Operation.AutoUnload)BeginAutomaticUnload(save,member,ship,pilot,notify);
                else { member.Order=FleetOrder.Idle;member.TargetAsteroidId=string.Empty;notify?.Invoke($"{pilot.Name}: mining hold заполнен."); }
                return;
            }
            var range = miningModules.Length>0?miningModules.Min(fitted=>MiningRangeKm(pilot,hull,Catalog.GetModule(fitted.ModuleId))):60f;
            var rangeBurstStrength = BurstStrengthForTarget(save, member.ShipUid, BurstEffect.Range);
            if(miningModules.Length>0 && rangeBurstStrength>0)range*=1f+.40f*rangeBurstStrength;
            member.PreferredRangeKm = range * .9f;
            var distance = Distance(member.X, member.Y, member.Z, asteroid.X, asteroid.Y, asteroid.Z) * KmPerWorldUnit;
            if (distance > member.PreferredRangeKm + MiningRangeArrivalToleranceKm)
            {
                if (save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == ship.Uid) return;
                MoveToward(member, asteroid, hull.SpeedMps / 1000f * dt / KmPerWorldUnit, member.PreferredRangeKm / KmPerWorldUnit);
                member.Order = FleetOrder.Approaching; return;
            }
            member.Order = FleetOrder.Mining;
            member.MiningCycles??=new();
            member.MiningCycles.RemoveAll(state=>miningModules.All(fitted=>fitted.Slot!=state.Slot));
            foreach(var fitted in miningModules)
            {
                var module=Catalog.GetModule(fitted.ModuleId);var state=member.MiningCycles.Find(saved=>saved.Slot==fitted.Slot);
                if(state==null){state=new MiningCycleSave{Slot=fitted.Slot};member.MiningCycles.Add(state);}
                var cycle=MiningCycleSeconds(pilot,hull,module,save,member,fitted,asteroid);state.ProgressSeconds+=dt;member.CycleProgressSeconds=state.ProgressSeconds;
                while(state.ProgressSeconds>=cycle&&member.Order==FleetOrder.Mining&&asteroid.RemainingUnits>0){state.ProgressSeconds-=cycle;CompleteCycle(save,pilot,ship,hull,module,fitted,asteroid,notify);}
                if(member.Order!=FleetOrder.Mining||asteroid.RemainingUnits<=0)break;
            }
            if(hasMiningDrones&&save.Operation.Enemies.Count==0&&member.Order==FleetOrder.Mining)
            {
                member.DroneCycleProgressSeconds+=dt;
                while(member.DroneCycleProgressSeconds>=60f&&member.Order==FleetOrder.Mining&&asteroid.RemainingUnits>0){member.DroneCycleProgressSeconds-=60f;CompleteDroneCycle(save,pilot,ship,hull,asteroid,notify);}
            }
        }

        static void CompleteCycle(GameSave save, CharacterSave pilot, ShipSave ship, ShipDefinition hull, MiningModuleDefinition module, FittedModuleSave fitted, AsteroidSave asteroid, Action<string> notify)
        {
            var ore = Catalog.GetOre(asteroid.OreId); var freeM3 = MiningHoldCapacity(pilot,hull) - HoldVolume(ship.MiningHold);
            if (freeM3 < ore.UnitVolumeM3)
            {
                var member = save.Operation.Fleet.Find(item => item.ShipUid == ship.Uid);
                if(save.Operation.AutoUnload){BeginAutomaticUnload(save,member,ship,pilot,notify);return;}
                member.Order = FleetOrder.Idle; member.TargetAsteroidId = string.Empty; notify?.Invoke($"{pilot.Name}: mining hold заполнен."); return;
            }
            var memberSave=save.Operation.Fleet.Find(x=>x.ShipUid==ship.Uid);var crystal=CompatibleCrystal(fitted,ore.Id,ship);
            var standardVolume = MiningYieldForModule(pilot,hull,module,crystal,ship);
            var burstStrength=BurstStrengthForTarget(save,ship.Uid,BurstEffect.Efficiency);
            var critChance = ore.Kind==ResourceKind.Gas?0:MiningCriticalChance(pilot,hull,module,save,memberSave);
            var critical = Random.NextDouble() < critChance;
            var standardRatio=Math.Min(standardVolume,freeM3)/ore.UnitVolumeM3;
            var standardUnits=Math.Min(StochasticUnits(standardRatio),asteroid.RemainingUnits);
            if(standardUnits<=0)return;
            var bonusUnits=0d;
            if(critical)
            {
                var bonusMultiplier=MiningCriticalBonusYield(pilot,hull,module);
                var remainingFreeM3=Math.Max(0,freeM3-standardUnits*ore.UnitVolumeM3);
                var bonusRatio=Math.Min(standardVolume*bonusMultiplier,remainingFreeM3)/ore.UnitVolumeM3;
                bonusUnits=StochasticUnits(bonusRatio);
            }
            AddItem(ship.MiningHold,ore.Id,standardUnits+bonusUnits);asteroid.RemainingUnits-=standardUnits;
            var residueChance = MiningResidueChance(module,crystal,burstStrength);
            var residueMultiplier=module.ResidueMultiplier+(crystal?.ResidueMultiplierBonus??0);
            if (residueChance > 0 && Random.NextDouble() < residueChance) asteroid.RemainingUnits = Math.Max(0, asteroid.RemainingUnits - standardUnits * residueMultiplier);
            WearCrystal(fitted,crystal,pilot,ship,notify);
            if (critical) notify?.Invoke($"CRITICAL: {pilot.Name} получил бонусную добычу без истощения цели.");
            if (asteroid.RemainingUnits <= 0)
            {
                memberSave.Order = FleetOrder.Idle; memberSave.TargetAsteroidId = string.Empty; notify?.Invoke($"{ore.DisplayName}: источник истощён.");
                if(save.Operation.AutoRetarget)TryAssignAutomaticTarget(save,memberSave,notify);
            }
            if(save.Operation.AutoUnload&&MiningHoldCapacity(pilot,hull)-HoldVolume(ship.MiningHold)<ore.UnitVolumeM3)
                BeginAutomaticUnload(save,memberSave,ship,pilot,notify);
        }

        static void CompleteDroneCycle(GameSave save, CharacterSave pilot, ShipSave ship, ShipDefinition hull, AsteroidSave asteroid, Action<string> notify)
        {
            var ore=Catalog.GetOre(asteroid.OreId);var freeM3=MiningHoldCapacity(pilot,hull)-HoldVolume(ship.MiningHold);
            if(ore==null||freeM3<ore.UnitVolumeM3)
            {
                var member=save.Operation.Fleet.Find(x=>x.ShipUid==ship.Uid);
                if(member!=null&&save.Operation.AutoUnload){BeginAutomaticUnload(save,member,ship,pilot,notify);return;}
                if(member!=null){member.Order=FleetOrder.Idle;member.TargetAsteroidId=string.Empty;}notify?.Invoke($"{pilot.Name}: mining hold заполнен.");return;
            }
            var volume=MiningDroneYieldM3PerCycle(save,pilot,hull,ship,60f);
            var units=Math.Min(StochasticUnits(Math.Min(volume,freeM3)/ore.UnitVolumeM3),asteroid.RemainingUnits);
            if(units<=0)return;AddItem(ship.MiningHold,ore.Id,units);asteroid.RemainingUnits-=units;
            if(asteroid.RemainingUnits<=0)
            {
                var member=save.Operation.Fleet.Find(x=>x.ShipUid==ship.Uid);
                if(member!=null){member.Order=FleetOrder.Idle;member.TargetAsteroidId=string.Empty;if(save.Operation.AutoRetarget)TryAssignAutomaticTarget(save,member,notify);}
                notify?.Invoke($"{ore.DisplayName}: источник истощён.");
            }
            var memberAfter=save.Operation.Fleet.Find(x=>x.ShipUid==ship.Uid);
            if(memberAfter!=null&&save.Operation.AutoUnload&&MiningHoldCapacity(pilot,hull)-HoldVolume(ship.MiningHold)<ore.UnitVolumeM3)
                BeginAutomaticUnload(save,memberAfter,ship,pilot,notify);
        }

        public static bool TryAssignAutomaticTarget(GameSave save,FleetMemberSave member,Action<string> notify=null)
        {
            var op=save?.Operation;var ship=FindShip(save,member?.ShipUid);var pilot=FindPilot(save,member?.PilotId);var hull=Catalog.GetShip(ship?.HullId);
            if(op==null||member==null||ship==null||pilot==null||hull==null||ship.Location!=ShipLocation.Belt||member.CoreReturnQueued)return false;
            if(Catalog.GetPackage(ship.PackageId)?.Role==PreparedPackageRole.Booster)
            {
                member.TargetAsteroidId=string.Empty;member.Order=FleetOrder.Idle;return false;
            }
            var mineable=op.Asteroids.Where(asteroid=>asteroid.RemainingUnits>0&&CanMineResource(ship,asteroid.OreId)).ToArray();
            if(mineable.Length==0){member.TargetAsteroidId=string.Empty;member.Order=FleetOrder.Idle;return false;}
            var heldVolumeM3=HoldVolume(ship.MiningHold);
            var freeHoldM3=MiningHoldCapacity(pilot,hull)-heldVolumeM3;
            var compatible=mineable.Where(asteroid=>
            {
                var resource=Catalog.GetOre(asteroid.OreId);
                return resource!=null&&resource.UnitVolumeM3<=freeHoldM3+1e-6;
            }).ToArray();
            if(compatible.Length==0)
            {
                // Do not repeatedly attach a full ship to different rocks. With
                // auto-unload enabled, the same condition starts its round trip.
                member.TargetAsteroidId=string.Empty;member.Order=FleetOrder.Idle;
                if(op.AutoUnload&&heldVolumeM3>1e-6)BeginAutomaticUnload(save,member,ship,pilot,notify);
                return false;
            }
            var occupied=op.Fleet.Where(other=>other!=member&&!string.IsNullOrEmpty(other.TargetAsteroidId)&&op.Asteroids.Exists(asteroid=>asteroid.Id==other.TargetAsteroidId&&asteroid.RemainingUnits>0)).Select(other=>other.TargetAsteroidId).ToHashSet();
            var target=compatible.Where(asteroid=>!occupied.Contains(asteroid.Id)).OrderByDescending(AsteroidRemainingM3).ThenBy(asteroid=>asteroid.Id,StringComparer.Ordinal).FirstOrDefault()
                ??compatible.OrderByDescending(AsteroidRemainingM3).ThenBy(asteroid=>asteroid.Id,StringComparer.Ordinal).First();
            if(!AssignTarget(save,member.ShipUid,target.Id))return false;
            notify?.Invoke($"{FindPilot(save,member.PilotId)?.Name}: автоматически выбран {Catalog.GetOre(target.OreId)?.DisplayName}, {target.RemainingUnits:N0} ед.");
            return true;
        }

        public static bool TryAssignAutomaticTarget(GameSave save,string shipUid,Action<string> notify=null)
        {
            return TryAssignAutomaticTarget(save,save?.Operation?.Fleet.Find(member=>member.ShipUid==shipUid),notify);
        }

        static void BeginAutomaticUnload(GameSave save,FleetMemberSave member,ShipSave ship,CharacterSave pilot,Action<string> notify)
        {
            if(member==null||ship==null||ship.Location!=ShipLocation.Belt||member.CoreReturnQueued)return;
            var result=ReturnShipWithResult(save,ship.Uid,true);
            notify?.Invoke(result.QueuedCount>0
                ?$"{pilot?.Name}: трюм заполнен, автовыгрузка начнётся после цикла Industrial Core."
                :$"{pilot?.Name}: трюм заполнен, автоматическая выгрузка — разгон, варп и возврат.");
        }

        static void Dock(GameSave save, FleetMemberSave member, bool rejoin)
        {
            var ship = FindShip(save, member.ShipUid); var pilot = FindPilot(save, member.PilotId); if (ship == null) { save.Operation.Fleet.Remove(member); return; }
            TransferAll(ship.MiningHold, save.StationInventory);
            // If the player finishes the operation while this ship is already on
            // an unload round trip, let the current warp complete but do not send
            // it back into the belt afterwards.
            if (rejoin && save.Operation.StopAfterFleetWarp) rejoin = false;
            if (rejoin && save.Operation.Active)
            {
                ship.Location = ShipLocation.Belt;
                RestoreReturnOrder(save, member, ship);
                ClearWarpState(member);
                Log(save.Operation, $"{pilot?.Name}: разгрузился и вернулся в зону добычи.");
            }
            else
            {
                DisableBursts(save, ship.Uid);
                ship.Location = ShipLocation.Station;
                ClearWarpState(member);
                save.Operation.Fleet.Remove(member);
                Log(save.Operation, $"{pilot?.Name}: пришвартован на Jita 4-4.");
            }
        }

        static void BeginReturn(GameSave save, FleetMemberSave member, ShipSave ship, bool returnAfterUnload)
        {
            if (save == null || member == null || ship == null) return;
            EnsureSharedWarpPoint(save.Operation);
            member.CoreReturnQueued = false;
            member.CoreReturnAfterUnload = false;
            member.ResumeOrder = member.Order is FleetOrder.Approaching or FleetOrder.Mining or FleetOrder.Idle ? member.Order : FleetOrder.Idle;
            member.ResumeTargetAsteroidId = member.TargetAsteroidId ?? string.Empty;
            member.WarpOriginX = member.X;
            member.WarpOriginY = member.Y;
            member.WarpOriginZ = member.Z;
            member.Order = returnAfterUnload ? FleetOrder.UnloadAndReturn : FleetOrder.DockAndStay;
            member.ReturnAfterUnload = returnAfterUnload;
            member.WarpPhase = FleetWarpPhase.AligningOut;
            member.WarpPhaseSecondsLeft = WarpAlignSeconds;
            member.TransitSecondsLeft = returnAfterUnload ? WarpAlignSeconds + WarpTransitSeconds + WarpInSeconds : WarpAlignSeconds;
            if (!returnAfterUnload) member.TargetAsteroidId = string.Empty;
            ship.Location = ShipLocation.Transit;
        }

        static void TickReturnWarp(GameSave save, FleetMemberSave member, ShipSave ship, float dt, Action<string> notify)
        {
            var remaining = Math.Max(0, dt);
            while (remaining > 0 && save.Operation.Fleet.Contains(member))
            {
                if (member.WarpPhase == FleetWarpPhase.None)
                {
                    // Compatibility guard for a return created by a pre-warp save.
                    member.WarpPhase = member.ReturnAfterUnload ? FleetWarpPhase.InTransit : FleetWarpPhase.AligningOut;
                    member.WarpPhaseSecondsLeft = member.ReturnAfterUnload ? WarpTransitSeconds : WarpAlignSeconds;
                }

                var phaseStep = Math.Min(remaining, Math.Max(0, member.WarpPhaseSecondsLeft));
                member.WarpPhaseSecondsLeft = Math.Max(0, member.WarpPhaseSecondsLeft - phaseStep);
                member.TransitSecondsLeft = Math.Max(0, member.TransitSecondsLeft - phaseStep);
                remaining -= phaseStep;
                if (member.WarpPhaseSecondsLeft > .0001f) break;

                switch (member.WarpPhase)
                {
                    case FleetWarpPhase.AligningOut:
                        if (!member.ReturnAfterUnload)
                        {
                            Dock(save, member, false);
                            return;
                        }
                        member.WarpPhase = FleetWarpPhase.InTransit;
                        member.WarpPhaseSecondsLeft = WarpTransitSeconds;
                        break;
                    case FleetWarpPhase.InTransit:
                        TransferAll(ship.MiningHold, save.StationInventory);
                        member.WarpPhase = FleetWarpPhase.WarpingIn;
                        member.WarpPhaseSecondsLeft = WarpInSeconds;
                        Log(save.Operation, $"{FindPilot(save, member.PilotId)?.Name}: разгрузился на Jita 4-4 и варпает обратно.");
                        break;
                    case FleetWarpPhase.WarpingIn:
                        member.X = member.WarpOriginX;
                        member.Y = member.WarpOriginY;
                        member.Z = member.WarpOriginZ;
                        Dock(save, member, true);
                        return;
                    default:
                        return;
                }

                // A zero phase timer must never trap the transition loop.
                if (phaseStep <= 0 && member.WarpPhaseSecondsLeft <= 0) return;
            }
        }

        static void RestoreReturnOrder(GameSave save, FleetMemberSave member, ShipSave ship)
        {
            member.X = member.WarpOriginX;
            member.Y = member.WarpOriginY;
            member.Z = member.WarpOriginZ;
            member.TargetAsteroidId = member.ResumeTargetAsteroidId ?? string.Empty;
            var targetAlive = save.Operation.Asteroids.Exists(asteroid =>
                asteroid.Id == member.TargetAsteroidId && asteroid.RemainingUnits > 0 && CanMineResource(ship, asteroid.OreId));
            if (targetAlive)
            {
                member.Order = member.ResumeOrder is FleetOrder.Approaching or FleetOrder.Mining
                    ? member.ResumeOrder
                    : FleetOrder.Idle;
                return;
            }

            member.Order = FleetOrder.Idle;
            member.TargetAsteroidId = string.Empty;
            if (save.Operation.AutoRetarget) TryAssignAutomaticTarget(save, member);
        }

        static void ClearWarpState(FleetMemberSave member)
        {
            member.TransitSecondsLeft = 0;
            member.ReturnAfterUnload = false;
            member.WarpPhase = FleetWarpPhase.None;
            member.WarpPhaseSecondsLeft = 0;
            member.ResumeOrder = FleetOrder.Idle;
            member.ResumeTargetAsteroidId = string.Empty;
        }

        static void EnsureSharedWarpPoint(OperationSave op)
        {
            if (op == null || (op.WarpPointInitialized &&
                !float.IsNaN(op.WarpPointX) && !float.IsInfinity(op.WarpPointX) &&
                !float.IsNaN(op.WarpPointY) && !float.IsInfinity(op.WarpPointY) &&
                !float.IsNaN(op.WarpPointZ) && !float.IsInfinity(op.WarpPointZ))) return;
            var angleDegrees = (op.BeltSeed * 17 + op.VisitNumber * 61) % 360;
            var radians = angleDegrees * Mathf.Deg2Rad;
            op.WarpPointX = Mathf.Cos(radians) * 260f;
            op.WarpPointY = 18f;
            op.WarpPointZ = Mathf.Sin(radians) * 260f;
            op.WarpPointInitialized = true;
        }

        static void DockAndUnloadPreviousOperation(GameSave save, OperationSave op)
        {
            if (save == null || op == null) return;
            op.Fleet ??= new List<FleetMemberSave>();
            foreach (var member in op.Fleet.ToArray()) Dock(save, member, false);

            // Recover ships left in belt/transit by an older save or an interrupted location switch.
            foreach (var ship in save.Ships.Where(candidate => candidate.Location != ShipLocation.Station))
            {
                TransferAll(ship.MiningHold, save.StationInventory);
                ship.Location = ShipLocation.Station;
            }

            op.Fleet.Clear();
            op.BurstEffects?.Clear();
            SetAllBurstModulesActive(save, false);
            foreach (var fitted in save.Ships.SelectMany(ship => ship.Modules).Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.IndustrialCore)) fitted.Active = false;
            op.BurstsActive = false;
            op.BurstShipUid = string.Empty;
            op.StopAfterFleetWarp = false;
            op.IndustrialCoreActive = false;
            op.IndustrialCoreStopRequested = false;
            op.IndustrialCoreSecondsLeft = 0;
            op.IndustrialCoreShipUid = string.Empty;
            ClearAutomaticCoreRestartIntent(op);
        }

        static void FinishCoreCycle(GameSave save, Action<string> notify)
        {
            var ship = FindShip(save, save.Operation.IndustrialCoreShipUid);
            var command = save.Operation.Fleet.Find(member => member.ShipUid == ship?.Uid);
            var hull = Catalog.GetShip(ship?.HullId);
            var fittedCore = GetCompatibleCoreFitting(ship, hull);
            var core = Catalog.GetModule(fittedCore?.ModuleId);
            var queuedReturn = command?.CoreReturnQueued == true;
            var queuedReturnAfterUnload = command?.CoreReturnAfterUnload == true;
            if (ship == null || command == null || core == null || save.Operation.IndustrialCoreStopRequested)
            {
                DeactivateIndustrialCore(save, fittedCore);
                notify?.Invoke("Industrial Core остановлен.");
                if (queuedReturn && ship != null)
                {
                    command.CoreReturnQueued = false;
                    BeginReturn(save, command, ship, queuedReturnAfterUnload);
                    notify?.Invoke($"{hull?.DisplayName}: выполняет отложенный возврат.");
                }
                return;
            }
            fittedCore.Active = true;
            save.Operation.IndustrialCoreSecondsLeft = CoreCycleSeconds(hull, core);
        }

        static void SpawnRaid(GameSave save, LocationDefinition location, Action<string> notify)
        {
            if (save.Operation.Enemies.Count >= location.MaxNpcCount) return;
            var count = Math.Min(location.MaxNpcCount - save.Operation.Enemies.Count, Math.Max(1, (int)Math.Ceiling(location.Threat * location.MaxNpcCount)));
            var targets = save.Operation.Fleet.Where(member => FindShip(save, member.ShipUid)?.Location == ShipLocation.Belt).ToArray(); if (targets.Length == 0) return;
            for (var i = 0; i < count; i++) save.Operation.Enemies.Add(new EnemySave { Id = Guid.NewGuid().ToString("N"), Name = NpcName(location,i), X = 38 + i * 2, Y = 2, Z = 45 + i * 3, ShieldHp = 50 + location.Threat * 220, ArmorHp = 30 + location.Threat * 160, StructureHp = 35 + location.Threat * 180, Dps = 2 + location.Threat * 18, SpeedKmPerSecond = .35f, TargetShipUid = targets[Random.Next(targets.Length)].ShipUid });
            notify?.Invoke($"В зону добычи прибыли NPC: {count}.");
        }

        static void TickEnemies(GameSave save, float dt, Action<string> notify)
        {
            var op = save.Operation;
            var beltMembers = op.Fleet
                .Where(member => member != null && FindShip(save, member.ShipUid)?.Location == ShipLocation.Belt)
                .ToArray();
            foreach (var member in op.Fleet.Where(member => member != null && !beltMembers.Contains(member)))
                ClearCombatDroneEngagement(member);

            // Each ship owns one persisted combat-drone squad. A squad must
            // launch and physically cross the distance to its target before it
            // can apply DPS; this keeps a newly spawned NPC alive long enough
            // to exist in the rendered world even at 20x simulation speed.
            foreach (var member in beltMembers)
            {
                var combatDps = ShipDroneDps(save, member.ShipUid);
                if (combatDps <= 0)
                {
                    ClearCombatDroneEngagement(member);
                    continue;
                }

                var enemy = op.Enemies.FirstOrDefault(candidate =>
                    candidate != null && candidate.StructureHp > 0 &&
                    string.Equals(candidate.Id, member.CombatDroneTargetEnemyId, StringComparison.Ordinal));
                if (enemy == null)
                {
                    enemy = op.Enemies
                        .Where(candidate => candidate != null && candidate.StructureHp > 0)
                        .OrderBy(candidate => beltMembers.Count(other => string.Equals(other.CombatDroneTargetEnemyId, candidate.Id, StringComparison.Ordinal)))
                        .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (enemy == null)
                    {
                        ClearCombatDroneEngagement(member);
                        continue;
                    }
                    BeginCombatDroneEngagement(member, enemy);
                }

                var damagingSeconds = dt;
                if (member.CombatDroneTravelSecondsLeft > 0)
                {
                    var travelSeconds = Math.Min(member.CombatDroneTravelSecondsLeft, damagingSeconds);
                    member.CombatDroneTravelSecondsLeft = Math.Max(0, member.CombatDroneTravelSecondsLeft - travelSeconds);
                    damagingSeconds -= travelSeconds;
                }
                if (damagingSeconds > 0 && enemy.StructureHp > 0)
                    DamageEnemy(enemy, combatDps * damagingSeconds);
            }

            foreach (var destroyed in op.Enemies.Where(enemy => enemy == null || enemy.StructureHp <= 0).ToArray())
            {
                if (destroyed != null) notify?.Invoke($"NPC уничтожен: {destroyed.Name}.");
                op.Enemies.Remove(destroyed);
            }
            foreach (var member in beltMembers.Where(member => !string.IsNullOrWhiteSpace(member.CombatDroneTargetEnemyId) &&
                         op.Enemies.All(enemy => !string.Equals(enemy.Id, member.CombatDroneTargetEnemyId, StringComparison.Ordinal))))
                ClearCombatDroneEngagement(member);

            foreach (var enemy in op.Enemies.ToArray())
            {
                var targetShip = FindShip(save, enemy.TargetShipUid); var targetMember = op.Fleet.Find(x => x.ShipUid == enemy.TargetShipUid);
                if (targetShip == null || targetShip.Location != ShipLocation.Belt || targetMember == null) { var next = op.Fleet.FirstOrDefault(x => FindShip(save,x.ShipUid)?.Location == ShipLocation.Belt); enemy.TargetShipUid = next?.ShipUid; continue; }
                var distance = Distance(enemy.X,enemy.Y,enemy.Z,targetMember.X,targetMember.Y,targetMember.Z) * KmPerWorldUnit;
                if (distance > 8) MoveEnemyToward(enemy, targetMember, enemy.SpeedKmPerSecond * dt / KmPerWorldUnit, 6 / KmPerWorldUnit); else DamageShip(save,targetShip, enemy.Dps * dt);
                if (targetShip.StructureHp <= 0) DestroyShip(save, targetShip, notify);
            }
        }

        static void BeginCombatDroneEngagement(FleetMemberSave member, EnemySave enemy)
        {
            member.CombatDroneTargetEnemyId = enemy.Id;
            var distanceKm = Distance(member.X, member.Y, member.Z, enemy.X, enemy.Y, enemy.Z) * KmPerWorldUnit;
            member.CombatDroneTravelSecondsTotal = CombatDroneLaunchSeconds + distanceKm / CombatDroneSpeedKmPerSecond;
            member.CombatDroneTravelSecondsLeft = member.CombatDroneTravelSecondsTotal;
        }

        static void ClearCombatDroneEngagement(FleetMemberSave member)
        {
            if (member == null) return;
            member.CombatDroneTargetEnemyId = string.Empty;
            member.CombatDroneTravelSecondsLeft = 0;
            member.CombatDroneTravelSecondsTotal = 0;
        }

        static void DestroyShip(GameSave save, ShipSave ship, Action<string> notify)
        {
            DisableBursts(save, ship.Uid);
            if (save.Operation.IndustrialCoreShipUid == ship.Uid) DeactivateIndustrialCore(save, GetCompatibleCoreFitting(ship, Catalog.GetShip(ship.HullId)));
            var pilot = save.Characters.Find(x => x.AssignedShipUid == ship.Uid); if (pilot != null) pilot.AssignedShipUid = string.Empty;
            save.Operation.Fleet.RemoveAll(x => x.ShipUid == ship.Uid); save.Ships.Remove(ship); notify?.Invoke($"{pilot?.Name}: {Catalog.GetShip(ship.HullId).DisplayName} уничтожен. Клон вернулся на станцию.");
        }

        public static float MiningRangeKm(CharacterSave pilot, ShipDefinition hull, MiningModuleDefinition module)
        {
            var gasExtractor=module.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
            var level = SkillService.GetLevel(pilot, hull.BonusSkillId);
            return module.RangeKm * (gasExtractor?1f:1f+hull.RangeBonusPerLevel*level);
        }

        public static float MiningHoldCapacity(CharacterSave pilot,ShipDefinition hull)
        {
            if(hull==null)return 0;
            var multiplier=1f+hull.MiningHoldBonusPerLevel*SkillService.GetLevel(pilot,hull.BonusSkillId);
            multiplier*=1f+hull.SecondaryMiningHoldBonusPerLevel*SkillService.GetLevel(pilot,hull.SecondaryBonusSkillId);
            return hull.MiningHoldM3*multiplier;
        }

        public static float MiningCycleSeconds(CharacterSave pilot, ShipDefinition hull, MiningModuleDefinition module, GameSave save, FleetMemberSave member)
        {
            var ship=FindShip(save,member?.ShipUid);var fitted=ship?.Modules.FirstOrDefault(candidate=>Catalog.GetModule(candidate.ModuleId)==module);
            var asteroid=save?.Operation?.Asteroids.Find(candidate=>candidate.Id==member?.TargetAsteroidId);
            return MiningCycleSeconds(pilot,hull,module,save,member,fitted,asteroid);
        }

        public static float MiningCycleSecondsForSlot(GameSave save,FleetMemberSave member,int slot)
        {
            if(save==null||member==null)return 0;
            var ship=FindShip(save,member.ShipUid);var pilot=FindPilot(save,member.PilotId);var hull=Catalog.GetShip(ship?.HullId);
            var fitted=ship?.Modules.FirstOrDefault(candidate=>candidate.Slot==slot);var module=Catalog.GetModule(fitted?.ModuleId);
            if(ship==null||pilot==null||hull==null||!IsExtractor(module))return 0;
            var asteroid=save.Operation?.Asteroids.Find(candidate=>candidate.Id==member.TargetAsteroidId);
            return MiningCycleSeconds(pilot,hull,module,save,member,fitted,asteroid);
        }

        public static float MiningYieldM3ForSlot(GameSave save,FleetMemberSave member,int slot)
        {
            if(save==null||member==null)return 0;
            var ship=FindShip(save,member.ShipUid);var pilot=FindPilot(save,member.PilotId);var hull=Catalog.GetShip(ship?.HullId);
            var fitted=ship?.Modules.FirstOrDefault(candidate=>candidate.Slot==slot);var module=Catalog.GetModule(fitted?.ModuleId);
            var asteroid=save.Operation?.Asteroids.Find(candidate=>candidate.Id==member.TargetAsteroidId);
            if(ship==null||pilot==null||hull==null||asteroid==null||!CanMineResource(fitted,Catalog.GetOre(asteroid.OreId)))return 0;
            return MiningYieldForModule(pilot,hull,module,CompatibleCrystal(fitted,asteroid.OreId,ship),ship);
        }

        static float MiningCycleSeconds(CharacterSave pilot, ShipDefinition hull, MiningModuleDefinition module, GameSave save, FleetMemberSave member,FittedModuleSave fitted,AsteroidSave asteroid)
        {
            var ship=FindShip(save,member?.ShipUid);
            var iceExtractor = module?.Kind is ModuleKind.IceMiningLaser or ModuleKind.IceHarvester;
            var gasExtractor = module?.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
            var multiplier = iceExtractor ? hull.IceRoleCycleMultiplier : gasExtractor ? 1f : hull.RoleCycleMultiplier;
            if(iceExtractor)
            {
                multiplier *= 1f-.05f*SkillService.GetLevel(pilot,"ice-harvesting");
                multiplier *= 1f-hull.IceCycleReductionPerLevel*SkillService.GetLevel(pilot,hull.BonusSkillId);
                multiplier *= 1f-hull.SecondaryIceCycleReductionPerLevel*SkillService.GetLevel(pilot,hull.SecondaryBonusSkillId);
                multiplier *= Catalog.PerfectIceHarvesterCycleImplantMultiplier;
                var upgrades=(ship?.Modules??new List<FittedModuleSave>()).Where(saved=>Catalog.GetModule(saved.ModuleId)?.Kind==ModuleKind.IceHarvesterUpgrade).Select(saved=>Catalog.GetModule(saved.ModuleId)?.TechLevel>=2?.09f:.05f).OrderByDescending(value=>value).ToArray();
                var penalties=new[]{1f,.86912f,.57058f,.28296f};for(var i=0;i<Math.Min(upgrades.Length,penalties.Length);i++)multiplier*=1f-upgrades[i]*penalties[i];
            }
            else if(gasExtractor)
            {
                multiplier*=hull.GasRoleCycleMultiplier;
                multiplier*=1f-hull.GasCycleReductionPerLevel*SkillService.GetLevel(pilot,hull.BonusSkillId);
                multiplier*=1f-hull.SecondaryGasCycleReductionPerLevel*SkillService.GetLevel(pilot,hull.SecondaryBonusSkillId);
            }
            else
            {
                multiplier *= 1f - hull.CycleReductionPerLevel * SkillService.GetLevel(pilot,hull.BonusSkillId);
                multiplier *= 1f - hull.SecondaryCycleReductionPerLevel * SkillService.GetLevel(pilot,hull.SecondaryBonusSkillId);
                multiplier *= CompatibleCrystal(fitted,asteroid?.OreId,ship)?.CycleMultiplier??1f;
            }
            var burstStrength=BurstStrengthForTarget(save,member?.ShipUid,BurstEffect.Cycle);
            if (burstStrength>0) multiplier *= Mathf.Max(.4f,1f-.15f*burstStrength);
            return Math.Max(1, module.CycleSeconds * multiplier);
        }

        public static float MiningYieldM3(CharacterSave pilot, ShipDefinition hull, MiningModuleDefinition module, GameSave save, FleetMemberSave member)
        {
            var ship=FindShip(save,member?.ShipUid);var asteroid=save?.Operation?.Asteroids.Find(candidate=>candidate.Id==member?.TargetAsteroidId);
            if(ship==null)return 0;
            var yield=0f;
            var resource=Catalog.GetOre(asteroid?.OreId);
            foreach(var fitted in ship.Modules.Where(candidate=>CanMineResource(candidate,resource)))
            {
                var fittedModule=Catalog.GetModule(fitted.ModuleId);yield+=MiningYieldForModule(pilot,hull,fittedModule,CompatibleCrystal(fitted,asteroid?.OreId,ship),ship);
            }
            return yield;
        }

        public static float MiningCriticalChance(CharacterSave pilot,ShipDefinition hull,MiningModuleDefinition module,GameSave save=null,FleetMemberSave member=null)
        {
            if(pilot==null||hull==null||module==null||module.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester)return 0;
            var chance=module.CriticalChance*(1f+.10f*SkillService.GetLevel(pilot,"mining-precision"));
            chance*=hull.RoleCriticalChanceMultiplier;
            chance*=1f+hull.CriticalChanceBonusPerLevel*SkillService.GetLevel(pilot,hull.BonusSkillId);
            var burstStrength=BurstStrengthForTarget(save,member?.ShipUid,BurstEffect.Efficiency);
            chance*=1f+.50f*burstStrength;
            return chance;
        }

        public static float MiningResidueChance(MiningModuleDefinition module,MiningCrystalDefinition crystal,float efficiencyBurstStrength)
        {
            if(module==null)return 0;
            var baseChance=Mathf.Max(0,module.ResidueChance+(crystal?.ResidueChanceBonus??0));
            return baseChance*Mathf.Max(0,1f-.15f*efficiencyBurstStrength);
        }

        public static float MiningCriticalBonusYield(CharacterSave pilot,ShipDefinition hull,MiningModuleDefinition module)
        {
            if(pilot==null||hull==null||module==null)return 0;
            var bonus=module.CriticalBonusYield*(1f+.05f*SkillService.GetLevel(pilot,"mining-exploitation"));
            return bonus*(1f+hull.CriticalBonusYieldPerLevel*SkillService.GetLevel(pilot,hull.BonusSkillId));
        }

        static float MiningYieldForModule(CharacterSave pilot,ShipDefinition hull,MiningModuleDefinition module,MiningCrystalDefinition crystal,ShipSave ship)
        {
            if(module?.Kind is ModuleKind.IceMiningLaser or ModuleKind.IceHarvester)return module.BaseYieldM3;
            if(module?.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester)return module.BaseYieldM3*hull.GasRoleYieldMultiplier;
            var yield = module.BaseYieldM3 * (crystal?.MiningAmountMultiplier??1f) * hull.RoleYieldMultiplier;
            yield *= 1f + .05f * SkillService.GetLevel(pilot,"mining");
            yield *= 1f + .05f * SkillService.GetLevel(pilot,"astrogeology");
            yield *= 1f + hull.YieldBonusPerLevel * SkillService.GetLevel(pilot,hull.BonusSkillId);
            yield *= 1f + hull.SecondaryYieldBonusPerLevel * SkillService.GetLevel(pilot,hull.SecondaryBonusSkillId);
            var upgrades = ship.Modules.Where(x => Catalog.GetModule(x.ModuleId)?.Kind == ModuleKind.MiningUpgrade)
                .Select(x => Catalog.GetModule(x.ModuleId)?.TechLevel >= 2 ? .09f : .05f)
                .OrderByDescending(value => value).ToArray();
            var stackingPenalties=new[]{1f,.86912f,.57058f,.28296f};for(var i=0;i<Math.Min(upgrades.Length,stackingPenalties.Length);i++)yield*=1f+upgrades[i]*stackingPenalties[i];
            yield *= Catalog.PerfectOreLaserYieldImplantMultiplier;
            return yield;
        }

        static MiningCrystalDefinition CompatibleCrystal(FittedModuleSave fitted,string oreId,ShipSave ship)
        {
            if(fitted==null||string.IsNullOrEmpty(oreId))return null;
            var oreFamilyId=Catalog.GetOreFamilyId(oreId);
            var module=Catalog.GetModule(fitted.ModuleId);var crystal=Catalog.GetCrystal(fitted.ChargeId);
            if(crystal?.SupportsOre(oreFamilyId)==true&&FittingService.ModuleAcceptsCrystal(module,crystal))return crystal;
            var package=Catalog.GetPackage(ship?.PackageId);var level=package?.ImplicitUniversalTypeALevel??0;
            if(level<=0||!string.Equals(package.MinerModuleId,module?.Id,StringComparison.OrdinalIgnoreCase))return null;
            var suffix=level==1?"a-i":"a-ii";
            return Catalog.Crystals.FirstOrDefault(candidate=>candidate.Id.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)&&candidate.SupportsOre(oreFamilyId));
        }

        static void WearCrystal(FittedModuleSave fitted,MiningCrystalDefinition crystal,CharacterSave pilot,ShipSave ship,Action<string> notify)
        {
            if(fitted==null||crystal==null)return;
            if((Catalog.GetPackage(ship?.PackageId)?.ImplicitUniversalTypeALevel??0)>0)return;
            var chance=crystal.VolatilityChance;
            if(Random.NextDouble()>=chance)return;
            fitted.ChargeDamage+=crystal.DamagePerVolatility;
            if(fitted.ChargeDamage<.999f)return;
            fitted.ChargeId=string.Empty;fitted.ChargeUid=string.Empty;fitted.ChargeDamage=0;notify?.Invoke($"{pilot.Name}: mining crystal разрушен.");
        }

        static float MiningDroneYieldM3PerCycle(GameSave save, CharacterSave pilot, ShipDefinition hull, ShipSave ship, float laserCycleSeconds)
        {
            var drone = Catalog.GetDrone(ship?.MiningDroneId);
            if (drone == null || !drone.Mining || pilot == null || ship.MiningDroneCount <= 0 || save.Operation.Enemies.Count > 0) return 0;
            var skill = SkillService.GetLevel(pilot, "drones");
            var count = Math.Min(ship.MiningDroneCount, Math.Min(skill, hull.DroneBandwidth / Math.Max(1, drone.Bandwidth)));
            if (count <= 0) return 0;
            var multiplier = 1f + .05f * SkillService.GetLevel(pilot, "mining-drone-operation");
            multiplier *= 1f + .10f * SkillService.GetLevel(pilot, "drone-interfacing");
            if (drone.Id == "mining-drone-ii" || drone.Id == "excavator") multiplier *= 1f + .02f * SkillService.GetLevel(pilot, "mining-drone-specialization");
            if (hull.IsCommandShip) multiplier *= 1f + hull.YieldBonusPerLevel * SkillService.GetLevel(pilot, hull.BonusSkillId);
            if (hull.IsCommandShip && save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == ship.Uid) multiplier *= hull.Id == "rorqual" ? 4f : hull.Id == "orca" ? 1.75f : 1.5f;
            return drone.BaseMiningM3PerMinute * count * multiplier * laserCycleSeconds / 60f;
        }

        public static float MiningDroneYieldM3PerCycle(GameSave save,FleetMemberSave member)
        {
            var ship=FindShip(save,member?.ShipUid);var pilot=FindPilot(save,member?.PilotId);var hull=Catalog.GetShip(ship?.HullId);
            return ship==null||pilot==null||hull==null?0:MiningDroneYieldM3PerCycle(save,pilot,hull,ship,60f);
        }

        static double StochasticUnits(double value)
        {
            var floor = Math.Floor(Math.Max(0, value));
            return floor + (Random.NextDouble() < value - floor ? 1d : 0d);
        }

        static float BurstStrengthForTarget(GameSave save, string shipUid, BurstEffect effect)
        {
            if (save?.Operation?.BurstEffects == null || string.IsNullOrWhiteSpace(shipUid)) return 0;
            return save.Operation.BurstEffects
                .Where(saved => saved.SecondsLeft > 0 && saved.Strength > 0 && string.Equals(saved.ShipUid, shipUid, StringComparison.Ordinal) && (BurstEffectForCharge(saved.ChargeId) & effect) != 0)
                .Select(saved => saved.Strength)
                .DefaultIfEmpty(0f)
                .Max();
        }

        static float BurstStrength(GameSave save, FleetMemberSave command, BurstEffect effect)
        {
            var ship = FindShip(save, command?.ShipUid); var hull = Catalog.GetShip(ship?.HullId); var pilot = FindPilot(save, command?.PilotId);
            if (ship == null || hull == null || pilot == null) return 0;
            var matchingModules = GetInstalledBurstModules(ship, hull)
                .Where(fitted => fitted.Active && (BurstEffectForCharge(fitted.ChargeId) & effect) != 0)
                .Select(fitted => Catalog.GetModule(fitted.ModuleId))
                .Where(module => module != null)
                .ToArray();
            if (matchingModules.Length == 0) return 0;

            var moduleStrength = matchingModules.Max(module => module.TechLevel >= 2 ? 1.25f : 1f);
            var foreman = 1f + .02f * SkillService.GetLevel(pilot, "mining-foreman");
            var director = 1f + .10f * SkillService.GetLevel(pilot, "mining-director");
            var strength = moduleStrength * foreman * director * Catalog.PerfectMiningForemanMindlinkMultiplier;
            var hullBurstSkill = string.IsNullOrWhiteSpace(hull.SecondaryBonusSkillId) ? hull.BonusSkillId : hull.SecondaryBonusSkillId;
            strength *= 1f + hull.CommandBurstStrengthBonusPerLevel * SkillService.GetLevel(pilot, hullBurstSkill);
            if (BurstCoreActive(save, command))
            {
                var core = Catalog.GetModule(GetCompatibleCoreFitting(ship, hull)?.ModuleId);
                strength *= hull.Id == "rorqual" ? (core?.TechLevel >= 2 ? 1.4f : 1.3f)
                    : hull.Id == "orca" ? (core?.TechLevel >= 2 ? 1.15f : 1.1f)
                    : (core?.TechLevel >= 2 ? 1.075f : 1.05f);
            }
            return strength;
        }

        static void TickBurstEffects(OperationSave op, float dt)
        {
            op.BurstEffects ??= new List<BurstRecipientEffectSave>();
            foreach (var effect in op.BurstEffects) effect.SecondsLeft -= dt;
            op.BurstEffects.RemoveAll(effect => effect == null || effect.SecondsLeft <= 0 || effect.Strength <= 0);
        }

        static void TickBurstModules(GameSave save, float dt, Action<string> notify)
        {
            var op = save?.Operation;
            if (op == null || !op.BurstsActive || string.IsNullOrWhiteSpace(op.BurstShipUid)) return;
            var command = GetFleetMember(save, op.BurstShipUid);
            var ship = FindShip(save, op.BurstShipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            if (command == null || ship?.Location != ShipLocation.Belt || hull == null)
            {
                DisableBursts(save, op.BurstShipUid); return;
            }

            foreach (var fitted in GetInstalledBurstModules(ship, hull).Where(module => module.Active).ToArray())
            {
                fitted.BurstCycleSecondsLeft -= dt;
                while (fitted.Active && fitted.BurstCycleSecondsLeft <= 0)
                {
                    if (BurstEffectForCharge(fitted.ChargeId) == BurstEffect.None)
                    {
                        fitted.Active = false; fitted.BurstCycleSecondsLeft = 0; break;
                    }
                    var pulseAge = Math.Max(0, -fitted.BurstCycleSecondsLeft);
                    PulseBurst(save, command, fitted, pulseAge);
                    fitted.BurstCycleSecondsLeft += Catalog.MiningBurstCycleSeconds;
                }
            }

            if (!GetInstalledBurstModules(ship, hull).Any(module => module.Active && BurstEffectForCharge(module.ChargeId) != BurstEffect.None))
            {
                op.BurstsActive = false;
                op.BurstShipUid = string.Empty;
                notify?.Invoke("Mining Foreman Bursts остановлены: нет настроенных встроенных эффектов.");
            }
        }

        static void PulseBurst(GameSave save, FleetMemberSave command, FittedModuleSave fitted, float pulseAgeSeconds)
        {
            var effect = BurstEffectForCharge(fitted.ChargeId);
            if (effect == BurstEffect.None) return;
            var strength = BurstStrength(save, command, effect);
            if (strength <= 0) return;
            var pilot = FindPilot(save, command.PilotId);
            var duration = Math.Max(0, Catalog.MiningBurstBaseEffectDurationSeconds * (1f + .10f * SkillService.GetLevel(pilot, "command-burst-specialist")) - Math.Max(0, pulseAgeSeconds));
            if (duration <= 0) return;
            save.Operation.BurstEffects ??= new List<BurstRecipientEffectSave>();
            foreach (var target in save.Operation.Fleet.Where(member => FindShip(save, member.ShipUid)?.Location == ShipLocation.Belt))
            {
                var saved = save.Operation.BurstEffects.Find(candidate => string.Equals(candidate.ShipUid, target.ShipUid, StringComparison.Ordinal) && string.Equals(candidate.SourceShipUid, command.ShipUid, StringComparison.Ordinal) && string.Equals(candidate.ChargeId, fitted.ChargeId, StringComparison.OrdinalIgnoreCase));
                if (saved == null)
                {
                    saved = new BurstRecipientEffectSave { ShipUid = target.ShipUid, SourceShipUid = command.ShipUid, ChargeId = fitted.ChargeId };
                    save.Operation.BurstEffects.Add(saved);
                }
                saved.Strength = strength;
                saved.SecondsLeft = duration;
            }
        }

        static bool BurstCoreActive(GameSave save, FleetMemberSave command)
        {
            return command != null && save?.Operation?.IndustrialCoreActive == true && save.Operation.IndustrialCoreShipUid == command.ShipUid;
        }

        static FleetMemberSave GetSelectedBurstMember(GameSave save)
        {
            var op = save?.Operation;
            if (op == null || !op.Active || !op.BurstsActive || string.IsNullOrWhiteSpace(op.BurstShipUid)) return null;
            var member = GetFleetMember(save, op.BurstShipUid); var ship = FindShip(save, op.BurstShipUid); var hull = Catalog.GetShip(ship?.HullId);
            if (member == null || ship?.Location != ShipLocation.Belt || hull?.IsCommandShip != true) return null;
            return GetInstalledBurstModules(ship, hull).Any(fitted => fitted.Active && BurstEffectForCharge(fitted.ChargeId) != BurstEffect.None) ? member : null;
        }

        static IEnumerable<FittedModuleSave> GetInstalledBurstModules(ShipSave ship, ShipDefinition hull)
        {
            if (ship == null || hull == null || hull.CommandBurstSlots <= 0) return Enumerable.Empty<FittedModuleSave>();
            return ship.Modules
                .Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningBurst)
                .OrderBy(fitted => fitted.Slot)
                .Take(hull.CommandBurstSlots);
        }

        static BurstEffect BurstEffectForCharge(string chargeId)
        {
            // ChargeId is retained only as a save-compatible built-in effect profile.
            // No physical charge or quantity is required or consumed.
            if (string.Equals(chargeId, "legacy-omnibus", StringComparison.OrdinalIgnoreCase)) return BurstEffect.All;
            if (string.IsNullOrWhiteSpace(chargeId)) return BurstEffect.None;
            var normalized = chargeId.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
            if (normalized == "42829" || normalized.Contains("field-enhancement")) return BurstEffect.Range;
            if (normalized == "42830" || normalized.Contains("optimization")) return BurstEffect.Cycle;
            if (normalized == "90733" || normalized.Contains("efficiency")) return BurstEffect.Efficiency;
            // Crystal preservation remains a readable legacy catalog ID, but is
            // deliberately not an installed/effective profile in this game.
            if (normalized == "42831" || normalized.Contains("preservation")) return BurstEffect.None;
            return BurstEffect.None;
        }

        static string ResolveDefaultBurstShipUid(GameSave save)
        {
            var op = save?.Operation;
            if (op == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(op.BurstShipUid))
            {
                var selectedShip = FindShip(save, op.BurstShipUid); var selectedHull = Catalog.GetShip(selectedShip?.HullId);
                if (GetFleetMember(save, op.BurstShipUid) != null && GetInstalledBurstModules(selectedShip, selectedHull).Any()) return op.BurstShipUid;
            }
            return op.Fleet.FirstOrDefault(member =>
            {
                var ship = FindShip(save, member.ShipUid); var hull = Catalog.GetShip(ship?.HullId);
                return ship?.Location == ShipLocation.Belt && GetInstalledBurstModules(ship, hull).Any();
            })?.ShipUid ?? string.Empty;
        }

        static void SetAllBurstModulesActive(GameSave save, bool active)
        {
            if (save?.Ships == null) return;
            foreach (var fitted in save.Ships.SelectMany(ship => ship.Modules).Where(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningBurst)) fitted.Active = active;
        }

        static void DisableBursts(GameSave save, string shipUid)
        {
            var op = save?.Operation;
            if (op == null || (!string.IsNullOrWhiteSpace(shipUid) && !string.IsNullOrWhiteSpace(op.BurstShipUid) && !string.Equals(op.BurstShipUid, shipUid, StringComparison.Ordinal))) return;
            var selectedUid = string.IsNullOrWhiteSpace(op.BurstShipUid) ? shipUid : op.BurstShipUid;
            var ship = FindShip(save, selectedUid); var hull = Catalog.GetShip(ship?.HullId);
            foreach (var fitted in GetInstalledBurstModules(ship, hull)) fitted.Active = false;
            op.BurstsActive = false;
            op.BurstShipUid = string.Empty;
        }

        static string ResolveDefaultCoreShipUid(GameSave save)
        {
            var op = save?.Operation;
            if (op == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(op.IndustrialCoreShipUid) && GetFleetMember(save, op.IndustrialCoreShipUid) != null) return op.IndustrialCoreShipUid;
            if (!string.IsNullOrWhiteSpace(op.BurstShipUid))
            {
                var burstShip = FindShip(save, op.BurstShipUid);
                if (GetCompatibleCoreFitting(burstShip, Catalog.GetShip(burstShip?.HullId)) != null) return op.BurstShipUid;
            }
            return op.Fleet.FirstOrDefault(member =>
            {
                var ship = FindShip(save, member.ShipUid);
                return ship?.Location == ShipLocation.Belt && GetCompatibleCoreFitting(ship, Catalog.GetShip(ship.HullId)) != null;
            })?.ShipUid ?? string.Empty;
        }

        static FittedModuleSave GetCompatibleCoreFitting(ShipSave ship, ShipDefinition hull)
        {
            if (ship == null || hull?.SupportsIndustrialCore != true) return null;
            return ship.Modules.FirstOrDefault(fitted =>
            {
                var module = Catalog.GetModule(fitted.ModuleId);
                return module?.Kind == ModuleKind.IndustrialCore && CoreFitsHull(hull, module);
            });
        }

        static bool CoreFitsHull(ShipDefinition hull, MiningModuleDefinition core)
        {
            if (hull == null || core == null) return false;
            if (hull.Id == "porpoise") return core.Id.StartsWith("medium-industrial-core", StringComparison.Ordinal);
            if (hull.Id == "orca") return core.Id.StartsWith("industrial-core", StringComparison.Ordinal) || core.Id.StartsWith("large-industrial-core", StringComparison.Ordinal);
            if (hull.Id == "rorqual") return core.Id.StartsWith("capital-industrial-core", StringComparison.Ordinal);
            return false;
        }

        static void DeactivateIndustrialCore(GameSave save, FittedModuleSave fittedCore)
        {
            if (fittedCore != null) fittedCore.Active = false;
            var op = save?.Operation; if (op == null) return;
            op.IndustrialCoreActive = false;
            op.IndustrialCoreStopRequested = false;
            op.IndustrialCoreSecondsLeft = 0;
            op.IndustrialCoreShipUid = string.Empty;
        }

        static void ClearAutomaticCoreRestartIntent(OperationSave operation)
        {
            if (operation == null) return;
            operation.AutoRestartIndustrialCore = false;
            operation.AutoRestartIndustrialCoreShipUid = string.Empty;
        }

        static bool RestartAutomaticCoreAfterArrival(GameSave save, Action<string> notify)
        {
            var operation = save?.Operation;
            if (operation?.AutoRestartIndustrialCore != true) return false;
            var shipUid = operation.AutoRestartIndustrialCoreShipUid;
            ClearAutomaticCoreRestartIntent(operation);
            var member = GetFleetMember(save, shipUid);
            var ship = FindShip(save, shipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            var fittedCore = GetCompatibleCoreFitting(ship, hull);
            var core = Catalog.GetModule(fittedCore?.ModuleId);
            if (operation.Active != true || member == null || ship?.Location != ShipLocation.Belt || core == null)
            {
                notify?.Invoke("Industrial Core не удалось автоматически перезапустить после перелёта.");
                return false;
            }
            foreach (var other in save.Ships.SelectMany(candidate => candidate.Modules)
                         .Where(candidate => Catalog.GetModule(candidate.ModuleId)?.Kind == ModuleKind.IndustrialCore))
                other.Active = false;
            fittedCore.Active = true;
            operation.IndustrialCoreActive = true;
            operation.IndustrialCoreStopRequested = false;
            operation.IndustrialCoreSecondsLeft = CoreCycleSeconds(hull, core);
            operation.IndustrialCoreShipUid = shipUid;
            notify?.Invoke($"{core.DisplayName} автоматически перезапущен после прибытия.");
            return true;
        }

        static void RestartAutomaticBurstsAfterArrival(GameSave save)
        {
            var operation = save?.Operation;
            var ship = FindShip(save, operation?.BurstShipUid);
            var hull = Catalog.GetShip(ship?.HullId);
            if (operation?.BurstsActive != true || GetFleetMember(save, operation.BurstShipUid) == null ||
                ship?.Location != ShipLocation.Belt || hull == null)
            {
                DisableBursts(save, operation?.BurstShipUid);
                return;
            }
            var modules = GetInstalledBurstModules(ship, hull).ToArray();
            foreach (var fitted in modules)
            {
                fitted.Active = BurstEffectForCharge(fitted.ChargeId) != BurstEffect.None;
                fitted.BurstCycleSecondsLeft = 0;
            }
            operation.BurstsActive = modules.Any(fitted => fitted.Active);
            if (!operation.BurstsActive)
            {
                operation.BurstShipUid = string.Empty;
                return;
            }
            TickBurstModules(save, 0f, null);
        }

        static FleetMemberSave GetFleetMember(GameSave save, string shipUid)
        {
            return string.IsNullOrWhiteSpace(shipUid) ? null : save?.Operation?.Fleet?.FirstOrDefault(member => string.Equals(member.ShipUid, shipUid, StringComparison.Ordinal));
        }

        static MiningModuleDefinition GetPrimaryMiner(ShipSave ship) => ship?.Modules.Select(item => Catalog.GetModule(item.ModuleId)).FirstOrDefault(item => IsExtractor(item));

        public static float CombatDroneDps(ShipSave ship,CharacterSave pilot)
        {
            var hull=Catalog.GetShip(ship?.HullId);var drone=Catalog.GetDrone(ship?.CombatDroneId);
            if(drone==null||pilot==null||hull==null)return 0;
            var count=Math.Min(ship.CombatDroneCount,Math.Min(SkillService.GetLevel(pilot,"drones"),hull.DroneBandwidth/Math.Max(1,drone.Bandwidth)));
            return drone.BaseDps*Math.Max(0,count)*(1+.1f*SkillService.GetLevel(pilot,"drone-interfacing"));
        }

        static float ShipDroneDps(GameSave save,string shipUid)
        {
            var ship=FindShip(save,shipUid);var pilot=save?.Characters?.Find(candidate=>candidate.AssignedShipUid==shipUid);
            return CombatDroneDps(ship,pilot);
        }
        static void DamageShip(GameSave save,ShipSave s,float d)
        {
            var pilot=save?.Characters?.FirstOrDefault(candidate=>candidate.AssignedShipUid==s.Uid);var multiplier=PreparedPackageService.IncomingShieldDamageMultiplier(s,pilot);
            var shieldRaw=Math.Min(s.ShieldHp,d*multiplier);s.ShieldHp-=shieldRaw;d-=shieldRaw/Math.Max(.01f,multiplier);
            var x=Math.Min(s.ArmorHp,d);s.ArmorHp-=x;d-=x;s.StructureHp=Math.Max(0,s.StructureHp-d);
        }
        static void DamageEnemy(EnemySave e,float d){var x=Math.Min(e.ShieldHp,d);e.ShieldHp-=x;d-=x;x=Math.Min(e.ArmorHp,d);e.ArmorHp-=x;d-=x;e.StructureHp=Math.Max(0,e.StructureHp-d);}
        static void GenerateBelt(OperationSave op, LocationDefinition loc)
        {
            var random=new System.Random(op.BeltSeed+op.VisitNumber);
            if(loc.OreIds?.Length>0&&loc.OreIds.All(id=>Catalog.GetOre(id)?.Kind==ResourceKind.Gas))
            {
                for(var i=0;i<loc.OreIds.Length;i++){var resource=Catalog.GetOre(loc.OreIds[i]);op.Asteroids.Add(new AsteroidSave{Id=$"G-{op.VisitNumber}-{i:00}",OreId=resource.Id,RemainingUnits=resource.DefaultAsteroidUnits,X=-16+i*32,Y=0,Z=28+i*8,Scale=5+i});}
                return;
            }
            for(var i=0;i<24;i++)
            {
                // Every resource advertised by the destination is guaranteed at least
                // one rock; the rest of the explicitly approximate layout stays seeded.
                var oreId=i<loc.OreIds.Length?loc.OreIds[i]:loc.OreIds[random.Next(loc.OreIds.Length)];
                var ore=Catalog.GetOre(oreId);var amountScale=loc.ResourceLayoutApproximate ? .8+random.NextDouble()*.4 : .35+random.NextDouble()*.9;
                op.Asteroids.Add(new AsteroidSave{Id=$"A-{op.VisitNumber}-{i:00}",OreId=oreId,RemainingUnits=ore.DefaultAsteroidUnits*amountScale,X=-35+(float)random.NextDouble()*70,Y=-6+(float)random.NextDouble()*12,Z=8+(float)random.NextDouble()*62,Scale=2.2f+(float)random.NextDouble()*3.8f});
            }
        }
        static float NextRaid(LocationDefinition location) => location.Threat<=0?99999f:Mathf.Lerp(location.SpawnMinSeconds,location.SpawnMaxSeconds,(float)Random.NextDouble());
        static string NpcName(LocationDefinition location,int index)
        {
            if(string.Equals(location?.Id,"wspace-c1-barren-reservoir",StringComparison.OrdinalIgnoreCase))return index%3==0?"Awakened Patroller":index%2==0?"Emergent Watchman":"Emergent Patroller";
            if(location.Security>=.5f)return index%2==0?"Guristas Arrogator":"Guristas Imputor";if(location.Security>0)return index%2==0?"Guristas Despoiler":"Guristas Nihilist";return index==0&&location.Security<=-.7f?"Guristas Dismantler":index%2==0?"Guristas Silencer":"Guristas Nihilist";
        }
        static void MoveToward(FleetMemberSave m,AsteroidSave a,float step,float stop){var dx=a.X-m.X;var dy=a.Y-m.Y;var dz=a.Z-m.Z;var len=Mathf.Sqrt(dx*dx+dy*dy+dz*dz);if(len<=stop||len<=0)return;var move=Mathf.Min(step,len-stop)/len;m.X+=dx*move;m.Y+=dy*move;m.Z+=dz*move;}
        static void MoveEnemyToward(EnemySave e,FleetMemberSave m,float step,float stop){var dx=m.X-e.X;var dy=m.Y-e.Y;var dz=m.Z-e.Z;var len=Mathf.Sqrt(dx*dx+dy*dy+dz*dz);if(len<=stop||len<=0)return;var move=Mathf.Min(step,len-stop)/len;e.X+=dx*move;e.Y+=dy*move;e.Z+=dz*move;}
        static float Distance(float ax,float ay,float az,float bx,float by,float bz){var x=ax-bx;var y=ay-by;var z=az-bz;return Mathf.Sqrt(x*x+y*y+z*z);}
        public static double AsteroidVolumeM3(AsteroidSave asteroid)=>Math.Max(0,asteroid?.RemainingUnits??0)*(Catalog.GetOre(asteroid?.OreId)?.UnitVolumeM3??0);
        static double AsteroidRemainingM3(AsteroidSave asteroid)=>AsteroidVolumeM3(asteroid);
        public static double HoldVolume(List<InventoryStack> hold) => hold?.Sum(stack => stack.Quantity*ItemVolumeM3(stack.ItemId)) ?? 0;
        public static double ItemVolumeM3(string itemId) => Catalog.TryGetItemVolumeM3(itemId, out var volumeM3) ? volumeM3 : 0d;
        public static double ItemQuantity(List<InventoryStack> list,string id) => list?.Find(x=>x.ItemId==id)?.Quantity ?? 0;
        public static void AddItem(List<InventoryStack> list,string id,double quantity){if(quantity<=0)return;var stack=list.Find(x=>x.ItemId==id);if(stack==null){stack=new InventoryStack{ItemId=id};list.Add(stack);}stack.Quantity+=quantity;}
        public static bool RemoveItem(List<InventoryStack> list,string id,double quantity){var stack=list?.Find(x=>x.ItemId==id);if(stack==null||stack.Quantity+1e-6<quantity)return false;stack.Quantity-=quantity;if(stack.Quantity<=1e-6)list.Remove(stack);return true;}
        static void TransferAll(List<InventoryStack> source,List<InventoryStack> destination){foreach(var stack in source)AddItem(destination,stack.ItemId,stack.Quantity);source.Clear();}
        static CharacterSave FindPilot(GameSave save,string id)=>save?.Characters?.Find(x=>x.Id==id);
        static ShipSave FindShip(GameSave save,string uid)=>save?.Ships?.Find(x=>x.Uid==uid);
        static bool IsExtractor(MiningModuleDefinition module)=>module?.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner or ModuleKind.IceMiningLaser or ModuleKind.IceHarvester or ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
        public static bool CanMineResource(MiningModuleDefinition module,OreDefinition resource)
        {
            if(module==null||resource==null)return false;
            if(resource.Kind==ResourceKind.Ice)return module.Kind is ModuleKind.IceMiningLaser or ModuleKind.IceHarvester;
            if(resource.Kind==ResourceKind.Gas)return module.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
            return module.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner;
        }
        public static bool CanMineResource(FittedModuleSave fitted,OreDefinition resource)
        {
            var module=Catalog.GetModule(fitted?.ModuleId);if(!CanMineResource(module,resource))return false;
            var crystal=Catalog.GetCrystal(fitted?.ChargeId);
            return crystal==null||(FittingService.ModuleAcceptsCrystal(module,crystal)&&crystal.SupportsOre(Catalog.GetOreFamilyId(resource)));
        }
        public static bool CanMineResource(ShipSave ship,string resourceId)
        {
            var resource=Catalog.GetOre(resourceId);if(ship==null||resource==null)return false;
            if(ship.Modules.Any(fitted=>CanMineResource(fitted,resource)))return true;
            return resource.Kind==ResourceKind.Ore&&Catalog.GetDrone(ship.MiningDroneId)?.Mining==true&&ship.MiningDroneCount>0;
        }
        public static bool LocationSupportsShip(LocationDefinition location,ShipSave ship,ShipDefinition hull)
        {
            if(location==null||ship==null||hull==null)return false;
            if(location.AllowedHullIds?.Length>0&&!location.AllowedHullIds.Any(id=>string.Equals(id,hull.Id,StringComparison.OrdinalIgnoreCase)))return false;
            if(hull.IsCommandShip)return true;
            return location.OreIds?.Any(id=>CanMineResource(ship,id))==true;
        }
        public static bool CanFly(CharacterSave pilot,ShipDefinition hull)=>pilot!=null&&hull!=null&&SkillService.Meets(pilot,hull.Requirements);
        public static bool CanFly(CharacterSave pilot,ShipSave ship)
        {
            var hull=Catalog.GetShip(ship?.HullId);return CanFly(pilot,hull)&&(string.IsNullOrWhiteSpace(ship?.PackageId)||PreparedPackageService.CanUsePackage(pilot,ship));
        }
        static long ResolveNow(long value)=>value>0?value:DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static bool CanAttemptAutomaticRoute(GameSave save,string locationId,long nowUnix)
        {
            var state=AutomaticRouteAttempts.GetValue(save,_=>new AutomaticRouteAttemptState());
            if(state.Unix==nowUnix&&string.Equals(state.LocationId,locationId,StringComparison.OrdinalIgnoreCase))return false;
            state.Unix=nowUnix;state.LocationId=locationId??string.Empty;return true;
        }
        static bool ShouldSnapshotDepletion(GameSave save,OperationSave operation)
        {
            var state=AutomaticRouteAttempts.GetValue(save,_=>new AutomaticRouteAttemptState());
            if(state.SnapshotInstanceSerial==operation.SiteInstanceSerial&&
               string.Equals(state.SnapshotLocationId,operation.LocationId,StringComparison.OrdinalIgnoreCase))return false;
            state.SnapshotInstanceSerial=operation.SiteInstanceSerial;
            state.SnapshotLocationId=operation.LocationId??string.Empty;
            return true;
        }
        static void Log(OperationSave op,string message){op.Log.Add(new OperationLogSave{AtSeconds=op.ElapsedSeconds,Message=message});if(op.Log.Count>30)op.Log.RemoveAt(0);}
    }
}
