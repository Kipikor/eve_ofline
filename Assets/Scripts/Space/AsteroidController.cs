using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Space
{
	[DisallowMultipleComponent]
	public class AsteroidController : MonoBehaviour
	{
		[Tooltip("Диаметр астероида в метрах. Равен локальному скейлу родителя.")]
		[SerializeField] private float diameter = 2f;

		[Header("Генератор (диаметр)")]
		[Min(0f), HideInInspector]
		[SerializeField] private float generateMinDiameter = 2f;
		[Min(0f), HideInInspector]
		[SerializeField] private float generateMaxDiameter = 250f;

		private const string SizeChildPrefix = "size_";
		private const string ConfigRelativePath = "Config/asteroid.json";

		[Serializable]
		private class AsteroidConfigEntry
		{
			public string asteroid_id;
			public string ore_id;
			public float diameter_min_size_0;
			public float diameter_max_size_0;
			public float diameter_min_size_1;
			public float diameter_max_size_1;
			public float diameter_min_size_2;
			public float diameter_max_size_2;
			public float diameter_min_size_3;
			public float diameter_max_size_3;
			public float hp_from_m3;
			public float m3_loss_to_break;
			public string pieces_to_break_weight;
		}

		[Serializable]
		private class AsteroidConfigContainer
		{
			public List<AsteroidConfigEntry> items;
		}

		private static Dictionary<string, AsteroidConfigEntry> asteroidIdToConfig;
		private static bool configLoadTried;

		private void Awake()
		{
			EnsureConfigLoaded();
			EnsureRootRigidbody();
			// В Awake не трогаем активность детей, чтобы избежать SendMessage во время инициализации
			ApplyDiameter(diameter, autoPickSize: false);
		}

		private void Start()
		{
			// Безопасно активируем подходящий size после инициализации
			ActivateSizeChildForDiameter(diameter);
		}

		private void Reset()
		{
			EnsureConfigLoaded();
			InitializeGeneratorBoundsFromConfigIfPossible();
			ApplyDiameter(diameter, autoPickSize: false);
			ScheduleEditorActivationPreview();
		}

		private void OnValidate()
		{
			if (generateMinDiameter > generateMaxDiameter)
			{
				generateMaxDiameter = generateMinDiameter;
			}

			EnsureConfigLoaded();
			if (generateMinDiameter <= 0f && generateMaxDiameter <= 0f)
			{
				InitializeGeneratorBoundsFromConfigIfPossible();
			}

			// Обновляем масштаб, но не меняем активность детей прямо в OnValidate
			ApplyDiameter(diameter, autoPickSize: false);
			ScheduleEditorActivationPreview();
		}

		public void SetDiameter(float newDiameter)
		{
			ApplyDiameter(newDiameter, autoPickSize: true);
		}

		public void RegenerateRandom()
		{
			EnsureValidGeneratorBounds();
			var minD = Mathf.Max(0f, generateMinDiameter);
			var maxD = Mathf.Max(minD, generateMaxDiameter);
			var randomDiameter = UnityEngine.Random.Range(minD, maxD);
			ApplyDiameter(randomDiameter, autoPickSize: true);
			// Случайный поворот вокруг оси Z (2D)
			var randomAngle = UnityEngine.Random.Range(0f, 360f);
			transform.rotation = Quaternion.Euler(0f, 0f, randomAngle);
		}

		private void EnsureValidGeneratorBounds()
		{
			if (generateMaxDiameter < generateMinDiameter)
			{
				generateMaxDiameter = generateMinDiameter;
			}

			if (generateMinDiameter <= 0f && generateMaxDiameter <= 0f)
			{
				InitializeGeneratorBoundsFromConfigIfPossible();
			}
		}

		private void ApplyDiameter(float newDiameter, bool autoPickSize)
		{
			diameter = Mathf.Max(0f, newDiameter);
			var s = Mathf.Max(0f, diameter);
			transform.localScale = new Vector3(s, s, s);

			if (autoPickSize)
			{
				ActivateSizeChildForDiameter(diameter);
			}
		}

		private void ActivateSizeChildForDiameter(float currentDiameter)
		{
			var sizeChildren = GetSizeChildren();
			// Сначала выключаем все
			foreach (var child in sizeChildren)
			{
				if (child != null)
				{
					child.gameObject.SetActive(false);
				}
			}

			// Если меньше минимального порога (2), ничего не активируем
			if (currentDiameter < 2f)
			{
				return;
			}

			// Определяем подходящие size по конфигу
			var candidates = GetCandidateSizesForDiameter(gameObject.name, currentDiameter);
			if (candidates.Count == 0)
			{
				// Если конфиг не найден или диаметр вне диапазонов — оставляем всё выключенным, но предупреждаем
				Debug.LogWarning($"[AsteroidController] Не найден подходящий size для диаметра {currentDiameter} у '{gameObject.name}'. Проверьте конфиг.", this);
				return;
			}

			var pickedIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
			if (pickedIndex >= 0 && pickedIndex < sizeChildren.Length && sizeChildren[pickedIndex] != null)
			{
				sizeChildren[pickedIndex].gameObject.SetActive(true);
				// Держим активного ребёнка совпадающим с родителем
				sizeChildren[pickedIndex].localPosition = Vector3.zero;
				sizeChildren[pickedIndex].localRotation = Quaternion.identity;
			}
			else
			{
				Debug.LogWarning($"[AsteroidController] Подобранный индекс size_{pickedIndex} отсутствует у '{gameObject.name}'.", this);
			}
		}

		#if UNITY_EDITOR
		private void ScheduleEditorActivationPreview()
		{
			// В редакторе переносим активацию на следующий цикл, чтобы избежать запретов в OnValidate/CheckConsistency
			if (Application.isPlaying) return;
			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (this == null) return;
				if (gameObject == null) return;
				ActivateSizeChildForDiameter(diameter);
			};
		}
		#endif

		private void EnsureRootRigidbody()
		{
			// Если Rigidbody2D на детях — переносим на корень, чтобы движение было у родителя
			var rootRb = GetComponent<Rigidbody2D>();
			var childBodies = GetComponentsInChildren<Rigidbody2D>(true);
			Rigidbody2D candidate = null;
			foreach (var rb in childBodies)
			{
				if (rb == null) continue;
				if (rb.gameObject == gameObject) continue;
				// Выберем любого ребёнка как источник параметров
				if (candidate == null) candidate = rb;
			}

			if (rootRb == null)
			{
				rootRb = gameObject.AddComponent<Rigidbody2D>();
				// По умолчанию отключим гравитацию, как в редакторской утилите
				rootRb.gravityScale = 0f;
				// Скопируем базовые параметры, если есть кандидат
				if (candidate != null)
				{
					rootRb.bodyType = candidate.bodyType;
					rootRb.sharedMaterial = candidate.sharedMaterial;
					rootRb.useFullKinematicContacts = candidate.useFullKinematicContacts;
					rootRb.useAutoMass = candidate.useAutoMass;
					rootRb.mass = candidate.mass;
					rootRb.linearDamping = candidate.linearDamping;
					rootRb.angularDamping = candidate.angularDamping;
					rootRb.gravityScale = 0f; // форсим 0
					rootRb.interpolation = candidate.interpolation;
					rootRb.sleepMode = candidate.sleepMode;
					rootRb.collisionDetectionMode = candidate.collisionDetectionMode;
					rootRb.constraints = candidate.constraints;
				}
			}
			else
			{
				// Приводим к ожидаемому состоянию
				rootRb.gravityScale = 0f;
			}

			// Удаляем Rigidbody2D у детей, чтобы они не жили своей жизнью
			foreach (var rb in childBodies)
			{
				if (rb == null) continue;
				if (rb.gameObject == gameObject) continue;
				#if UNITY_EDITOR
				if (!Application.isPlaying)
					UnityEditor.Undo.DestroyObjectImmediate(rb);
				else
					Destroy(rb);
				#else
				Destroy(rb);
				#endif
			}
		}

		private Transform[] GetSizeChildren()
		{
			// Ищем size_0 ... size_3 непосредственно у родителя
			var result = new Transform[4];
			for (int i = 0; i < 4; i++)
			{
				var child = transform.Find($"{SizeChildPrefix}{i}");
				result[i] = child;
			}
			return result;
		}

		private List<int> GetCandidateSizesForDiameter(string asteroidId, float currentDiameter)
		{
			var candidates = new List<int>();
			var has = TryGetConfigFor(asteroidId, out var cfg);
			if (!has || cfg == null)
			{
				return candidates;
			}

			for (int i = 0; i < 4; i++)
			{
				var (minD, maxD) = GetRange(cfg, i);
				if (currentDiameter >= minD && currentDiameter <= maxD)
				{
					candidates.Add(i);
				}
			}

			return candidates;
		}

		private (float minD, float maxD) GetRange(AsteroidConfigEntry cfg, int sizeIndex)
		{
			switch (sizeIndex)
			{
				case 0: return (cfg.diameter_min_size_0, cfg.diameter_max_size_0);
				case 1: return (cfg.diameter_min_size_1, cfg.diameter_max_size_1);
				case 2: return (cfg.diameter_min_size_2, cfg.diameter_max_size_2);
				case 3: return (cfg.diameter_min_size_3, cfg.diameter_max_size_3);
				default: return (float.MaxValue, float.MinValue);
			}
		}

		private void InitializeGeneratorBoundsFromConfigIfPossible()
		{
			if (TryGetConfigFor(gameObject.name, out var cfg) && cfg != null)
			{
				// Берём глобальные минимумы/максимумы из size_0..size_3
				float min = Mathf.Min(cfg.diameter_min_size_0, cfg.diameter_min_size_1, cfg.diameter_min_size_2, cfg.diameter_min_size_3);
				float max = Mathf.Max(cfg.diameter_max_size_0, cfg.diameter_max_size_1, cfg.diameter_max_size_2, cfg.diameter_max_size_3);
				generateMinDiameter = min;
				generateMaxDiameter = max;
			}
			else
			{
				// Фоллбек
				generateMinDiameter = 2f;
				generateMaxDiameter = 250f;
			}
		}

		private static bool TryGetConfigFor(string asteroidId, out AsteroidConfigEntry entry)
		{
			EnsureConfigLoaded();
			if (!string.IsNullOrEmpty(asteroidIdToConfigKey(asteroidId)) && asteroidIdToConfig != null)
			{
				return asteroidIdToConfig.TryGetValue(asteroidIdToConfigKey(asteroidId), out entry);
			}
			entry = null;
			return false;
		}

		private static string asteroidIdToConfigKey(string asteroidId)
		{
			return asteroidId ?? string.Empty;
		}

		private static void EnsureConfigLoaded()
		{
			if (asteroidIdToConfig != null) return;
			if (configLoadTried) return;
			configLoadTried = true;

			try
			{
				var fullPath = Path.Combine(Application.dataPath, ConfigRelativePath);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[AsteroidController] Конфиг не найден: {fullPath}");
					return;
				}

				var json = File.ReadAllText(fullPath);
				if (string.IsNullOrWhiteSpace(json))
				{
					Debug.LogWarning("[AsteroidController] Конфиг пустой: asteroid.json");
					return;
				}

				// JsonUtility не парсит массив верхнего уровня — оборачиваем
				var wrapped = "{ \"items\": " + json + " }";
				var container = JsonUtility.FromJson<AsteroidConfigContainer>(wrapped);
				if (container == null || container.items == null || container.items.Count == 0)
				{
					Debug.LogWarning("[AsteroidController] Не удалось распарсить asteroid.json");
					return;
				}

				asteroidIdToConfig = new Dictionary<string, AsteroidConfigEntry>(StringComparer.Ordinal);
				foreach (var item in container.items)
				{
					if (item != null && !string.IsNullOrEmpty(item.asteroid_id))
					{
						var key = asteroidIdToConfigKey(item.asteroid_id);
						if (!asteroidIdToConfig.ContainsKey(key))
						{
							asteroidIdToConfig.Add(key, item);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AsteroidController] Ошибка загрузки конфига: {e.Message}");
			}
		}

		// Публичные геттеры для инспектора/кнопки
		public float Diameter => diameter;
		public float GenerateMinDiameter
		{
			get => generateMinDiameter;
			set => generateMinDiameter = Mathf.Max(0f, value);
		}
		public float GenerateMaxDiameter
		{
			get => generateMaxDiameter;
			set => generateMaxDiameter = Mathf.Max(0f, value);
		}
	}
}


