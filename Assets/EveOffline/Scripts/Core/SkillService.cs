using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOffline
{
    public static class SkillService
    {
        public const int MaxTrainingQueueEntries = 150;
        const double SpEpsilon = .001d;

        public static double RequiredSp(string skillId, int level)
        {
            var skill = Catalog.GetSkill(skillId);
            if (skill == null || level <= 0) return 0;
            level = Math.Min(level, 5);
            return Math.Ceiling(250d * skill.Rank * Math.Pow(2d, 2.5d * (level - 1)));
        }

        public static CharacterSkillSave GetState(CharacterSave pilot, string skillId, bool create = false)
        {
            if (pilot == null || string.IsNullOrWhiteSpace(skillId)) return null;
            var state = pilot.Skills?.Find(item => string.Equals(item.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
            if (state == null && create)
            {
                pilot.Skills ??= new();
                state = new CharacterSkillSave { SkillId = skillId };
                pilot.Skills.Add(state);
            }
            return state;
        }

        public static int GetLevel(CharacterSave pilot, string skillId)
        {
            var state = GetState(pilot, skillId);
            if (state == null || !state.BookOwned) return 0;
            for (var level = 5; level >= 1; level--)
                if (state.SkillPoints + SpEpsilon >= RequiredSp(skillId, level)) return level;
            return 0;
        }

        public static double AllocatedSp(CharacterSave pilot) => pilot?.Skills?.Sum(item => item.SkillPoints) ?? 0d;

        public static double TotalSp(CharacterSave pilot) => AllocatedSp(pilot) + (pilot?.UnallocatedSkillPoints ?? 0d);

        public static bool Meets(CharacterSave pilot, SkillRequirement[] requirements)
        {
            if (requirements == null) return true;
            return requirements.All(req => GetLevel(pilot, req.SkillId) >= req.Level);
        }

        public static bool TryBuyBook(GameSave save, CharacterSave pilot, string skillId, out string message)
        {
            var skill = Catalog.GetSkill(skillId);
            if (skill == null) { message = "Неизвестный навык."; return false; }
            var state = GetState(pilot, skillId, true);
            if (state.BookOwned) { message = "Книга уже изучена."; return false; }
            var price = skill.TypeId > 0 ? MarketService.GetSellPrice(save, skill.TypeId, skill.BookFallbackPrice) : skill.BookFallbackPrice;
            if (save.Isk < price) { message = $"Не хватает ISK: книга стоит {price:N0}."; return false; }
            save.Isk -= price;
            state.BookOwned = true;
            message = $"{pilot.Name}: куплена и введена книга {skill.DisplayName} за {price:N0} ISK. Обучение начнётся после prerequisites.";
            return true;
        }

        /// <summary>
        /// Migrates the legacy single active skill into the serialized queue,
        /// removes malformed/completed duplicates, and mirrors the first queue
        /// entry back into TrainingSkillId/TrainingTargetLevel for old callers.
        /// </summary>
        public static void NormalizeQueue(CharacterSave pilot)
        {
            if (pilot == null) return;
            pilot.Skills ??= new();
            pilot.TrainingQueue ??= new();

            if (pilot.TrainingQueue.Count == 0 &&
                !string.IsNullOrWhiteSpace(pilot.TrainingSkillId) &&
                pilot.TrainingTargetLevel is >= 1 and <= 5)
            {
                pilot.TrainingQueue.Add(new SkillQueueEntrySave
                {
                    SkillId = pilot.TrainingSkillId,
                    TargetLevel = pilot.TrainingTargetLevel
                });
            }

            var normalized = new List<SkillQueueEntrySave>(Math.Min(pilot.TrainingQueue.Count, MaxTrainingQueueEntries));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pilot.TrainingQueue)
            {
                if (normalized.Count >= MaxTrainingQueueEntries || entry == null || entry.TargetLevel is < 1 or > 5) continue;
                var skill = Catalog.GetSkill(entry.SkillId);
                if (skill == null) continue;
                var state = GetState(pilot, skill.Id);
                if (state?.BookOwned != true) continue;
                if (state.SkillPoints + SpEpsilon >= RequiredSp(skill.Id, entry.TargetLevel)) continue;
                var key = $"{skill.Id}\n{entry.TargetLevel}";
                if (!seen.Add(key)) continue;
                normalized.Add(new SkillQueueEntrySave { SkillId = skill.Id, TargetLevel = entry.TargetLevel });
            }

            pilot.TrainingQueue.Clear();
            pilot.TrainingQueue.AddRange(normalized);
            SyncLegacyActive(pilot);
        }

        public static IReadOnlyList<SkillQueueEntrySave> GetTrainingQueue(CharacterSave pilot)
        {
            NormalizeQueue(pilot);
            if (pilot?.TrainingQueue == null) return Array.Empty<SkillQueueEntrySave>();
            return pilot.TrainingQueue;
        }

        public static bool TryEnqueueNextLevel(CharacterSave pilot, string skillId, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            var plannedLevel = ProjectedLevelAtQueueEnd(pilot, skillId);
            if (plannedLevel >= 5) { message = "Навык уже изучен или поставлен в очередь до V уровня."; return false; }
            return TryEnqueueToTarget(pilot, skillId, plannedLevel + 1, out message);
        }

        public static bool TryEnqueueToTarget(CharacterSave pilot, string skillId, int targetLevel, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            var skill = Catalog.GetSkill(skillId);
            var state = GetState(pilot, skillId);
            if (skill == null) { message = "Неизвестный навык."; return false; }
            if (state?.BookOwned != true) { message = "Сначала купи книгу навыка."; return false; }
            if (targetLevel is < 1 or > 5) { message = "Целевой уровень должен быть от I до V."; return false; }
            if (!MeetsProjectedAtQueueEnd(pilot, skill.Prerequisites))
            {
                message = "Не выполнены prerequisite-навыки к моменту этого пункта очереди.";
                return false;
            }

            var currentLevel = GetLevel(pilot, skill.Id);
            if (targetLevel <= currentLevel)
            {
                message = $"{skill.DisplayName} уже изучен до {ToRoman(currentLevel)}.";
                return false;
            }

            var plannedLevel = ProjectedLevelAtQueueEnd(pilot, skill.Id);
            if (targetLevel <= plannedLevel)
            {
                message = $"{skill.DisplayName} {ToRoman(targetLevel)} уже есть в очереди или покрыт более высоким уровнем.";
                return false;
            }

            var entriesNeeded = targetLevel - plannedLevel;
            if (pilot.TrainingQueue.Count + entriesNeeded > MaxTrainingQueueEntries)
            {
                message = $"В очереди может быть не больше {MaxTrainingQueueEntries} пунктов.";
                return false;
            }

            var firstAddedIndex = pilot.TrainingQueue.Count;
            for (var level = plannedLevel + 1; level <= targetLevel; level++)
                pilot.TrainingQueue.Add(new SkillQueueEntrySave { SkillId = skill.Id, TargetLevel = level });
            SyncLegacyActive(pilot);

            var prefix = firstAddedIndex == 0 ? "начал обучение" : "добавил в очередь";
            message = entriesNeeded == 1
                ? $"{pilot.Name} {prefix} {skill.DisplayName} {ToRoman(targetLevel)}."
                : $"{pilot.Name} {prefix} {skill.DisplayName} до {ToRoman(targetLevel)} ({entriesNeeded} уровня).";
            return true;
        }

        public static bool TryEnqueueToLevel(CharacterSave pilot, string skillId, int targetLevel, out string message) =>
            TryEnqueueToTarget(pilot, skillId, targetLevel, out message);

        // Compatibility: the old action now appends one level instead of replacing
        // the currently trained skill.
        public static bool TryTrainNextLevel(CharacterSave pilot, string skillId, out string message) =>
            TryEnqueueNextLevel(pilot, skillId, out message);

        public static bool TryRemoveQueueEntry(CharacterSave pilot, int queueIndex, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            if (queueIndex < 0 || queueIndex >= pilot.TrainingQueue.Count)
            {
                message = "Пункт очереди не найден.";
                return false;
            }

            var removed = pilot.TrainingQueue[queueIndex];
            pilot.TrainingQueue.RemoveAt(queueIndex);
            SyncLegacyActive(pilot);
            var skill = Catalog.GetSkill(removed.SkillId);
            message = $"{pilot.Name}: из очереди удалён {skill?.DisplayName ?? removed.SkillId} {ToRoman(removed.TargetLevel)}.";
            return true;
        }

        public static bool TryRemoveQueueEntry(CharacterSave pilot, string skillId, int targetLevel, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            var index = pilot.TrainingQueue.FindIndex(entry =>
                entry != null && entry.TargetLevel == targetLevel &&
                string.Equals(entry.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
            return TryRemoveQueueEntry(pilot, index, out message);
        }

        public static bool TryRemoveTrainingQueueEntry(CharacterSave pilot, int queueIndex, out string message) =>
            TryRemoveQueueEntry(pilot, queueIndex, out message);

        /// <summary>
        /// Moves one queue entry by exactly one position. The candidate order is
        /// validated in full before the live queue is touched, so a rejected move
        /// leaves both the serialized queue and its legacy active-entry mirror intact.
        /// </summary>
        public static bool TryMoveTrainingQueueEntry(
            CharacterSave pilot,
            int queueIndex,
            int direction,
            out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            if (direction is not (-1 or 1))
            {
                message = "Пункт очереди можно сдвинуть только на одну позицию вверх или вниз.";
                return false;
            }

            var queue = pilot.TrainingQueue;
            if (queue == null || queueIndex < 0 || queueIndex >= queue.Count)
            {
                message = "Пункт очереди не найден.";
                return false;
            }

            var targetIndex = queueIndex + direction;
            if (targetIndex < 0 || targetIndex >= queue.Count)
            {
                message = direction < 0
                    ? "Этот навык уже находится в начале очереди."
                    : "Этот навык уже находится в конце очереди.";
                return false;
            }

            // Validate a reordered copy first. Do not normalize the live pilot here:
            // even normalization would violate the no-mutation-on-error contract.
            var candidate = new List<SkillQueueEntrySave>(queue);
            (candidate[queueIndex], candidate[targetIndex]) = (candidate[targetIndex], candidate[queueIndex]);
            if (!TryValidateTrainingQueueOrder(pilot, candidate, out message)) return false;

            var moved = queue[queueIndex];
            (queue[queueIndex], queue[targetIndex]) = (queue[targetIndex], queue[queueIndex]);
            SyncLegacyActive(pilot);
            var skill = Catalog.GetSkill(moved.SkillId);
            message = $"{pilot.Name}: {skill?.DisplayName ?? moved.SkillId} {ToRoman(moved.TargetLevel)} перемещён " +
                      (direction < 0 ? "выше." : "ниже.");
            return true;
        }

        public static bool TryMoveTrainingQueueEntryUp(CharacterSave pilot, int queueIndex, out string message) =>
            TryMoveTrainingQueueEntry(pilot, queueIndex, -1, out message);

        public static bool TryMoveTrainingQueueEntryDown(CharacterSave pilot, int queueIndex, out string message) =>
            TryMoveTrainingQueueEntry(pilot, queueIndex, 1, out message);

        public static bool TryClearTrainingQueue(CharacterSave pilot, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            if (pilot.TrainingQueue.Count == 0) { message = "Очередь обучения уже пуста."; return false; }
            var removed = pilot.TrainingQueue.Count;
            pilot.TrainingQueue.Clear();
            SyncLegacyActive(pilot);
            message = $"{pilot.Name}: очередь обучения очищена ({removed} пунктов).";
            return true;
        }

        public static void ClearTrainingQueue(CharacterSave pilot)
        {
            if (pilot == null) return;
            pilot.TrainingQueue ??= new();
            pilot.TrainingQueue.Clear();
            SyncLegacyActive(pilot);
        }

        public static bool TryBuyLargeSkillInjector(GameSave save, CharacterSave pilot, out string message)
        {
            if (save == null || pilot == null) { message = "Пилот не выбран."; return false; }
            var price = MarketService.InjectorPrice(save);
            if (price <= 0) { message = "Цена Large Skill Injector сейчас недоступна."; return false; }
            if (save.Isk < price) { message = $"Не хватает ISK: Large Skill Injector стоит {price:N0}."; return false; }
            var injectedSp = MarketService.InjectorSp(save, pilot);
            save.Isk -= price;
            pilot.UnallocatedSkillPoints += injectedSp;
            message = $"{pilot.Name}: куплен Large Skill Injector, {injectedSp:N0} SP добавлено в свободный запас.";
            return true;
        }

        public static bool TryBuySmallSkillInjector(GameSave save, CharacterSave pilot, out string message)
        {
            if (save == null || pilot == null) { message = "Пилот не выбран."; return false; }
            var price = MarketService.SmallInjectorPrice(save);
            if (price <= 0) { message = "Цена Small Skill Injector сейчас недоступна."; return false; }
            if (save.Isk < price) { message = $"Не хватает ISK: Small Skill Injector стоит {price:N0}."; return false; }
            var injectedSp = MarketService.SmallInjectorSp(save, pilot);
            save.Isk -= price;
            pilot.UnallocatedSkillPoints += injectedSp;
            message = $"{pilot.Name}: куплен Small Skill Injector, {injectedSp:N0} SP добавлено в свободный запас.";
            return true;
        }

        // Compatibility action used by the existing UI: invest in exactly the
        // next unfinished level of the selected skill.
        public static bool TryApplyUnallocated(CharacterSave pilot, string skillId, out string message)
        {
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            var currentLevel = GetLevel(pilot, skillId);
            if (currentLevel >= 5) { message = "Навык уже V уровня."; return false; }
            return TryApplyUnallocatedToLevel(pilot, skillId, currentLevel + 1, out _, out message);
        }

        public static bool TryApplyUnallocatedToLevel(
            CharacterSave pilot,
            string skillId,
            int targetLevel,
            out string message) =>
            TryApplyUnallocatedToLevel(pilot, skillId, targetLevel, out _, out message);

        /// <summary>
        /// Invests as much of the pilot's free-SP reserve as possible into one
        /// explicitly selected next skill level. This does not enqueue a skill
        /// and never distributes SP to any other level or skill.
        /// </summary>
        public static bool TryApplyUnallocatedToLevel(
            CharacterSave pilot,
            string skillId,
            int targetLevel,
            out double appliedSp,
            out string message)
        {
            appliedSp = 0d;
            if (pilot == null) { message = "Пилот не выбран."; return false; }
            NormalizeQueue(pilot);
            var skill = Catalog.GetSkill(skillId);
            var state = GetState(pilot, skillId);
            if (skill == null || state?.BookOwned != true) { message = "Сначала купи и введи книгу навыка."; return false; }
            if (targetLevel is < 1 or > 5) { message = "Целевой уровень должен быть от I до V."; return false; }
            if (!Meets(pilot, skill.Prerequisites)) { message = "Не выполнены prerequisite-навыки."; return false; }
            var current = GetLevel(pilot, skillId);
            if (current >= 5) { message = "Навык уже V уровня."; return false; }
            if (targetLevel != current + 1)
            {
                message = $"Можно вложить SP только в следующий уровень: {skill.DisplayName} {ToRoman(current + 1)}.";
                return false;
            }
            if (pilot.UnallocatedSkillPoints <= SpEpsilon) { message = "У пилота нет свободных SP."; return false; }

            var targetSp = RequiredSp(skillId, targetLevel);
            appliedSp = Math.Min(pilot.UnallocatedSkillPoints, Math.Max(0, targetSp - state.SkillPoints));
            if (appliedSp <= SpEpsilon) { message = "Этот уровень уже полностью изучен."; return false; }
            state.SkillPoints += appliedSp;
            pilot.UnallocatedSkillPoints = Math.Max(0d, pilot.UnallocatedSkillPoints - appliedSp);
            var completed = state.SkillPoints + SpEpsilon >= targetSp;
            if (completed)
            {
                state.SkillPoints = targetSp;
                RemoveCompletedQueueEntries(pilot);
                SyncLegacyActive(pilot);
            }
            message = completed
                ? $"{pilot.Name}: {skill.DisplayName} {ToRoman(targetLevel)} изучен за {appliedSp:N0} свободных SP."
                : $"{pilot.Name}: в {skill.DisplayName} {ToRoman(targetLevel)} вложено {appliedSp:N0} свободных SP.";
            return true;
        }

        public static void TickAll(GameSave save, double realSeconds, Action<string> notify = null)
        {
            if (save?.Characters == null || realSeconds <= 0) return;
            var spPerSecond = Catalog.PerfectTrainingSpPerMinute / 60d;
            if (spPerSecond <= 0) return;

            foreach (var pilot in save.Characters)
            {
                NormalizeQueue(pilot);
                var secondsLeft = realSeconds;
                while (secondsLeft > 0 && pilot.TrainingQueue.Count > 0)
                {
                    var active = pilot.TrainingQueue[0];
                    var skill = Catalog.GetSkill(active.SkillId);
                    var state = GetState(pilot, active.SkillId, true);
                    if (skill == null || !state.BookOwned || !Meets(pilot, skill.Prerequisites))
                    {
                        pilot.TrainingQueue.RemoveAt(0);
                        SyncLegacyActive(pilot);
                        notify?.Invoke($"{pilot.Name}: недоступный пункт удалён из очереди обучения.");
                        continue;
                    }

                    var targetSp = RequiredSp(skill.Id, active.TargetLevel);
                    var missingSp = Math.Max(0, targetSp - state.SkillPoints);
                    var secondsNeeded = missingSp / spPerSecond;
                    if (secondsLeft + 1e-9d < secondsNeeded)
                    {
                        state.SkillPoints = Math.Min(targetSp, state.SkillPoints + spPerSecond * secondsLeft);
                        secondsLeft = 0;
                        continue;
                    }

                    state.SkillPoints = targetSp;
                    secondsLeft = Math.Max(0, secondsLeft - secondsNeeded);
                    pilot.TrainingQueue.RemoveAt(0);
                    SyncLegacyActive(pilot);
                    notify?.Invoke($"{pilot.Name}: {skill.DisplayName} {ToRoman(active.TargetLevel)} изучен.");
                }
            }
        }

        /// <summary>Remaining time for the active (first) queue entry.</summary>
        public static double TrainingSecondsLeft(CharacterSave pilot)
        {
            NormalizeQueue(pilot);
            if (pilot?.TrainingQueue == null || pilot.TrainingQueue.Count == 0) return 0;
            var entry = pilot.TrainingQueue[0];
            var state = GetState(pilot, entry.SkillId);
            var remaining = Math.Max(0, RequiredSp(entry.SkillId, entry.TargetLevel) - (state?.SkillPoints ?? 0));
            return SecondsForSp(remaining);
        }

        public static double TrainingSecondsLeft(CharacterSave pilot, bool includeFullQueue) =>
            includeFullQueue ? TotalTrainingSecondsLeft(pilot) : TrainingSecondsLeft(pilot);

        /// <summary>Remaining time for all serialized queue entries in order.</summary>
        public static double TotalTrainingSecondsLeft(CharacterSave pilot)
        {
            NormalizeQueue(pilot);
            if (pilot?.TrainingQueue == null || pilot.TrainingQueue.Count == 0) return 0;
            var projectedSp = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var totalSp = 0d;
            foreach (var entry in pilot.TrainingQueue)
            {
                if (entry == null || Catalog.GetSkill(entry.SkillId) == null) continue;
                if (!projectedSp.TryGetValue(entry.SkillId, out var fromSp))
                    fromSp = GetState(pilot, entry.SkillId)?.SkillPoints ?? 0d;
                var targetSp = RequiredSp(entry.SkillId, entry.TargetLevel);
                totalSp += Math.Max(0, targetSp - fromSp);
                projectedSp[entry.SkillId] = Math.Max(fromSp, targetSp);
            }
            return SecondsForSp(totalSp);
        }

        public static double TrainingQueueSecondsLeft(CharacterSave pilot) => TotalTrainingSecondsLeft(pilot);

        static double SecondsForSp(double skillPoints)
        {
            if (skillPoints <= 0 || Catalog.PerfectTrainingSpPerMinute <= 0) return 0;
            return skillPoints / Catalog.PerfectTrainingSpPerMinute * 60d;
        }

        static int ProjectedLevelAtQueueEnd(CharacterSave pilot, string skillId)
        {
            var projected = GetLevel(pilot, skillId);
            if (pilot?.TrainingQueue == null) return projected;
            foreach (var entry in pilot.TrainingQueue)
                if (entry != null && string.Equals(entry.SkillId, skillId, StringComparison.OrdinalIgnoreCase))
                    projected = Math.Max(projected, entry.TargetLevel);
            return projected;
        }

        static bool MeetsProjectedAtQueueEnd(CharacterSave pilot, SkillRequirement[] requirements)
        {
            if (requirements == null) return true;
            return requirements.All(requirement => ProjectedLevelAtQueueEnd(pilot, requirement.SkillId) >= requirement.Level);
        }

        static bool TryValidateTrainingQueueOrder(
            CharacterSave pilot,
            IReadOnlyList<SkillQueueEntrySave> queue,
            out string message)
        {
            if (queue == null) { message = "Очередь обучения недоступна."; return false; }
            if (queue.Count > MaxTrainingQueueEntries)
            {
                message = $"В очереди может быть не больше {MaxTrainingQueueEntries} пунктов.";
                return false;
            }

            var projectedLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int ProjectedLevel(string skillId)
            {
                if (!projectedLevels.TryGetValue(skillId, out var level))
                {
                    level = GetLevel(pilot, skillId);
                    projectedLevels[skillId] = level;
                }
                return level;
            }

            for (var index = 0; index < queue.Count; index++)
            {
                var entry = queue[index];
                if (entry == null)
                {
                    message = $"Пункт {index + 1}: повреждённая запись навыка.";
                    return false;
                }

                var skill = Catalog.GetSkill(entry.SkillId);
                if (skill == null)
                {
                    message = $"Пункт {index + 1}: неизвестный навык.";
                    return false;
                }
                if (entry.TargetLevel is < 1 or > 5)
                {
                    message = $"{skill.DisplayName}: уровень должен быть от I до V.";
                    return false;
                }

                var state = GetState(pilot, skill.Id);
                if (state?.BookOwned != true)
                {
                    message = $"{skill.DisplayName}: сначала нужна книга навыка.";
                    return false;
                }

                foreach (var requirement in skill.Prerequisites ?? Array.Empty<SkillRequirement>())
                {
                    if (requirement == null || ProjectedLevel(requirement.SkillId) >= requirement.Level) continue;
                    var prerequisite = Catalog.GetSkill(requirement.SkillId);
                    message = $"{skill.DisplayName} {ToRoman(entry.TargetLevel)} нельзя поставить раньше " +
                              $"{prerequisite?.DisplayName ?? requirement.SkillId} {ToRoman(requirement.Level)}.";
                    return false;
                }

                var previousLevel = ProjectedLevel(skill.Id);
                if (entry.TargetLevel != previousLevel + 1)
                {
                    message = entry.TargetLevel <= previousLevel
                        ? $"{skill.DisplayName} {ToRoman(entry.TargetLevel)} уже достигнут до этого пункта очереди."
                        : $"Перед {skill.DisplayName} {ToRoman(entry.TargetLevel)} должен стоять уровень {ToRoman(previousLevel + 1)}.";
                    return false;
                }
                projectedLevels[skill.Id] = entry.TargetLevel;
            }

            message = string.Empty;
            return true;
        }

        static void RemoveCompletedQueueEntries(CharacterSave pilot)
        {
            if (pilot?.TrainingQueue == null) return;
            pilot.TrainingQueue.RemoveAll(entry =>
            {
                if (entry == null) return true;
                var state = GetState(pilot, entry.SkillId);
                return state != null && state.SkillPoints + SpEpsilon >= RequiredSp(entry.SkillId, entry.TargetLevel);
            });
        }

        static void SyncLegacyActive(CharacterSave pilot)
        {
            if (pilot == null) return;
            if (pilot.TrainingQueue != null && pilot.TrainingQueue.Count > 0)
            {
                pilot.TrainingSkillId = pilot.TrainingQueue[0].SkillId;
                pilot.TrainingTargetLevel = pilot.TrainingQueue[0].TargetLevel;
                return;
            }
            pilot.TrainingSkillId = string.Empty;
            pilot.TrainingTargetLevel = 0;
        }

        public static string ToRoman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "–" };
    }
}
