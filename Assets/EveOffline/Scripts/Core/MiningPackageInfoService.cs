using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    public sealed class MiningPackageYieldEstimate
    {
        public bool HasExtractor;
        public double MinimumM3PerSecond;
        public double SkilledM3PerSecond;
        public ResourceKind ResourceKind;
    }

    public sealed class MiningPackageCombatEstimate
    {
        public double MinimumEffectiveHp;
        public double SkilledEffectiveHp;
        public int CombatDroneCount;
        public double MinimumDroneDps;
        public double SkilledDroneDps;
    }

    /// <summary>Deterministic Academy descriptions and theoretical package throughput.</summary>
    public static class MiningPackageInfoService
    {
        static readonly IReadOnlyDictionary<string, string> HullDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["venture"] = "Стартовый универсальный фрегат: дешёвый, быстрый, небольшой трюм; готовые комплекты доступны для руды и газа.",
            ["venture-consortium"] = "Усиленная Venture: больше трюм и прочность, повышенный шанс mining critical; бурстов нет.",
            ["prospect"] = "Экспедиционный фрегат: быстрый универсал для руды, льда и газа, заметно эффективнее Venture; бурстов нет.",
            ["endurance"] = "Экспедиционный ледовый фрегат: огромный трюм и один очень сильный ice-лазер; готовые комплекты только для льда, бурстов нет.",
            ["pioneer"] = "Добывающий эсминец с тремя модулями: широкий выбор руды и газа, высокая дальность рудных лазеров; бурстов нет.",
            ["pioneer-consortium"] = "Усиленный Pioneer: больше трюм, прочность и шанс critical; готовые комплекты доступны для руды и газа, бурстов нет.",
            ["perseverance"] = "Специализированный ледовый эсминец: три ледовых лазера, 21 000 м³ трюм и сильные critical-бонусы; бурстов нет.",
            ["outrider"] = "Командный добывающий эсминец: в Mining-комплекте копает тремя лазерами, в Booster-комплекте даёт 1 burst; core нет.",
            ["retriever"] = "Баржа большого трюма: реже разгружается; по руде её прямой поток сейчас равен другим T1-баржам.",
            ["procurer"] = "Защитная баржа: самый крепкий T1-вариант и большой drone bay; по руде её прямой поток сейчас равен другим T1-баржам.",
            ["covetor"] = "Флотская баржа с маленьким трюмом: большая дальность и быстрый лёд; по руде её прямой поток сейчас равен другим T1-баржам.",
            ["mackinaw"] = "Exhumer с огромным трюмом: спокойная автономная добыча и редкие разгрузки.",
            ["skiff"] = "Защитный Exhumer: максимальная живучесть среди малых добывающих кораблей.",
            ["hulk"] = "Флотский Exhumer с небольшим трюмом: большая дальность и лучший ледовый темп; по руде его прямой поток сейчас равен другим Exhumer.",
            ["porpoise"] = "Малый руководитель флота: 2 mining bursts, Medium Industrial Core и сжатие астероидной руды/газа; сам лазерами не копает.",
            ["orca"] = "Крупный highsec-руководитель: 3 mining bursts, Large Industrial Core и общее сжатие руды, льда/газа; сам лазерами не копает.",
            ["rorqual"] = "Капитальный руководитель: 3 встроенных mining burst-эффекта, четвёртый слот пуст; Capital Industrial Core и общее сжатие руды, льда/газа; сам лазерами не копает."
        };

        public static string HullDescription(ShipDefinition hull)
        {
            if (hull == null) return "Описание корпуса недоступно.";
            return HullDescriptions.TryGetValue(hull.Id, out var value) ? value : "Добывающий корпус с готовыми комплектами для доступных ресурсов.";
        }

        public static string BurstSummary(ShipDefinition hull)
        {
            if (hull == null || hull.CommandBurstSlots <= 0) return "Бурсты: нет";
            var effectCount = Math.Min(hull.CommandBurstSlots, Catalog.PreparedBurstProfileIds.Count);
            var emptySlots = hull.CommandBurstSlots - effectCount;
            var core = hull.SupportsIndustrialCore ? " • Industrial Core без топлива" : " • без Industrial Core";
            var compression = hull.Id == "porpoise"
                ? " • сжатие: руда/газ"
                : hull.Id is "orca" or "rorqual"
                    ? " • сжатие: руда/лёд/газ"
                    : string.Empty;
            var empty = emptySlots > 0 ? $" • пустых слотов: {emptySlots}" : string.Empty;
            return $"Бурсты: {effectCount} • весь флот{empty}{core}{compression}";
        }

        public static MiningPackageYieldEstimate Estimate(PreparedMiningPackage package)
        {
            var estimate = new MiningPackageYieldEstimate();
            var hull = Catalog.GetShip(package?.HullId);
            var module = Catalog.GetModule(package?.MinerModuleId);
            if (package == null || hull == null || module == null || hull.MiningHighSlots <= 0) return estimate;
            estimate.HasExtractor = true;
            estimate.ResourceKind = package.Role == PreparedPackageRole.Ice ? ResourceKind.Ice : package.Role == PreparedPackageRole.Gas ? ResourceKind.Gas : ResourceKind.Ore;
            estimate.MinimumM3PerSecond = Rate(package, false);
            estimate.SkilledM3PerSecond = Math.Max(estimate.MinimumM3PerSecond, Rate(package, true));
            return estimate;
        }

        public static string YieldSummary(PreparedMiningPackage package)
        {
            var estimate = Estimate(package);
            if (!estimate.HasExtractor) return "Личная добыча: нет • командный комплект";
            var note = estimate.ResourceKind == ResourceKind.Ice ? " • лёд блоками по 1000 м³" : string.Empty;
            return $"Ожидаемая добыча ≈ {estimate.MinimumM3PerSecond:0.#}–{estimate.SkilledM3PerSecond:0.#} м³/с{note}";
        }

        public static MiningPackageCombatEstimate EstimateCombat(PreparedMiningPackage package)
        {
            var estimate = new MiningPackageCombatEstimate();
            var hull = Catalog.GetShip(package?.HullId);
            if (package == null || hull == null) return estimate;

            var ship = new ShipSave { HullId = hull.Id };
            PreparedPackageService.ApplyLockedFit(ship, package);
            estimate.CombatDroneCount = Math.Max(0, ship.CombatDroneCount);

            var minimumPilot = EstimatedPilot(BuildLevels(package, false));
            var skilledPilot = EstimatedPilot(BuildLevels(package, true));
            estimate.MinimumEffectiveHp = EffectiveHp(ship, minimumPilot);
            estimate.SkilledEffectiveHp = Math.Max(estimate.MinimumEffectiveHp, EffectiveHp(ship, skilledPilot));
            estimate.MinimumDroneDps = OperationService.CombatDroneDps(ship, minimumPilot);
            estimate.SkilledDroneDps = Math.Max(estimate.MinimumDroneDps, OperationService.CombatDroneDps(ship, skilledPilot));
            return estimate;
        }

        public static string CombatSummary(PreparedMiningPackage package)
        {
            var estimate = EstimateCombat(package);
            var durability = NumberRange(estimate.MinimumEffectiveHp, estimate.SkilledEffectiveHp);
            if (estimate.CombatDroneCount <= 0) return $"Живучесть ≈ {durability} EHP • боевых дронов нет";
            var dps = NumberRange(estimate.MinimumDroneDps, estimate.SkilledDroneDps);
            return $"Живучесть ≈ {durability} EHP • боевые дроны ×{estimate.CombatDroneCount} • {dps} DPS";
        }

        static double Rate(PreparedMiningPackage package, bool skilled)
        {
            var hull = Catalog.GetShip(package.HullId);
            var module = Catalog.GetModule(package.MinerModuleId);
            if (hull == null || module == null) return 0;
            var levels = BuildLevels(package, skilled);
            int Level(string id) => string.IsNullOrWhiteSpace(id) ? 0 : levels.TryGetValue(id, out var value) ? value : 0;

            var ice = module.Kind is ModuleKind.IceMiningLaser or ModuleKind.IceHarvester;
            var gas = module.Kind is ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester;
            double cycleMultiplier = ice ? hull.IceRoleCycleMultiplier : gas ? hull.GasRoleCycleMultiplier : hull.RoleCycleMultiplier;
            if (ice)
            {
                cycleMultiplier *= 1d - .05d * Level("ice-harvesting");
                cycleMultiplier *= 1d - hull.IceCycleReductionPerLevel * Level(hull.BonusSkillId);
                cycleMultiplier *= 1d - hull.SecondaryIceCycleReductionPerLevel * Level(hull.SecondaryBonusSkillId);
                cycleMultiplier *= Catalog.PerfectIceHarvesterCycleImplantMultiplier;
                cycleMultiplier *= UpgradeCycleMultiplier(package, hull);
            }
            else if (gas)
            {
                cycleMultiplier *= 1d - hull.GasCycleReductionPerLevel * Level(hull.BonusSkillId);
                cycleMultiplier *= 1d - hull.SecondaryGasCycleReductionPerLevel * Level(hull.SecondaryBonusSkillId);
            }
            else
            {
                cycleMultiplier *= 1d - hull.CycleReductionPerLevel * Level(hull.BonusSkillId);
                cycleMultiplier *= 1d - hull.SecondaryCycleReductionPerLevel * Level(hull.SecondaryBonusSkillId);
            }

            var cycleSeconds = Math.Max(1d, module.CycleSeconds * cycleMultiplier);
            double standard;
            if (ice) standard = module.BaseYieldM3;
            else if (gas) standard = module.BaseYieldM3 * hull.GasRoleYieldMultiplier;
            else
            {
                standard = module.BaseYieldM3 * CrystalAmountMultiplier(package) * hull.RoleYieldMultiplier;
                standard *= 1d + .05d * Level("mining");
                standard *= 1d + .05d * Level("astrogeology");
                standard *= 1d + hull.YieldBonusPerLevel * Level(hull.BonusSkillId);
                standard *= 1d + hull.SecondaryYieldBonusPerLevel * Level(hull.SecondaryBonusSkillId);
                standard *= UpgradeYieldMultiplier(package, hull);
                standard *= Catalog.PerfectOreLaserYieldImplantMultiplier;
            }

            var expected = standard;
            if (!gas)
            {
                var criticalChance = module.CriticalChance * (1d + .10d * Level("mining-precision"));
                criticalChance *= hull.RoleCriticalChanceMultiplier;
                criticalChance *= 1d + hull.CriticalChanceBonusPerLevel * Level(hull.BonusSkillId);
                var criticalBonusMultiplier = module.CriticalBonusYield * (1d + .05d * Level("mining-exploitation"));
                criticalBonusMultiplier *= 1d + hull.CriticalBonusYieldPerLevel * Level(hull.BonusSkillId);
                var criticalBonus = standard * criticalBonusMultiplier;
                expected += criticalChance * criticalBonus;
            }
            return Math.Max(0, expected * hull.MiningHighSlots / cycleSeconds);
        }

        static Dictionary<string, int> BuildLevels(PreparedMiningPackage package, bool skilled)
        {
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var requirement in PreparedPackageService.RequiredSkills(package)) levels[requirement.SkillId] = Math.Max(levels.TryGetValue(requirement.SkillId, out var current) ? current : 0, requirement.Level);
            if (!skilled) return levels;
            void Max(string id) { if (!string.IsNullOrWhiteSpace(id)) levels[id] = 5; }
            var hull = Catalog.GetShip(package.HullId);
            if (package.Role is PreparedPackageRole.Ore or PreparedPackageRole.Mercoxit)
            {
                Max("mining"); Max("astrogeology"); Max("mining-precision"); Max("mining-exploitation");
            }
            else if (package.Role == PreparedPackageRole.Ice)
            {
                Max("ice-harvesting"); Max("mining-precision"); Max("mining-exploitation");
            }
            Max(hull?.BonusSkillId); Max(hull?.SecondaryBonusSkillId); Max(hull?.ShieldResistBonusSkillId);
            if (Catalog.GetTankPreset(package.TankPresetId) != null) Max("shield-management");
            if (Catalog.GetDrone(package.CombatDroneId) != null) { Max("drones"); Max("drone-interfacing"); }
            return levels;
        }

        static CharacterSave EstimatedPilot(IReadOnlyDictionary<string, int> levels)
        {
            var pilot = new CharacterSave();
            foreach (var pair in levels)
            {
                if (pair.Value <= 0 || Catalog.GetSkill(pair.Key) == null) continue;
                pilot.Skills.Add(new CharacterSkillSave { SkillId = pair.Key, BookOwned = true, SkillPoints = SkillService.RequiredSp(pair.Key, pair.Value) });
            }
            return pilot;
        }

        static double EffectiveHp(ShipSave ship, CharacterSave pilot)
        {
            var shieldMultiplier = Math.Max(.01d, PreparedPackageService.IncomingShieldDamageMultiplier(ship, pilot));
            return PreparedPackageService.MaxShieldHp(ship, pilot) / shieldMultiplier
                + PreparedPackageService.MaxArmorHp(ship)
                + PreparedPackageService.MaxStructureHp(ship);
        }

        static string NumberRange(double minimum, double maximum)
        {
            minimum = Math.Max(0d, minimum); maximum = Math.Max(minimum, maximum);
            return Math.Abs(maximum - minimum) < .5d ? $"{maximum:N0}" : $"{minimum:N0}–{maximum:N0}";
        }

        static double CrystalAmountMultiplier(PreparedMiningPackage package) => package.ImplicitUniversalTypeALevel == 1 ? 1.5d : package.ImplicitUniversalTypeALevel == 2 ? 1.8d : 1d;

        static double UpgradeYieldMultiplier(PreparedMiningPackage package, ShipDefinition hull)
        {
            var upgrade = Catalog.GetModule(package.LowUpgradeId);
            if (upgrade?.Kind != ModuleKind.MiningUpgrade) return 1d;
            var bonus = upgrade.TechLevel >= 2 ? .09d : .05d;
            return StackedMultiplier(hull.LowSlots, index => 1d + bonus * StackingPenalty(index));
        }

        static double UpgradeCycleMultiplier(PreparedMiningPackage package, ShipDefinition hull)
        {
            var upgrade = Catalog.GetModule(package.LowUpgradeId);
            if (upgrade?.Kind != ModuleKind.IceHarvesterUpgrade) return 1d;
            var bonus = upgrade.TechLevel >= 2 ? .09d : .05d;
            return StackedMultiplier(hull.LowSlots, index => 1d - bonus * StackingPenalty(index));
        }

        static double StackedMultiplier(int count, Func<int, double> factor)
        {
            var result = 1d;
            for (var index = 0; index < Math.Min(4, Math.Max(0, count)); index++) result *= factor(index);
            return result;
        }

        static double StackingPenalty(int index) => index switch { 0 => 1d, 1 => .86912d, 2 => .57058d, _ => .28296d };
    }
}
