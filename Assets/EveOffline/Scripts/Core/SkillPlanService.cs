using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    public sealed class SkillPlanTargetResult
    {
        public string PilotId;
        public string PilotName;
        public int EntriesCopied;
        public int EntriesSkipped;
        public int BooksPurchased;
        public double BookCostIsk;
    }

    public sealed class SkillPlanCopyResult
    {
        public bool Success;
        public string Message;
        public int TargetCount;
        public int TargetsChanged;
        public int EntriesCopied;
        public int EntriesSkipped;
        public int BooksPurchased;
        public double BookCostIsk;
        public List<SkillPlanTargetResult> Targets = new();
    }

    public sealed class WorkerSkillStateSyncResult
    {
        public bool Success;
        public string Message;
        public string SourcePilotId;
        public string SourcePilotName;
        public int TargetCount;
        public int TargetsChanged;
    }

    /// <summary>
    /// Keeps Pilot 01 independent. Pilot 02 may either provide the legacy
    /// replace-all queue plan or act as the exact learned-skill/SP/queue template
    /// for Pilots 03-10. Public apply operations are preflighted before mutation.
    /// </summary>
    public static class SkillPlanService
    {
        sealed class PlannedTarget
        {
            public CharacterSave Live;
            public CharacterSave Preview;
            public readonly HashSet<string> Books = new(StringComparer.OrdinalIgnoreCase);
            public SkillPlanTargetResult Result;
        }

        /// <summary>
        /// Previews an exact, free skill-state synchronization from Characters[1]
        /// (Pilot 02) to Characters[2..]. Identity, ship and deployment fields are
        /// outside the synchronization contract.
        /// </summary>
        public static WorkerSkillStateSyncResult PreviewSyncWorkerSkillStateFromPilot02(GameSave save)
        {
            return BuildWorkerSkillStateSync(save,out _,out _);
        }

        /// <summary>
        /// Replaces every recipient worker's complete skill state with a deep copy
        /// of Pilot 02: learned skills/books/SP, unallocated SP and the full queue
        /// including its active-entry mirror. Pilot 01, Pilot 02, wallet, identity,
        /// assigned ship and deployment flags are never changed.
        /// </summary>
        public static bool TrySyncWorkerSkillStateFromPilot02(GameSave save,out WorkerSkillStateSyncResult result)
        {
            result=BuildWorkerSkillStateSync(save,out var source,out var targets);
            if(!result.Success)return false;
            foreach(var target in targets)
            {
                target.Skills=CloneSkillsExact(source.Skills);
                target.UnallocatedSkillPoints=source.UnallocatedSkillPoints;
                target.TrainingQueue=CloneQueueExact(source.TrainingQueue);
                target.TrainingSkillId=source.TrainingSkillId;
                target.TrainingTargetLevel=source.TrainingTargetLevel;
            }
            result.Message=$"Полное состояние навыков Пилота 02 синхронизировано у {result.TargetCount} рабочих пилотов; изменено {result.TargetsChanged}.";
            return true;
        }

        public static SkillPlanCopyResult PreviewCopyQueueToOtherWorkers(GameSave save, CharacterSave source, bool buyMissingBooks) =>
            BuildMassPlan(save, source, buyMissingBooks, false, out _);

        public static bool TryCopyQueueToOtherWorkers(GameSave save, CharacterSave source, bool buyMissingBooks, out SkillPlanCopyResult result)
        {
            result = BuildMassPlan(save, source, buyMissingBooks, true, out var plans);
            if (!result.Success) return false;
            Commit(save, result, plans);
            return true;
        }

        public static bool TryCopyQueue(GameSave save, CharacterSave source, CharacterSave target, bool buyMissingBooks, out SkillPlanCopyResult result)
        {
            result = BuildPlan(save, source, new[] { target }, buyMissingBooks, true, out var plans);
            if (!result.Success) return false;
            Commit(save, result, plans);
            return true;
        }

        static SkillPlanCopyResult BuildMassPlan(GameSave save, CharacterSave source, bool buyMissingBooks, bool requireAffordable, out List<PlannedTarget> plans)
        {
            plans = new();
            if (save?.Characters == null || save.Characters.Count < 2)
                return Failure("Нет рабочих пилотов для копирования плана.");
            var workerTemplate = save.Characters[1];
            if (source == null || workerTemplate == null || !SamePilot(source, workerTemplate))
                return Failure("Общую очередь рабочих можно копировать только от Пилота 02 к Пилотам 03–10.");
            var targets = save.Characters.Skip(2).Where(pilot => pilot != null).ToArray();
            return BuildPlan(save, source, targets, buyMissingBooks, requireAffordable, out plans);
        }

        static WorkerSkillStateSyncResult BuildWorkerSkillStateSync(GameSave save,out CharacterSave source,out CharacterSave[] targets)
        {
            source=null;targets=Array.Empty<CharacterSave>();
            var result=new WorkerSkillStateSyncResult();
            if(save?.Characters==null||save.Characters.Count<2||save.Characters[1]==null)
            {
                result.Message="Пилот 02 — шаблон рабочих навыков — не найден.";
                return result;
            }
            source=save.Characters[1];
            targets=save.Characters.Skip(2).Where(pilot=>pilot!=null).ToArray();
            if(targets.Length==0)
            {
                result.Message="Пилоты 03–10 для синхронизации навыков не найдены.";
                return result;
            }
            result.SourcePilotId=source.Id??string.Empty;
            result.SourcePilotName=source.Name??string.Empty;
            result.TargetCount=targets.Length;
            var template=source;
            result.TargetsChanged=targets.Count(target=>!SkillStateEquals(template,target));
            result.Success=true;
            result.Message=$"Можно синхронизировать полное состояние навыков у {result.TargetCount} рабочих пилотов; изменится {result.TargetsChanged}.";
            return result;
        }

        static SkillPlanCopyResult BuildPlan(GameSave save, CharacterSave source, IReadOnlyCollection<CharacterSave> targets, bool buyMissingBooks, bool requireAffordable, out List<PlannedTarget> plans)
        {
            plans = new();
            if (save?.Characters == null || source == null) return Failure("Пилот-источник не выбран.");
            if (IsCommander(save, source)) return Failure("План руководителя всегда индивидуальный и не копируется.");
            if (targets == null || targets.Count == 0) return Failure("Других рабочих пилотов нет.");
            if (targets.Any(target => target == null || IsCommander(save, target) || SamePilot(source, target)))
                return Failure("Руководитель и пилот-источник не могут быть получателями копии.");

            var sourcePreview = ClonePilot(source);
            var sourceQueue = SkillService.GetTrainingQueue(sourcePreview)
                .Select(entry => new SkillQueueEntrySave { SkillId = entry.SkillId, TargetLevel = entry.TargetLevel })
                .ToList();
            if (sourceQueue.Count == 0) return Failure("Очередь выбранного пилота пуста — сначала составь план.");

            var result = new SkillPlanCopyResult { TargetCount = targets.Count };
            foreach (var target in targets)
            {
                var planned = PlanTarget(save, sourceQueue, target, buyMissingBooks, out var error);
                if (planned == null)
                    return Failure($"{target.Name}: {error}", result);
                plans.Add(planned);
                result.Targets.Add(planned.Result);
                result.EntriesCopied += planned.Result.EntriesCopied;
                result.EntriesSkipped += planned.Result.EntriesSkipped;
                result.BooksPurchased += planned.Result.BooksPurchased;
                result.BookCostIsk += planned.Result.BookCostIsk;
            }

            if (requireAffordable && buyMissingBooks && save.Isk + .001d < result.BookCostIsk)
                return Failure($"Для книг всем рабочим пилотам нужно {result.BookCostIsk:N0} ISK, в кошельке {save.Isk:N0} ISK.", result);

            result.Success = true;
            result.Message = buyMissingBooks
                ? $"План готов для {result.TargetCount} пилотов: {result.EntriesCopied} пунктов, книг {result.BooksPurchased} на {result.BookCostIsk:N0} ISK."
                : $"План готов для {result.TargetCount} пилотов: {result.EntriesCopied} пунктов, без покупки пропущено {result.EntriesSkipped}.";
            return result;
        }

        static PlannedTarget PlanTarget(GameSave save, IReadOnlyList<SkillQueueEntrySave> sourceQueue, CharacterSave target, bool buyMissingBooks, out string error)
        {
            error = null;
            var preview = ClonePilot(target);
            SkillService.ClearTrainingQueue(preview);
            var plan = new PlannedTarget
            {
                Live = target,
                Preview = preview,
                Result = new SkillPlanTargetResult { PilotId = target.Id, PilotName = target.Name }
            };

            foreach (var templateEntry in sourceQueue)
            {
                var skill = Catalog.GetSkill(templateEntry.SkillId);
                if (skill == null || templateEntry.TargetLevel is < 1 or > 5)
                {
                    if (buyMissingBooks) { error = "в шаблоне найден неизвестный пункт."; return null; }
                    plan.Result.EntriesSkipped++;
                    continue;
                }

                if (SkillService.GetLevel(preview, skill.Id) >= templateEntry.TargetLevel)
                {
                    plan.Result.EntriesSkipped++;
                    continue;
                }

                var state = SkillService.GetState(preview, skill.Id, true);
                if (!state.BookOwned)
                {
                    if (!buyMissingBooks)
                    {
                        plan.Result.EntriesSkipped++;
                        continue;
                    }
                    state.BookOwned = true;
                    if (plan.Books.Add(skill.Id))
                    {
                        plan.Result.BooksPurchased++;
                        plan.Result.BookCostIsk += skill.TypeId > 0
                            ? MarketService.GetSellPrice(save, skill.TypeId, skill.BookFallbackPrice)
                            : skill.BookFallbackPrice;
                    }
                }

                var before = SkillService.GetTrainingQueue(preview).Count;
                if (!SkillService.TryEnqueueToTarget(preview, skill.Id, templateEntry.TargetLevel, out var queueMessage))
                {
                    if (buyMissingBooks) { error = queueMessage; return null; }
                    plan.Result.EntriesSkipped++;
                    continue;
                }
                plan.Result.EntriesCopied += SkillService.GetTrainingQueue(preview).Count - before;
            }

            return plan;
        }

        static void Commit(GameSave save, SkillPlanCopyResult result, IReadOnlyList<PlannedTarget> plans)
        {
            if (result.BooksPurchased > 0) save.Isk = Math.Max(0d, save.Isk - result.BookCostIsk);
            foreach (var plan in plans)
            {
                foreach (var skillId in plan.Books)
                    SkillService.GetState(plan.Live, skillId, true).BookOwned = true;
                plan.Live.TrainingQueue = plan.Preview.TrainingQueue
                    .Select(entry => new SkillQueueEntrySave { SkillId = entry.SkillId, TargetLevel = entry.TargetLevel })
                    .ToList();
                plan.Live.TrainingSkillId = plan.Preview.TrainingSkillId;
                plan.Live.TrainingTargetLevel = plan.Preview.TrainingTargetLevel;
            }
            result.Success = true;
            result.TargetsChanged = plans.Count;
            result.Message = $"План полностью заменён у {plans.Count} рабочих пилотов: {result.EntriesCopied} пунктов" +
                (result.BooksPurchased > 0 ? $", куплено книг {result.BooksPurchased} за {result.BookCostIsk:N0} ISK." :
                 result.EntriesSkipped > 0 ? $", без покупки пропущено {result.EntriesSkipped}." : ".");
        }

        static CharacterSave ClonePilot(CharacterSave pilot) => new()
        {
            Id = pilot.Id,
            Name = pilot.Name,
            AssignedShipUid = pilot.AssignedShipUid,
            DeployOnLaunch = pilot.DeployOnLaunch,
            UnallocatedSkillPoints = pilot.UnallocatedSkillPoints,
            TrainingSkillId = pilot.TrainingSkillId,
            TrainingTargetLevel = pilot.TrainingTargetLevel,
            Skills = (pilot.Skills ?? new()).Where(state => state != null).Select(state => new CharacterSkillSave
            {
                SkillId = state.SkillId,
                BookOwned = state.BookOwned,
                SkillPoints = state.SkillPoints
            }).ToList(),
            TrainingQueue = (pilot.TrainingQueue ?? new()).Where(entry => entry != null).Select(entry => new SkillQueueEntrySave
            {
                SkillId = entry.SkillId,
                TargetLevel = entry.TargetLevel
            }).ToList()
        };

        static List<CharacterSkillSave> CloneSkillsExact(List<CharacterSkillSave> source)
        {
            return source?.Select(state=>state==null?null:new CharacterSkillSave
            {
                SkillId=state.SkillId,
                BookOwned=state.BookOwned,
                SkillPoints=state.SkillPoints
            }).ToList();
        }

        static List<SkillQueueEntrySave> CloneQueueExact(List<SkillQueueEntrySave> source)
        {
            return source?.Select(entry=>entry==null?null:new SkillQueueEntrySave
            {
                SkillId=entry.SkillId,
                TargetLevel=entry.TargetLevel
            }).ToList();
        }

        static bool SkillStateEquals(CharacterSave source,CharacterSave target)
        {
            if(source==null||target==null||!source.UnallocatedSkillPoints.Equals(target.UnallocatedSkillPoints)||
               !string.Equals(source.TrainingSkillId,target.TrainingSkillId,StringComparison.Ordinal)||
               source.TrainingTargetLevel!=target.TrainingTargetLevel)return false;
            var sourceSkills=source.Skills;var targetSkills=target.Skills;
            if(sourceSkills==null||targetSkills==null)
            {
                if(!ReferenceEquals(sourceSkills,targetSkills))return false;
            }
            else
            {
                if(sourceSkills.Count!=targetSkills.Count)return false;
                for(var index=0;index<sourceSkills.Count;index++)
                {
                    var left=sourceSkills[index];var right=targetSkills[index];
                    if(left==null||right==null)
                    {
                        if(!ReferenceEquals(left,right))return false;
                        continue;
                    }
                    if(!string.Equals(left.SkillId,right.SkillId,StringComparison.Ordinal)||left.BookOwned!=right.BookOwned||!left.SkillPoints.Equals(right.SkillPoints))return false;
                }
            }
            var sourceQueue=source.TrainingQueue;var targetQueue=target.TrainingQueue;
            if(sourceQueue==null||targetQueue==null)return ReferenceEquals(sourceQueue,targetQueue);
            if(sourceQueue.Count!=targetQueue.Count)return false;
            for(var index=0;index<sourceQueue.Count;index++)
            {
                var left=sourceQueue[index];var right=targetQueue[index];
                if(left==null||right==null)
                {
                    if(!ReferenceEquals(left,right))return false;
                    continue;
                }
                if(!string.Equals(left.SkillId,right.SkillId,StringComparison.Ordinal)||left.TargetLevel!=right.TargetLevel)return false;
            }
            return true;
        }

        static bool IsCommander(GameSave save, CharacterSave pilot) =>
            save?.Characters?.Count > 0 && SamePilot(save.Characters[0], pilot);

        static bool SamePilot(CharacterSave left, CharacterSave right) =>
            ReferenceEquals(left, right) || left != null && right != null &&
            !string.IsNullOrWhiteSpace(left.Id) && string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);

        static SkillPlanCopyResult Failure(string message, SkillPlanCopyResult partial = null)
        {
            var result = partial ?? new SkillPlanCopyResult();
            result.Success = false;
            result.Message = message;
            return result;
        }
    }
}
