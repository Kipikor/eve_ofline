using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EveOffline
{
    /// <summary>Atomic purchase and locked-fit facade for prepared mining packages.</summary>
    public static class PreparedPackageService
    {
        const int BurstSlotBase=100;
        const int CoreSlot=200;

        public static string DisplayName(ShipSave ship)
        {
            var package=Catalog.GetPackage(ship?.PackageId);
            return package?.DisplayName??Catalog.GetShip(ship?.HullId)?.DisplayName??"Неизвестный корабль";
        }

        public static string EquipmentSummary(PreparedMiningPackage package)
        {
            if(package==null)return string.Empty;
            var hull=Catalog.GetShip(package.HullId);var parts=new List<string>();
            if(!string.IsNullOrEmpty(package.MinerModuleId))parts.Add($"{Catalog.GetModule(package.MinerModuleId)?.DisplayName} × {hull?.MiningHighSlots??0}");
            if(!string.IsNullOrEmpty(package.LowUpgradeId))parts.Add($"{Catalog.GetModule(package.LowUpgradeId)?.DisplayName} × {hull?.LowSlots??0}");
            if(!string.IsNullOrEmpty(package.BurstModuleId))parts.Add($"{Catalog.GetModule(package.BurstModuleId)?.DisplayName} × {BurstModuleCount(package)}");
            if(!string.IsNullOrEmpty(package.IndustrialCoreId))parts.Add(Catalog.GetModule(package.IndustrialCoreId)?.DisplayName);
            var compressors=(package.CompressorModuleIds??Array.Empty<string>()).Select(Catalog.GetModule).Where(module=>module?.Kind==ModuleKind.Compressor).Select(module=>module.DisplayName).ToArray();
            if(compressors.Length>0)parts.Add("Сжатие: "+string.Join(", ",compressors));
            if(!string.IsNullOrEmpty(package.CombatDroneId))parts.Add($"{Catalog.GetDrone(package.CombatDroneId)?.DisplayName} × {CombatDroneCount(package)}");
            var tank=Catalog.GetTankPreset(package.TankPresetId);if(tank!=null)parts.Add(tank.DisplayName);
            if(package.ImplicitUniversalTypeALevel>0)parts.Add($"универсальный Type A {(package.ImplicitUniversalTypeALevel==1?"I":"II")} без износа");
            return string.Join(" • ",parts.Where(part=>!string.IsNullOrWhiteSpace(part)));
        }

        public static string CompressionSummary(PreparedMiningPackage package)
        {
            var kinds=(package?.CompressorModuleIds??Array.Empty<string>())
                .Select(Catalog.GetModule)
                .Where(module=>module?.Kind==ModuleKind.Compressor)
                .Aggregate(CompressionKind.None,(combined,module)=>combined|module.CompressionKind);
            if(kinds==CompressionKind.None)return "Сжатие: нет";
            var labels=new List<string>();
            if((kinds&CompressionKind.Ore)!=0)labels.Add("руда");
            if((kinds&CompressionKind.Ice)!=0)labels.Add("лёд");
            if((kinds&CompressionKind.Gas)!=0)labels.Add("газ");
            if((kinds&CompressionKind.Mercoxit)!=0)labels.Add("Mercoxit");
            return "Сжатие: "+string.Join(" / ",labels);
        }

        public static int BurstModuleCount(PreparedMiningPackage package)
        {
            var hull=Catalog.GetShip(package?.HullId);
            if(hull==null||hull.CommandBurstSlots<=0||string.IsNullOrWhiteSpace(package?.BurstModuleId))return 0;
            return (package.BurstChargeIds??Array.Empty<string>())
                .Take(hull.CommandBurstSlots)
                .TakeWhile(profileId=>Catalog.PreparedBurstProfileIds.Contains(profileId,StringComparer.OrdinalIgnoreCase))
                .Count();
        }

        public static bool CanUsePackage(CharacterSave pilot,PreparedMiningPackage package)
        {
            return pilot!=null&&package!=null&&RequiredSkills(package).All(requirement=>SkillService.GetLevel(pilot,requirement.SkillId)>=requirement.Level);
        }

        public static bool CanUsePackage(CharacterSave pilot,ShipSave ship)
        {
            var package=Catalog.GetPackage(ship?.PackageId)??FindClosestPackage(ship);
            return package!=null&&CanUsePackage(pilot,package);
        }

        public static IReadOnlyList<SkillRequirement> RequiredSkills(PreparedMiningPackage package)
        {
            if(package==null)return Array.Empty<SkillRequirement>();
            var requirements=new Dictionary<string,SkillRequirement>(StringComparer.OrdinalIgnoreCase);
            var ordered=new List<SkillRequirement>();
            var expanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddRequirement(SkillRequirement source)
            {
                if(source==null||string.IsNullOrWhiteSpace(source.SkillId)||source.Level<=0)return;
                if(!requirements.TryGetValue(source.SkillId,out var accumulated))
                {
                    accumulated=new SkillRequirement(source.SkillId,Math.Min(5,source.Level));
                    requirements.Add(source.SkillId,accumulated);
                }
                else accumulated.Level=Math.Max(accumulated.Level,Math.Min(5,source.Level));
                if(expanded.Contains(source.SkillId))return;
                if(!visiting.Add(source.SkillId))throw new InvalidOperationException($"Cyclic package skill prerequisite at {source.SkillId}.");
                var definition=Catalog.GetSkill(source.SkillId)??throw new InvalidOperationException($"Unknown package skill prerequisite: {source.SkillId}.");
                foreach(var prerequisite in definition.Prerequisites??Array.Empty<SkillRequirement>())AddRequirement(prerequisite);
                visiting.Remove(source.SkillId);expanded.Add(source.SkillId);ordered.Add(accumulated);
            }
            void Add(IEnumerable<SkillRequirement> source){foreach(var requirement in source??Array.Empty<SkillRequirement>())AddRequirement(requirement);}
            var hull=Catalog.GetShip(package.HullId);var extractor=Catalog.GetModule(package.MinerModuleId);
            Add(hull?.Requirements);
            Add(extractor?.Requirements);
            if(package.ImplicitUniversalTypeALevel>0)
            {
                var processingLevel=package.ImplicitUniversalTypeALevel==1?3:4;
                if(package.Role==PreparedPackageRole.Mercoxit)AddRequirement(new SkillRequirement("mercoxit-ore-processing",processingLevel));
                else
                {
                    AddRequirement(new SkillRequirement("simple-ore-processing",processingLevel));
                    AddRequirement(new SkillRequirement("coherent-ore-processing",processingLevel));
                    AddRequirement(new SkillRequirement("variegated-ore-processing",processingLevel));
                    AddRequirement(new SkillRequirement("complex-ore-processing",processingLevel));
                }
            }
            // Gas Cloud Harvesting permits one active Scoop per trained level.
            // The module's own requirement (notably T2 at V) remains in the
            // closure and therefore wins over a lower fitted-count gate.
            if(package.Role==PreparedPackageRole.Gas&&extractor?.Kind==ModuleKind.GasCloudScoop)
                AddRequirement(new SkillRequirement("gas-cloud-harvesting",Math.Min(5,Math.Max(1,hull?.MiningHighSlots??1))));
            Add(Catalog.GetModule(package.LowUpgradeId)?.Requirements);
            Add(Catalog.GetDrone(package.CombatDroneId)?.Requirements);
            var combatDroneCount=CombatDroneCount(package);if(combatDroneCount>0)AddRequirement(new SkillRequirement("drones",combatDroneCount));
            Add(Catalog.GetTankPreset(package.TankPresetId)?.Requirements);
            Add(Catalog.GetModule(package.IndustrialCoreId)?.Requirements);
            Add(Catalog.GetModule(package.BurstModuleId)?.Requirements);
            foreach(var compressorId in package.CompressorModuleIds??Array.Empty<string>())Add(Catalog.GetModule(compressorId)?.Requirements);
            return ordered.AsReadOnly();
        }

        public static double PackagePrice(GameSave save,PreparedMiningPackage package)
        {
            if(package==null)return 0;
            var hull=Catalog.GetShip(package.HullId);if(hull==null)return 0;
            if(package.Id=="venture-ore-t0")return 0;
            var price=MarketService.HullSellPrice(save,hull);
            var miner=Catalog.GetModule(package.MinerModuleId);if(miner!=null)price+=MarketService.ModuleSellPrice(save,miner)*hull.MiningHighSlots;
            var low=Catalog.GetModule(package.LowUpgradeId);if(low!=null)price+=MarketService.ModuleSellPrice(save,low)*hull.LowSlots;
            var drone=Catalog.GetDrone(package.CombatDroneId);if(drone!=null)price+=MarketService.DroneSellPrice(save,drone)*CombatDroneCount(package);
            var core=Catalog.GetModule(package.IndustrialCoreId);if(core!=null)price+=MarketService.ModuleSellPrice(save,core);
            var burst=Catalog.GetModule(package.BurstModuleId);if(burst!=null)price+=MarketService.ModuleSellPrice(save,burst)*BurstModuleCount(package);
            foreach(var compressorId in package.CompressorModuleIds??Array.Empty<string>())
            {
                var compressor=Catalog.GetModule(compressorId);if(compressor?.Kind==ModuleKind.Compressor)price+=MarketService.ModuleSellPrice(save,compressor);
            }
            var tank=Catalog.GetTankPreset(package.TankPresetId);
            if(tank!=null)for(var i=0;i<tank.ComponentTypeIds.Length;i++)price+=MarketService.GetSellPrice(save,tank.ComponentTypeIds[i],i<tank.ComponentFallbackPrices.Length?tank.ComponentFallbackPrices[i]:0);
            return Math.Max(0,price);
        }

        public static bool TryBuy(GameSave save,string pilotId,string packageId,out string shipUid,out string message)
        {
            shipUid=string.Empty;var pilot=save?.Characters?.Find(candidate=>candidate.Id==pilotId);var package=Catalog.GetPackage(packageId);
            if(save==null||pilot==null||package==null){message="Пилот или готовый комплект не найден.";return false;}
            if(!string.IsNullOrWhiteSpace(pilot.AssignedShipUid)){message="Сначала освободи слот пилота: ему уже назначен корабль.";return false;}
            if(!CanUsePackage(pilot,package)){message="Пилоту не хватает навыков для полного комплекта.";return false;}
            if(!TryBuyToHangar(save,packageId,out shipUid,out message))return false;
            var purchasedShipUid=shipUid;
            var ship=save.Ships.Find(candidate=>candidate.Uid==purchasedShipUid);
            ship.ShieldHp=MaxShieldHp(ship,pilot);
            pilot.AssignedShipUid=purchasedShipUid;pilot.DeployOnLaunch=true;
            message=$"Куплен {package.DisplayName}: {PackagePrice(save,package):N0} ISK. Комплект назначен пилоту и готов к вылету.";return true;
        }

        /// <summary>Buys a locked package into the common hangar without a pilot or skill gate.</summary>
        public static bool TryBuyToHangar(GameSave save,string packageId,out string shipUid,out string message)
        {
            shipUid=string.Empty;var package=Catalog.GetPackage(packageId);var hull=Catalog.GetShip(package?.HullId);
            if(save==null||package==null||hull==null){message="Готовый комплект не найден.";return false;}
            var price=PackagePrice(save,package);if(price<0||save.Isk+1e-6<price){message=$"Не хватает ISK: комплект стоит {price:N0}.";return false;}
            var uid=$"ship-{save.NextShipSerial:000}";var ship=SaveService.CreateShip(uid,hull.Id);ApplyLockedFit(ship,package);
            save.Isk-=price;save.NextShipSerial++;save.Ships.Add(ship);shipUid=uid;
            message=$"Куплен {package.DisplayName}: {price:N0} ISK. Комплект помещён в общий ангар.";return true;
        }

        public static void ApplyLockedFit(ShipSave ship,PreparedMiningPackage package,bool preserveRuntimeState=false)
        {
            if(ship==null||package==null)throw new ArgumentNullException(ship==null?nameof(ship):nameof(package));
            var hull=Catalog.GetShip(package.HullId)??throw new ArgumentException("Unknown package hull.",nameof(package));
            var oldLocation=ship.Location;var oldMiningHold=ship.MiningHold;var oldCargo=ship.CargoHold;var oldFuel=ship.FuelHold;
            ship.HullId=hull.Id;ship.PackageId=package.Id;ship.TankPresetId=package.TankPresetId;ship.Modules??=new List<FittedModuleSave>();ship.Modules.Clear();
            for(var slot=0;slot<hull.MiningHighSlots&&!string.IsNullOrEmpty(package.MinerModuleId);slot++)ship.Modules.Add(new FittedModuleSave{Slot=slot,ModuleId=package.MinerModuleId,Active=true});
            for(var slot=0;slot<hull.LowSlots&&!string.IsNullOrEmpty(package.LowUpgradeId);slot++)ship.Modules.Add(new FittedModuleSave{Slot=40+slot,ModuleId=package.LowUpgradeId,Active=true});
            for(var slot=0;slot<BurstModuleCount(package);slot++)
            {
                var charge=package.BurstChargeIds[slot];
                ship.Modules.Add(new FittedModuleSave{Slot=BurstSlotBase+slot,ModuleId=package.BurstModuleId,ChargeId=charge,ChargeQuantity=0,ChargeQuantityInitialized=true,Active=false});
            }
            if(!string.IsNullOrEmpty(package.IndustrialCoreId))ship.Modules.Add(new FittedModuleSave{Slot=CoreSlot,ModuleId=package.IndustrialCoreId,Active=false});
            ship.CombatDroneId=package.CombatDroneId??string.Empty;ship.CombatDroneCount=CombatDroneCount(package);ship.MiningDroneId=string.Empty;ship.MiningDroneCount=0;
            if(!preserveRuntimeState)
            {
                ship.Location=ShipLocation.Station;ship.MiningHold=new List<InventoryStack>();ship.CargoHold=new List<InventoryStack>();ship.FuelHold=new List<InventoryStack>();
                ship.ShieldHp=MaxShieldHp(ship);ship.ArmorHp=hull.ArmorHp;ship.StructureHp=hull.StructureHp;
            }
            else
            {
                ship.Location=oldLocation;ship.MiningHold=oldMiningHold??new List<InventoryStack>();ship.CargoHold=oldCargo??new List<InventoryStack>();ship.FuelHold=oldFuel??new List<InventoryStack>();
                ship.ShieldHp=Math.Min(Math.Max(0,ship.ShieldHp),MaxShieldHp(ship));ship.ArmorHp=Math.Min(Math.Max(0,ship.ArmorHp),hull.ArmorHp);ship.StructureHp=Math.Min(Math.Max(0,ship.StructureHp),hull.StructureHp);
            }
        }

        public static PreparedMiningPackage FindClosestPackage(ShipSave ship)
        {
            if(ship==null)return null;var exact=Catalog.GetPackage(ship.PackageId);if(exact!=null)return exact;
            var modules=ship.Modules??new List<FittedModuleSave>();
            var extractors=modules.Select(fitted=>Catalog.GetModule(fitted.ModuleId)).Where(module=>module?.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner or ModuleKind.IceMiningLaser or ModuleKind.IceHarvester or ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester).ToArray();
            var miner=extractors.FirstOrDefault();
            if(IsExactStarterVenture(ship,extractors))return Catalog.GetPackage("venture-ore-t0");
            if(miner!=null)
            {
                var candidates=Catalog.Packages.Where(package=>package.HullId==ship.HullId&&package.MinerModuleId==miner.Id)
                    .Where(package=>package.Grade!=PreparedPackageGrade.T0)
                    .Where(package=>MatchesLockedFit(ship,package))
                    .ToArray();
                if(candidates.Any(package=>package.ImplicitUniversalTypeALevel>0))
                {
                    var legacyCrystalLevel=LegacyCrystalLevel(ship);
                    if(legacyCrystalLevel<=0)return null;
                    candidates=candidates.Where(package=>package.ImplicitUniversalTypeALevel==legacyCrystalLevel).ToArray();
                }
                return candidates.OrderBy(package=>package.Id,StringComparer.Ordinal).FirstOrDefault();
            }
            var core=modules.Select(fitted=>Catalog.GetModule(fitted.ModuleId)).FirstOrDefault(module=>module?.Kind==ModuleKind.IndustrialCore);
            var burst=modules.Select(fitted=>Catalog.GetModule(fitted.ModuleId)).FirstOrDefault(module=>module?.Kind==ModuleKind.MiningBurst);
            if(burst==null)return null;
            return Catalog.Packages.FirstOrDefault(package=>package.HullId==ship.HullId&&package.IsCommandPackage&&
                string.Equals(package.BurstModuleId,burst.Id,StringComparison.OrdinalIgnoreCase)&&
                (string.IsNullOrWhiteSpace(package.IndustrialCoreId)?core==null:string.Equals(package.IndustrialCoreId,core?.Id,StringComparison.OrdinalIgnoreCase))&&
                MatchesLockedFit(ship,package));
        }

        public static float MaxShieldHp(ShipSave ship)=>MaxShieldHp(ship,null);

        public static float MaxShieldHp(ShipSave ship,CharacterSave pilot)
        {
            var hull=Catalog.GetShip(ship?.HullId);if(hull==null)return 0;var tank=Catalog.GetTankPreset(ship?.TankPresetId);
            return Math.Max(0,(hull.ShieldHp+(tank?.ShieldHpBonus??0))*(1f+.05f*SkillService.GetLevel(pilot,"shield-management")));
        }

        public static float MaxTotalHp(ShipSave ship)=>MaxTotalHp(ship,null);
        public static float MaxTotalHp(ShipSave ship,CharacterSave pilot)
        {
            return MaxShieldHp(ship,pilot)+MaxArmorHp(ship)+MaxStructureHp(ship);
        }

        public static float MaxArmorHp(ShipSave ship)=>Math.Max(0,Catalog.GetShip(ship?.HullId)?.ArmorHp??0);
        public static float MaxStructureHp(ShipSave ship)=>Math.Max(0,Catalog.GetShip(ship?.HullId)?.StructureHp??0);

        public static float IncomingShieldDamageMultiplier(ShipSave ship)=>IncomingShieldDamageMultiplier(ship,null);
        public static float IncomingShieldDamageMultiplier(ShipSave ship,CharacterSave pilot)
        {
            var multiplier=Catalog.GetTankPreset(ship?.TankPresetId)?.ShieldDamageMultiplier??1f;var hull=Catalog.GetShip(ship?.HullId);
            if(hull?.ShieldResistBonusPerLevel>0)multiplier*=Mathf.Pow(1f-hull.ShieldResistBonusPerLevel,SkillService.GetLevel(pilot,hull.ShieldResistBonusSkillId));
            return Math.Clamp(multiplier,.05f,1f);
        }

        static int LegacyCrystalLevel(ShipSave ship)
        {
            var extractorCharges=(ship?.Modules??new List<FittedModuleSave>())
                .Where(fitted=>Catalog.GetModule(fitted.ModuleId)?.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner)
                .Select(fitted=>fitted.ChargeId)
                .ToArray();
            if(extractorCharges.Length==0||extractorCharges.Any(string.IsNullOrWhiteSpace))return 0;
            var levels=extractorCharges.Select(id=>id.EndsWith("-a-ii",StringComparison.OrdinalIgnoreCase)?2:id.EndsWith("-a-i",StringComparison.OrdinalIgnoreCase)?1:0).Distinct().ToArray();
            return levels.Length==1?levels[0]:0;
        }

        static bool IsExactStarterVenture(ShipSave ship,IReadOnlyCollection<MiningModuleDefinition> extractors)
        {
            if(ship==null||!string.Equals(ship.HullId,"venture",StringComparison.OrdinalIgnoreCase)||extractors==null||extractors.Count!=2||extractors.Any(module=>!string.Equals(module?.Id,"miner-i",StringComparison.OrdinalIgnoreCase)))return false;
            if((ship.Modules?.Count??0)!=2)return false;
            if(!string.IsNullOrWhiteSpace(ship.TankPresetId)||!string.IsNullOrWhiteSpace(ship.CombatDroneId)||ship.CombatDroneCount>0||!string.IsNullOrWhiteSpace(ship.MiningDroneId)||ship.MiningDroneCount>0)return false;
            return !(ship.Modules??new List<FittedModuleSave>()).Any(fitted=>Catalog.GetModule(fitted.ModuleId)?.Kind is ModuleKind.MiningUpgrade or ModuleKind.IceHarvesterUpgrade or ModuleKind.ShieldTank);
        }

        static bool MatchesLockedFit(ShipSave ship,PreparedMiningPackage package)
        {
            var hull=Catalog.GetShip(package?.HullId);if(ship==null||package==null||hull==null||!string.Equals(ship.HullId,package.HullId,StringComparison.OrdinalIgnoreCase))return false;
            // Pre-v9 prepared fits did not persist the derived shield preset.
            // An empty legacy value may inherit it only after every materialized
            // module and combat drone below has matched the package exactly.
            if(!string.IsNullOrWhiteSpace(ship.TankPresetId)&&!string.Equals(ship.TankPresetId,package.TankPresetId??string.Empty,StringComparison.OrdinalIgnoreCase))return false;
            if(!string.Equals(ship.CombatDroneId??string.Empty,package.CombatDroneId??string.Empty,StringComparison.OrdinalIgnoreCase)||ship.CombatDroneCount!=CombatDroneCount(package))return false;
            if(!string.IsNullOrWhiteSpace(ship.MiningDroneId)||ship.MiningDroneCount!=0)return false;
            var expected=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            void Expect(string moduleId,int count){if(!string.IsNullOrWhiteSpace(moduleId)&&count>0)expected[moduleId]=count;}
            Expect(package.MinerModuleId,hull.MiningHighSlots);Expect(package.LowUpgradeId,hull.LowSlots);Expect(package.BurstModuleId,BurstModuleCount(package));Expect(package.IndustrialCoreId,1);
            var actual=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var fitted in ship.Modules??new List<FittedModuleSave>())
            {
                if(string.IsNullOrWhiteSpace(fitted?.ModuleId))return false;
                actual[fitted.ModuleId]=actual.TryGetValue(fitted.ModuleId,out var count)?count+1:1;
            }
            if(package.IsCommandPackage&&hull.CommandBurstSlots>BurstModuleCount(package)&&
               actual.TryGetValue(package.BurstModuleId,out var legacyBurstCount)&&
               expected.TryGetValue(package.BurstModuleId,out var preparedBurstCount)&&
               legacyBurstCount==preparedBurstCount+1&&
               (ship.Modules??new List<FittedModuleSave>()).Count(fitted=>string.Equals(fitted.ModuleId,package.BurstModuleId,StringComparison.OrdinalIgnoreCase)&&string.Equals(fitted.ChargeId,"mining-equipment-preservation-charge",StringComparison.OrdinalIgnoreCase))==1)
                actual[package.BurstModuleId]=preparedBurstCount;
            return actual.Count==expected.Count&&expected.All(pair=>actual.TryGetValue(pair.Key,out var count)&&count==pair.Value);
        }

        static int CombatDroneCount(PreparedMiningPackage package)
        {
            var hull=Catalog.GetShip(package?.HullId);var drone=Catalog.GetDrone(package?.CombatDroneId);if(hull==null||drone==null||drone.Bandwidth<=0||drone.VolumeM3<=0)return 0;
            return Math.Max(0,Math.Min(5,Math.Min(hull.DroneBandwidth/drone.Bandwidth,(int)Math.Floor(hull.DroneBayM3/drone.VolumeM3))));
        }
    }
}
