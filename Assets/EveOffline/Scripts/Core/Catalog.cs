using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EveOffline
{
    public enum SecurityBand { HighSec, LowSec, NullSec }
    public enum MiningSiteKind { StaticBelt, DynamicAnomaly }
    public enum ShipClass { MiningFrigate, MiningDestroyer, MiningBarge, Exhumer, IndustrialCommand, CapitalIndustrial }
    public enum ResourceKind { Ore, Ice, Gas }
    public enum ModuleKind { MiningLaser, StripMiner, IceMiningLaser, IceHarvester, GasCloudScoop, GasCloudHarvester, MiningUpgrade, IceHarvesterUpgrade, IndustrialCore, MiningBurst, Compressor, ShieldTank, Utility }
    public enum PreparedPackageRole { Ore, Ice, Gas, Mercoxit, Booster }
    public enum PreparedPackageGrade { T0, T1, T2, ORE }

    [Flags]
    public enum CompressionKind { None = 0, Ore = 1, Ice = 2, Gas = 4, Mercoxit = 8 }

    [Serializable]
    public sealed class SkillRequirement
    {
        public string SkillId;
        public int Level;
        public SkillRequirement(string skillId, int level) { SkillId = skillId; Level = level; }
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public int Rank;
        public double BookFallbackPrice;
        public string Description;
        public SkillRequirement[] Prerequisites;

        public SkillDefinition(string id, string name, int typeId, int rank, double bookPrice, string description, params SkillRequirement[] prerequisites)
        {
            Id = id; DisplayName = name; TypeId = typeId; Rank = rank; BookFallbackPrice = bookPrice; Description = description; Prerequisites = prerequisites ?? Array.Empty<SkillRequirement>();
        }
    }

    [Serializable]
    public sealed class OreDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public float UnitVolumeM3;
        public int DefaultAsteroidUnits;
        public double FallbackBuyPrice;
        public double FallbackSellPrice;
        public Color Color;
        public ResourceKind Kind;
        public string FamilyId;
        public string BaseOreId;
        public int Grade;
        public double ValueMultiplier;

        public OreDefinition(string id, string name, int typeId, float unitVolumeM3, int units, double buy, double sell, Color color, ResourceKind kind = ResourceKind.Ore, string familyId = null, string baseOreId = null, int grade = 1, double valueMultiplier = 1d)
        {
            Id = id; DisplayName = name; TypeId = typeId; UnitVolumeM3 = unitVolumeM3; DefaultAsteroidUnits = units; ValueMultiplier = valueMultiplier; FallbackBuyPrice = buy * valueMultiplier; FallbackSellPrice = sell * valueMultiplier; Color = color; Kind = kind; FamilyId = string.IsNullOrWhiteSpace(familyId) ? id : familyId; BaseOreId = string.IsNullOrWhiteSpace(baseOreId) ? FamilyId : baseOreId; Grade = grade;
        }
    }

    [Serializable]
    public sealed class MiningModuleDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public ModuleKind Kind;
        public int TechLevel;
        public float BaseYieldM3;
        public float CycleSeconds;
        public float RangeKm;
        public float ResidueChance;
        public float ResidueMultiplier;
        public float CriticalChance;
        public float CriticalBonusYield;
        public float FuelPerCycle;
        public float VolumeM3 = 5f;
        public bool CanMineMercoxit;
        public bool AcceptsRegularCrystals;
        public bool AcceptsMercoxitCrystals;
        public CompressionKind CompressionKind;
        public double FallbackPrice;
        public SkillRequirement[] Requirements;

        public MiningModuleDefinition(string id, string name, int typeId, ModuleKind kind, int tech, float yield, float cycle, float range, float residueChance, float residueMultiplier, double price, params SkillRequirement[] requirements)
        {
            Id = id; DisplayName = name; TypeId = typeId; Kind = kind; TechLevel = tech; BaseYieldM3 = yield; CycleSeconds = cycle; RangeKm = range; ResidueChance = residueChance; ResidueMultiplier = residueMultiplier; CriticalChance = .01f; CriticalBonusYield = 2f; FallbackPrice = price; Requirements = requirements ?? Array.Empty<SkillRequirement>();
        }
    }

    [Serializable]
    public sealed class TankPresetDefinition
    {
        public string Id;
        public string DisplayName;
        public int[] ComponentTypeIds;
        public double[] ComponentFallbackPrices;
        public float ShieldHpBonus;
        public float ShieldDamageMultiplier;
        public SkillRequirement[] Requirements;

        public TankPresetDefinition(string id,string name,int[] typeIds,double[] prices,float shieldHpBonus,float shieldDamageMultiplier,params SkillRequirement[] requirements)
        {
            Id=id;DisplayName=name;ComponentTypeIds=typeIds??Array.Empty<int>();ComponentFallbackPrices=prices??Array.Empty<double>();ShieldHpBonus=shieldHpBonus;ShieldDamageMultiplier=shieldDamageMultiplier;Requirements=requirements??Array.Empty<SkillRequirement>();
        }
    }

    [Serializable]
    public sealed class PreparedMiningPackage
    {
        public string Id;
        public string DisplayName;
        public string HullId;
        public PreparedPackageRole Role;
        public PreparedPackageGrade Grade;
        public string MinerModuleId;
        public string LowUpgradeId;
        public string CombatDroneId;
        public string TankPresetId;
        public string IndustrialCoreId;
        public string BurstModuleId;
        public string[] BurstChargeIds;
        public string[] CompressorModuleIds;
        public int ImplicitUniversalTypeALevel;
        public bool ImplicitUniversalTypeA2=>ImplicitUniversalTypeALevel==2;
        public bool IsCommandPackage=>Role==PreparedPackageRole.Booster;

        public PreparedMiningPackage(string id,string name,string hullId,PreparedPackageRole role,PreparedPackageGrade grade,string miner,string low,string drone,string tank,string core=null,string burst=null,string[] charges=null,int universalTypeALevel=0,string[] compressors=null)
        {
            Id=id;DisplayName=name;HullId=hullId;Role=role;Grade=grade;MinerModuleId=miner;LowUpgradeId=low;CombatDroneId=drone;TankPresetId=tank;IndustrialCoreId=core;BurstModuleId=burst;BurstChargeIds=charges??Array.Empty<string>();ImplicitUniversalTypeALevel=universalTypeALevel;CompressorModuleIds=compressors??Array.Empty<string>();
        }
    }

    [Serializable]
    public sealed class DroneDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public bool Mining;
        public float BaseDps;
        public float BaseMiningM3PerMinute;
        public int Bandwidth;
        public float VolumeM3;
        public double FallbackPrice;
        public SkillRequirement[] Requirements;

        public DroneDefinition(string id, string name, int typeId, bool mining, float dps, float miningPerMinute, int bandwidth, float volumeM3, double price, params SkillRequirement[] requirements)
        {
            Id = id; DisplayName = name; TypeId = typeId; Mining = mining; BaseDps = dps; BaseMiningM3PerMinute = miningPerMinute; Bandwidth = bandwidth; VolumeM3 = volumeM3; FallbackPrice = price; Requirements = requirements ?? Array.Empty<SkillRequirement>();
        }
    }

    [Serializable]
    public sealed class MiningCrystalDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public string[] OreIds;
        public float MiningAmountMultiplier;
        public float CycleMultiplier;
        public float ResidueChanceBonus;
        public float ResidueMultiplierBonus;
        public float VolatilityChance;
        public float DamagePerVolatility;
        public float VolumeM3;
        public double FallbackPrice;
        public SkillRequirement[] Requirements;

        public MiningCrystalDefinition(string id,string name,int typeId,string[] oreIds,float amount,float cycle,float residueChance,float residueMultiplier,float volatility,float damage,double price,params SkillRequirement[] requirements)
        {
            Id=id;DisplayName=name;TypeId=typeId;OreIds=oreIds;MiningAmountMultiplier=amount;CycleMultiplier=cycle;ResidueChanceBonus=residueChance;ResidueMultiplierBonus=residueMultiplier;VolatilityChance=volatility;DamagePerVolatility=damage;FallbackPrice=price;Requirements=requirements??Array.Empty<SkillRequirement>();
        }

        public bool SupportsOre(string oreId)=>OreIds?.Any(x=>Catalog.SameOreFamily(x,oreId))==true;
    }

    [Serializable]
    public sealed class BurstChargeDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public float VolumeM3;
        public double FallbackPrice;

        public BurstChargeDefinition(string id, string name, int typeId, float volumeM3, double fallbackPrice)
        {
            Id = id; DisplayName = name; TypeId = typeId; VolumeM3 = volumeM3; FallbackPrice = fallbackPrice;
        }
    }

    [Serializable]
    public sealed class ShipDefinition
    {
        public string Id;
        public string DisplayName;
        public int TypeId;
        public ShipClass Class;
        public int MiningHighSlots;
        public int LowSlots;
        public int MidSlots;
        public bool UsesStripMiners;
        public float MiningHoldM3;
        public float CargoHoldM3;
        public float FuelHoldM3;
        public float ShieldHp;
        public float ShieldRechargeSeconds;
        public float ArmorHp;
        public float StructureHp;
        public float SpeedMps;
        public int DroneBandwidth;
        public int DroneBayM3;
        public int CommandBurstSlots;
        public bool SupportsIndustrialCore;
        public bool IceOnly;
        public bool SupportsIceMiningLasers;
        public float RoleYieldMultiplier = 1f;
        public float RoleCycleMultiplier = 1f;
        public float IceRoleCycleMultiplier = 1f;
        public float RoleCriticalChanceMultiplier = 1f;
        public string BonusSkillId;
        public float YieldBonusPerLevel;
        public float RangeBonusPerLevel;
        public float CriticalChanceBonusPerLevel;
        public float CriticalBonusYieldPerLevel;
        public float CycleReductionPerLevel;
        public float IceCycleReductionPerLevel;
        public string SecondaryBonusSkillId;
        public float SecondaryYieldBonusPerLevel;
        public float SecondaryCycleReductionPerLevel;
        public float SecondaryIceCycleReductionPerLevel;
        public float GasRoleYieldMultiplier = 1f;
        public float GasRoleCycleMultiplier = 1f;
        // Gas cycle bonuses follow the same skill pairing as the other hull
        // bonuses: primary uses BonusSkillId, secondary uses SecondaryBonusSkillId.
        public float GasCycleReductionPerLevel;
        public float SecondaryGasCycleReductionPerLevel;
        public string ShieldResistBonusSkillId;
        public float ShieldResistBonusPerLevel;
        public float MiningHoldBonusPerLevel;
        public float SecondaryMiningHoldBonusPerLevel;
        public float CommandBurstStrengthBonusPerLevel;
        public float CommandBurstRangeBonusPerLevel;
        public float RoleCommandBurstRangeMultiplier = 1f;
        public double FallbackBuyPrice;
        public SkillRequirement[] Requirements;

        public float TotalEhp => ShieldHp + ArmorHp + StructureHp;
        public bool IsCommandShip => CommandBurstSlots > 0;

        public ShipDefinition(string id, string name, int typeId, ShipClass shipClass, int miningSlots, int lowSlots, bool strips, float miningHold, float cargo, float fuel, float shield, float shieldRechargeSeconds, float armor, float structure, float speed, int droneBandwidth, int droneBay, int bursts, bool core, double price, params SkillRequirement[] requirements)
        {
            Id = id; DisplayName = name; TypeId = typeId; Class = shipClass; MiningHighSlots = miningSlots; LowSlots = lowSlots; UsesStripMiners = strips; MiningHoldM3 = miningHold; CargoHoldM3 = cargo; FuelHoldM3 = fuel; ShieldHp = shield; ShieldRechargeSeconds = shieldRechargeSeconds; ArmorHp = armor; StructureHp = structure; SpeedMps = speed; DroneBandwidth = droneBandwidth; DroneBayM3 = droneBay; CommandBurstSlots = bursts; SupportsIndustrialCore = core; FallbackBuyPrice = price; Requirements = requirements ?? Array.Empty<SkillRequirement>();
        }
    }

    [Serializable]
    public sealed class LocationDefinition
    {
        public string Id;
        public string SystemName;
        public int SystemId;
        public string BeltName;
        public long BeltId;
        public float Security;
        public SecurityBand Band;
        public string[] OreIds;
        public float Threat;
        public float SpawnMinSeconds;
        public float SpawnMaxSeconds;
        public int MaxNpcCount;
        public bool IsAnomaly;
        public bool ResourceLayoutApproximate;
        public MiningSiteKind SiteKind;
        public float AnomalyRespawnMinSeconds;
        public float AnomalyRespawnMaxSeconds;
        public float AnomalyInitialSpawnChance;
        public int AuthoredAsteroidCountMin;
        public int AuthoredAsteroidCountMax;
        public string[] AllowedHullIds;

        public string DisplayName => BeltName?.StartsWith(SystemName + " ", StringComparison.OrdinalIgnoreCase) == true
            ? BeltName
            : $"{SystemName} — {BeltName}";

        public LocationDefinition(string id, string system, int systemId, string belt, long beltId, float security, string[] ores, float threat, float spawnMin, float spawnMax, int maxNpc, bool anomaly = false, bool approximateLayout = false, float anomalyRespawnMinSeconds = 300f, float anomalyRespawnMaxSeconds = 600f, float anomalyInitialSpawnChance = 1f, int authoredAsteroidCountMin = 24, int authoredAsteroidCountMax = 24, string[] allowedHullIds = null)
        {
            Id = id; SystemName = system; SystemId = systemId; BeltName = belt; BeltId = beltId; Security = security; Band = Math.Round(security, 1, MidpointRounding.AwayFromZero) >= .5 ? SecurityBand.HighSec : security > 0 ? SecurityBand.LowSec : SecurityBand.NullSec; OreIds = ores; Threat = threat; SpawnMinSeconds = spawnMin; SpawnMaxSeconds = spawnMax; MaxNpcCount = maxNpc; IsAnomaly = anomaly;
            // ESI/SDE provide exact celestial identities, but not a live rock-by-
            // rock amount snapshot. Every authored baseline is therefore labelled.
            ResourceLayoutApproximate = approximateLayout || beltId != 0;
            SiteKind = anomaly ? MiningSiteKind.DynamicAnomaly : MiningSiteKind.StaticBelt;
            AnomalyRespawnMinSeconds = Math.Max(1f, anomalyRespawnMinSeconds);
            AnomalyRespawnMaxSeconds = Math.Max(AnomalyRespawnMinSeconds, anomalyRespawnMaxSeconds);
            AnomalyInitialSpawnChance = Mathf.Clamp01(anomalyInitialSpawnChance);
            AuthoredAsteroidCountMin = Math.Max(1, authoredAsteroidCountMin);
            AuthoredAsteroidCountMax = Math.Max(AuthoredAsteroidCountMin, authoredAsteroidCountMax);
            AllowedHullIds = allowedHullIds ?? Array.Empty<string>();
        }
    }

    public static class Catalog
    {
        const string CompressedItemPrefix = "compressed:";
        public const int FleetCapacity = 10;
        public const float PerfectTrainingSpPerMinute = 45f;
        public const int HeavyWaterTypeId = 16272;
        public const int LargeSkillInjectorTypeId = 40520;
        public const int SmallSkillInjectorTypeId = 45635;
        public const double LargeSkillInjectorSkillPoints = 400_000d;
        public const double SmallSkillInjectorSkillPoints = 80_000d;
        public const double HeavyWaterFallbackPrice = 170d;
        public const double LargeSkillInjectorFallbackPrice = 740_000_000d;
        public const double SmallSkillInjectorFallbackPrice = 148_000_000d;
        // The game assumes the best mining clone is already installed. These
        // constants keep that design choice visible to both simulation and UI.
        public const float PerfectOreLaserYieldImplantMultiplier = 1.1025f; // Michi 5% * Highwall MX-1005 5%
        public const float PerfectIceHarvesterCycleImplantMultiplier = .95f; // Yeti BX-2: 5% shorter cycle
        public const float PerfectMiningForemanMindlinkMultiplier = 1.25f;
        public const float MiningBurstMagazineVolumeM3 = 60f;
        public const float MiningBurstCycleSeconds = 60f;
        public const float MiningBurstBaseEffectDurationSeconds = 60f;

        static SkillRequirement R(string id, int level) => new(id, level);

        public static readonly IReadOnlyList<SkillDefinition> Skills = new List<SkillDefinition>
        {
            new("spaceship-command", "Spaceship Command", 3327, 1, 20_000, "Базовое управление кораблями."),
            new("advanced-spaceship-command", "Advanced Spaceship Command", 20342, 5, 45_000_000, "Управление крупными и капитальными кораблями.", R("spaceship-command", 5)),
            new("capital-ships", "Capital Ships", 20533, 14, 500_000_000, "Базовое управление capital-классом.", R("advanced-spaceship-command", 5)),
            new("science", "Science", 3402, 1, 20_000, "Базовый научный навык."),
            new("mechanics", "Mechanics", 3392, 1, 20_000, "+5% к structure HP за уровень."),
            new("hull-upgrades", "Hull Upgrades", 3394, 2, 60_000, "Позволяет использовать Damage Control.", R("mechanics",1)),
            new("power-grid-management", "Power Grid Management", 3413, 1, 25_000, "+5% к powergrid корабля за уровень; prerequisite щитовых модулей."),
            new("shield-operation", "Shield Operation", 3416, 1, 35_000, "-5% ко времени перезарядки щита за уровень.", R("power-grid-management",1)),
            new("shield-management", "Shield Management", 3419, 3, 180_000, "+5% к shield HP за уровень.", R("power-grid-management",3)),
            new("shield-upgrades", "Shield Upgrades", 3425, 2, 90_000, "Позволяет использовать shield extenders.", R("power-grid-management",2), R("science",1)),
            new("tactical-shield-manipulation", "Tactical Shield Manipulation", 3420, 4, 450_000, "Позволяет использовать shield hardeners.", R("power-grid-management",3)),
            new("electronics-upgrades", "Electronics Upgrades", 3432, 2, 100_000, "Требование для expedition frigates."),
            new("advanced-mass-production", "Advanced Mass Production", 24625, 8, 20_000_000, "Требование для industrial core skills."),
            new("reprocessing", "Reprocessing", 3385, 1, 50_000, "Базовый навык переработки и prerequisite рудных кристаллов.", R("industry",1)),
            new("reprocessing-efficiency", "Reprocessing Efficiency", 3389, 3, 400_000, "Продвинутая переработка и prerequisite сложных кристаллов.", R("reprocessing",4)),
            new("metallurgy", "Metallurgy", 3409, 3, 400_000, "Prerequisite сложных рудных кристаллов.", R("science",4)),
            new("mining", "Mining", 3386, 1, 20_000, "+5% к объёму добычи лазерами за уровень."),
            new("astrogeology", "Astrogeology", 3410, 3, 450_000, "+5% к объёму добычи за уровень.", R("science", 4), R("mining", 4)),
            new("mining-frigate", "Mining Frigate", 32918, 2, 40_000, "Бонусы Venture, Prospect и Endurance.", R("spaceship-command", 1)),
            new("expedition-frigates", "Expedition Frigates", 33856, 4, 30_000_000, "Управление Prospect и Endurance.", R("spaceship-command", 3), R("electronics-upgrades", 5), R("industry", 5)),
            new("mining-destroyer", "Mining Destroyer", 89241, 2, 1_000_000, "Бонусы Pioneer, Perseverance и их вариантов.", R("mining", 4), R("mining-frigate", 3), R("industry", 3)),
            new("command-burst-specialist", "Command Burst Specialist", 3354, 6, 10_000_000, "Улучшает command bursts.", R("leadership", 5)),
            new("command-destroyers", "Command Destroyers", 37615, 6, 60_000_000, "Управление Outrider и одним command burst.", R("spaceship-command", 5), R("command-burst-specialist", 4)),
            new("industry", "Industry", 3380, 1, 20_000, "Базовый промышленный навык."),
            new("ore-hauler", "ORE Hauler", 3184, 4, 2_000_000, "Требование для industrial command ships.", R("spaceship-command", 3)),
            new("mining-barge", "Mining Barge", 17940, 4, 500_000, "Бонусы Retriever, Procurer и Covetor.", R("astrogeology", 3), R("industry", 5), R("mining-frigate", 3)),
            new("exhumers", "Exhumers", 22551, 5, 25_000_000, "Бонусы Mackinaw, Skiff и Hulk.", R("spaceship-command", 4), R("industry", 5), R("astrogeology", 5)),
            new("ice-harvesting", "Ice Harvesting", 16281, 1, 400_000, "-5% к длительности цикла ледовых добывающих модулей за уровень.", R("mining", 4)),
            new("gas-cloud-harvesting", "Gas Cloud Harvesting", 25544, 1, 30_000_000, "Каждый уровень позволяет использовать ещё один Gas Cloud Scoop; также открывает Gas Cloud Harvesters.", R("mining",4)),
            new("mining-upgrades", "Mining Upgrades", 22578, 4, 80_000, "Позволяет ставить Mining Laser Upgrade и улучшает fitting.", R("mining", 3)),
            new("mining-precision", "Mining Precision", 90727, 2, 1_500_000, "+10% к базовому шансу mining critical за уровень.", R("mining", 3)),
            new("mining-exploitation", "Mining Exploitation", 90728, 6, 12_000_000, "+5% к бонусной добыче critical за уровень.", R("mining", 5), R("mining-precision", 4), R("astrogeology", 3)),
            new("drones", "Drones", 3436, 1, 25_000, "+1 одновременно управляемый дрон за уровень."),
            new("light-drone-operation", "Light Drone Operation", 24241, 1, 50_000, "Боевые light drones.", R("drones", 1)),
            new("medium-drone-operation", "Medium Drone Operation", 33699, 2, 100_000, "Боевые medium drones.", R("drones", 3)),
            new("heavy-drone-operation", "Heavy Drone Operation", 3441, 5, 1_000_000, "Боевые heavy drones.", R("drones", 5)),
            new("gallente-drone-specialization", "Gallente Drone Specialization", 12486, 5, 5_000_000, "Нужен для T2 Hobgoblin, Hammerhead и Ogre; +2% к их урону за уровень.", R("drones", 5)),
            new("drone-interfacing", "Drone Interfacing", 3442, 5, 900_000, "+10% к урону и mining yield дронов за уровень.", R("drones", 5)),
            new("mining-drone-operation", "Mining Drone Operation", 3438, 2, 60_000, "+5% к добыче mining drones за уровень.", R("mining", 2), R("drones", 1)),
            new("mining-drone-specialization", "Mining Drone Specialization", 22541, 5, 8_000_000, "Mining Drone II.", R("mining-drone-operation", 5), R("drones", 5)),
            new("leadership", "Leadership", 3348, 1, 100_000, "Базовый навык fleet boosts."),
            new("mining-foreman", "Mining Foreman", 22536, 2, 500_000, "Сила mining foreman bursts.", R("leadership", 1)),
            new("mining-director", "Mining Director", 22552, 5, 2_500_000, "Усиливает mining boosts.", R("leadership", 1), R("mining-foreman", 5)),
            new("industrial-command-ships", "Industrial Command Ships", 29637, 8, 50_000_000, "Porpoise и Orca.", R("spaceship-command", 5), R("ore-hauler", 3), R("mining-director", 1)),
            new("industrial-reconfiguration", "Industrial Reconfiguration", 58956, 4, 12_000_000, "Medium и Large Industrial Core.", R("advanced-mass-production", 1)),
            new("shipboard-compression-technology", "Shipboard Compression Technology", 62450, 3, 150_000_000, "Medium и Large компрессоры ресурсов.", R("industrial-reconfiguration", 1)),
            new("capital-industrial-ships", "Capital Industrial Ships", 28374, 12, 75_000_000, "Rorqual и её бонусы.", R("advanced-spaceship-command", 5), R("industrial-command-ships", 3), R("capital-ships", 2)),
            new("capital-industrial-reconfiguration", "Capital Industrial Reconfiguration", 28585, 8, 25_000_000, "Capital Industrial Core.", R("advanced-mass-production", 4)),
            new("capital-shipboard-compression-technology", "Capital Shipboard Compression Technology", 62451, 3, 150_000_000, "Capital-компрессоры ресурсов.", R("capital-industrial-reconfiguration", 1), R("shipboard-compression-technology", 4))
            ,new("simple-ore-processing", "Simple Ore Processing", 60377, 3, 1_000_000, "Кристаллы Veldspar, Scordite, Pyroxeres и Plagioclase.", R("science",3), R("reprocessing",4))
            ,new("coherent-ore-processing", "Coherent Ore Processing", 60378, 6, 4_000_000, "Кристаллы Omber, Kernite, Jaspet, Hemorphite и Hedbergite.", R("science",3), R("reprocessing",5))
            ,new("variegated-ore-processing", "Variegated Ore Processing", 60379, 9, 9_000_000, "Кристаллы Gneiss, Dark Ochre и Crokite.", R("metallurgy",3), R("reprocessing-efficiency",4))
            ,new("complex-ore-processing", "Complex Ore Processing", 60380, 11, 15_000_000, "Кристаллы Bistot, Arkonor, Spodumain и Mercoxit.", R("metallurgy",4), R("reprocessing-efficiency",5))
        }.AsReadOnly();

        // These definitions only keep old saves, queues and inventory references
        // readable. Mercoxit is an ordinary ore in this project, so neither skill
        // is offered in the active academy catalog or required by new equipment.
        static readonly IReadOnlyList<SkillDefinition> LegacyMercoxitSkills = new List<SkillDefinition>
        {
            new("deep-core-mining", "Deep Core Mining [legacy]", 11395, 6, 500_000, "Устаревший навык: Mercoxit теперь добывается обычными рудными модулями.", R("mining", 5), R("astrogeology", 5)),
            new("mercoxit-ore-processing", "Mercoxit Ore Processing [legacy]", 12189, 5, 6_000_000, "Устаревший навык: Mercoxit использует Complex Ore Processing.", R("metallurgy",4), R("reprocessing-efficiency",5))
        }.AsReadOnly();

        static readonly IReadOnlyList<SkillDefinition> AllSkillDefinitions = Skills.Concat(LegacyMercoxitSkills).ToArray();

        static void AddOreFamily(List<OreDefinition> ores, string id, string name, int baseTypeId, int grade2TypeId, int grade3TypeId, int grade4TypeId, float unitVolumeM3, int units, double buy, double sell, Color color)
        {
            ores.Add(new OreDefinition(id, name, baseTypeId, unitVolumeM3, units, buy, sell, color, ResourceKind.Ore, id, id, 1, 1d));
            ores.Add(new OreDefinition(id + "-ii-grade", name + " II-Grade", grade2TypeId, unitVolumeM3, units, buy, sell, Color.Lerp(color, Color.white, .08f), ResourceKind.Ore, id, id, 2, 1.05d));
            ores.Add(new OreDefinition(id + "-iii-grade", name + " III-Grade", grade3TypeId, unitVolumeM3, units, buy, sell, Color.Lerp(color, Color.white, .16f), ResourceKind.Ore, id, id, 3, 1.10d));
            ores.Add(new OreDefinition(id + "-iv-grade", name + " IV-Grade", grade4TypeId, unitVolumeM3, units, buy, sell, Color.Lerp(color, Color.white, .24f), ResourceKind.Ore, id, id, 4, 1.15d));
        }

        static void AddMercoxitFamily(List<OreDefinition> ores)
        {
            const string id = "mercoxit";
            const string name = "Mercoxit";
            var color = new Color(.20f, .82f, .64f);
            ores.Add(new OreDefinition(id, name, 11396, 40f, 2_500, 6900, 8200, color, ResourceKind.Ore, id, id, 1, 1d));
            ores.Add(new OreDefinition(id + "-ii-grade", name + " II-Grade", 17869, 40f, 2_500, 6900, 8200, Color.Lerp(color, Color.white, .08f), ResourceKind.Ore, id, id, 2, 1.05d));
            ores.Add(new OreDefinition(id + "-iii-grade", name + " III-Grade", 17870, 40f, 2_500, 6900, 8200, Color.Lerp(color, Color.white, .16f), ResourceKind.Ore, id, id, 3, 1.10d));
        }

        static IReadOnlyList<OreDefinition> BuildOres()
        {
            var ores = new List<OreDefinition>();
            AddOreFamily(ores, "veldspar", "Veldspar", 1230, 17470, 17471, 46689, .1f, 120_000, 14, 17, new Color(.56f, .43f, .34f));
            AddOreFamily(ores, "scordite", "Scordite", 1228, 17463, 17464, 46687, .15f, 90_000, 18, 22, new Color(.48f, .60f, .64f));
            AddOreFamily(ores, "pyroxeres", "Pyroxeres", 1224, 17459, 17460, 46686, .3f, 55_000, 27, 34, new Color(.40f, .62f, .43f));
            AddOreFamily(ores, "plagioclase", "Plagioclase", 18, 17455, 17456, 46685, .35f, 48_000, 31, 38, new Color(.64f, .53f, .73f));
            AddOreFamily(ores, "omber", "Omber", 1227, 17867, 17868, 46684, .6f, 32_000, 60, 75, new Color(.70f, .55f, .34f));
            AddOreFamily(ores, "kernite", "Kernite", 20, 17452, 17453, 46683, 1.2f, 22_000, 145, 175, new Color(.56f, .32f, .25f));
            AddOreFamily(ores, "jaspet", "Jaspet", 1226, 17448, 17449, 46682, 2f, 16_000, 225, 270, new Color(.45f, .39f, .34f));
            AddOreFamily(ores, "hemorphite", "Hemorphite", 1231, 17444, 17445, 46681, 3f, 12_000, 510, 620, new Color(.72f, .28f, .24f));
            AddOreFamily(ores, "hedbergite", "Hedbergite", 21, 17440, 17441, 46680, 3f, 12_000, 565, 690, new Color(.45f, .22f, .21f));
            AddOreFamily(ores, "gneiss", "Gneiss", 1229, 17865, 17866, 46679, 5f, 9_000, 1010, 1220, new Color(.63f, .67f, .48f));
            AddOreFamily(ores, "dark-ochre", "Dark Ochre", 1232, 17436, 17437, 46675, 8f, 7_000, 1180, 1425, new Color(.36f, .26f, .22f));
            AddOreFamily(ores, "crokite", "Crokite", 1225, 17432, 17433, 46677, 16f, 5_000, 2020, 2440, new Color(.48f, .58f, .36f));
            AddOreFamily(ores, "bistot", "Bistot", 1223, 17428, 17429, 46676, 16f, 5_000, 2380, 2860, new Color(.31f, .46f, .58f));
            AddOreFamily(ores, "arkonor", "Arkonor", 22, 17425, 17426, 46678, 16f, 5_000, 2875, 3450, new Color(.73f, .55f, .33f));
            AddOreFamily(ores, "spodumain", "Spodumain", 19, 17466, 17467, 46688, 16f, 7_000, 980, 1190, new Color(.49f, .36f, .55f));
            AddMercoxitFamily(ores);

            // Reserved rookie-system variants. They intentionally are not part
            // of any normal location pool and are only selected by explicit
            // authored newcomer-site rules.
            ores.Add(new OreDefinition("veldspar-0-grade", "Veldspar 0-Grade", 92371, .1f, 120_000, 14, 17, new Color(.56f, .43f, .34f), ResourceKind.Ore, "veldspar", "veldspar", 0, .5d));
            ores.Add(new OreDefinition("scordite-0-grade", "Scordite 0-Grade", 92373, .15f, 90_000, 18, 22, new Color(.48f, .60f, .64f), ResourceKind.Ore, "scordite", "scordite", 0, .5d));
            ores.Add(new OreDefinition("pyroxeres-0-grade", "Pyroxeres 0-Grade", 95395, .3f, 55_000, 27, 34, new Color(.40f, .62f, .43f), ResourceKind.Ore, "pyroxeres", "pyroxeres", 0, .5d));

            ores.Add(new OreDefinition("clear-icicle", "Clear Icicle", 16262, 1000f, 100, 186_000, 370_000, new Color(.58f, .86f, 1f), ResourceKind.Ice));
            ores.Add(new OreDefinition("fullerite-c50", "Fullerite-C50", 30370, 1f, 12_000, 4504, 4705, new Color(.55f, .94f, .74f), ResourceKind.Gas));
            ores.Add(new OreDefinition("fullerite-c60", "Fullerite-C60", 30371, 1f, 6_000, 4512, 4885, new Color(.52f, .75f, 1f), ResourceKind.Gas));
            return ores.AsReadOnly();
        }

        public static readonly IReadOnlyList<OreDefinition> Ores = BuildOres();

        public static readonly IReadOnlyList<MiningModuleDefinition> Modules = new List<MiningModuleDefinition>
        {
            new("miner-i", "Miner I", 483, ModuleKind.MiningLaser, 1, 10, 15, 10, 0, 0, 35_000, R("mining", 1)) { CanMineMercoxit = true },
            new("miner-ii", "Miner II", 482, ModuleKind.MiningLaser, 2, 15, 15, 12, .34f, 1, 750_000, R("mining", 4)) { CanMineMercoxit = true },
            new("ore-miner", "ORE Miner", 28750, ModuleKind.MiningLaser, 1, 21, 15, 16, 0, 0, 65_000_000, R("mining", 1)) { CanMineMercoxit = true },
            // Legacy deep-core IDs stay resolvable for old ships and inventories,
            // but they now use the same ordinary ore skills and compatibility.
            new("deep-core-mining-laser-i", "Deep Core Mining Laser I [legacy]", 12108, ModuleKind.MiningLaser, 1, 40, 60, 5, 0, 0, 300_000, R("mining", 1)) { CanMineMercoxit = true },
            new("ore-deep-core-mining-laser", "ORE Deep Core Mining Laser [legacy]", 28748, ModuleKind.MiningLaser, 1, 40, 60, 7, 0, 0, 110_000_000, R("mining", 1)) { CanMineMercoxit = true },
            new("strip-miner-i", "Strip Miner I", 17482, ModuleKind.StripMiner, 1, 150, 45, 15, 0, 0, 1_500_000, R("mining", 4), R("astrogeology", 1)) { CanMineMercoxit = true },
            new("modulated-strip-miner-ii", "Modulated Strip Miner II", 17912, ModuleKind.StripMiner, 2, 120, 45, 15, .34f, 1, 4_500_000, R("mining", 5)) { CanMineMercoxit = true, AcceptsRegularCrystals = true, AcceptsMercoxitCrystals = true },
            new("ore-strip-miner", "ORE Strip Miner", 28754, ModuleKind.StripMiner, 1, 200, 45, 18.75f, 0, 0, 190_000_000, R("mining", 4), R("astrogeology", 1)) { CanMineMercoxit = true },
            new("modulated-deep-core-miner-ii", "Modulated Deep Core Miner II [legacy]", 18068, ModuleKind.MiningLaser, 2, 30, 45, 10, .34f, 1, 1_847_000, R("mining", 5)) { CanMineMercoxit = true, AcceptsRegularCrystals = true, AcceptsMercoxitCrystals = true },
            new("modulated-deep-core-strip-miner-ii", "Modulated Deep Core Strip Miner II [legacy]", 24305, ModuleKind.StripMiner, 2, 80, 45, 15, .34f, 1, 5_000_000, R("mining", 5)) { CanMineMercoxit = true, AcceptsRegularCrystals = true, AcceptsMercoxitCrystals = true },
            new("ice-mining-laser-i", "Ice Mining Laser I", 37450, ModuleKind.IceMiningLaser, 1, 1000, 360, 7, 0, 0, 597_900, R("ice-harvesting", 1)),
            new("ice-mining-laser-ii", "Ice Mining Laser II", 37451, ModuleKind.IceMiningLaser, 2, 1000, 300, 8, .34f, 1, 1_340_000, R("ice-harvesting", 5)),
            new("ore-ice-mining-laser", "ORE Ice Mining Laser", 37452, ModuleKind.IceMiningLaser, 1, 1000, 300, 11, 0, 0, 83_500_000, R("ice-harvesting", 1)),
            new("ice-harvester-i", "Ice Harvester I", 16278, ModuleKind.IceHarvester, 1, 1000, 240, 10, 0, 0, 3_194_000, R("ice-harvesting", 1)),
            new("ice-harvester-ii", "Ice Harvester II", 22229, ModuleKind.IceHarvester, 2, 1000, 200, 10, .34f, 1, 5_462_000, R("ice-harvesting", 5)),
            new("gas-cloud-scoop-i","Gas Cloud Scoop I",25266,ModuleKind.GasCloudScoop,1,10,30,1.5f,0,0,3_059_000,R("gas-cloud-harvesting",1)),
            new("gas-cloud-scoop-ii","Gas Cloud Scoop II",25812,ModuleKind.GasCloudScoop,2,20,40,1.5f,.34f,1,4_992_000,R("gas-cloud-harvesting",5)),
            new("syndicate-gas-cloud-scoop","Syndicate Gas Cloud Scoop",28788,ModuleKind.GasCloudScoop,1,20,30,1.5f,0,0,116_700_000,R("gas-cloud-harvesting",1)),
            new("gas-cloud-harvester-i","Gas Cloud Harvester I",60313,ModuleKind.GasCloudHarvester,1,50,100,1.5f,0,0,3_275_000,R("gas-cloud-harvesting",1)),
            new("gas-cloud-harvester-ii","Gas Cloud Harvester II",60314,ModuleKind.GasCloudHarvester,2,100,80,1.5f,.34f,1,5_480_000,R("gas-cloud-harvesting",5)),
            new("ore-gas-cloud-harvester","ORE Gas Cloud Harvester",60315,ModuleKind.GasCloudHarvester,1,100,80,1.5f,0,0,182_300_000,R("gas-cloud-harvesting",5)),
            new("mining-laser-upgrade-i", "Mining Laser Upgrade I", 22542, ModuleKind.MiningUpgrade, 1, 0, 0, 0, 0, 0, 90_000, R("mining-upgrades", 1)),
            new("mining-laser-upgrade-ii", "Mining Laser Upgrade II", 28576, ModuleKind.MiningUpgrade, 2, 0, 0, 0, 0, 0, 1_800_000, R("mining-upgrades", 4)),
            new("ice-harvester-upgrade-i","Ice Harvester Upgrade I",22576,ModuleKind.IceHarvesterUpgrade,1,0,0,0,0,0,39_720,R("mining-upgrades",1)),
            new("ice-harvester-upgrade-ii","Ice Harvester Upgrade II",28578,ModuleKind.IceHarvesterUpgrade,2,0,0,0,0,0,585_200,R("mining-upgrades",4)),
            new("mining-burst-i", "Mining Foreman Burst I", 42528, ModuleKind.MiningBurst, 1, 0, 60, 15, 0, 0, 900_000, R("leadership", 1), R("mining-foreman", 1)) { VolumeM3 = 60 },
            new("mining-burst", "Mining Foreman Burst II", 43551, ModuleKind.MiningBurst, 2, 0, 60, 15, 0, 0, 3_500_000, R("leadership", 1), R("mining-foreman", 1), R("mining-director", 1)) { VolumeM3 = 60 },
            new("medium-industrial-core-i", "Medium Industrial Core I", 62590, ModuleKind.IndustrialCore, 1, 0, 75, 0, 0, 0, 30_000_000, R("industrial-reconfiguration", 1)) { FuelPerCycle = 125, VolumeM3 = 3500 },
            new("medium-industrial-core-ii", "Medium Industrial Core II", 62591, ModuleKind.IndustrialCore, 2, 0, 75, 0, 0, 0, 95_000_000, R("industrial-reconfiguration", 4)) { FuelPerCycle = 250, VolumeM3 = 3500 },
            new("industrial-core-i", "Large Industrial Core I", 58945, ModuleKind.IndustrialCore, 1, 0, 150, 0, 0, 0, 90_000_000, R("industrial-reconfiguration", 1)) { FuelPerCycle = 250, VolumeM3 = 3500 },
            new("industrial-core-ii", "Large Industrial Core II", 58950, ModuleKind.IndustrialCore, 2, 0, 150, 0, 0, 0, 260_000_000, R("industrial-reconfiguration", 5)) { FuelPerCycle = 500, VolumeM3 = 3500 },
            new("capital-industrial-core-i", "Capital Industrial Core I", 28583, ModuleKind.IndustrialCore, 1, 0, 300, 0, 0, 0, 450_000_000, R("capital-industrial-reconfiguration", 1)) { FuelPerCycle = 1000, VolumeM3 = 1000 },
            new("capital-industrial-core-ii", "Capital Industrial Core II", 42890, ModuleKind.IndustrialCore, 2, 0, 300, 0, 0, 0, 1_200_000_000, R("capital-industrial-reconfiguration", 5)) { FuelPerCycle = 1500, VolumeM3 = 1000 },
            new("medium-asteroid-ore-compressor-i", "Medium Asteroid Ore Compressor I", 62622, ModuleKind.Compressor, 1, 0, 60, 44, 0, 0, 6_000_000, R("shipboard-compression-technology", 1)) { VolumeM3 = 500, CompressionKind = CompressionKind.Ore },
            new("medium-gas-compressor-i", "Medium Gas Compressor I", 62624, ModuleKind.Compressor, 1, 0, 60, 44, 0, 0, 30_000_000, R("shipboard-compression-technology", 3)) { VolumeM3 = 500, CompressionKind = CompressionKind.Gas },
            new("large-asteroid-ore-compressor-i", "Large Asteroid Ore Compressor I", 62625, ModuleKind.Compressor, 1, 0, 60, 83, 0, 0, 12_000_000, R("shipboard-compression-technology", 1)) { VolumeM3 = 1000, CompressionKind = CompressionKind.Ore },
            new("large-ice-compressor-i", "Large Ice Compressor I", 62628, ModuleKind.Compressor, 1, 0, 60, 83, 0, 0, 30_000_000, R("shipboard-compression-technology", 2)) { VolumeM3 = 1000, CompressionKind = CompressionKind.Ice },
            new("large-gas-compressor-i", "Large Gas Compressor I", 62626, ModuleKind.Compressor, 1, 0, 60, 83, 0, 0, 60_000_000, R("shipboard-compression-technology", 3)) { VolumeM3 = 1000, CompressionKind = CompressionKind.Gas },
            new("large-mercoxit-compressor-i", "Large Mercoxit Compressor I [legacy]", 62630, ModuleKind.Compressor, 1, 0, 60, 83, 0, 0, 45_000_000, R("shipboard-compression-technology", 1)) { VolumeM3 = 1000, CompressionKind = CompressionKind.Ore },
            new("capital-asteroid-ore-compressor-i", "Capital Asteroid Ore Compressor I", 62632, ModuleKind.Compressor, 1, 0, 60, 144, 0, 0, 24_340_000, R("capital-shipboard-compression-technology", 1)) { VolumeM3 = 2000, CompressionKind = CompressionKind.Ore },
            new("capital-ice-compressor-i", "Capital Ice Compressor I", 62633, ModuleKind.Compressor, 1, 0, 60, 144, 0, 0, 60_000_000, R("capital-shipboard-compression-technology", 2)) { VolumeM3 = 2000, CompressionKind = CompressionKind.Ice },
            new("capital-gas-compressor-i", "Capital Gas Compressor I", 62634, ModuleKind.Compressor, 1, 0, 60, 144, 0, 0, 121_000_000, R("capital-shipboard-compression-technology", 3)) { VolumeM3 = 2000, CompressionKind = CompressionKind.Gas },
            new("capital-mercoxit-compressor-i", "Capital Mercoxit Compressor I [legacy]", 62635, ModuleKind.Compressor, 1, 0, 60, 144, 0, 0, 91_280_000, R("capital-shipboard-compression-technology", 1)) { VolumeM3 = 2000, CompressionKind = CompressionKind.Ore }
        }.AsReadOnly();

        public static readonly IReadOnlyList<DroneDefinition> Drones = new List<DroneDefinition>
        {
            new("hobgoblin-i", "Hobgoblin I", 2454, false, 8, 0, 5, 5, 45_000, R("drones", 1), R("light-drone-operation", 1)),
            new("hobgoblin-ii", "Hobgoblin II", 2456, false, 12, 0, 5, 5, 900_000, R("drones", 5), R("light-drone-operation", 5), R("gallente-drone-specialization", 1)),
            new("hammerhead-i", "Hammerhead I", 2183, false, 16, 0, 10, 10, 60_000, R("medium-drone-operation", 1)),
            new("hammerhead-ii", "Hammerhead II", 2185, false, 24, 0, 10, 10, 1_500_000, R("medium-drone-operation", 5), R("gallente-drone-specialization", 1)),
            new("ogre-i", "Ogre I", 2444, false, 28, 0, 25, 25, 250_000, R("heavy-drone-operation", 1)),
            new("ogre-ii", "Ogre II", 2446, false, 42, 0, 25, 25, 3_500_000, R("heavy-drone-operation", 5), R("gallente-drone-specialization", 1)),
            new("mining-drone-i", "Mining Drone I", 10246, true, 0, 20, 5, 5, 50_000, R("mining-drone-operation", 1)),
            new("mining-drone-ii", "Mining Drone II", 10250, true, 0, 33, 5, 5, 850_000, R("mining-drone-specialization", 1)),
            new("excavator", "Excavator Mining Drone", 41030, true, 0, 220, 25, 1100, 900_000_000, R("mining-drone-specialization", 4), R("capital-industrial-ships", 1))
        }.AsReadOnly();

        public static readonly IReadOnlyList<ShipDefinition> Ships = BuildShips();
        public static readonly IReadOnlyList<TankPresetDefinition> TankPresets = BuildTankPresets();

        static IReadOnlyList<TankPresetDefinition> BuildTankPresets()
        {
            var result=new List<TankPresetDefinition>();var penalties=new[]{1f,.86912f,.57058f,.28296f};
            foreach(var hull in Ships)
            foreach(var grade in new[]{PreparedPackageGrade.T1,PreparedPackageGrade.T2})
            {
                var t2=grade==PreparedPackageGrade.T2;var mids=Math.Max(0,hull.MidSlots);
                var extenders=mids<=2?1:mids<=5?2:3;var hardeners=Math.Max(0,mids-extenders);
                int extenderType;double extenderPrice;float extenderHp;
                if(hull.Class==ShipClass.CapitalIndustrial){extenderType=t2?40357:40354;extenderPrice=t2?80_000_000d:35_000_000d;extenderHp=t2?72000:57600;}
                else if(hull.Id=="orca"){extenderType=t2?3841:3839;extenderPrice=t2?5_500_000d:2_100_000d;extenderHp=t2?2600:1900;}
                else{extenderType=t2?3831:3829;extenderPrice=t2?2_400_000d:900_000d;extenderHp=t2?1100:800;}
                var hardenerType=t2?2281:578;var hardenerPrice=t2?2_200_000d:220_000d;var bonus=t2?.325f:.25f;var multiplier=1f;
                for(var i=0;i<hardeners;i++)multiplier*=1f-bonus*penalties[Math.Min(i,penalties.Length-1)];
                var ids=Enumerable.Repeat(extenderType,extenders).Concat(Enumerable.Repeat(hardenerType,hardeners)).ToArray();
                var prices=Enumerable.Repeat(extenderPrice,extenders).Concat(Enumerable.Repeat(hardenerPrice,hardeners)).ToArray();
                var requirements=t2?new[]{R("shield-upgrades",4),R("tactical-shield-manipulation",4),R("shield-management",5)}:new[]{R("shield-upgrades",1),R("tactical-shield-manipulation",1),R("shield-management",3)};
                result.Add(new TankPresetDefinition($"{hull.Id}-shield-{(t2?"ii":"i")}",$"{hull.DisplayName} Shield {(t2?"II":"I")}",ids,prices,extenderHp*extenders,multiplier,requirements));
            }
            return result.AsReadOnly();
        }

        public static readonly IReadOnlyList<BurstChargeDefinition> BurstCharges = new List<BurstChargeDefinition>
        {
            new("mining-laser-optimization-charge", "Mining Laser Optimization Charge", 42830, .01f, 450),
            new("mining-laser-field-enhancement-charge", "Mining Laser Field Enhancement Charge", 42829, .01f, 450),
            new("mining-laser-efficiency-charge", "Mining Laser Efficiency Charge", 90733, .01f, 1_500),
            // Retained only so old inventory/save IDs remain readable. Prepared
            // packages never install this crystal-preservation profile.
            new("mining-equipment-preservation-charge", "Mining Equipment Preservation Charge", 42831, .01f, 450)
        }.AsReadOnly();

        public static readonly IReadOnlyList<string> PreparedBurstProfileIds = Array.AsReadOnly(new[]
        {
            "mining-laser-optimization-charge",
            "mining-laser-field-enhancement-charge",
            "mining-laser-efficiency-charge"
        });

        public static readonly IReadOnlyList<MiningCrystalDefinition> Crystals = BuildCrystals();

        static IReadOnlyList<MiningCrystalDefinition> BuildCrystals()
        {
            var result=new List<MiningCrystalDefinition>();
            void Family(string stem,string slug,string skill,string[] ores,int[] ids)
            {
                var variants=new[]{
                    ("a-i", "Type A I",1.5f,1f,0f,0f,.025f,3,150_000d),
                    ("a-ii","Type A II",1.8f,1f,.036f,0f,.03f,4,900_000d),
                    ("b-i", "Type B I",1.5f,.9f,.20f,0f,.0375f,3,180_000d),
                    ("b-ii","Type B II",1.8f,.8f,.30f,0f,.05f,4,1_100_000d),
                    ("c-i", "Type C I",.25f,1f,.40f,18f,.06f,3,120_000d),
                    ("c-ii","Type C II",.20f,1f,.59f,28f,.075f,4,750_000d)};
                for(var i=0;i<variants.Length;i++)
                {
                    var v=variants[i];var volatility=(i==2||i==4)&&slug=="simple"?.025f:v.Item7;
                    var crystal = new MiningCrystalDefinition($"{slug}-{v.Item1}",$"{stem} {v.Item2}",ids[i],ores,v.Item3,v.Item4,v.Item5,v.Item6,volatility,.05f,v.Item9,R("mining",1),R(skill,v.Item8));
                    crystal.VolumeM3 = i % 2 == 0 ? 6f : 10f;
                    result.Add(crystal);
                }
            }
            Family("Simple Asteroid Mining Crystal","simple","simple-ore-processing",new[]{"veldspar","scordite","pyroxeres","plagioclase"},new[]{60276,60281,60279,60283,60280,60284});
            Family("Coherent Asteroid Mining Crystal","coherent","coherent-ore-processing",new[]{"omber","kernite","jaspet","hemorphite","hedbergite"},new[]{60285,60288,60286,60289,60287,60290});
            Family("Variegated Asteroid Mining Crystal","variegated","variegated-ore-processing",new[]{"gneiss","dark-ochre","crokite"},new[]{60291,60294,60292,60295,60293,60296});
            Family("Complex Asteroid Mining Crystal","complex","complex-ore-processing",new[]{"bistot","arkonor","spodumain","mercoxit"},new[]{60297,60300,60298,60301,60299,60302});
            // Legacy crystal IDs remain usable, but share the ordinary complex
            // ore skill instead of requiring a Mercoxit-only processing skill.
            Family("Mercoxit Asteroid Mining Crystal [legacy]","mercoxit-crystal","complex-ore-processing",new[]{"mercoxit"},new[]{18054,18608,60309,60311,60310,60312});
            return result.AsReadOnly();
        }

        static IReadOnlyList<ShipDefinition> BuildShips()
        {
            var ships = new List<ShipDefinition>();
            ShipDefinition S(string id, string name, int typeId, ShipClass c, int slots, int lowSlots, bool strips, float mining, float cargo, float fuel, float shield, float shieldRechargeSeconds, float armor, float structure, float speed, int bandwidth, int bay, int bursts, bool core, double price, params SkillRequirement[] req)
            { var s = new ShipDefinition(id,name,typeId,c,slots,lowSlots,strips,mining,cargo,fuel,shield,shieldRechargeSeconds,armor,structure,speed,bandwidth,bay,bursts,core,price,req); ships.Add(s); return s; }

            var venture = S("venture", "Venture", 32880, ShipClass.MiningFrigate, 2, 1, false, 5000, 50, 0, 225, 625, 175, 200, 335, 10, 10, 0, false, 337_100, R("mining-frigate",1));
            venture.RoleYieldMultiplier=2f; venture.BonusSkillId="mining-frigate"; venture.YieldBonusPerLevel=.05f;
            venture.GasRoleYieldMultiplier=2f;venture.GasCycleReductionPerLevel=.05f;
            var ventureCi = S("venture-consortium", "Venture Consortium Issue", 89648, ShipClass.MiningFrigate, 2, 1, false, 6250, 150, 0, 450, 625, 350, 400, 350, 10, 40, 0, false, 28_000_000, R("mining-frigate",3));
            ventureCi.RoleYieldMultiplier=2f; ventureCi.RoleCriticalChanceMultiplier=1.5f; ventureCi.BonusSkillId="mining-frigate"; ventureCi.YieldBonusPerLevel=.05f;
            ventureCi.GasRoleYieldMultiplier=2f;ventureCi.GasCycleReductionPerLevel=.05f;
            var prospect = S("prospect", "Prospect", 33697, ShipClass.MiningFrigate, 2, 4, false, 12500, 150, 0, 800, 625, 600, 600, 380, 0, 0, 0, false, 30_000_000, R("mining-frigate",5), R("expedition-frigates",1));
            prospect.RoleYieldMultiplier=2f; prospect.SupportsIceMiningLasers=true; prospect.BonusSkillId="mining-frigate"; prospect.YieldBonusPerLevel=.05f; prospect.SecondaryBonusSkillId="expedition-frigates"; prospect.SecondaryYieldBonusPerLevel=.05f;
            prospect.GasRoleYieldMultiplier=2f;prospect.GasCycleReductionPerLevel=.05f;
            var endurance = S("endurance", "Endurance", 37135, ShipClass.MiningFrigate, 1, 3, false, 19000, 200, 0, 1100, 625, 400, 500, 420, 15, 30, 0, false, 35_000_000, R("mining-frigate",5), R("expedition-frigates",1));
            endurance.RoleYieldMultiplier=4f; endurance.SupportsIceMiningLasers=true; endurance.IceRoleCycleMultiplier=.5f; endurance.BonusSkillId="mining-frigate"; endurance.YieldBonusPerLevel=.05f; endurance.IceCycleReductionPerLevel=.05f; endurance.SecondaryBonusSkillId="expedition-frigates"; endurance.SecondaryIceCycleReductionPerLevel=.05f;
            var pioneer = S("pioneer", "Pioneer", 89240, ShipClass.MiningDestroyer, 3, 2, false, 8000, 250, 0, 1000, 625, 500, 1500, 200, 20, 40, 0, false, 8_000_000, R("mining-destroyer",1));
            pioneer.RoleYieldMultiplier=1.5f; pioneer.BonusSkillId="mining-destroyer"; pioneer.YieldBonusPerLevel=.10f; pioneer.RangeBonusPerLevel=.20f;
            pioneer.GasRoleCycleMultiplier=.75f;pioneer.GasCycleReductionPerLevel=.05f;
            var pioneerCi = S("pioneer-consortium", "Pioneer Consortium Issue", 89647, ShipClass.MiningDestroyer, 3, 2, false, 10000, 250, 0, 1500, 625, 750, 2250, 210, 20, 80, 0, false, 75_000_000, R("mining-destroyer",2));
            pioneerCi.RoleYieldMultiplier=1.5f; pioneerCi.RoleCriticalChanceMultiplier=1.5f; pioneerCi.BonusSkillId="mining-destroyer"; pioneerCi.YieldBonusPerLevel=.10f; pioneerCi.RangeBonusPerLevel=.20f;
            pioneerCi.GasRoleCycleMultiplier=.75f;pioneerCi.GasCycleReductionPerLevel=.05f;
            var perseverance = S("perseverance", "Perseverance", 91174, ShipClass.MiningDestroyer, 3, 2, false, 21000, 250, 0, 1500, 625, 750, 2250, 210, 20, 60, 0, false, 342_000_000, R("mining-destroyer",2), R("ice-harvesting",1));
            perseverance.IceOnly=true; perseverance.SupportsIceMiningLasers=true; perseverance.RoleCriticalChanceMultiplier=2f; perseverance.BonusSkillId="mining-destroyer"; perseverance.CriticalChanceBonusPerLevel=.10f; perseverance.CriticalBonusYieldPerLevel=.05f; perseverance.RangeBonusPerLevel=.20f;
            var outrider = S("outrider", "Outrider", 89649, ShipClass.MiningDestroyer, 3, 3, false, 20000, 300, 0, 1000, 625, 500, 1500, 300, 25, 100, 1, false, 180_000_000, R("mining-destroyer",5),R("command-destroyers",1));
            outrider.BonusSkillId="mining-destroyer"; outrider.YieldBonusPerLevel=.15f;
            outrider.SecondaryBonusSkillId="command-destroyers"; outrider.CommandBurstStrengthBonusPerLevel=.02f; outrider.CommandBurstRangeBonusPerLevel=.05f;
            var retriever = S("retriever", "Retriever", 17478, ShipClass.MiningBarge, 2, 3, true, 27500, 450, 0, 4000, 1500, 3000, 4000, 125, 50, 50, 0, false, 76_000_000, R("mining-barge",1), R("astrogeology",3));
            var procurer = S("procurer", "Procurer", 17480, ShipClass.MiningBarge, 2, 3, true, 16000, 350, 0, 6000, 2500, 5000, 6000, 100, 50, 100, 0, false, 62_000_000, R("mining-barge",1), R("astrogeology",3));
            var covetor = S("covetor", "Covetor", 17476, ShipClass.MiningBarge, 2, 3, true, 9000, 350, 0, 3000, 1000, 2000, 3000, 150, 50, 50, 0, false, 69_000_000, R("mining-barge",1), R("astrogeology",3));
            foreach(var s in new[]{retriever,procurer,covetor}) { s.BonusSkillId="mining-barge"; s.YieldBonusPerLevel=.03f; }
            retriever.MiningHoldBonusPerLevel=.05f; retriever.IceRoleCycleMultiplier=.875f; retriever.IceCycleReductionPerLevel=.02f; retriever.GasRoleCycleMultiplier=.875f; retriever.GasCycleReductionPerLevel=.02f;
            procurer.IceCycleReductionPerLevel=.02f; procurer.GasCycleReductionPerLevel=.02f;
            covetor.IceRoleCycleMultiplier=.70f; covetor.IceCycleReductionPerLevel=.03f; covetor.GasRoleCycleMultiplier=.70f; covetor.GasCycleReductionPerLevel=.03f; covetor.RangeBonusPerLevel=.06f;
            var mackinaw = S("mackinaw", "Mackinaw", 22548, ShipClass.Exhumer, 2, 3, true, 31500, 450, 0, 5500, 1500, 5000, 5500, 130, 50, 50, 0, false, 385_000_000, R("exhumers",1), R("mining-barge",5));
            var skiff = S("skiff", "Skiff", 22546, ShipClass.Exhumer, 2, 3, true, 18500, 350, 0, 6500, 2500, 6000, 6500, 110, 50, 100, 0, false, 375_000_000, R("exhumers",1), R("mining-barge",5));
            var hulk = S("hulk", "Hulk", 22544, ShipClass.Exhumer, 2, 3, true, 11500, 350, 0, 4500, 1000, 3000, 4500, 160, 50, 50, 0, false, 400_000_000, R("exhumers",1), R("mining-barge",5));
            foreach(var s in new[]{mackinaw,skiff,hulk}) { s.RoleCycleMultiplier=.85f; s.BonusSkillId="mining-barge"; s.YieldBonusPerLevel=.03f; s.SecondaryBonusSkillId="exhumers"; s.SecondaryYieldBonusPerLevel=.06f; s.SecondaryCycleReductionPerLevel=.03f; }
            mackinaw.MiningHoldBonusPerLevel=.05f; mackinaw.SecondaryMiningHoldBonusPerLevel=.025f; mackinaw.IceRoleCycleMultiplier=.875f; mackinaw.IceCycleReductionPerLevel=.04f; mackinaw.GasRoleCycleMultiplier=.875f; mackinaw.GasCycleReductionPerLevel=.03f; mackinaw.SecondaryGasCycleReductionPerLevel=.03f;
            skiff.IceCycleReductionPerLevel=.04f; skiff.SecondaryGasCycleReductionPerLevel=.03f;
            hulk.IceRoleCycleMultiplier=.70f; hulk.IceCycleReductionPerLevel=.03f; hulk.SecondaryIceCycleReductionPerLevel=.04f; hulk.GasRoleCycleMultiplier=.70f; hulk.GasCycleReductionPerLevel=.03f; hulk.SecondaryGasCycleReductionPerLevel=.03f; hulk.RangeBonusPerLevel=.06f;
            var porpoise = S("porpoise", "Porpoise", 42244, ShipClass.IndustrialCommand, 0, 2, false, 50000, 500, 4800, 6000, 1800, 3000, 8000, 100, 50, 125, 2, true, 125_000_000, R("industrial-command-ships",1));
            porpoise.BonusSkillId="industrial-command-ships"; porpoise.YieldBonusPerLevel=.10f;
            porpoise.MiningHoldBonusPerLevel=.05f;
            porpoise.CommandBurstStrengthBonusPerLevel=.02f; porpoise.CommandBurstRangeBonusPerLevel=.05f;
            var orca = S("orca", "Orca", 28606, ShipClass.IndustrialCommand, 0, 2, false, 150000, 30000, 6400, 30000, 6000, 7000, 45000, 60, 50, 200, 3, true, 2_550_000_000, R("industrial-command-ships",1));
            orca.BonusSkillId="industrial-command-ships"; orca.YieldBonusPerLevel=.10f;
            orca.MiningHoldBonusPerLevel=.05f;
            orca.CommandBurstStrengthBonusPerLevel=.03f; orca.CommandBurstRangeBonusPerLevel=.05f;
            // The hull retains four physical command slots. Ready-made fits use
            // only the three project profiles; the fourth slot stays empty.
            var rorqual = S("rorqual", "Rorqual", 28352, ShipClass.CapitalIndustrial, 0, 4, false, 300000, 40000, 10000, 90000, 14400, 60000, 300000, 60, 125, 8800, 4, true, 6_900_000_000, R("capital-industrial-ships",1), R("capital-ships",2));
            rorqual.BonusSkillId="capital-industrial-ships"; rorqual.YieldBonusPerLevel=.10f;
            rorqual.CommandBurstStrengthBonusPerLevel=.05f; rorqual.CommandBurstRangeBonusPerLevel=.05f; rorqual.RoleCommandBurstRangeMultiplier=1.5f;
            var mids=new Dictionary<string,int>{{"venture",3},{"venture-consortium",4},{"prospect",3},{"endurance",4},{"pioneer",3},{"pioneer-consortium",4},{"perseverance",4},{"outrider",5},{"retriever",2},{"procurer",3},{"covetor",2},{"mackinaw",4},{"skiff",5},{"hulk",4},{"porpoise",4},{"orca",5},{"rorqual",7}};
            foreach(var ship in ships)ship.MidSlots=mids.TryGetValue(ship.Id,out var count)?count:0;
            endurance.ShieldResistBonusSkillId="expedition-frigates";endurance.ShieldResistBonusPerLevel=.04f;
            outrider.ShieldResistBonusSkillId="mining-destroyer";outrider.ShieldResistBonusPerLevel=.06f;
            foreach(var ship in new[]{mackinaw,skiff,hulk}){ship.ShieldResistBonusSkillId="exhumers";ship.ShieldResistBonusPerLevel=.04f;}
            return ships.AsReadOnly();
        }

        public static readonly IReadOnlyList<PreparedMiningPackage> Packages = BuildPackages();

        static IReadOnlyList<PreparedMiningPackage> BuildPackages()
        {
            var result=new List<PreparedMiningPackage>();
            string Tank(ShipDefinition hull,PreparedPackageGrade grade)
            {
                if(grade==PreparedPackageGrade.T0)return string.Empty;
                var suffix=grade==PreparedPackageGrade.T1?"i":"ii";
                return $"{hull.Id}-shield-{suffix}";
            }
            string Drone(ShipDefinition hull,PreparedPackageGrade grade)
            {
                if(grade==PreparedPackageGrade.T0)return string.Empty;
                var suffix=grade==PreparedPackageGrade.T1?"i":"ii";
                string id;
                if(hull.Id=="orca"||hull.Id=="rorqual")id=$"ogre-{suffix}";
                else if(hull.Id=="porpoise"||hull.Class==ShipClass.MiningBarge||hull.Class==ShipClass.Exhumer)id=$"hammerhead-{suffix}";
                else id=$"hobgoblin-{suffix}";
                var drone=Drones.FirstOrDefault(candidate=>candidate.Id==id);
                return drone!=null&&hull.DroneBandwidth>=drone.Bandwidth&&hull.DroneBayM3>=drone.VolumeM3?id:string.Empty;
            }
            void AddMining(ShipDefinition hull,PreparedPackageRole role,PreparedPackageGrade grade,string miner,bool universal=false)
            {
                var key=role.ToString().ToLowerInvariant();var tier=grade.ToString().ToLowerInvariant();
                var low=role==PreparedPackageRole.Ice?(grade==PreparedPackageGrade.T1?"ice-harvester-upgrade-i":"ice-harvester-upgrade-ii")
                    :role==PreparedPackageRole.Gas?string.Empty:(grade==PreparedPackageGrade.T1?"mining-laser-upgrade-i":"mining-laser-upgrade-ii");
                if(universal)
                {
                    foreach(var level in new[]{1,2})result.Add(new PreparedMiningPackage($"{hull.Id}-{key}-{tier}-a{level}",$"{hull.DisplayName} • {role} {grade} • Type A {level}",hull.Id,role,grade,miner,low,Drone(hull,grade),Tank(hull,grade),universalTypeALevel:level));
                }
                else result.Add(new PreparedMiningPackage($"{hull.Id}-{key}-{tier}",$"{hull.DisplayName} • {role} {grade}",hull.Id,role,grade,miner,low,Drone(hull,grade),Tank(hull,grade)));
            }

            // The only free starter package: two Miner I modules and nothing else.
            // T1 and above are deliberately complete, paid locked fits.
            result.Add(new PreparedMiningPackage("venture-ore-t0","Venture • Ore T0","venture",PreparedPackageRole.Ore,PreparedPackageGrade.T0,"miner-i",string.Empty,string.Empty,string.Empty));

            foreach(var hull in Ships.Where(ship=>ship.MiningHighSlots>0))
            {
                if(!hull.IceOnly)
                {
                    if(hull.UsesStripMiners)
                    {
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.T1,"strip-miner-i");
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.T2,"modulated-strip-miner-ii",true);
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.ORE,"ore-strip-miner");
                    }
                    else
                    {
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.T1,"miner-i");
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.T2,"miner-ii",true);
                        AddMining(hull,PreparedPackageRole.Ore,PreparedPackageGrade.ORE,"ore-miner");
                    }
                }
                if(hull.SupportsIceMiningLasers)
                {
                    AddMining(hull,PreparedPackageRole.Ice,PreparedPackageGrade.T1,"ice-mining-laser-i");
                    AddMining(hull,PreparedPackageRole.Ice,PreparedPackageGrade.T2,"ice-mining-laser-ii");
                    AddMining(hull,PreparedPackageRole.Ice,PreparedPackageGrade.ORE,"ore-ice-mining-laser");
                }
                else if(hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer)
                {
                    AddMining(hull,PreparedPackageRole.Ice,PreparedPackageGrade.T1,"ice-harvester-i");
                    AddMining(hull,PreparedPackageRole.Ice,PreparedPackageGrade.T2,"ice-harvester-ii");
                }
                if(hull.Id is "venture" or "venture-consortium" or "prospect" or "pioneer" or "pioneer-consortium")
                {
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.T1,"gas-cloud-scoop-i");
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.T2,"gas-cloud-scoop-ii");
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.ORE,"syndicate-gas-cloud-scoop");
                }
                else if(hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer)
                {
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.T1,"gas-cloud-harvester-i");
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.T2,"gas-cloud-harvester-ii");
                    AddMining(hull,PreparedPackageRole.Gas,PreparedPackageGrade.ORE,"ore-gas-cloud-harvester");
                }
            }

            var charges=PreparedBurstProfileIds.ToArray();
            foreach(var hull in Ships.Where(ship=>ship.IsCommandShip))
            {
                var compressors=hull.Id switch
                {
                    "porpoise"=>new[]{"medium-asteroid-ore-compressor-i","medium-gas-compressor-i"},
                    "orca"=>new[]{"large-asteroid-ore-compressor-i","large-ice-compressor-i","large-gas-compressor-i"},
                    "rorqual"=>new[]{"capital-asteroid-ore-compressor-i","capital-ice-compressor-i","capital-gas-compressor-i"},
                    _=>Array.Empty<string>()
                };
                foreach(var grade in new[]{PreparedPackageGrade.T1,PreparedPackageGrade.T2})
                {
                    var suffix=grade==PreparedPackageGrade.T1?"i":"ii";
                    var core=!hull.SupportsIndustrialCore?string.Empty:hull.Id=="rorqual"?$"capital-industrial-core-{suffix}":hull.Id=="porpoise"?$"medium-industrial-core-{suffix}":$"industrial-core-{suffix}";
                    var burst=grade==PreparedPackageGrade.T1?"mining-burst-i":"mining-burst";
                    var tier=grade.ToString().ToLowerInvariant();
                    result.Add(new PreparedMiningPackage($"{hull.Id}-booster-{tier}",$"{hull.DisplayName} • Booster {grade}",hull.Id,PreparedPackageRole.Booster,grade,string.Empty,string.Empty,Drone(hull,grade),Tank(hull,grade),core,burst,charges,compressors:compressors));
                }
            }
            return result.AsReadOnly();
        }

        public static readonly IReadOnlyList<LocationDefinition> Locations = BuildLocations();

        static IReadOnlyList<LocationDefinition> BuildLocations()
        {
            var locations = new List<LocationDefinition>
            {
            new("uitra-belt-1", "Uitra", 30030141, "Uitra I - Asteroid Belt 1", 40342603, .9240959883f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("uitra-belt-2", "Uitra", 30030141, "Uitra III - Asteroid Belt 1", 40342606, .9240959883f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("uitra-belt-3", "Uitra", 30030141, "Uitra VII - Asteroid Belt 1", 40342620, .9240959883f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("uitra-belt-4", "Uitra", 30030141, "Uitra VII - Asteroid Belt 2", 40342621, .9240959883f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-1", "Kakakela", 30001405, "Kakakela I - Asteroid Belt 1", 40089401, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-2", "Kakakela", 30001405, "Kakakela IV - Asteroid Belt 1", 40089407, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-3", "Kakakela", 30001405, "Kakakela V - Asteroid Belt 1", 40089409, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-4", "Kakakela", 30001405, "Kakakela VI - Asteroid Belt 1", 40089428, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-5", "Kakakela", 30001405, "Kakakela VII - Asteroid Belt 1", 40089455, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-6", "Kakakela", 30001405, "Kakakela VII - Asteroid Belt 2", 40089456, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-7", "Kakakela", 30001405, "Kakakela VII - Asteroid Belt 3", 40089458, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-8", "Kakakela", 30001405, "Kakakela VII - Asteroid Belt 4", 40089460, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-9", "Kakakela", 30001405, "Kakakela VIII - Asteroid Belt 1", 40089462, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("kakakela-belt-10", "Kakakela", 30001405, "Kakakela VIII - Asteroid Belt 2", 40089463, .9490000010f, new[]{"veldspar","scordite","pyroxeres"}, 0, 9999, 9999, 0),
            new("sobaseki-belt-1", "Sobaseki", 30001363, "Sobaseki IV - Asteroid Belt 1", 40086852, .8410254717f, new[]{"veldspar","scordite","pyroxeres"}, .14f, 210, 420, 2),
            new("sobaseki-belt-2", "Sobaseki", 30001363, "Sobaseki VIII - Asteroid Belt 1", 40086862, .8410254717f, new[]{"veldspar","scordite","pyroxeres"}, .14f, 210, 420, 2),
            new("sobaseki-belt-3", "Sobaseki", 30001363, "Sobaseki IX - Asteroid Belt 1", 40086865, .8410254717f, new[]{"veldspar","scordite","pyroxeres"}, .14f, 210, 420, 2),
            new("sobaseki-belt-4", "Sobaseki", 30001363, "Sobaseki X - Asteroid Belt 1", 40086876, .8410254717f, new[]{"veldspar","scordite","pyroxeres"}, .14f, 210, 420, 2),
            new("sobaseki-belt-5", "Sobaseki", 30001363, "Sobaseki XII - Asteroid Belt 1", 40086898, .8410254717f, new[]{"veldspar","scordite","pyroxeres"}, .14f, 210, 420, 2),
            new("saisio-belt-1", "Saisio", 30000146, "Saisio III - Asteroid Belt 1", 40009272, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-2", "Saisio", 30000146, "Saisio VII - Asteroid Belt 1", 40009287, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-3", "Saisio", 30000146, "Saisio VII - Asteroid Belt 2", 40009283, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-4", "Saisio", 30000146, "Saisio VII - Asteroid Belt 3", 40009296, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-5", "Saisio", 30000146, "Saisio VIII - Asteroid Belt 1", 40009301, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-6", "Saisio", 30000146, "Saisio VIII - Asteroid Belt 2", 40009307, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-7", "Saisio", 30000146, "Saisio VIII - Asteroid Belt 3", 40009320, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-8", "Saisio", 30000146, "Saisio VIII - Asteroid Belt 4", 40009323, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("saisio-belt-9", "Saisio", 30000146, "Saisio VIII - Asteroid Belt 5", 40009325, .6524402499f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-1", "Korsiki", 30000181, "Korsiki II - Asteroid Belt 1", 40011383, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-2", "Korsiki", 30000181, "Korsiki II - Asteroid Belt 2", 40011386, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-3", "Korsiki", 30000181, "Korsiki II - Asteroid Belt 3", 40011388, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-4", "Korsiki", 30000181, "Korsiki II - Asteroid Belt 4", 40011384, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-5", "Korsiki", 30000181, "Korsiki II - Asteroid Belt 5", 40011382, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-6", "Korsiki", 30000181, "Korsiki VI - Asteroid Belt 1", 40011444, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-7", "Korsiki", 30000181, "Korsiki VI - Asteroid Belt 2", 40011446, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("korsiki-belt-8", "Korsiki", 30000181, "Korsiki VII - Asteroid Belt 1", 40011450, .6431657672f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .34f, 145, 290, 3),
            new("otomainen-belt-1", "Otomainen", 30000172, "Otomainen III - Asteroid Belt 1", 40010885, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-2", "Otomainen", 30000172, "Otomainen VII - Asteroid Belt 1", 40010891, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-3", "Otomainen", 30000172, "Otomainen VIII - Asteroid Belt 1", 40010893, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-4", "Otomainen", 30000172, "Otomainen VIII - Asteroid Belt 2", 40010894, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-5", "Otomainen", 30000172, "Otomainen IX - Asteroid Belt 1", 40010896, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-6", "Otomainen", 30000172, "Otomainen IX - Asteroid Belt 2", 40010898, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-7", "Otomainen", 30000172, "Otomainen XI - Asteroid Belt 1", 40010916, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-8", "Otomainen", 30000172, "Otomainen XI - Asteroid Belt 2", 40010929, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-9", "Otomainen", 30000172, "Otomainen XI - Asteroid Belt 3", 40010926, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-10", "Otomainen", 30000172, "Otomainen XI - Asteroid Belt 4", 40010928, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-11", "Otomainen", 30000172, "Otomainen XII - Asteroid Belt 1", 40010931, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-12", "Otomainen", 30000172, "Otomainen XII - Asteroid Belt 2", 40010937, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-13", "Otomainen", 30000172, "Otomainen XII - Asteroid Belt 3", 40010932, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("otomainen-belt-14", "Otomainen", 30000172, "Otomainen XIII - Asteroid Belt 1", 40010941, .4791586101f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f, 120, 240, 3),
            new("manatirid-belt-1", "Manatirid", 30005230, "Manatirid III - Asteroid Belt 1", 40330675, .5485262871f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f,120,240,3),
            new("manatirid-belt-2", "Manatirid", 30005230, "Manatirid VII - Asteroid Belt 1", 40330736, .5485262871f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f,120,240,3),
            new("manatirid-belt-3", "Manatirid", 30005230, "Manatirid VIII - Asteroid Belt 1", 40330741, .5485262871f, new[]{"veldspar","scordite","pyroxeres","plagioclase"}, .48f,120,240,3),
            new("manatirid-clear-icicle", "Manatirid", 30005230, "Clear Icicle anomaly", 0, .5485263f, new[]{"clear-icicle"}, .48f, 120, 240, 3, true, true,6*3600,6*3600,1f),
            new("otsela-belt-1", "Otsela", 30000194, "Otsela V - Asteroid Belt 1",40012332,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-2", "Otsela",30000194,"Otsela V - Asteroid Belt 2",40012334,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-3", "Otsela",30000194,"Otsela VI - Asteroid Belt 1",40012352,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-4", "Otsela",30000194,"Otsela VI - Asteroid Belt 2",40012351,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-5", "Otsela",30000194,"Otsela VI - Asteroid Belt 3",40012375,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-6", "Otsela",30000194,"Otsela VII - Asteroid Belt 1",40012377,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-7", "Otsela",30000194,"Otsela VIII - Asteroid Belt 1",40012382,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("otsela-belt-8", "Otsela",30000194,"Otsela VIII - Asteroid Belt 2",40012383,.3843998909f,new[]{"pyroxeres","kernite"},.64f,95,195,4),
            new("hakonen-belt-1","Hakonen",30001448,"Hakonen III - Asteroid Belt 1",40092207,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-2","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 1",40092238,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-3","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 2",40092236,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-4","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 3",40092256,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-5","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 4",40092253,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-6","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 5",40092262,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-7","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 6",40092265,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-8","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 7",40092261,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-9","Hakonen",30001448,"Hakonen VIII - Asteroid Belt 8",40092266,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-10","Hakonen",30001448,"Hakonen IX - Asteroid Belt 1",40092297,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-11","Hakonen",30001448,"Hakonen IX - Asteroid Belt 2",40092290,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-12","Hakonen",30001448,"Hakonen IX - Asteroid Belt 3",40092291,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-13","Hakonen",30001448,"Hakonen IX - Asteroid Belt 4",40092294,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-14","Hakonen",30001448,"Hakonen IX - Asteroid Belt 5",40092292,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-15","Hakonen",30001448,"Hakonen IX - Asteroid Belt 6",40092296,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-16","Hakonen",30001448,"Hakonen IX - Asteroid Belt 7",40092298,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-17","Hakonen",30001448,"Hakonen IX - Asteroid Belt 8",40092301,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-18","Hakonen",30001448,"Hakonen IX - Asteroid Belt 9",40092302,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-19","Hakonen",30001448,"Hakonen IX - Asteroid Belt 10",40092303,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-20","Hakonen",30001448,"Hakonen IX - Asteroid Belt 11",40092295,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("hakonen-belt-21","Hakonen",30001448,"Hakonen IX - Asteroid Belt 12",40092299,.2963790298f,new[]{"pyroxeres","kernite"},.76f,80,170,5),
            new("p3en-e-belt-1","P3EN-E",30000250,"P3EN-E IV - Asteroid Belt 1",40015864,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-2","P3EN-E",30000250,"P3EN-E VI - Asteroid Belt 1",40015868,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-3","P3EN-E",30000250,"P3EN-E VI - Asteroid Belt 2",40015869,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-4","P3EN-E",30000250,"P3EN-E VII - Asteroid Belt 1",40015871,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-5","P3EN-E",30000250,"P3EN-E X - Asteroid Belt 1",40015883,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-6","P3EN-E",30000250,"P3EN-E X - Asteroid Belt 2",40015885,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-7","P3EN-E",30000250,"P3EN-E X - Asteroid Belt 3",40015887,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-8","P3EN-E",30000250,"P3EN-E X - Asteroid Belt 4",40015907,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("p3en-e-belt-9","P3EN-E",30000250,"P3EN-E X - Asteroid Belt 5",40015910,-.2748796642f,new[]{"pyroxeres","kernite","bistot","arkonor"},.9f,65,145,6),
            new("y-zxio-belt-1","Y-ZXIO",30000306,"Y-ZXIO II - Asteroid Belt 1",40019239,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            new("y-zxio-belt-2","Y-ZXIO",30000306,"Y-ZXIO III - Asteroid Belt 1",40019244,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            new("y-zxio-belt-3","Y-ZXIO",30000306,"Y-ZXIO V - Asteroid Belt 1",40019271,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            new("y-zxio-belt-4","Y-ZXIO",30000306,"Y-ZXIO VI - Asteroid Belt 1",40019273,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            new("y-zxio-belt-5","Y-ZXIO",30000306,"Y-ZXIO VI - Asteroid Belt 2",40019289,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            new("y-zxio-belt-6","Y-ZXIO",30000306,"Y-ZXIO VII - Asteroid Belt 1",40019295,-.9004377723f,new[]{"pyroxeres","kernite","mercoxit","bistot"},1f,50,120,7),
            // Authored compatibility templates. Current EVE publications support
            // the broad security/category rules, but do not publish live spawn
            // odds or cooldowns. Listed SDE identities are legacy references,
            // not proof that the same template currently spawns on Tranquility.
            new("uitra-managed-mining-site","Uitra",30030141,"Managed Mining Site • authored approx [legacy SDE 13451]",0,.9240959883f,new[]{"veldspar"},0,9999,9999,0,true,true,24*3600,24*3600,1f,10,12,new[]{"venture"}),
            new("sobaseki-anom-kernite-omber","Sobaseki",30001363,"Kernite and Omber Deposit • authored approx [legacy SDE 1347]",0,.8410254717f,new[]{"kernite","omber"},.14f,210,420,2,true,true,6*3600,12*3600,.2f),
            new("korsiki-anom-small-omber","Korsiki",30000181,"Small Omber Deposit • authored approx [legacy SDE 1345]",0,.6431657672f,new[]{"omber"},.34f,145,290,3,true,true,6*3600,12*3600,.2f),
            new("korsiki-anom-kernite-omber","Korsiki",30000181,"Kernite and Omber Deposit • authored approx [legacy SDE 1347]",0,.6431657672f,new[]{"kernite","omber"},.34f,145,290,3,true,true,6*3600,12*3600,.2f),
            new("korsiki-hidden-omber","Korsiki",30000181,"Hidden Omber Deposit • authored approx [legacy SDE 13469]",0,.6431657672f,new[]{"omber"},.34f,145,290,3,true,true,12*3600,24*3600,.05f),
            new("otomainen-anom-small-omber","Otomainen",30000172,"Small Omber Deposit • authored approx [legacy SDE 1345]",0,.4791586101f,new[]{"omber"},.48f,120,240,3,true,true,6*3600,12*3600,.2f),
            new("manatirid-anom-kernite-omber","Manatirid",30005230,"Kernite and Omber Deposit • authored approx [legacy SDE 1347]",0,.5485262871f,new[]{"kernite","omber"},.48f,120,240,3,true,true,6*3600,12*3600,.2f),
            new("otsela-anom-average-jaspet","Otsela",30000194,"Average Jaspet Deposit • authored approx [legacy SDE 1466]",0,.3843998909f,new[]{"jaspet"},.64f,95,195,4,true,true,2*3600,6*3600,.25f),
            new("hakonen-hidden-lowsec","Hakonen",30001448,"Hidden Lowsec Deposit • authored approx [legacy SDE 13493]",0,.2963790298f,new[]{"jaspet","kernite"},.76f,80,170,5,true,true,2*3600,6*3600,.1f),
            new("p3en-e-prospecting-l1","P3EN-E",30000250,"Legacy Prospecting-inspired L1 • authored approx [SDE 10828]",0,-.2748796642f,new[]{"veldspar","scordite","plagioclase"},.9f,65,145,6,true,true,3600,3900),
            new("p3en-e-prospecting-l2","P3EN-E",30000250,"Legacy Prospecting-inspired L2 • authored approx [SDE 10829]",0,-.2748796642f,new[]{"pyroxeres","omber","kernite","gneiss"},.9f,65,145,6,true,true,4.1f*3600,4.3f*3600),
            new("y-zxio-prospecting-l3","Y-ZXIO",30000306,"Legacy Prospecting-inspired L3 • authored approx [SDE 10832]",0,-.9004377723f,new[]{"dark-ochre","crokite","bistot","mercoxit"},1f,50,120,7,true,true,10*3600,10.2f*3600)
                ,new("wspace-c1-barren-reservoir","W-space C1",0,"Barren Perimeter Reservoir",0,-1f,new[]{"fullerite-c60","fullerite-c50"},1f,900,1200,6,true,true)
            };
            HighSecBeltCatalog.AddMissingTheForgeBelts(locations);
            return locations.AsReadOnly();
        }

        static readonly Dictionary<string, SkillDefinition> SkillsById = AllSkillDefinitions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, OreDefinition> OresById = Ores.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<int, OreDefinition> OresByTypeId = Ores.ToDictionary(x => x.TypeId);
        static readonly Dictionary<string, MiningModuleDefinition> ModulesById = Modules.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, DroneDefinition> DronesById = Drones.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, MiningCrystalDefinition> CrystalsById = Crystals.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, BurstChargeDefinition> BurstChargesById = BurstCharges.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, ShipDefinition> ShipsById = Ships.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, LocationDefinition> LocationsById = Locations.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, TankPresetDefinition> TankPresetsById = TankPresets.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, PreparedMiningPackage> PackagesById = Packages.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);

        public static SkillDefinition GetSkill(string id) => id != null && SkillsById.TryGetValue(id, out var value) ? value : null;
        public static OreDefinition GetOre(string id) => id != null && OresById.TryGetValue(id, out var value) ? value : null;
        public static MiningModuleDefinition GetModule(string id) => id != null && ModulesById.TryGetValue(id, out var value) ? value : null;
        public static DroneDefinition GetDrone(string id) => id != null && DronesById.TryGetValue(id, out var value) ? value : null;
        public static MiningCrystalDefinition GetCrystal(string id)=>id!=null&&CrystalsById.TryGetValue(id,out var value)?value:null;
        public static BurstChargeDefinition GetBurstCharge(string id)=>id!=null&&BurstChargesById.TryGetValue(id,out var value)?value:null;
        public static ShipDefinition GetShip(string id) => id != null && ShipsById.TryGetValue(id, out var value) ? value : null;
        public static LocationDefinition GetLocation(string id) => id != null && LocationsById.TryGetValue(id, out var value) ? value : null;
        public static TankPresetDefinition GetTankPreset(string id)=>id!=null&&TankPresetsById.TryGetValue(id,out var value)?value:null;
        public static PreparedMiningPackage GetPackage(string id)=>id!=null&&PackagesById.TryGetValue(id,out var value)?value:null;
        public static OreDefinition GetOreByTypeId(int typeId) => OresByTypeId.TryGetValue(typeId, out var value) ? value : null;
        public static ShipDefinition GetShipByTypeId(int typeId) => Ships.FirstOrDefault(x => x.TypeId == typeId);
        public static MiningModuleDefinition GetModuleByTypeId(int typeId) => Modules.FirstOrDefault(x => x.TypeId == typeId);
        public static SkillDefinition GetSkillByTypeId(int typeId) => AllSkillDefinitions.FirstOrDefault(x => x.TypeId == typeId);
        public static DroneDefinition GetDroneByTypeId(int typeId) => Drones.FirstOrDefault(x => x.TypeId == typeId);
        public static MiningCrystalDefinition GetCrystalByTypeId(int typeId)=>Crystals.FirstOrDefault(x=>x.TypeId==typeId);
        public static BurstChargeDefinition GetBurstChargeByTypeId(int typeId)=>BurstCharges.FirstOrDefault(x=>x.TypeId==typeId);

        public static string CompressedItemId(string rawId)
        {
            var source = GetOre(rawId);
            return source == null ? string.Empty : CompressedItemPrefix + source.Id;
        }

        public static bool TryGetCompressedSource(string itemId, out OreDefinition source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(itemId) || !itemId.StartsWith(CompressedItemPrefix, StringComparison.OrdinalIgnoreCase)) return false;
            source = GetOre(itemId.Substring(CompressedItemPrefix.Length));
            return source != null;
        }

        public static CompressionKind CompressionKindFor(OreDefinition resource)
        {
            if (resource == null) return CompressionKind.None;
            if (resource.Kind == ResourceKind.Ice) return CompressionKind.Ice;
            if (resource.Kind == ResourceKind.Gas) return CompressionKind.Gas;
            return CompressionKind.Ore;
        }

        public static string GetOreFamilyId(string oreId)
        {
            if (string.IsNullOrWhiteSpace(oreId)) return string.Empty;
            var ore = GetOre(oreId);
            return string.IsNullOrWhiteSpace(ore?.FamilyId) ? oreId : ore.FamilyId;
        }

        public static string GetOreFamilyId(OreDefinition ore) => ore == null ? string.Empty : GetOreFamilyId(ore.Id);

        public static int GetOreGrade(string oreId)
        {
            var ore = GetOre(oreId);
            return ore?.Grade ?? -1;
        }

        public static int GetOreGrade(OreDefinition ore) => ore?.Grade ?? -1;

        public static bool IsMercoxitFamily(string oreId) => string.Equals(GetOreFamilyId(oreId), "mercoxit", StringComparison.OrdinalIgnoreCase);
        public static bool IsMercoxitFamily(OreDefinition ore) => ore != null && IsMercoxitFamily(ore.Id);

        public static bool SameOreFamily(string leftOreId, string rightOreId)
        {
            if (string.IsNullOrWhiteSpace(leftOreId) || string.IsNullOrWhiteSpace(rightOreId)) return false;
            return string.Equals(GetOreFamilyId(leftOreId), GetOreFamilyId(rightOreId), StringComparison.OrdinalIgnoreCase);
        }

        public static OreDefinition GetOreVariant(string familyId, int grade)
        {
            if (string.IsNullOrWhiteSpace(familyId)) return null;
            var canonicalFamilyId = GetOreFamilyId(familyId);
            return Ores.FirstOrDefault(x => x.Grade == grade && string.Equals(x.FamilyId, canonicalFamilyId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryGetItemVolumeM3(string itemId, out float volumeM3)
        {
            volumeM3 = 0;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (TryGetCompressedSource(itemId, out var compressedSource))
            {
                var divisor = compressedSource.Kind == ResourceKind.Ore ? 100f : 10f;
                volumeM3 = compressedSource.UnitVolumeM3 / divisor;
                return volumeM3 > 0;
            }
            var ore = GetOre(itemId); if (ore != null) { volumeM3 = ore.UnitVolumeM3; return volumeM3 > 0; }
            var module = GetModule(itemId); if (module != null) { volumeM3 = module.VolumeM3; return volumeM3 > 0; }
            var drone = GetDrone(itemId); if (drone != null) { volumeM3 = drone.VolumeM3; return volumeM3 > 0; }
            var crystal = GetCrystal(itemId); if (crystal != null) { volumeM3 = crystal.VolumeM3; return volumeM3 > 0; }
            var charge = GetBurstCharge(itemId); if (charge != null) { volumeM3 = charge.VolumeM3; return volumeM3 > 0; }
            if (string.Equals(itemId, "heavy-water", StringComparison.OrdinalIgnoreCase)) { volumeM3 = .4f; return true; }
            return false;
        }
    }
}
