using System;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Контроллер одной планеты в экономической симуляции.
	/// Пока это заглушка, позже сюда добавим загрузку конфигов и логику ресурсов.
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
		}

		[Header("Процессные слоты (генерируется из конфигов)")]
		[SerializeField] private System.Collections.Generic.List<ProcessSlotInfo> processSlots =
			new System.Collections.Generic.List<ProcessSlotInfo>();

		/// <summary>Список слотов процессов для этой планеты.</summary>
		public System.Collections.Generic.IReadOnlyList<ProcessSlotInfo> ProcessSlots => processSlots;

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

#if UNITY_EDITOR
		private void OnValidate()
		{
			EditorUpdateProcessSlotsFromDatabase();
		}

		/// <summary>
		/// Обновляет список процессных слотов на основе PlanetDatabase + planet_type.json.
		/// Существующие значения penaltyPercent не затираются, если слот уже есть.
		/// </summary>
		private void EditorUpdateProcessSlotsFromDatabase()
		{
			if (string.IsNullOrWhiteSpace(planetId)) return;

			var db = Resources.Load<PlanetDatabase>("planet_database");
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

			var desiredNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

			int n = Mathf.Min(types.Length, Mathf.Min(counts.Length, penalties.Length));
			for (int i = 0; i < n; i++)
			{
				string typeName = types[i];
				if (string.IsNullOrWhiteSpace(typeName)) continue;

				int count = ParseInt(counts[i], 0);
				if (count <= 0) continue; // слоты с нулевым количеством не создаём

				string slotName = "PS_" + typeName.Trim();
				desiredNames.Add(slotName);

				float defaultPenalty = ComputeDefaultPenaltyPercent(penalties[i]);

				var existing = processSlots.Find(s => s != null && string.Equals(s.slotName, slotName, StringComparison.Ordinal));
				if (existing != null)
				{
					// Обновляем только количество, штраф оставляем как настроил геймдизайнер
					existing.slotCount = count;
				}
				else
				{
					var info = new ProcessSlotInfo
					{
						slotName = slotName,
						slotCount = count,
						penaltyPercent = defaultPenalty
					};
					processSlots.Add(info);
				}
			}

			// Удаляем слоты, которых больше нет в конфиге типа
			for (int i = processSlots.Count - 1; i >= 0; i--)
			{
				var s = processSlots[i];
				if (s == null || !desiredNames.Contains(s.slotName))
				{
					processSlots.RemoveAt(i);
				}
			}

			UnityEditor.EditorUtility.SetDirty(this);
		}

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
#endif
	}
}


