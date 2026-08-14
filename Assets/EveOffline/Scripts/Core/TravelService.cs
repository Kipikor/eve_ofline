using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EveOffline
{
    /// <summary>
    /// Persisted, deliberately short inter-system travel. This is a gameplay
    /// approximation: the player chooses a destination, while piloting and gates
    /// are abstracted into one observable countdown.
    /// </summary>
    public static class TravelService
    {
        public static bool TryStart(GameSave save, string destinationLocationId, out string message, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            var operation = save?.Operation;
            var destination = Catalog.GetLocation(destinationLocationId);
            if (save == null || operation == null || destination == null)
            {
                message = "Неизвестное место добычи.";
                return false;
            }
            if (operation.TravelActive)
            {
                var currentDestination = Catalog.GetLocation(operation.TravelDestinationLocationId);
                message = $"Флот уже летит в {currentDestination?.SystemName ?? "выбранную систему"}: {Mathf.CeilToInt(operation.TravelSecondsLeft)} сек.";
                return false;
            }
            if (operation.BeltWarpActive)
            {
                message = $"Флот уже варпает в следующий белт: {Mathf.CeilToInt(operation.BeltWarpSecondsLeft)} сек.";
                return false;
            }
            if (operation.IndustrialCoreActive)
            {
                message = $"Нельзя начать перелёт: Industrial Core закончит цикл через {Mathf.CeilToInt(operation.IndustrialCoreSecondsLeft)} сек.";
                return false;
            }
            if (operation.Active && string.Equals(operation.LocationId, destination.Id, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Флот уже находится в {destination.DisplayName}.";
                return false;
            }
            if (!MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                message = $"Место сейчас недоступно: {MiningSiteService.AvailabilityText(save, destination.Id, nowUnix)}.";
                return false;
            }

            var source = operation.Active ? Catalog.GetLocation(operation.LocationId) : null;
            if (operation.Active && source != null && source.SystemId != 0 && source.SystemId == destination.SystemId && destination.SiteKind == MiningSiteKind.StaticBelt)
                return TryStartSameSystemBeltWarp(save, destination.Id, false, out message, nowUnix);
            operation.TravelShipUids ??= new List<string>();
            var candidates = SelectedEligibleShips(save, destination, operation.Active).Take(Catalog.FleetCapacity).ToList();
            if (candidates.Count == 0)
            {
                message = "Для этого места нет выбранных пилотов с подходящими кораблями, оснащением и навыками.";
                return false;
            }

            if (operation.Active && !OperationService.TryStop(save, out message, nowUnix)) return false;

            operation.TravelShipUids.Clear();
            foreach (var ship in candidates)
            {
                // TryStop completes any pending ten-second unload first, so the
                // same Transit state can safely represent this longer journey.
                if (ship.Location != ShipLocation.Station) continue;
                ship.Location = ShipLocation.Transit;
                operation.TravelShipUids.Add(ship.Uid);
            }
            if (operation.TravelShipUids.Count == 0)
            {
                message = "Подходящие корабли не готовы к вылету со станции.";
                return false;
            }

            operation.Active = false;
            operation.TravelActive = true;
            operation.TravelSourceLocationId = source?.Id ?? string.Empty;
            operation.TravelDestinationLocationId = destination.Id;
            operation.TravelTotalSeconds = EstimateSeconds(source, destination);
            operation.TravelSecondsLeft = operation.TravelTotalSeconds;
            operation.TravelIsAutomatic = false;
            message = $"Флот из {operation.TravelShipUids.Count} кораблей вылетел в {destination.DisplayName}. В пути {Mathf.CeilToInt(operation.TravelTotalSeconds)} сек.";
            return true;
        }

        /// <summary>
        /// Starts the deterministic global belt route without docking, unloading
        /// or consulting DeployOnLaunch. The exact active roster and all ship
        /// damage/holds stay in memory and in the save during the journey.
        /// </summary>
        public static bool TryStartAutomaticBeltTravel(GameSave save, string destinationLocationId, out string message, long nowUnix = 0)
        {
            nowUnix = ResolveNow(nowUnix);
            var operation = save?.Operation;
            var source = Catalog.GetLocation(operation?.LocationId);
            var destination = Catalog.GetLocation(destinationLocationId);
            if (operation?.Active != true || source == null || destination?.SiteKind != MiningSiteKind.StaticBelt)
            {
                message = "Автомаршрут может переходить только между обычными белтами активной операции.";
                return false;
            }
            var securityFloor = operation.AutoSecurityFloorInitialized
                ? operation.AutoSecurityFloorTenths
                : MiningSiteService.SecurityTierTenths(source);
            var destinationTier = MiningSiteService.SecurityTierTenths(destination);
            if (destinationTier < securityFloor)
            {
                message = $"Автомаршрут не может опуститься ниже security {securityFloor / 10f:0.0}: выбран белт {destinationTier / 10f:0.0}.";
                return false;
            }
            if (string.Equals(source.Id, destination.Id, StringComparison.OrdinalIgnoreCase))
            {
                message = "Флот уже находится в выбранном белте.";
                return false;
            }
            if (source.SystemId != 0 && source.SystemId == destination.SystemId)
                return TryStartSameSystemBeltWarp(save, destination.Id, true, out message, nowUnix);
            if (operation.TravelActive || operation.BeltWarpActive)
            {
                message = "Флот уже находится в перелёте.";
                return false;
            }
            if (operation.IndustrialCoreActive)
            {
                message = $"Автопереход ждёт границы цикла Industrial Core: {Mathf.CeilToInt(operation.IndustrialCoreSecondsLeft)} сек.";
                return false;
            }
            if (!MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                message = $"Следующий белт недоступен: {MiningSiteService.AvailabilityText(save, destination.Id, nowUnix)}.";
                return false;
            }
            if (!ValidateActiveFleetForBeltTravel(save, destination, out message)) return false;

            MiningSiteService.SnapshotActiveOperation(save, nowUnix);
            operation.TravelShipUids ??= new List<string>();
            operation.TravelShipUids.Clear();
            foreach (var member in operation.Fleet)
            {
                member.Order = FleetOrder.Idle;
                member.TargetAsteroidId = string.Empty;
                member.MiningCycles?.Clear();
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                if (ship == null) continue;
                ship.Location = ShipLocation.Transit;
                operation.TravelShipUids.Add(ship.Uid);
            }
            operation.Enemies?.Clear();
            operation.BurstEffects?.Clear();
            operation.TravelActive = true;
            operation.TravelSourceLocationId = source.Id;
            operation.TravelDestinationLocationId = destination.Id;
            operation.TravelTotalSeconds = EstimateSeconds(source, destination);
            operation.TravelSecondsLeft = operation.TravelTotalSeconds;
            operation.TravelIsAutomatic = true;
            message = $"Флот автоматически отварпывает в {destination.DisplayName}. В пути {Mathf.CeilToInt(operation.TravelTotalSeconds)} сек.";
            return true;
        }

        public static bool Tick(GameSave save, float dt, out string message, long nowUnix = 0)
        {
            message = string.Empty;
            nowUnix = ResolveNow(nowUnix);
            var operation = save?.Operation;
            if (operation?.BeltWarpActive == true)
            {
                if (dt <= 0) return false;
                operation.BeltWarpSecondsLeft = Mathf.Max(0, operation.BeltWarpSecondsLeft - dt);
                if (operation.BeltWarpSecondsLeft > 0) return false;
                return CompleteSameSystemBeltWarp(save, out message, nowUnix);
            }
            if (operation?.TravelActive != true || dt <= 0) return false;

            operation.TravelSecondsLeft = Mathf.Max(0, operation.TravelSecondsLeft - dt);
            if (operation.TravelSecondsLeft > 0) return false;
            return operation.TravelIsAutomatic
                ? CompleteAutomaticBeltTravel(save, out message, nowUnix)
                : Complete(save, out message, nowUnix);
        }

        public static float EstimateSeconds(LocationDefinition source, LocationDefinition destination)
        {
            if (destination == null) return 10f;
            var destinationIndex = IndexOf(destination.Id);
            var sourceIndex = source == null ? -1 : IndexOf(source.Id);
            var progression = sourceIndex < 0 ? destinationIndex + 1 : Math.Abs(destinationIndex - sourceIndex) + 1;
            var securityDepth = Mathf.Max(0, .9f - destination.Security);
            var anomalyDelay = destination.IsAnomaly ? 3f : 0f;
            return Mathf.Clamp(8f + progression * 3f + securityDepth * 8f + anomalyDelay, 10f, 45f);
        }

        public static float Progress01(OperationSave operation)
        {
            if (operation?.TravelActive != true || operation.TravelTotalSeconds <= 0) return 0;
            return Mathf.Clamp01(1f - operation.TravelSecondsLeft / operation.TravelTotalSeconds);
        }

        public static bool TryStartSameSystemBeltWarp(GameSave save, string destinationLocationId, out string message, long nowUnix = 0)
        {
            return TryStartSameSystemBeltWarp(save, destinationLocationId, false, out message, ResolveNow(nowUnix));
        }

        static bool TryStartSameSystemBeltWarp(GameSave save, string destinationLocationId, bool automatic, out string message, long nowUnix)
        {
            var operation = save?.Operation;
            var source = Catalog.GetLocation(operation?.LocationId);
            var destination = Catalog.GetLocation(destinationLocationId);
            if (operation?.Active != true || source == null || destination == null ||
                source.SystemId == 0 || source.SystemId != destination.SystemId || destination.SiteKind != MiningSiteKind.StaticBelt)
            {
                message = "Следующий белт должен находиться в текущей системе.";
                return false;
            }
            if (operation.TravelActive || operation.BeltWarpActive)
            {
                message = "Флот уже находится в перелёте.";
                return false;
            }
            if (operation.IndustrialCoreActive)
            {
                message = $"Нельзя отварпать: Industrial Core закончит цикл через {Mathf.CeilToInt(operation.IndustrialCoreSecondsLeft)} сек.";
                return false;
            }
            if (!MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                message = $"Следующий белт недоступен: {MiningSiteService.AvailabilityText(save, destination.Id, nowUnix)}.";
                return false;
            }
            if (!ValidateActiveFleetForBeltTravel(save, destination, out message)) return false;

            MiningSiteService.SnapshotActiveOperation(save, nowUnix);
            operation.BeltWarpActive = true;
            operation.BeltWarpDestinationLocationId = destination.Id;
            operation.BeltWarpTotalSeconds = OperationService.WarpAlignSeconds + OperationService.WarpInSeconds;
            operation.BeltWarpSecondsLeft = operation.BeltWarpTotalSeconds;
            operation.BeltWarpIsAutomatic = automatic;
            foreach (var member in operation.Fleet)
            {
                member.Order = FleetOrder.Idle;
                member.TargetAsteroidId = string.Empty;
                member.MiningCycles?.Clear();
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                if (ship != null) ship.Location = ShipLocation.Transit;
            }
            operation.Enemies.Clear();
            message = $"Флот {(automatic ? "автоматически " : string.Empty)}отварпывает в {destination.BeltName}. В пути {Mathf.CeilToInt(operation.BeltWarpTotalSeconds)} сек.";
            return true;
        }

        public static float BeltWarpProgress01(OperationSave operation)
        {
            if (operation?.BeltWarpActive != true || operation.BeltWarpTotalSeconds <= 0) return 0;
            return Mathf.Clamp01(1f - operation.BeltWarpSecondsLeft / operation.BeltWarpTotalSeconds);
        }

        static bool Complete(GameSave save, out string message, long nowUnix)
        {
            var operation = save.Operation;
            var destination = Catalog.GetLocation(operation.TravelDestinationLocationId);
            if (destination == null)
            {
                RecoverToStation(save, operation);
                message = "Маршрут больше недоступен; флот безопасно вернулся на Jita 4-4.";
                return true;
            }
            if (!MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                RecoverToStation(save, operation);
                message = $"{destination.DisplayName} больше недоступен; флот безопасно вернулся на Jita 4-4.";
                return true;
            }

            var roster = new HashSet<string>(operation.TravelShipUids ?? new List<string>(), StringComparer.Ordinal);
            foreach (var ship in save.Ships.Where(ship => roster.Contains(ship.Uid) && ship.Location == ShipLocation.Transit))
                ship.Location = ShipLocation.Station;

            // OperationService owns belt generation. Temporarily expose exactly
            // the persisted travel roster to it, then restore the player's launch
            // toggles for the next journey.
            var launchSelections = save.Characters.ToDictionary(pilot => pilot.Id, pilot => pilot.DeployOnLaunch);
            foreach (var pilot in save.Characters)
                pilot.DeployOnLaunch = roster.Contains(pilot.AssignedShipUid);

            try
            {
                OperationService.Start(save, destination.Id, nowUnix);
            }
            finally
            {
                foreach (var pilot in save.Characters)
                    if (launchSelections.TryGetValue(pilot.Id, out var selected)) pilot.DeployOnLaunch = selected;
            }

            var arrived = operation.Active && string.Equals(operation.LocationId, destination.Id, StringComparison.OrdinalIgnoreCase);
            if (arrived) CommitManualSecurityFloor(operation, destination);
            ClearTravel(operation);
            message = arrived
                ? $"Флот прибыл в {destination.DisplayName}. Операция началась."
                : $"Флот достиг {destination.DisplayName}, но операцию запустить не удалось.";
            return true;
        }

        static bool CompleteAutomaticBeltTravel(GameSave save, out string message, long nowUnix)
        {
            var operation = save.Operation;
            var destination = Catalog.GetLocation(operation.TravelDestinationLocationId);
            if (destination == null || destination.SiteKind != MiningSiteKind.StaticBelt ||
                !MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                RestoreAutomaticFleetToSource(save, operation);
                ClearTravel(operation);
                message = "Следующий белт исчез; флот вернулся в исходную точку без разгрузки.";
                return true;
            }

            OperationService.CompleteSameSystemBeltWarp(save, destination, nowUnix);
            ClearTravel(operation);
            message = $"Флот прибыл в {destination.DisplayName}; трюмы, HP и состав флота сохранены.";
            return true;
        }

        static bool CompleteSameSystemBeltWarp(GameSave save, out string message, long nowUnix)
        {
            var operation = save.Operation;
            var destination = Catalog.GetLocation(operation.BeltWarpDestinationLocationId);
            var automatic = operation.BeltWarpIsAutomatic;
            if (destination == null || !MiningSiteService.IsAvailable(save, destination.Id, nowUnix))
            {
                foreach (var member in operation.Fleet)
                {
                    var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                    if (ship != null) ship.Location = ShipLocation.Belt;
                }
                ClearBeltWarp(operation);
                message = "Следующий белт исчез; флот вернулся в исходную точку без разгрузки.";
                return true;
            }

            OperationService.CompleteSameSystemBeltWarp(save, destination, nowUnix);
            if (!automatic) CommitManualSecurityFloor(operation, destination);
            ClearBeltWarp(operation);
            message = $"Флот прибыл в {destination.BeltName}; трюмы и состояние кораблей сохранены.";
            return true;
        }

        static void ClearBeltWarp(OperationSave operation)
        {
            operation.BeltWarpActive = false;
            operation.BeltWarpDestinationLocationId = string.Empty;
            operation.BeltWarpSecondsLeft = 0;
            operation.BeltWarpTotalSeconds = 0;
            operation.BeltWarpIsAutomatic = false;
        }

        static void ClearTravel(OperationSave operation)
        {
            operation.TravelActive = false;
            operation.TravelSourceLocationId = string.Empty;
            operation.TravelDestinationLocationId = string.Empty;
            operation.TravelSecondsLeft = 0;
            operation.TravelTotalSeconds = 0;
            operation.TravelShipUids?.Clear();
            operation.TravelIsAutomatic = false;
        }

        static void CommitManualSecurityFloor(OperationSave operation, LocationDefinition destination)
        {
            if (operation == null || destination == null) return;
            operation.AutoSecurityFloorTenths = MiningSiteService.SecurityTierTenths(destination);
            operation.AutoSecurityFloorInitialized = true;
        }

        static void RestoreAutomaticFleetToSource(GameSave save, OperationSave operation)
        {
            if (save == null || operation?.Fleet == null) return;
            foreach (var member in operation.Fleet)
            {
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                if (ship != null) ship.Location = ShipLocation.Belt;
            }
        }

        static bool ValidateActiveFleetForBeltTravel(GameSave save, LocationDefinition destination, out string message)
        {
            var operation = save?.Operation;
            if (operation?.Fleet == null || operation.Fleet.Count == 0 || operation.Fleet.Any(member =>
                    save.Ships.Find(ship => ship.Uid == member.ShipUid)?.Location != ShipLocation.Belt ||
                    member.Order is FleetOrder.UnloadAndReturn or FleetOrder.DockAndStay))
            {
                message = "Переход начнётся, когда весь активный флот будет в белте.";
                return false;
            }

            foreach (var member in operation.Fleet)
            {
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                var hull = Catalog.GetShip(ship?.HullId);
                var pilot = save.Characters.Find(candidate => candidate.Id == member.PilotId);
                if (pilot != null && OperationService.CanFly(pilot, ship) && OperationService.LocationSupportsShip(destination, ship, hull)) continue;
                var pilotName = pilot?.Name ?? "Неизвестный пилот";
                var shipName = hull?.DisplayName ?? "неизвестном корабле";
                var destinationName = string.IsNullOrWhiteSpace(destination.BeltName) ? destination.DisplayName : destination.BeltName;
                message = $"Переход отменён: {pilotName} на {shipName} не подходит для {destinationName}. Весь флот остался в текущем белте.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        static IEnumerable<ShipSave> SelectedEligibleShips(GameSave save, LocationDefinition destination, bool activeOperation)
        {
            foreach (var pilot in save.Characters)
            {
                if (!pilot.DeployOnLaunch || string.IsNullOrWhiteSpace(pilot.AssignedShipUid)) continue;
                var ship = save.Ships.Find(candidate => candidate.Uid == pilot.AssignedShipUid);
                var hull = Catalog.GetShip(ship?.HullId);
                var belongsToOperation = activeOperation && save.Operation.Fleet.Any(member => member.ShipUid == ship?.Uid);
                if (ship == null || (!belongsToOperation && ship.Location != ShipLocation.Station)) continue;
                if (!OperationService.CanFly(pilot, ship) || !SupportsLocation(ship, hull, destination)) continue;
                yield return ship;
            }
        }

        static bool SupportsLocation(ShipSave ship, ShipDefinition hull, LocationDefinition destination)
        {
            return OperationService.LocationSupportsShip(destination, ship, hull);
        }

        static int IndexOf(string locationId)
        {
            for (var i = 0; i < Catalog.Locations.Count; i++)
                if (string.Equals(Catalog.Locations[i].Id, locationId, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        static void RecoverToStation(GameSave save, OperationSave operation)
        {
            var roster = new HashSet<string>(operation.TravelShipUids ?? new List<string>(), StringComparer.Ordinal);
            foreach (var ship in save.Ships.Where(ship => roster.Contains(ship.Uid) && ship.Location == ShipLocation.Transit))
                ship.Location = ShipLocation.Station;
            ClearTravel(operation);
        }

        static long ResolveNow(long value) => value > 0 ? value : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
