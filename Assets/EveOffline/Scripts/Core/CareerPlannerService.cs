using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    /// <summary>
    /// Read-only training plan for one required skill. SP values are cumulative
    /// within the skill, matching SkillService.RequiredSp.
    /// </summary>
    public sealed class CareerSkillPlan
    {
        public string SkillId { get; }
        public string DisplayName { get; }
        public int CurrentLevel { get; }
        public int RequiredLevel { get; }
        public double CurrentAllocatedSp { get; }
        public double TargetAllocatedSp { get; }
        public double MissingAllocatedSp { get; }
        public double CoveredByExistingUnallocatedSp { get; }
        public double RemainingSpAfterUnallocated { get; }
        public bool BookOwned { get; }
        public bool NeedsBook => !BookOwned;
        public double MissingBookPriceIsk { get; }
        public bool IsDirectHullRequirement { get; }
        public bool RequirementMet => CurrentLevel >= RequiredLevel;

        internal CareerSkillPlan(
            string skillId,
            string displayName,
            int currentLevel,
            int requiredLevel,
            double currentAllocatedSp,
            double targetAllocatedSp,
            double missingAllocatedSp,
            double coveredByExistingUnallocatedSp,
            bool bookOwned,
            double missingBookPriceIsk,
            bool isDirectHullRequirement)
        {
            SkillId = skillId;
            DisplayName = displayName;
            CurrentLevel = currentLevel;
            RequiredLevel = requiredLevel;
            CurrentAllocatedSp = currentAllocatedSp;
            TargetAllocatedSp = targetAllocatedSp;
            MissingAllocatedSp = missingAllocatedSp;
            CoveredByExistingUnallocatedSp = coveredByExistingUnallocatedSp;
            RemainingSpAfterUnallocated = Math.Max(0d, missingAllocatedSp - coveredByExistingUnallocatedSp);
            BookOwned = bookOwned;
            MissingBookPriceIsk = missingBookPriceIsk;
            IsDirectHullRequirement = isDirectHullRequirement;
        }
    }

    /// <summary>A contiguous part of an injector plan with the same SP yield.</summary>
    public sealed class CareerInjectorTierPlan
    {
        public double StartingTotalSp { get; }
        public double EndingTotalSp { get; }
        public double SpPerInjector { get; }
        public int InjectorCount { get; }
        public double GrantedSp { get; }
        public double CostIsk { get; }

        internal CareerInjectorTierPlan(
            double startingTotalSp,
            double endingTotalSp,
            double spPerInjector,
            int injectorCount,
            double injectorPriceIsk)
        {
            StartingTotalSp = startingTotalSp;
            EndingTotalSp = endingTotalSp;
            SpPerInjector = spPerInjector;
            InjectorCount = injectorCount;
            GrantedSp = spPerInjector * injectorCount;
            CostIsk = injectorPriceIsk * injectorCount;
        }
    }

    /// <summary>Complete, side-effect-free path for one pilot to unlock one hull.</summary>
    public sealed class ShipCareerPlan
    {
        public string PilotId { get; }
        public string PilotName { get; }
        public string HullId { get; }
        public string HullName { get; }
        public ShipClass HullClass { get; }
        public string PackageId { get; }
        public string PackageName { get; }
        public PreparedPackageRole? PackageRole { get; }
        public PreparedPackageGrade? PackageGrade { get; }
        public double PackagePriceIsk { get; }
        public bool CanFlyNow { get; }
        public IReadOnlyList<CareerSkillPlan> Skills { get; }
        public int MissingSkillBookCount { get; }
        public double SkillBookCostIsk { get; }
        public double MissingAllocatedSp { get; }
        public double ExistingUnallocatedSp { get; }
        public double ExistingUnallocatedSpUsed { get; }
        public double RemainingSpAfterUnallocated { get; }
        public double NaturalTrainingSecondsWithoutUnallocated { get; }
        public double NaturalTrainingSeconds { get; }
        public double InjectorPriceIsk { get; }
        public int LargeInjectorCount { get; }
        public double InjectorSpGranted { get; }
        public double InjectorSpLeftOver { get; }
        public double InjectorCostIsk { get; }
        public double TotalInstantTrainingCostIsk => SkillBookCostIsk + InjectorCostIsk;
        public IReadOnlyList<CareerInjectorTierPlan> InjectorTiers { get; }

        internal ShipCareerPlan(
            CharacterSave pilot,
            ShipDefinition hull,
            bool canFlyNow,
            IReadOnlyList<CareerSkillPlan> skills,
            double existingUnallocatedSp,
            double injectorPriceIsk,
            IReadOnlyList<CareerInjectorTierPlan> injectorTiers,
            PreparedMiningPackage package=null,
            double packagePriceIsk=0)
        {
            PilotId = pilot.Id;
            PilotName = pilot.Name;
            HullId = hull.Id;
            HullName = hull.DisplayName;
            HullClass = hull.Class;
            PackageId=package?.Id??string.Empty;PackageName=package?.DisplayName??hull.DisplayName;PackageRole=package?.Role;PackageGrade=package?.Grade;PackagePriceIsk=packagePriceIsk;
            CanFlyNow = canFlyNow;
            Skills = skills;
            MissingSkillBookCount = skills.Count(skill => skill.NeedsBook);
            SkillBookCostIsk = skills.Sum(skill => skill.MissingBookPriceIsk);
            MissingAllocatedSp = skills.Sum(skill => skill.MissingAllocatedSp);
            ExistingUnallocatedSp = existingUnallocatedSp;
            ExistingUnallocatedSpUsed = skills.Sum(skill => skill.CoveredByExistingUnallocatedSp);
            RemainingSpAfterUnallocated = skills.Sum(skill => skill.RemainingSpAfterUnallocated);
            NaturalTrainingSecondsWithoutUnallocated = ToTrainingSeconds(MissingAllocatedSp);
            NaturalTrainingSeconds = ToTrainingSeconds(RemainingSpAfterUnallocated);
            InjectorPriceIsk = injectorPriceIsk;
            InjectorTiers = injectorTiers;
            LargeInjectorCount = injectorTiers.Sum(tier => tier.InjectorCount);
            InjectorSpGranted = injectorTiers.Sum(tier => tier.GrantedSp);
            InjectorSpLeftOver = Math.Max(0d, InjectorSpGranted - RemainingSpAfterUnallocated);
            InjectorCostIsk = injectorTiers.Sum(tier => tier.CostIsk);
        }

        static double ToTrainingSeconds(double skillPoints)
        {
            return Catalog.PerfectTrainingSpPerMinute > 0
                ? Math.Max(0d, skillPoints) / Catalog.PerfectTrainingSpPerMinute * 60d
                : double.PositiveInfinity;
        }
    }

    public static class CareerPlannerService
    {
        sealed class RequiredSkill
        {
            public string SkillId;
            public int Level;
            public bool Direct;
        }

        /// <summary>Builds a plan by catalog hull id without changing the save or pilot.</summary>
        public static ShipCareerPlan CreatePlan(GameSave save, CharacterSave pilot, string hullId)
        {
            var hull = Catalog.GetShip(hullId);
            if (hull == null) throw new ArgumentException($"Unknown hull id: {hullId}", nameof(hullId));
            return CreatePlan(save, pilot, hull);
        }

        /// <summary>Builds a plan for a catalog hull without changing the save or pilot.</summary>
        public static ShipCareerPlan CreatePlan(GameSave save, CharacterSave pilot, ShipDefinition hull)
        {
            if (pilot == null) throw new ArgumentNullException(nameof(pilot));
            if (hull == null) throw new ArgumentNullException(nameof(hull));

            var required = BuildRequirementClosure(hull.Requirements);
            var unallocatedLeft = SanitiseSp(pilot.UnallocatedSkillPoints);
            var skillPlans = new List<CareerSkillPlan>(required.Count);

            foreach (var item in required)
            {
                var definition = Catalog.GetSkill(item.SkillId);
                if (definition == null)
                    throw new InvalidOperationException($"Hull {hull.Id} requires unknown skill {item.SkillId}.");

                var state = SkillService.GetState(pilot, item.SkillId);
                var currentSp = SanitiseSp(state?.SkillPoints ?? 0d);
                var targetSp = SkillService.RequiredSp(item.SkillId, item.Level);
                var missingSp = Math.Max(0d, targetSp - currentSp);
                var covered = Math.Min(unallocatedLeft, missingSp);
                unallocatedLeft -= covered;
                var bookOwned = state?.BookOwned == true;
                var bookPrice = bookOwned
                    ? 0d
                    : MarketService.GetSellPrice(save, definition.TypeId, definition.BookFallbackPrice);

                skillPlans.Add(new CareerSkillPlan(
                    item.SkillId,
                    definition.DisplayName,
                    SkillService.GetLevel(pilot, item.SkillId),
                    item.Level,
                    currentSp,
                    targetSp,
                    missingSp,
                    covered,
                    bookOwned,
                    SanitisePrice(bookPrice),
                    item.Direct));
            }

            var readOnlySkills = skillPlans.AsReadOnly();
            var canFlyNow = readOnlySkills.All(skill => skill.RequirementMet);
            var remainingSp = readOnlySkills.Sum(skill => skill.RemainingSpAfterUnallocated);
            var injectorPrice = SanitisePrice(MarketService.InjectorPrice(save));
            var injectorTiers = BuildInjectorPlan(save, pilot, remainingSp, injectorPrice);

            return new ShipCareerPlan(
                pilot,
                hull,
                canFlyNow,
                readOnlySkills,
                SanitiseSp(pilot.UnallocatedSkillPoints),
                injectorPrice,
                injectorTiers);
        }

        /// <summary>
        /// Returns all catalog hulls in catalog order. Each result includes CanFlyNow
        /// and the complete unlock plan, so the UI does not need to recalculate it.
        /// </summary>
        public static IReadOnlyList<ShipCareerPlan> CreateAllShipPlans(GameSave save, CharacterSave pilot)
        {
            if (pilot == null) throw new ArgumentNullException(nameof(pilot));
            return Catalog.Ships.Select(hull => CreatePlan(save, pilot, hull)).ToList().AsReadOnly();
        }

        public static ShipCareerPlan CreatePackagePlan(GameSave save,CharacterSave pilot,string packageId)
        {
            var package=Catalog.GetPackage(packageId)??throw new ArgumentException($"Unknown package id: {packageId}",nameof(packageId));
            var hull=Catalog.GetShip(package.HullId);if(pilot==null)throw new ArgumentNullException(nameof(pilot));
            var required=BuildRequirementClosure(PreparedPackageService.RequiredSkills(package));var unallocatedLeft=SanitiseSp(pilot.UnallocatedSkillPoints);var plans=new List<CareerSkillPlan>(required.Count);
            foreach(var item in required)
            {
                var definition=Catalog.GetSkill(item.SkillId)??throw new InvalidOperationException($"Package {package.Id} requires unknown skill {item.SkillId}.");var state=SkillService.GetState(pilot,item.SkillId);var currentSp=SanitiseSp(state?.SkillPoints??0);var targetSp=SkillService.RequiredSp(item.SkillId,item.Level);var missing=Math.Max(0,targetSp-currentSp);var covered=Math.Min(unallocatedLeft,missing);unallocatedLeft-=covered;var owned=state?.BookOwned==true;
                plans.Add(new CareerSkillPlan(item.SkillId,definition.DisplayName,SkillService.GetLevel(pilot,item.SkillId),item.Level,currentSp,targetSp,missing,covered,owned,owned?0:SanitisePrice(MarketService.GetSellPrice(save,definition.TypeId,definition.BookFallbackPrice)),item.Direct));
            }
            var skills=plans.AsReadOnly();var remaining=skills.Sum(skill=>skill.RemainingSpAfterUnallocated);var injectorPrice=SanitisePrice(MarketService.InjectorPrice(save));
            return new ShipCareerPlan(pilot,hull,skills.All(skill=>skill.RequirementMet),skills,SanitiseSp(pilot.UnallocatedSkillPoints),injectorPrice,BuildInjectorPlan(save,pilot,remaining,injectorPrice),package,PreparedPackageService.PackagePrice(save,package));
        }

        public static IReadOnlyList<ShipCareerPlan> CreateAllPackagePlans(GameSave save,CharacterSave pilot)
        {
            if(pilot==null)throw new ArgumentNullException(nameof(pilot));return Catalog.Packages.Select(package=>CreatePackagePlan(save,pilot,package.Id)).ToList().AsReadOnly();
        }

        public static IReadOnlyList<ShipCareerPlan> CreatePackagePlansForHull(GameSave save,CharacterSave pilot,string hullId)
        {
            if(pilot==null)throw new ArgumentNullException(nameof(pilot));
            if(Catalog.GetShip(hullId)==null)throw new ArgumentException($"Unknown hull id: {hullId}",nameof(hullId));
            return Catalog.Packages.Where(package=>string.Equals(package.HullId,hullId,StringComparison.OrdinalIgnoreCase)).Select(package=>CreatePackagePlan(save,pilot,package.Id)).ToList().AsReadOnly();
        }

        static IReadOnlyList<RequiredSkill> BuildRequirementClosure(IEnumerable<SkillRequirement> directRequirements)
        {
            var byId = new Dictionary<string, RequiredSkill>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<RequiredSkill>();
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var requirement in directRequirements ?? Array.Empty<SkillRequirement>())
                AddRequirement(requirement, true, byId, ordered, expanded, visiting);

            return ordered.AsReadOnly();
        }

        static void AddRequirement(
            SkillRequirement requirement,
            bool direct,
            IDictionary<string, RequiredSkill> byId,
            ICollection<RequiredSkill> ordered,
            ISet<string> expanded,
            ISet<string> visiting)
        {
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.SkillId) || requirement.Level <= 0)
                return;

            var level = Math.Min(5, requirement.Level);
            if (!byId.TryGetValue(requirement.SkillId, out var accumulated))
            {
                accumulated = new RequiredSkill { SkillId = requirement.SkillId, Level = level, Direct = direct };
                byId.Add(requirement.SkillId, accumulated);
            }
            else
            {
                accumulated.Level = Math.Max(accumulated.Level, level);
                accumulated.Direct |= direct;
            }

            if (expanded.Contains(requirement.SkillId)) return;
            if (!visiting.Add(requirement.SkillId))
                throw new InvalidOperationException($"Cyclic skill prerequisite at {requirement.SkillId}.");

            var definition = Catalog.GetSkill(requirement.SkillId);
            if (definition == null)
                throw new InvalidOperationException($"Unknown skill prerequisite: {requirement.SkillId}.");

            foreach (var prerequisite in definition.Prerequisites ?? Array.Empty<SkillRequirement>())
                AddRequirement(prerequisite, false, byId, ordered, expanded, visiting);

            visiting.Remove(requirement.SkillId);
            expanded.Add(requirement.SkillId);
            ordered.Add(accumulated);
        }

        static IReadOnlyList<CareerInjectorTierPlan> BuildInjectorPlan(
            GameSave save,
            CharacterSave pilot,
            double requiredSp,
            double injectorPriceIsk)
        {
            if (requiredSp <= 0d) return Array.Empty<CareerInjectorTierPlan>();

            var startingTotalSp = SanitiseSp(SkillService.TotalSp(pilot));
            var spYield = SanitiseSp(MarketService.InjectorSp(save, pilot));
            if (spYield <= 0d)
                throw new InvalidOperationException("Large Skill Injector returned no skill points.");

            var injectorCount = (int)Math.Ceiling(requiredSp / spYield);
            return new List<CareerInjectorTierPlan>
            {
                new(
                    startingTotalSp,
                    startingTotalSp + spYield * injectorCount,
                    spYield,
                    injectorCount,
                    injectorPriceIsk)
            }.AsReadOnly();
        }

        static double SanitiseSp(double value)
        {
            return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0d;
        }

        static double SanitisePrice(double value)
        {
            return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0d;
        }
    }
}
