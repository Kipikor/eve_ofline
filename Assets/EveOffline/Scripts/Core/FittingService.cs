using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    public static class FittingService
    {
        public const double RepairIskPerHp = 100d;
        public const int DefaultBurstMagazineCharges = 1000;
        const int BurstSlotBase = 100;
        const int CoreSlot = 200;
        const double Epsilon = 0.000001d;

        public static bool TrySwapMiner(GameSave save, string pilotId, string shipUid, int slot, string moduleId, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            if (slot < 0 || slot >= hull.MiningHighSlots) { message = "Некорректный mining slot."; return false; }

            MiningModuleDefinition next = null;
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                next = Catalog.GetModule(moduleId);
                if (!CanFitMiningModule(hull, next)) { message = "Этот модуль несовместим с mining slots корпуса."; return false; }
                if (!SkillService.Meets(pilot, next.Requirements)) { message = "Пилоту не хватает навыков для модуля."; return false; }
            }

            var modules = CloneModules(ship.Modules);
            var station = CloneStacks(save.StationInventory);
            var instances = CloneInstances(save.StationItemInstances);
            var nextSerial = Math.Max(1, save.NextItemSerial);
            var current = modules.FirstOrDefault(fitted => fitted.Slot == slot && IsMiner(fitted));
            if (current != null && string.Equals(current.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Этот mining module уже установлен.";
                return true;
            }

            if (current != null)
            {
                AddStack(station, current.ModuleId, 1);
                ReturnCharge(current, station, instances, ref nextSerial);
                modules.Remove(current);
            }
            if (next != null)
            {
                if (!RemoveStack(station, next.Id, 1)) { message = $"На складе нет {next.DisplayName}."; return false; }
                modules.Add(new FittedModuleSave { Slot = slot, ModuleId = next.Id, Active = true });
            }

            CommitShipAndStation(save, ship, modules, station, instances, nextSerial);
            message = next == null ? $"Mining slot {slot + 1} освобождён." : $"Установлен {next.DisplayName}.";
            return true;
        }

        public static bool TrySetCrystal(GameSave save, string pilotId, string shipUid, int slot, string crystalId, string instanceUid, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            if (slot < 0 || slot >= hull.MiningHighSlots) { message = "Некорректный mining slot."; return false; }

            var modules = CloneModules(ship.Modules);
            var fitted = modules.FirstOrDefault(candidate => candidate.Slot == slot && CrystalCapable(Catalog.GetModule(candidate.ModuleId)));
            var fittedModule = Catalog.GetModule(fitted?.ModuleId);
            if (fitted == null) { message = "В этом slot нет modulated mining module для кристалла."; return false; }

            var station = CloneStacks(save.StationInventory);
            var instances = CloneInstances(save.StationItemInstances);
            var nextSerial = Math.Max(1, save.NextItemSerial);
            var wantsUnload = string.IsNullOrWhiteSpace(crystalId) && string.IsNullOrWhiteSpace(instanceUid);
            if (wantsUnload && string.IsNullOrWhiteSpace(fitted.ChargeId)) { message = "Кристалл уже выгружен."; return true; }
            if (!wantsUnload && !string.IsNullOrWhiteSpace(instanceUid) && string.Equals(fitted.ChargeUid, instanceUid, StringComparison.Ordinal))
            {
                message = "Этот кристалл уже заряжен.";
                return true;
            }

            ReturnCharge(fitted, station, instances, ref nextSerial);
            fitted.ChargeId = string.Empty;
            fitted.ChargeUid = string.Empty;
            fitted.ChargeDamage = 0;
            if (!wantsUnload)
            {
                MiningCrystalDefinition crystal;
                float damage;
                string uid;
                if (!string.IsNullOrWhiteSpace(instanceUid))
                {
                    var instance = instances.FirstOrDefault(candidate => candidate.Uid == instanceUid);
                    crystal = Catalog.GetCrystal(instance?.ItemId);
                    if (instance == null || crystal == null || (!string.IsNullOrWhiteSpace(crystalId) && crystal.Id != crystalId))
                    { message = "Выбранный экземпляр кристалла не найден на складе."; return false; }
                    if (!ModuleAcceptsCrystal(fittedModule, crystal)) { message = "Этот mining module не принимает выбранное семейство кристаллов."; return false; }
                    if (!SkillService.Meets(pilot, crystal.Requirements)) { message = "Пилоту не хватает processing skills для кристалла."; return false; }
                    damage = Math.Clamp(instance.Damage, 0f, .999f);
                    uid = instance.Uid;
                    instances.Remove(instance);
                }
                else
                {
                    crystal = Catalog.GetCrystal(crystalId);
                    if (crystal == null) { message = "Неизвестный mining crystal."; return false; }
                    if (!ModuleAcceptsCrystal(fittedModule, crystal)) { message = "Этот mining module не принимает выбранное семейство кристаллов."; return false; }
                    if (!SkillService.Meets(pilot, crystal.Requirements)) { message = "Пилоту не хватает processing skills для кристалла."; return false; }
                    if (!RemoveStack(station, crystal.Id, 1)) { message = $"На складе нет {crystal.DisplayName}."; return false; }
                    damage = 0;
                    uid = NextItemUid(ref nextSerial);
                }
                fitted.ChargeId = crystal.Id;
                fitted.ChargeUid = uid;
                fitted.ChargeDamage = damage;
            }

            CommitShipAndStation(save, ship, modules, station, instances, nextSerial);
            message = wantsUnload ? "Кристалл выгружен без ремонта износа." : $"Заряжен {Catalog.GetCrystal(fitted.ChargeId).DisplayName}; износ {fitted.ChargeDamage:P0}.";
            return true;
        }

        public static bool TrySetDrones(GameSave save, string pilotId, string shipUid, bool mining, string droneId, int count, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            if (count < 0 || count > 5) { message = "Количество дронов должно быть от 0 до 5."; return false; }
            DroneDefinition next = null;
            if (count > 0)
            {
                next = Catalog.GetDrone(droneId);
                if (next == null || next.Mining != mining) { message = "Выбран неверный тип дрона."; return false; }
                if (!SkillService.Meets(pilot, next.Requirements)) { message = "Пилоту не хватает навыков для дрона."; return false; }
            }

            var currentId = mining ? ship.MiningDroneId : ship.CombatDroneId;
            var currentCount = mining ? ship.MiningDroneCount : ship.CombatDroneCount;
            if (string.Equals(currentId, droneId, StringComparison.OrdinalIgnoreCase) && currentCount == count)
            { message = "Такой комплект дронов уже загружен."; return true; }

            var station = CloneStacks(save.StationInventory);
            if (!string.IsNullOrWhiteSpace(currentId) && currentCount > 0) AddStack(station, currentId, currentCount);
            if (next != null && !RemoveStack(station, next.Id, count)) { message = $"На складе нет полного комплекта: нужно {count} × {next.DisplayName}."; return false; }

            var combatId = mining ? ship.CombatDroneId : next?.Id;
            var combatCount = mining ? ship.CombatDroneCount : count;
            var miningId = mining ? next?.Id : ship.MiningDroneId;
            var miningCount = mining ? count : ship.MiningDroneCount;
            var bayUsed = DroneVolume(combatId, combatCount) + DroneVolume(miningId, miningCount);
            if (bayUsed > hull.DroneBayM3 + .001f) { message = $"Drone bay переполнен: {bayUsed:N0}/{hull.DroneBayM3:N0} м³."; return false; }

            CommitStacks(save.StationInventory, station);
            if (mining) { ship.MiningDroneId = next?.Id ?? string.Empty; ship.MiningDroneCount = count; }
            else { ship.CombatDroneId = next?.Id ?? string.Empty; ship.CombatDroneCount = count; }
            message = count == 0 ? $"{(mining ? "Mining" : "Combat")} drones выгружены." : $"Загружено {count} × {next.DisplayName}; drone bay {bayUsed:N0}/{hull.DroneBayM3:N0} м³.";
            return true;
        }

        public static float DroneBayUsedM3(ShipSave ship)
        {
            return ship == null ? 0 : DroneVolume(ship.CombatDroneId, ship.CombatDroneCount) + DroneVolume(ship.MiningDroneId, ship.MiningDroneCount);
        }

        public static bool TryAddMiningUpgrade(GameSave save, string pilotId, string shipUid, string moduleId, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            var module = Catalog.GetModule(moduleId);
            if (module?.Kind != ModuleKind.MiningUpgrade) { message = "Это не Mining Laser Upgrade."; return false; }
            if (!SkillService.Meets(pilot, module.Requirements)) { message = "Пилоту не хватает навыков."; return false; }
            if (ship.Modules.Count(fitted => Catalog.GetModule(fitted.ModuleId)?.Kind == ModuleKind.MiningUpgrade) >= hull.LowSlots) { message = $"Low slots заняты: {hull.LowSlots}/{hull.LowSlots}."; return false; }
            var station = CloneStacks(save.StationInventory);
            if (!RemoveStack(station, module.Id, 1)) { message = "На складе нет этого mining upgrade."; return false; }
            var modules = CloneModules(ship.Modules);
            modules.Add(new FittedModuleSave { Slot = 40 + modules.Count(candidate => Catalog.GetModule(candidate.ModuleId)?.Kind == ModuleKind.MiningUpgrade), ModuleId = module.Id, Active = true });
            CommitStacks(save.StationInventory, station); CommitModules(ship, modules);
            message = $"Установлен {module.DisplayName}.";
            return true;
        }

        public static bool TryRemoveAllMiningUpgrades(GameSave save, string pilotId, string shipUid, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out _, out var ship, out _, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            var modules = CloneModules(ship.Modules);
            var fitted = modules.Where(candidate => Catalog.GetModule(candidate.ModuleId)?.Kind == ModuleKind.MiningUpgrade).ToArray();
            if (fitted.Length == 0) { message = "Mining upgrades уже сняты."; return true; }
            var station = CloneStacks(save.StationInventory);
            foreach (var module in fitted) { AddStack(station, module.ModuleId, 1); modules.Remove(module); }
            CommitStacks(save.StationInventory, station); CommitModules(ship, modules);
            message = "Mining Laser Upgrades сняты на склад.";
            return true;
        }

        public static bool TrySetBurstModule(GameSave save, string pilotId, string shipUid, int burstSlot, string moduleId, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            if (burstSlot < 0 || burstSlot >= hull.CommandBurstSlots) { message = "У корпуса нет такого command burst slot."; return false; }
            MiningModuleDefinition next = null;
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                next = Catalog.GetModule(moduleId);
                if (next?.Kind != ModuleKind.MiningBurst) { message = "Это не Mining Foreman Burst."; return false; }
                if (!SkillService.Meets(pilot, next.Requirements)) { message = "Пилоту не хватает навыков для burst module."; return false; }
            }

            var modules = CloneModules(ship.Modules);
            var bursts = modules.Where(IsBurst).OrderBy(candidate => candidate.Slot).ToList();
            var current = burstSlot < bursts.Count ? bursts[burstSlot] : null;
            if (current == null && burstSlot > bursts.Count) { message = "Сначала заполни предыдущий burst slot."; return false; }
            if (current != null && current.ModuleId == moduleId) { message = "Этот burst module уже установлен."; return true; }
            var station = CloneStacks(save.StationInventory);
            var instances = CloneInstances(save.StationItemInstances);
            var nextSerial = Math.Max(1, save.NextItemSerial);
            if (current != null)
            {
                AddStack(station, current.ModuleId, 1);
                ReturnCharge(current, station, instances, ref nextSerial);
                modules.Remove(current);
            }
            if (next != null)
            {
                if (!RemoveStack(station, next.Id, 1)) { message = $"На складе нет {next.DisplayName}."; return false; }
                modules.Add(new FittedModuleSave { Slot = BurstSlotBase + burstSlot, ModuleId = next.Id, Active = false });
            }
            CanonicalizeBurstSlots(modules);
            CommitShipAndStation(save, ship, modules, station, instances, nextSerial);
            message = next == null ? "Burst module снят." : $"Установлен {next.DisplayName} в burst slot {burstSlot + 1}.";
            return true;
        }

        public static bool TrySetBurstCharge(GameSave save, string pilotId, string shipUid, int burstSlot, string chargeId, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out _, out var ship, out var hull, out message)) return false;
            if (burstSlot < 0 || burstSlot >= hull.CommandBurstSlots) { message = "У корпуса нет такого command burst slot."; return false; }
            var fitted = (ship.Modules ?? new List<FittedModuleSave>()).Where(IsBurst).OrderBy(candidate => candidate.Slot).ElementAtOrDefault(burstSlot);
            if (fitted == null) { message = "Сначала установи burst module в этот slot."; return false; }
            message = "Заряды отключены: burst использует бесплатный встроенный эффект и не требует пополнения.";
            return true;
        }

        public static bool TrySetIndustrialCore(GameSave save, string pilotId, string shipUid, string moduleId, out string message)
        {
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            if (!CanRefit(ship, out message)) return false;
            MiningModuleDefinition next = null;
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                next = Catalog.GetModule(moduleId);
                if (next?.Kind != ModuleKind.IndustrialCore || !CoreMatchesHull(hull, next.Id)) { message = "Industrial Core несовместим с этим корпусом."; return false; }
                if (!SkillService.Meets(pilot, next.Requirements)) { message = "Пилоту не хватает навыков для Industrial Core."; return false; }
            }
            var modules = CloneModules(ship.Modules);
            var current = modules.FirstOrDefault(candidate => Catalog.GetModule(candidate.ModuleId)?.Kind == ModuleKind.IndustrialCore);
            if (current != null && current.ModuleId == moduleId) { message = "Этот Industrial Core уже установлен."; return true; }
            var station = CloneStacks(save.StationInventory);
            if (current != null) { AddStack(station, current.ModuleId, 1); modules.Remove(current); }
            if (next != null)
            {
                if (!RemoveStack(station, next.Id, 1)) { message = $"На складе нет {next.DisplayName}."; return false; }
                modules.Add(new FittedModuleSave { Slot = CoreSlot, ModuleId = next.Id, Active = false });
            }
            CommitStacks(save.StationInventory, station); CommitModules(ship, modules);
            message = next == null ? "Industrial Core снят." : $"Установлен {next.DisplayName}.";
            return true;
        }

        public static bool TryTransferFuel(GameSave save, string pilotId, string shipUid, double requested, bool toShip, out double transferred, out string message)
        {
            transferred = 0;
            if (!TryResolveStationShip(save, pilotId, shipUid, out _, out var ship, out var hull, out message)) return false;
            if (hull.FuelHoldM3 <= 0) { message = "У корабля нет топливного отсека."; return false; }
            return TryTransferStack(save.StationInventory, ship.FuelHold, "heavy-water", .4f, hull.FuelHoldM3, requested, toShip, out transferred, out message);
        }

        public static bool TryTransferCargo(GameSave save, string pilotId, string shipUid, string itemId, double requested, bool toShip, out double transferred, out string message)
        {
            transferred = 0;
            if (!TryResolveStationShip(save, pilotId, shipUid, out _, out var ship, out var hull, out message)) return false;
            if (!Catalog.TryGetItemVolumeM3(itemId, out var volumeM3)) { message = "Неизвестен packaged volume предмета; перенос отменён."; return false; }
            return TryTransferStack(save.StationInventory, ship.CargoHold, itemId, volumeM3, hull.CargoHoldM3, requested, toShip, out transferred, out message);
        }

        public static bool TryRepairShip(GameSave save, string pilotId, string shipUid, out double cost, out string message)
        {
            cost = 0;
            if (!TryResolveStationShip(save, pilotId, shipUid, out var pilot, out var ship, out var hull, out message)) return false;
            var maxShield = PreparedPackageService.MaxShieldHp(ship, pilot);
            var missingShield = Math.Max(0, maxShield - ship.ShieldHp);
            var missingArmor = Math.Max(0, hull.ArmorHp - ship.ArmorHp);
            var missingStructure = Math.Max(0, hull.StructureHp - ship.StructureHp);
            var missingHp = missingShield + missingArmor + missingStructure;
            if (missingHp <= .0001f) { message = "Корабль уже полностью отремонтирован."; return true; }
            cost = missingHp * RepairIskPerHp;
            if (save.Isk + Epsilon < cost) { message = $"Ремонт {missingHp:N0} HP стоит {cost:N0} ISK — денег не хватает."; cost = 0; return false; }
            save.Isk -= cost;
            ship.ShieldHp = maxShield;
            ship.ArmorHp = hull.ArmorHp;
            ship.StructureHp = hull.StructureHp;
            message = $"Восстановлено {missingHp:N0} HP за {cost:N0} ISK (100 ISK/HP).";
            return true;
        }

        public static double StackQuantity(List<InventoryStack> stacks, string itemId)
        {
            return stacks?.Where(stack => stack != null && stack.ItemId == itemId).Sum(stack => Math.Max(0, stack.Quantity)) ?? 0;
        }

        static bool TryTransferStack(List<InventoryStack> stationLive, List<InventoryStack> shipLive, string itemId, float volumeM3, double capacityM3, double requested, bool toShip, out double transferred, out string message)
        {
            transferred = 0;
            if (string.IsNullOrWhiteSpace(itemId) || volumeM3 <= 0 || requested <= 0 || double.IsNaN(requested)) { message = "Некорректный перенос."; return false; }
            var station = CloneStacks(stationLive);
            var hold = CloneStacks(shipLive);
            var source = toShip ? station : hold;
            var destination = toShip ? hold : station;
            var available = StackQuantity(source, itemId);
            if (available <= Epsilon) { message = "В исходном отсеке нет выбранного предмета."; return false; }
            var amount = double.IsPositiveInfinity(requested) ? available : Math.Min(requested, available);
            if (toShip)
            {
                if (!TryStackVolume(hold, out var usedM3)) { message = "В грузовом отсеке есть предмет с неизвестным объёмом."; return false; }
                var capacityUnits = Math.Max(0, (capacityM3 - usedM3) / volumeM3);
                amount = Math.Min(amount, capacityUnits);
            }
            if (amount <= Epsilon) { message = "В отсеке нет свободного объёма."; return false; }
            if (!RemoveStack(source, itemId, amount)) { message = "Предметы изменились; перенос отменён."; return false; }
            AddStack(destination, itemId, amount);
            CommitStacks(stationLive, station);
            CommitStacks(shipLive, hold);
            transferred = amount;
            message = $"{(toShip ? "Загружено" : "Выгружено")} {amount:N0} × {itemId}.";
            return true;
        }

        static bool TryResolveStationShip(GameSave save, string pilotId, string shipUid, out CharacterSave pilot, out ShipSave ship, out ShipDefinition hull, out string message)
        {
            pilot = save?.Characters?.Find(candidate => candidate.Id == pilotId);
            ship = save?.Ships?.Find(candidate => candidate.Uid == shipUid);
            hull = Catalog.GetShip(ship?.HullId);
            if (save == null || pilot == null || ship == null || hull == null) { message = "Пилот или корабль не найден."; return false; }
            if (pilot.AssignedShipUid != ship.Uid) { message = "Корабль не назначен выбранному пилоту."; return false; }
            var resolvedShipUid = ship.Uid;
            if (ship.Location != ShipLocation.Station || save.Operation?.Fleet?.Any(member => member.ShipUid == resolvedShipUid) == true)
            { message = "Переоснащать можно только пришвартованный корабль вне активного флота."; return false; }
            if (save.Operation?.IndustrialCoreActive == true && save.Operation.IndustrialCoreShipUid == ship.Uid)
            { message = "Industrial Core ещё блокирует корабль."; return false; }
            save.StationInventory ??= new List<InventoryStack>();
            save.StationItemInstances ??= new List<ItemInstanceSave>();
            ship.Modules ??= new List<FittedModuleSave>();
            ship.CargoHold ??= new List<InventoryStack>();
            ship.FuelHold ??= new List<InventoryStack>();
            message = string.Empty;
            return true;
        }

        static bool CanRefit(ShipSave ship, out string message)
        {
            if (Catalog.GetPackage(ship?.PackageId) != null)
            {
                message = "Готовый комплект заблокирован: его добывающие модули, upgrades, дроны, bursts и Industrial Core не меняются вручную.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        static bool CoreMatchesHull(ShipDefinition hull, string moduleId)
        {
            if (hull?.SupportsIndustrialCore != true) return false;
            return hull.Id == "rorqual" ? moduleId.StartsWith("capital-industrial-core", StringComparison.Ordinal)
                : hull.Id == "porpoise" ? moduleId.StartsWith("medium-industrial-core", StringComparison.Ordinal)
                : hull.Id == "orca" && moduleId.StartsWith("industrial-core", StringComparison.Ordinal);
        }

        public static bool CanFitMiningModule(ShipDefinition hull, MiningModuleDefinition module)
        {
            if (hull == null || module == null) return false;
            if (module.Kind == ModuleKind.IceMiningLaser) return hull.SupportsIceMiningLasers;
            if (module.Kind == ModuleKind.IceHarvester) return hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer;
            if (module.Kind == ModuleKind.GasCloudScoop)
                return hull.Id is "venture" or "venture-consortium" or "prospect" or "pioneer" or "pioneer-consortium";
            if (module.Kind == ModuleKind.GasCloudHarvester)
                return hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer;
            if (hull.IceOnly) return false;
            return hull.UsesStripMiners ? module.Kind == ModuleKind.StripMiner : module.Kind == ModuleKind.MiningLaser;
        }

        public static bool ModuleAcceptsCrystal(MiningModuleDefinition module, MiningCrystalDefinition crystal)
        {
            if (!CrystalCapable(module) || crystal == null) return false;
            // Mercoxit crystals use the same compatibility contract as every
            // other asteroid-ore family. The legacy capability flag remains on
            // definitions only so older catalog/save data can still be read.
            return module.AcceptsRegularCrystals;
        }

        static bool CrystalCapable(MiningModuleDefinition module) => module?.AcceptsRegularCrystals == true || module?.AcceptsMercoxitCrystals == true;

        static bool IsMiner(FittedModuleSave fitted) => Catalog.GetModule(fitted?.ModuleId)?.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner or ModuleKind.IceMiningLaser or ModuleKind.IceHarvester or ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
        static bool IsBurst(FittedModuleSave fitted) => Catalog.GetModule(fitted?.ModuleId)?.Kind == ModuleKind.MiningBurst;
        static float DroneVolume(string droneId, int count) => Math.Max(0, count) * (Catalog.GetDrone(droneId)?.VolumeM3 ?? 0);

        static void ReturnCharge(FittedModuleSave fitted, List<InventoryStack> station, List<ItemInstanceSave> instances, ref int nextSerial)
        {
            if (fitted == null || string.IsNullOrWhiteSpace(fitted.ChargeId)) return;
            if (Catalog.GetCrystal(fitted.ChargeId) != null)
            {
                var uid = string.IsNullOrWhiteSpace(fitted.ChargeUid) ? NextItemUid(ref nextSerial) : fitted.ChargeUid;
                instances.RemoveAll(candidate => candidate.Uid == uid);
                instances.Add(new ItemInstanceSave { Uid = uid, ItemId = fitted.ChargeId, Damage = Math.Clamp(fitted.ChargeDamage, 0f, .999f) });
            }
            else if (Catalog.GetBurstCharge(fitted.ChargeId) != null && fitted.ChargeQuantity > 0) AddStack(station, fitted.ChargeId, fitted.ChargeQuantity);
        }

        static void CommitShipAndStation(GameSave save, ShipSave ship, List<FittedModuleSave> modules, List<InventoryStack> station, List<ItemInstanceSave> instances, int nextSerial)
        {
            CommitModules(ship, modules);
            CommitStacks(save.StationInventory, station);
            save.StationItemInstances.Clear();
            save.StationItemInstances.AddRange(instances);
            save.NextItemSerial = nextSerial;
        }

        static void CanonicalizeBurstSlots(List<FittedModuleSave> modules)
        {
            var bursts = modules.Where(IsBurst).OrderBy(candidate => candidate.Slot).ToArray();
            for (var index = 0; index < bursts.Length; index++) bursts[index].Slot = BurstSlotBase + index;
        }

        static bool TryStackVolume(List<InventoryStack> stacks, out double volumeM3)
        {
            volumeM3 = 0;
            foreach (var stack in stacks ?? Enumerable.Empty<InventoryStack>())
            {
                if (stack == null || stack.Quantity <= 0) continue;
                if (!Catalog.TryGetItemVolumeM3(stack.ItemId, out var itemVolume)) return false;
                volumeM3 += stack.Quantity * itemVolume;
            }
            return true;
        }

        static List<InventoryStack> CloneStacks(IEnumerable<InventoryStack> source) => source?.Where(stack => stack != null).Select(stack => new InventoryStack { ItemId = stack.ItemId, Quantity = stack.Quantity }).ToList() ?? new List<InventoryStack>();
        static List<ItemInstanceSave> CloneInstances(IEnumerable<ItemInstanceSave> source) => source?.Where(item => item != null).Select(item => new ItemInstanceSave { Uid = item.Uid, ItemId = item.ItemId, Damage = item.Damage }).ToList() ?? new List<ItemInstanceSave>();
        static List<FittedModuleSave> CloneModules(IEnumerable<FittedModuleSave> source) => source?.Where(module => module != null).Select(module => new FittedModuleSave { Slot = module.Slot, ModuleId = module.ModuleId, ChargeId = module.ChargeId, ChargeUid = module.ChargeUid, ChargeDamage = module.ChargeDamage, ChargeQuantity = module.ChargeQuantity, ChargeQuantityInitialized = module.ChargeQuantityInitialized, BurstCycleSecondsLeft = module.BurstCycleSecondsLeft, Active = module.Active }).ToList() ?? new List<FittedModuleSave>();
        static void CommitStacks(List<InventoryStack> destination, IEnumerable<InventoryStack> source) { destination.Clear(); destination.AddRange(source.Where(stack => stack.Quantity > Epsilon)); }
        static void CommitModules(ShipSave ship, IEnumerable<FittedModuleSave> modules) { ship.Modules.Clear(); ship.Modules.AddRange(modules); }

        static void AddStack(List<InventoryStack> stacks, string itemId, double quantity)
        {
            if (stacks == null || string.IsNullOrWhiteSpace(itemId) || quantity <= Epsilon) return;
            var stack = stacks.FirstOrDefault(candidate => candidate.ItemId == itemId);
            if (stack == null) { stack = new InventoryStack { ItemId = itemId }; stacks.Add(stack); }
            stack.Quantity += quantity;
        }

        static bool RemoveStack(List<InventoryStack> stacks, string itemId, double quantity)
        {
            if (stacks == null || string.IsNullOrWhiteSpace(itemId) || quantity <= Epsilon) return false;
            var available = StackQuantity(stacks, itemId);
            if (available + Epsilon < quantity) return false;
            var remaining = quantity;
            foreach (var stack in stacks.Where(candidate => candidate.ItemId == itemId).ToArray())
            {
                var removed = Math.Min(Math.Max(0, stack.Quantity), remaining);
                stack.Quantity -= removed;
                remaining -= removed;
                if (stack.Quantity <= Epsilon) stacks.Remove(stack);
                if (remaining <= Epsilon) break;
            }
            return remaining <= Epsilon;
        }

        static string NextItemUid(ref int nextSerial) => $"item-{nextSerial++:000000}";
    }
}
