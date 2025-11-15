using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Контроллер одной планеты в экономической симуляции.
	/// Подтягивает данные из конфигов/датабаз и хранит текущее состояние планеты.
	/// </summary>
	[DisallowMultipleComponent]
	public class PlanetController : MonoBehaviour
	{
		[Header("Идентификатор планеты")]
		[Tooltip("ID планеты из конфигов (planet.json / planet_type.json и др.).")]
		[SerializeField] private string planetId;

		/// <summary>ID планеты из конфигов.</summary>
		public string PlanetId => planetId;

		[Serializable]
		public class ProcessSlotInfo
		{
			[Tooltip("Имя слота процесса (например, PS_Universal).")]
			public string slotName;

			[Tooltip("Количество слотов этого типа для планеты.")]
			public int slotCount = 1;

			[Tooltip("Штраф (в процентах), уже с учётом +100%. Например, 140 = 140%.")]
			public float penaltyPercent = 100f;

			[Tooltip("ID текущего рецепта, запущенного в этом слоте (если есть).")]
			public string currentRecipeId;

			[Tooltip("Сколько тиков осталось до завершения процесса в этом слоте.")]
			public int ticksRemaining;

			public bool IsBusy => !string.IsNullOrEmpty(currentRecipeId) && ticksRemaining > 0;
		}

		[Header("Процессные слоты (генерируется из конфигов)")]
		[SerializeField] private List<ProcessSlotInfo> processSlots =
			new List<ProcessSlotInfo>();

		/// <summary>Список слотов процессов для этой планеты.</summary>
		public IReadOnlyList<ProcessSlotInfo> ProcessSlots => processSlots;

		[Serializable]
		public class ResourceState
		{
			[Tooltip("ID ресурса, например PR_Workers.")]
			public string resourceId;

			[Tooltip("Человекочитаемое имя ресурса.")]
			public string resourceName;

			[Tooltip("Стартовое количество при создании планеты.")]
			public float startAmount;

			[Tooltip("Текущее количество ресурса на планете.")]
			public float currentAmount;

			[Tooltip("Целевой запас ресурса, который планета считает комфортным.")]
			public float targetAmount;

			[Tooltip("Нижний порог запаса, ниже которого считаем, что ресурса критически мало.")]
			public float warningAmount;

			[Tooltip("Базовая цена ресурса из planet_resource_database (base_cost).")]
			public float basePrice;

			[Tooltip("Текущая цена ресурса на планете.")]
			public float currentPrice;
		}

		[Header("Ресурсы планеты (генерируется из конфигов)")]
		[SerializeField] private List<ResourceState> resources = new List<ResourceState>();

		/// <summary>Текущее состояние ресурсов на планете.</summary>
		public IReadOnlyList<ResourceState> Resources => resources;

		private void Awake()
		{
			// В редакторе при генерации галактики не пытаемся регистрировать и не ругаемся
			if (!Application.isPlaying)
			{
				return;
			}

			// Гарантируем наличие менеджера времени и галактики без ручной привязки
			_ = PlanetTimeManager.Instance;
			_ = GalaxyManager.Instance;

			if (string.IsNullOrWhiteSpace(planetId))
			{
				Debug.LogWarning($"[PlanetController] У объекта '{name}' не задан planetId. Конфиг не сможет подцепиться.");
			}
			else
			{
				GalaxyManager.Instance.RegisterPlanet(this);
			}
		}

		private void OnDestroy()
		{
			if (!string.IsNullOrWhiteSpace(planetId) && GalaxyManager.HasInstance)
			{
				GalaxyManager.Instance.UnregisterPlanet(this);
			}
		}

		// ====== Тиковая логика экономики ======

		private Dictionary<string, float> _incomePerTick;
		private Dictionary<string, float> _consumptionPerTick;
		private Dictionary<string, float> _needAnytimePerPlanet;
		private bool _economyLoaded;

		private void OnEnable()
		{
			var tm = PlanetTimeManager.Instance;
			if (tm != null)
			{
				tm.OnTick += HandleTicks;
			}
		}

		private void OnDisable()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null)
			{
				tm.OnTick -= HandleTicks;
			}
		}

		private void HandleTicks(uint tickCount)
		{
			if (tickCount == 0) return;
			if (resources == null || resources.Count == 0) return;

			EnsureEconomyPerTickLoaded();
			if ((_incomePerTick == null || _incomePerTick.Count == 0) &&
			    (_consumptionPerTick == null || _consumptionPerTick.Count == 0))
				return;

			// 1. Обновляем таймеры процессов в слотах и выдаём результаты, если таймеры истекли
			UpdateRunningProcesses((int)tickCount);

			// 2. Пытаемся запустить новые процессы (ограничены константой)
			StartBestProcessesPerTick();

			for (int i = 0; i < resources.Count; i++)
			{
				var r = resources[i];
				if (r == null || string.IsNullOrEmpty(r.resourceId)) continue;
				string id = r.resourceId;

				float value = r.currentAmount;
				float target = 0f;
				float warning = 0f;
				float price = r.currentPrice <= 0f ? r.basePrice : r.currentPrice;

				if (_incomePerTick != null &&
				    _incomePerTick.TryGetValue(id, out float inc) &&
				    Math.Abs(inc) > float.Epsilon)
				{
					value += inc * tickCount;
				}

				if (_consumptionPerTick != null &&
				    _consumptionPerTick.TryGetValue(id, out float cons) &&
				    Math.Abs(cons) > float.Epsilon)
				{
					value -= cons * tickCount;

					// Целевой запас по обычным ресурсам = расход за тик * число тиков вперёд
					float reserveTicks = GetReserveTicksAhead();
					float penaltyTicks = GetReservePenaltyTicksAhead();
					if (cons > 0f && reserveTicks > 0f)
					{
						target = cons * reserveTicks;
						if (penaltyTicks > 0f)
						{
							warning = cons * penaltyTicks;
						}
					}
				}

				// Естественная убыль кредитов
				if (string.Equals(id, "PR_Credits", StringComparison.Ordinal))
				{
					float decay = GetCreditDecayPerTick();
					if (decay > 0f)
					{
						float factor = Mathf.Pow(1f - Mathf.Clamp01(decay), tickCount);
						value *= factor;
					}

					// Цена кредитов не меняется никогда
					price = r.basePrice;
				}

				// Естественная убыль населения (рабочие + инженеры)
				if (string.Equals(id, "PR_Workers", StringComparison.Ordinal) ||
				    string.Equals(id, "PR_Engineers", StringComparison.Ordinal))
				{
					float decay = GetPopulationDecayPerTick();
					if (decay > 0f)
					{
						float factor = Mathf.Pow(1f - Mathf.Clamp01(decay), tickCount);
						value *= factor;
					}

					// Целевой запас населения по формуле из need_anytime
					if (_needAnytimePerPlanet != null &&
					    _needAnytimePerPlanet.TryGetValue(id, out float needBase) &&
					    needBase > 0f)
					{
						// Используем горизонты вперёд: полный и штрафной
						float fullTicks = GetReserveTicksAhead();
						float shortTicks = GetReservePenaltyTicksAhead();
						float perTickDecay = GetPopulationDecayPerTick();
						float baseFactor = 1f - Mathf.Clamp01(perTickDecay);
						float powerFull = Mathf.Pow(baseFactor, fullTicks);
						float powerShort = Mathf.Pow(baseFactor, shortTicks);
						// target = need * (1 + (1 - baseFactor^ticks))
						target = needBase * (1f + (1f - powerFull));
						warning = needBase * (1f + (1f - powerShort));
					}
				}

				// Динамика цены для всех ресурсов, кроме кредитов
				if (!string.Equals(id, "PR_Credits", StringComparison.Ordinal))
				{
					float decrease = GetPriceDecreasePerTick();
					float increase = GetPriceIncreasePerTick();

					if (target > 0f)
					{
						if (value >= target && decrease > 0f)
						{
							// Избыток: цена плавно уменьшается относительно текущей
							float factor = Mathf.Pow(1f - Mathf.Clamp01(decrease), tickCount);
							price *= factor;
						}
						else if (value < target && increase > 0f)
						{
							// Дефицит: цена плавно растёт
							float factor = Mathf.Pow(1f + Mathf.Clamp01(increase), tickCount);
							price *= factor;
						}
					}
				}

				r.currentAmount = ClampResourceAmount(value);
				r.targetAmount = ClampResourceAmount(target);
				r.warningAmount = ClampResourceAmount(warning);
				r.currentPrice = ClampPrice(id, price);
			}
		}

		private void EnsureEconomyPerTickLoaded()
		{
			if (_economyLoaded) return;
			_economyLoaded = true;
			_incomePerTick = null;
			_consumptionPerTick = null;
			_needAnytimePerPlanet = null;

			if (string.IsNullOrWhiteSpace(planetId)) return;

			var planetDb = UnityEngine.Resources.Load<PlanetDatabase>("planet_database");
			if (planetDb == null || planetDb.Planets == null || planetDb.Planets.Count == 0) return;

			PlanetDatabase.PlanetRecord planetRec = null;
			for (int i = 0; i < planetDb.Planets.Count; i++)
			{
				var rec = planetDb.Planets[i];
				if (rec != null && string.Equals(rec.idPlanet, planetId, StringComparison.Ordinal))
				{
					planetRec = rec;
					break;
				}
			}
			if (planetRec == null) return;

			// Доходы
			if (!string.IsNullOrWhiteSpace(planetRec.baseIncomeTikRaw))
			{
				_incomePerTick = ParseStartResources(planetRec.baseIncomeTikRaw);
			}
			else
			{
				_incomePerTick = new Dictionary<string, float>(StringComparer.Ordinal);
			}

			// Базовое потребление
			if (!string.IsNullOrWhiteSpace(planetRec.baseConsumptionTikRaw))
			{
				_consumptionPerTick = ParseStartResources(planetRec.baseConsumptionTikRaw);
			}
			else
			{
				_consumptionPerTick = new Dictionary<string, float>(StringComparer.Ordinal);
			}

			// Базовая потребность (need_anytime) — используется для расчёта целевого населения
			if (!string.IsNullOrWhiteSpace(planetRec.needAnytimeRaw))
			{
				_needAnytimePerPlanet = ParseStartResources(planetRec.needAnytimeRaw);
			}
			else
			{
				_needAnytimePerPlanet = new Dictionary<string, float>(StringComparer.Ordinal);
			}
		}

		private void UpdateRunningProcesses(int ticks)
		{
			if (ticks <= 0 || processSlots == null || processSlots.Count == 0) return;

			for (int i = 0; i < processSlots.Count; i++)
			{
				var slot = processSlots[i];
				if (slot == null || !slot.IsBusy) continue;

				slot.ticksRemaining -= ticks;
				if (slot.ticksRemaining > 0) continue;

				// Процесс завершился — выдаём результаты и освобождаем слот
				var recipe = PlanetRecipeDatabase.GetById(slot.currentRecipeId);
				if (recipe != null && recipe.outResources != null)
				{
					for (int ri = 0; ri < resources.Count; ri++)
					{
						var rs = resources[ri];
						if (rs == null || string.IsNullOrEmpty(rs.resourceId)) continue;

						if (recipe.outResources.TryGetValue(rs.resourceId, out float add) && Math.Abs(add) > float.Epsilon)
						{
							rs.currentAmount = ClampResourceAmount(rs.currentAmount + add);
						}
					}
				}

				slot.currentRecipeId = null;
				slot.ticksRemaining = 0;
			}
		}

		private void StartBestProcessesPerTick()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm == null || tm.Constants == null) return;

			int maxStarts = Mathf.Max(0, Mathf.FloorToInt(tm.Constants.maxProcessesPerTick));
			if (maxStarts <= 0) return;

			if (processSlots == null || processSlots.Count == 0) return;

			var recipes = PlanetRecipeDatabase.Recipes;
			if (recipes == null || recipes.Count == 0) return;

			// Собираем кандидатов: (потенциальная прибыль, индекс слота, рецепт)
			var candidates = new List<(float profit, int slotIndex, PlanetRecipeDatabase.PlanetRecipe recipe)>();

			for (int si = 0; si < processSlots.Count; si++)
			{
				var slot = processSlots[si];
				if (slot == null || slot.IsBusy) continue;

				float inputMultiplier = slot.penaltyPercent > 0f ? (slot.penaltyPercent / 100f) : 1f;

				for (int ri = 0; ri < recipes.Count; ri++)
				{
					var recipe = recipes[ri];
					if (recipe == null) continue;
					if (!recipe.CanUseSlot(slot.slotName)) continue;

					if (!CanRunRecipeWithCurrentResources(recipe, inputMultiplier)) continue;

					float profit = EstimateRecipeProfit(recipe, inputMultiplier);
					if (profit <= 0f) continue;

					candidates.Add((profit, si, recipe));
				}
			}

			if (candidates.Count == 0) return;

			// Сортируем по убыванию прибыли
			candidates.Sort((a, b) => b.profit.CompareTo(a.profit));

			int started = 0;
			for (int i = 0; i < candidates.Count && started < maxStarts; i++)
			{
				var c = candidates[i];
				var slot = processSlots[c.slotIndex];
				if (slot == null || slot.IsBusy) continue;
				float inputMultiplier = slot.penaltyPercent > 0f ? (slot.penaltyPercent / 100f) : 1f;

				if (!CanRunRecipeWithCurrentResources(c.recipe, inputMultiplier)) continue;

				// Списываем входные ресурсы с учётом штрафа слота
				ConsumeResources(c.recipe.inResources, inputMultiplier);

				// Запускаем процесс в слоте
				slot.currentRecipeId = c.recipe.id;
				slot.ticksRemaining = c.recipe.processTicks;
				started++;
			}
		}

		private bool CanRunRecipeWithCurrentResources(PlanetRecipeDatabase.PlanetRecipe recipe, float inputMultiplier)
		{
			if (recipe == null || recipe.inResources == null) return true;

			if (inputMultiplier <= 0f) inputMultiplier = 1f;

			for (int i = 0; i < resources.Count; i++)
			{
				var rs = resources[i];
				if (rs == null || string.IsNullOrEmpty(rs.resourceId)) continue;

				if (recipe.inResources.TryGetValue(rs.resourceId, out float need) && need > 0f)
				{
					float effectiveNeed = need * inputMultiplier;
					if (rs.currentAmount < effectiveNeed)
						return false;
				}
			}
			return true;
		}

		private void ConsumeResources(Dictionary<string, float> amounts, float inputMultiplier)
		{
			if (amounts == null) return;

			if (inputMultiplier <= 0f) inputMultiplier = 1f;

			for (int i = 0; i < resources.Count; i++)
			{
				var rs = resources[i];
				if (rs == null || string.IsNullOrEmpty(rs.resourceId)) continue;

				if (amounts.TryGetValue(rs.resourceId, out float need) && need > 0f)
				{
					float effectiveNeed = need * inputMultiplier;
					rs.currentAmount = ClampResourceAmount(rs.currentAmount - effectiveNeed);
				}
			}
		}

		private float EstimateRecipeProfit(PlanetRecipeDatabase.PlanetRecipe recipe, float inputMultiplier)
		{
			if (recipe == null) return 0f;

			if (inputMultiplier <= 0f) inputMultiplier = 1f;

			float GetPriceFor(string id)
			{
				for (int i = 0; i < resources.Count; i++)
				{
					var rs = resources[i];
					if (rs == null || !string.Equals(rs.resourceId, id, StringComparison.Ordinal)) continue;
					return rs.currentPrice > 0f ? rs.currentPrice : rs.basePrice;
				}
				return 0f;
			}

			float cost = 0f;
			if (recipe.inResources != null)
			{
				foreach (var kv in recipe.inResources)
				{
					float p = GetPriceFor(kv.Key);
					if (p > 0f && kv.Value > 0f)
					{
						float effectiveNeed = kv.Value * inputMultiplier;
						cost += p * effectiveNeed;
					}
				}
			}

			float income = 0f;
			if (recipe.outResources != null)
			{
				foreach (var kv in recipe.outResources)
				{
					float p = GetPriceFor(kv.Key);
					if (p > 0f && kv.Value > 0f)
						income += p * kv.Value;
				}
			}

			return income - cost;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			EditorUpdateProcessSlotsFromDatabase();
			EditorUpdateResourcesFromDatabase();
		}

		/// <summary>
		/// Обновляет список процессных слотов на основе PlanetDatabase.
		/// Для каждого слота создаётся отдельная запись, чтобы штрафы были индивидуальными.
		/// </summary>
		private void EditorUpdateProcessSlotsFromDatabase()
		{
			if (string.IsNullOrWhiteSpace(planetId)) return;

			var db = UnityEngine.Resources.Load<PlanetDatabase>("planet_database");
			if (db == null || db.Planets == null || db.Planets.Count == 0) return;

			PlanetDatabase.PlanetRecord found = null;
			for (int i = 0; i < db.Planets.Count; i++)
			{
				var rec = db.Planets[i];
				if (rec != null && string.Equals(rec.idPlanet, planetId, StringComparison.Ordinal))
				{
					found = rec;
					break;
				}
			}

			if (found == null) return;

			// Парсим строки из типа планеты
			var types = SplitCsv(found.processSlotTypeRaw);
			var counts = SplitCsv(found.processSlotCountRaw);
			var penalties = SplitCsv(found.processSlotBasePenaltyRaw);

			// Будем собирать новый список слотов, переиспользуя старые элементы, где возможно
			var oldSlots = new List<ProcessSlotInfo>(processSlots);
			var newSlots = new List<ProcessSlotInfo>();

			int n = Mathf.Min(types.Length, Mathf.Min(counts.Length, penalties.Length));
			for (int i = 0; i < n; i++)
			{
				string typeName = types[i];
				if (string.IsNullOrWhiteSpace(typeName)) continue;

				int count = ParseInt(counts[i], 0);
				if (count <= 0) continue; // слоты с нулевым количеством не создаём

				string slotName = "PS_" + typeName.Trim();
				float defaultPenalty = ComputeDefaultPenaltyPercent(penalties[i]);

				// Для каждого слота этого типа создаём отдельную запись
				for (int k = 0; k < count; k++)
				{
					ProcessSlotInfo reuse = null;
					for (int s = 0; s < oldSlots.Count; s++)
					{
						var candidate = oldSlots[s];
						if (candidate != null && string.Equals(candidate.slotName, slotName, StringComparison.Ordinal))
						{
							reuse = candidate;
							oldSlots.RemoveAt(s);
							break;
						}
					}

					if (reuse != null)
					{
						// Переиспользуем существующий слот, чтобы не терять настроенный штраф
						reuse.slotName = slotName;
						reuse.slotCount = 1;
						newSlots.Add(reuse);
					}
					else
					{
						var info = new ProcessSlotInfo
						{
							slotName = slotName,
							slotCount = 1,
							penaltyPercent = defaultPenalty
						};
						newSlots.Add(info);
					}
				}
			}

			processSlots = newSlots;

			UnityEditor.EditorUtility.SetDirty(this);
		}

		/// <summary>
		/// Обновляет список ресурсов планеты на основе PlanetDatabase + PlanetResourceDatabase.
		/// Все ресурсы из базы ресурсов присутствуют, стартовое значение берётся из start_resource.
		/// </summary>
		private void EditorUpdateResourcesFromDatabase()
		{
			if (string.IsNullOrWhiteSpace(planetId)) return;

			var planetDb = UnityEngine.Resources.Load<PlanetDatabase>("planet_database");
			if (planetDb == null || planetDb.Planets == null || planetDb.Planets.Count == 0) return;

			PlanetDatabase.PlanetRecord planetRec = null;
			for (int i = 0; i < planetDb.Planets.Count; i++)
			{
				var rec = planetDb.Planets[i];
				if (rec != null && string.Equals(rec.idPlanet, planetId, StringComparison.Ordinal))
				{
					planetRec = rec;
					break;
				}
			}
			if (planetRec == null) return;

			var resDb = UnityEngine.Resources.Load<PlanetResourceDatabase>("planet_resource_database");
			if (resDb == null || resDb.Resources == null || resDb.Resources.Count == 0) return;

			// Разбираем стартовые ресурсы планеты: "PR_Workers:2000,PR_Engineers:1000,..."
			var startAmounts = ParseStartResources(planetRec.startResourceRaw);

			var idToExisting = new Dictionary<string, ResourceState>(StringComparer.Ordinal);
			for (int i = 0; i < resources.Count; i++)
			{
				var r = resources[i];
				if (r == null || string.IsNullOrEmpty(r.resourceId)) continue;
				idToExisting[r.resourceId] = r;
			}

			var newList = new List<ResourceState>(resDb.Resources.Count);
			for (int i = 0; i < resDb.Resources.Count; i++)
			{
				var def = resDb.Resources[i];
				if (def == null || string.IsNullOrEmpty(def.resourceId)) continue;

				startAmounts.TryGetValue(def.resourceId, out float startAmount);
				startAmount = ClampResourceAmount(startAmount);

				if (!idToExisting.TryGetValue(def.resourceId, out var state) || state == null)
				{
					state = new ResourceState
					{
						resourceId = def.resourceId,
						resourceName = def.resourceName,
						startAmount = startAmount,
						currentAmount = startAmount
					};
				}
				else
				{
					state.resourceName = def.resourceName;
					state.startAmount = startAmount;
					state.currentAmount = ClampResourceAmount(state.currentAmount);
				}

				// Базовая цена ресурса берётся из planet_resource_database.
				state.basePrice = def.baseCost;
				// Текущая цена при инициализации равна базовой.
				state.currentPrice = def.baseCost;

				newList.Add(state);
			}

			resources = newList;
			UnityEditor.EditorUtility.SetDirty(this);
		}

#endif

		private static string[] SplitCsv(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
			var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++)
			{
				parts[i] = parts[i].Trim();
			}
			return parts;
		}

		private static int ParseInt(string raw, int fallback)
		{
			if (string.IsNullOrWhiteSpace(raw)) return fallback;
			if (int.TryParse(raw.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v))
				return v;
			return fallback;
		}

		private static float ComputeDefaultPenaltyPercent(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return 100f;
			string trimmed = raw.Trim();
			if (trimmed.EndsWith("%", StringComparison.Ordinal)) trimmed = trimmed.Substring(0, trimmed.Length - 1);
			if (!float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float basePercent))
			{
				basePercent = 0f;
			}

			// Из конфига приходит, например, 40% → нужно 140%
			return 100f + basePercent;
		}

		private static Dictionary<string, float> ParseStartResources(string raw)
		{
			var result = new Dictionary<string, float>(StringComparer.Ordinal);
			if (string.IsNullOrWhiteSpace(raw)) return result;

			var entries = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < entries.Length; i++)
			{
				var part = entries[i].Trim();
				if (string.IsNullOrEmpty(part)) continue;
				int idx = part.IndexOf(':');
				if (idx <= 0 || idx >= part.Length - 1) continue;

				string id = part.Substring(0, idx).Trim();
				string valueRaw = part.Substring(idx + 1).Trim();

				if (string.IsNullOrEmpty(id)) continue;
				if (!float.TryParse(valueRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float amount))
				{
					continue;
				}

				result[id] = ClampResourceAmount(amount);
			}

			return result;
		}

		private static float ClampResourceAmount(float value)
		{
			if (value <= 0f) return 0f;

			float minShare = GetMinResourceShare();
			if (value > 0f && value < minShare)
				value = minShare;

			// Округляем до тысячных, чтобы избежать шлейфа 307.6018 и подобных
			value = Mathf.Round(value * 1000f) / 1000f;

			return value;
		}

		internal static float ClampPrice(string resourceId, float value)
		{
			if (value <= 0f) return 0f;

			// Используем ту же минимальную долю, что и для ресурсов, как шаг округления.
			float step = GetMinResourceShare();
			if (step <= 0f) step = 0.001f;

			value = Mathf.Round(value / step) * step;

			// Ограничиваем цену относительно среднегалактической
			if (!string.IsNullOrEmpty(resourceId))
			{
				// Кредиты не ограничиваем — их цена фиксирована как basePrice
				if (!string.Equals(resourceId, "PR_Credits", StringComparison.Ordinal))
				{
					var gm = GalaxyManager.HasInstance ? GalaxyManager.Instance : null;
					var tm = PlanetTimeManager.TryGetExistingInstance();
					var constants = tm != null ? tm.Constants : null;
					if (gm != null && constants != null && gm.GalacticPrices != null)
					{
						float minMul = constants.minPriceMultiplier > 0f ? constants.minPriceMultiplier : 0.3f;
						float maxMul = constants.maxPriceMultiplier > 0f ? constants.maxPriceMultiplier : 3f;

						float galacticPrice = 0f;
						var prices = gm.GalacticPrices;
						for (int i = 0; i < prices.Count; i++)
						{
							var e = prices[i];
							if (e == null || !string.Equals(e.resourceId, resourceId, StringComparison.Ordinal)) continue;
							galacticPrice = e.currentPrice > 0f ? e.currentPrice : e.lastPrice;
							break;
						}

						if (galacticPrice > 0f)
						{
							float minAllowed = galacticPrice * minMul;
							float maxAllowed = galacticPrice * maxMul;
							if (maxAllowed < minAllowed)
							{
								var tmp = minAllowed;
								minAllowed = maxAllowed;
								maxAllowed = tmp;
							}
							value = Mathf.Clamp(value, minAllowed, maxAllowed);
						}
					}
				}
			}

			return value;
		}

		private static float GetMinResourceShare()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.minResourceShare);
			}
			return 0.001f;
		}

		private static float GetCreditDecayPerTick()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.creditDecayPerTick);
			}
			return 0.01f;
		}

		private static float GetPopulationDecayPerTick()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.populationDecayPerTick);
			}
			return 0.05f;
		}

		private static float GetReserveTicksAhead()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.reserveTicksAhead);
			}
			return 150f;
		}

		private static float GetReservePenaltyTicksAhead()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.reservePenaltyTicksAhead);
			}
			return 75f;
		}

		private static float GetPriceDecreasePerTick()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.priceDecreasePerTick);
			}
			return 0.01f;
		}

		private static float GetPriceIncreasePerTick()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null && tm.Constants != null)
			{
				return Mathf.Max(0f, tm.Constants.priceIncreasePerTick);
			}
			return 0.02f;
		}
	}
}


