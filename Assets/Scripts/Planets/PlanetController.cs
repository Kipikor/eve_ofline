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
		private const string PlanetConstFileRelative = "Config/planet_const.json";
		private const string MinResourceShareName = "Минимальная доля ресурсов";
		private const string CreditDecayName = "Естественная убыть PR_Credits со счёта каждый тик от доли существующих, касается всех кто может накапливать PR_Credits";
		private const string PopulationDecayName = "Естественная убыль населения на планетах каждый тик от всех имеющихся";
		private const string ReserveTicksAheadName = "На сколько тиков вперёд запасает ресурсов планета(целевой запас). Так же планеты не будет продавать ресурс если его текущий запас ниже целевого.";
		private const string ReservePenaltyTicksAheadName = "Штрафы за отсутствие запасов начинают начисляться если их меньше чем на сколько тиков вперёд";

		private static float s_minResourceShare = -1f;
		private static float s_creditDecayPerTick = -1f;
		private static float s_populationDecayPerTick = -1f;
		private static float s_reserveTicksAhead = -1f;
		private static float s_reservePenaltyTicksAhead = -1f;

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
		}

		[Header("Ресурсы планеты (генерируется из конфигов)")]
		[SerializeField] private List<ResourceState> resources = new List<ResourceState>();

		/// <summary>Текущее состояние ресурсов на планете.</summary>
		public IReadOnlyList<ResourceState> Resources => resources;

		private void Awake()
		{
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

			for (int i = 0; i < resources.Count; i++)
			{
				var r = resources[i];
				if (r == null || string.IsNullOrEmpty(r.resourceId)) continue;
				string id = r.resourceId;

				float value = r.currentAmount;
				float target = 0f;
				float warning = 0f;

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

				r.currentAmount = ClampResourceAmount(value);
				r.targetAmount = ClampResourceAmount(target);
				r.warningAmount = ClampResourceAmount(warning);
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

		private static float GetMinResourceShare()
		{
			if (s_minResourceShare > 0f) return s_minResourceShare;

			s_minResourceShare = 0.001f; // значение по умолчанию
			ReadFloatConstFromConfig(MinResourceShareName, ref s_minResourceShare);
			return s_minResourceShare;
		}

		private static float GetCreditDecayPerTick()
		{
			if (s_creditDecayPerTick >= 0f) return s_creditDecayPerTick;

			s_creditDecayPerTick = 0.01f; // значение по умолчанию
			ReadFloatConstFromConfig(CreditDecayName, ref s_creditDecayPerTick);
			return s_creditDecayPerTick;
		}

		private static float GetPopulationDecayPerTick()
		{
			if (s_populationDecayPerTick >= 0f) return s_populationDecayPerTick;

			s_populationDecayPerTick = 0.05f; // значение по умолчанию
			ReadFloatConstFromConfig(PopulationDecayName, ref s_populationDecayPerTick);
			return s_populationDecayPerTick;
		}

		private static float GetReserveTicksAhead()
		{
			if (s_reserveTicksAhead >= 0f) return s_reserveTicksAhead;

			s_reserveTicksAhead = 150f; // значение по умолчанию
			ReadFloatConstFromConfig(ReserveTicksAheadName, ref s_reserveTicksAhead);
			return s_reserveTicksAhead;
		}

		private static float GetReservePenaltyTicksAhead()
		{
			if (s_reservePenaltyTicksAhead >= 0f) return s_reservePenaltyTicksAhead;

			s_reservePenaltyTicksAhead = 75f; // значение по умолчанию
			ReadFloatConstFromConfig(ReservePenaltyTicksAheadName, ref s_reservePenaltyTicksAhead);
			return s_reservePenaltyTicksAhead;
		}

		private static void ReadFloatConstFromConfig(string name, ref float target)
		{
			try
			{
				string fullPath = Path.Combine(Application.dataPath, PlanetConstFileRelative);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[PlanetController] Не найден файл констант планет: Assets/{PlanetConstFileRelative}");
					return;
				}

				var lines = File.ReadAllLines(fullPath);
				for (int i = 0; i < lines.Length; i++)
				{
					if (!lines[i].Contains(name)) continue;

					for (int j = i + 1; j < Mathf.Min(lines.Length, i + 6); j++)
					{
						if (!lines[j].Contains("\"Значение\"")) continue;
						int colonIndex = lines[j].IndexOf(':');
						if (colonIndex < 0) continue;

						string raw = lines[j].Substring(colonIndex + 1);
						raw = raw.Replace(",", string.Empty);
						raw = raw.Replace("\"", string.Empty);
						raw = raw.Replace("}", string.Empty);
						raw = raw.Trim();

						if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
							    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
						{
							if (parsed >= 0f)
								target = parsed;
							return;
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[PlanetController] Ошибка чтения planet_const.json: " + e);
			}
		}
	}
}


