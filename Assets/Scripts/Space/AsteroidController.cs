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

		[Header("Collisions")]
		[SerializeField] private bool enableAsteroidCollisions = true;
		[SerializeField, Min(0f)] private float minAsteroidImpactSpeed = 1.0f; // м/с, ниже — урона нет
		[SerializeField] private bool logAsteroidImpacts = false;

		// Метаданные для защиты после раскола — чтобы «братья» не взрывали друг друга сразу
		[SerializeField] private int spawnGroupId;
		[SerializeField] private float spawnProtectionUntil;
		public void InitializeSpawnMeta(int groupId, float protectionSeconds)
		{
			spawnGroupId = groupId;
			spawnProtectionUntil = Time.time + Mathf.Max(0f, protectionSeconds);
		}

		[Header("Генератор (диаметр)")]
		[Min(0f), HideInInspector]
		[SerializeField] private float generateMinDiameter = 2f;
		[Min(0f), HideInInspector]
		[SerializeField] private float generateMaxDiameter = 250f;

		private const string SizeChildPrefix = "size_";
		private const string ConfigRelativePath = "Config/asteroid.json";
		private const string OreConfigRelativePath = "Config/ore.json";

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
			public float hp_from_m2;
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
		
		[Serializable]
		private class OreConfigEntry
		{
			public string ore_id;
			public string ore_name;
			public string ore_icon;
			public string ore_descr;
			public float cost;
			public float cagro;
			public float density;
		}
		
		[Serializable]
		private class OreConfigContainer
		{
			public List<OreConfigEntry> items;
		}
		
		private static Dictionary<string, OreConfigEntry> oreIdToConfig;
		private static bool oreConfigLoadTried;
		
		[Header("Состояние (боевое)")]
		[SerializeField] private int currentHitPoints;

		private void Awake()
		{
			EnsureConfigLoaded();
			EnsureOreConfigLoaded();
			EnsureRootRigidbody();
			// В Awake не трогаем активность детей, чтобы избежать SendMessage во время инициализации
			ApplyDiameter(diameter, autoPickSize: false);
			UpdateDerivedStatsAndApplyPhysics();
		}

		private void Start()
		{
			// Безопасно активируем подходящий size после инициализации
			ActivateSizeChildForDiameter(diameter);
		}

		private void OnEnable()
		{
			// Новый спавн: устанавливаем текущее здоровье в максимум
			currentHitPoints = GetMaxHitPoints();
		}

		private void Reset()
		{
			EnsureConfigLoaded();
			EnsureOreConfigLoaded();
			InitializeGeneratorBoundsFromConfigIfPossible();
			ApplyDiameter(diameter, autoPickSize: false);
			ScheduleEditorActivationPreview();
			UpdateDerivedStatsAndApplyPhysics();
			// При первом добавлении выставим текущее HP в максимум
			currentHitPoints = GetMaxHitPoints();
		}

		private void OnValidate()
		{
			if (generateMinDiameter > generateMaxDiameter)
			{
				generateMaxDiameter = generateMinDiameter;
			}

			EnsureConfigLoaded();
			EnsureOreConfigLoaded();
			if (generateMinDiameter <= 0f && generateMaxDiameter <= 0f)
			{
				InitializeGeneratorBoundsFromConfigIfPossible();
			}

			// Обновляем масштаб, но не меняем активность детей прямо в OnValidate
			ApplyDiameter(diameter, autoPickSize: false);
			ScheduleEditorActivationPreview();
			UpdateDerivedStatsAndApplyPhysics();
			currentHitPoints = Mathf.Clamp(currentHitPoints, 0, GetMaxHitPoints());
		}

		public void SetDiameter(float newDiameter)
		{
			ApplyDiameter(newDiameter, autoPickSize: true);
		}

		public void ApplyDamage(int damage)
		{
			if (damage <= 0) return;
			CurrentHitPoints -= damage;
			if (CurrentHitPoints <= 0)
			{
				BreakIntoPieces();
			}
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
			UpdateDerivedStatsAndApplyPhysics();
			currentHitPoints = Mathf.Clamp(currentHitPoints, 0, GetMaxHitPoints());
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
			
			// При смене диаметра обновляем массу/HP
			UpdateDerivedStatsAndApplyPhysics();
			currentHitPoints = Mathf.Clamp(currentHitPoints, 0, GetMaxHitPoints());
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

		private void UpdateDerivedStatsAndApplyPhysics()
		{
			// Масса из площади круга и плотности
			var mass = GetMassRounded();
			var rb = GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				rb.mass = Mathf.Max(0, mass);
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

		private void BreakIntoPieces()
		{
			if (!TryGetConfigFor(gameObject.name, out var cfg) || cfg == null)
			{
				gameObject.SetActive(false);
				var m1 = UnityEngine.Object.FindFirstObjectByType<AsteroidManager>();
				if (m1 != null) m1.NotifyDisabledInstance(gameObject);
				return;
			}

			int volume = GetVolumeRounded();
			int lost = Mathf.RoundToInt(volume * Mathf.Clamp01(cfg.m3_loss_to_break));
			int remain = Mathf.Max(0, volume - lost);

			int pieces = ChoosePiecesCount(cfg.pieces_to_break_weight);
			if (pieces <= 0 || remain <= 0)
			{
				// просто исчез
				gameObject.SetActive(false);
				var m2 = UnityEngine.Object.FindFirstObjectByType<AsteroidManager>();
				if (m2 != null) m2.NotifyDisabledInstance(gameObject);
				return;
			}

			var splitVolumes = AllocateVolumes(remain, pieces);
			var diameters = new List<float>(pieces);
			for (int i = 0; i < splitVolumes.Count; i++)
			{
				float d = DiameterFromVolume(splitVolumes[i]);
				diameters.Add(d);
			}

			// Базовая кинематика от родителя
			var rb = GetComponent<Rigidbody2D>();
			Vector2 baseVel = rb != null ? rb.linearVelocity : Vector2.zero;
			float baseAng = rb != null ? rb.angularVelocity : 0f;

			var manager = UnityEngine.Object.FindFirstObjectByType<AsteroidManager>();
			Vector3 pos = transform.position;
			if (manager != null)
			{
				// Общая группа спавна для защиты «братьев»
				int groupId = manager.AcquireSpawnGroupId();
				float vMin = Mathf.Max(0f, manager.SplitImpulseMin);
				float vMax = Mathf.Max(vMin, manager.SplitImpulseMax);
				for (int i = 0; i < diameters.Count; i++)
				{
					float d = diameters[i];
					if (d < 2f) continue; // рассыпались в пыль
					// Импульс разлёта
					float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
					float extraSpeed = UnityEngine.Random.Range(vMin, vMax);
					Vector2 impulse = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * extraSpeed;
					Vector2 childVel = baseVel + impulse;
					float childAng = baseAng + UnityEngine.Random.Range(-10f, 10f);
					// Попытка взять свободный из пула
					bool ok = manager.TrySpawnSpecific(gameObject.name, pos, d, childVel, childAng, groupId);
					if (!ok)
					{
						// Нет свободных — пропускаем
						continue;
					}
				}
			}

			// Отключаем исходный астероид
			gameObject.SetActive(false);
			if (manager != null) manager.NotifyDisabledInstance(gameObject);
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (!enableAsteroidCollisions) return;
			if (collision == null || collision.rigidbody == null) return;
			var otherCtrl = collision.collider.GetComponentInParent<AsteroidController>();
			if (otherCtrl == null) return;

			// Обрабатываем столкновение только один раз (по id объекта)
			if (GetInstanceID() > otherCtrl.GetInstanceID()) return;

			// Если оба — «братья» одного раскола и оба ещё под защитой — не наносим урон
			float now = Time.time;
			if (spawnGroupId != 0 && spawnGroupId == otherCtrl.spawnGroupId &&
				now < spawnProtectionUntil && now < otherCtrl.spawnProtectionUntil)
			{
				return;
			}

			float v = collision.relativeVelocity.magnitude;
			if (v < Mathf.Max(0f, minAsteroidImpactSpeed)) return;

			var rbA = GetComponent<Rigidbody2D>();
			var rbB = otherCtrl.GetComponent<Rigidbody2D>();
			float m1 = rbA != null && rbA.mass > 0f ? rbA.mass : Mathf.Max(1f, GetMassRounded());
			float m2 = rbB != null && rbB.mass > 0f ? rbB.mass : Mathf.Max(1f, otherCtrl.GetMassRounded());

			// Приведённая масса и энергия удара (кДж, т.к. масса — в "тоннах")
			float mu = (m1 > 0f && m2 > 0f) ? (m1 * m2) / (m1 + m2) : Mathf.Max(m1, m2);
			float E_total_kJ = 0.5f * mu * v * v;
			float E_each_kJ = E_total_kJ * 0.5f;
			int damageEach = Mathf.Max(1, Mathf.RoundToInt(E_each_kJ)); // 1 кДж = 1 урон

			// Применяем урон обоим
			ApplyDamage(damageEach);
			otherCtrl.ApplyDamage(damageEach);

			if (logAsteroidImpacts)
			{
				Debug.Log($"[AsteroidImpact] v={v:0.###} m1={m1:0.###} m2={m2:0.###} mu={mu:0.###} E_total={E_total_kJ:0.###}kJ dmg_each={damageEach}", this);
			}
		}

		private static int ChoosePiecesCount(string weights)
		{
			if (string.IsNullOrWhiteSpace(weights)) return 0;
			var parts = weights.Split(',');
			var options = new List<(int n, int w)>();
			int totalW = 0;
			for (int i = 0; i < parts.Length; i++)
			{
				var p = parts[i].Trim();
				var kv = p.Split(':');
				if (kv.Length != 2) continue;
				if (!int.TryParse(kv[0], out var n)) continue;
				if (!int.TryParse(kv[1], out var w)) continue;
				if (w <= 0) continue;
				options.Add((n, w));
				totalW += w;
			}
			if (totalW <= 0 || options.Count == 0) return 0;
			int pick = UnityEngine.Random.Range(0, totalW);
			int acc = 0;
			for (int i = 0; i < options.Count; i++)
			{
				acc += options[i].w;
				if (pick < acc) return options[i].n;
			}
			return options[options.Count - 1].n;
		}

		private static List<int> AllocateVolumes(int total, int pieces)
		{
			var xs = new float[pieces];
			float sum = 0f;
			for (int i = 0; i < pieces; i++)
			{
				xs[i] = UnityEngine.Random.Range(1f, 2f); // гарантирует max/min <= 2
				sum += xs[i];
			}
			var vols = new List<int>(pieces);
			int acc = 0;
			for (int i = 0; i < pieces; i++)
			{
				int vi = Mathf.RoundToInt(total * (xs[i] / sum));
				vols.Add(vi);
				acc += vi;
			}
			// Коррекция суммы
			int diff = total - acc;
			for (int i = 0; diff != 0 && i < pieces; i++)
			{
				int sign = diff > 0 ? 1 : -1;
				vols[i] = Mathf.Max(0, vols[i] + sign);
				diff -= sign;
			}
			return vols;
		}

		private static float DiameterFromVolume(int vol)
		{
			if (vol <= 0) return 0f;
			// V = 4/3 * pi * r^3 => r = cbrt( V * 3 / (4*pi) ), d = 2r
			double r = Math.Pow(vol * 3.0 / (4.0 * Math.PI), 1.0 / 3.0);
			return (float)(2.0 * r);
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

		private static void EnsureOreConfigLoaded()
		{
			if (oreIdToConfig != null) return;
			if (oreConfigLoadTried) return;
			oreConfigLoadTried = true;
			try
			{
				var fullPath = Path.Combine(Application.dataPath, OreConfigRelativePath);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[AsteroidController] Ore конфиг не найден: {fullPath}");
					return;
				}
				var json = File.ReadAllText(fullPath);
				if (string.IsNullOrWhiteSpace(json))
				{
					Debug.LogWarning("[AsteroidController] Ore конфиг пустой: ore.json");
					return;
				}
				var wrapped = "{ \"items\": " + json + " }";
				var container = JsonUtility.FromJson<OreConfigContainer>(wrapped);
				if (container == null || container.items == null || container.items.Count == 0)
				{
					Debug.LogWarning("[AsteroidController] Не удалось распарсить ore.json");
					return;
				}
				oreIdToConfig = new Dictionary<string, OreConfigEntry>(StringComparer.Ordinal);
				foreach (var item in container.items)
				{
					if (item != null && !string.IsNullOrEmpty(item.ore_id))
					{
						if (!oreIdToConfig.ContainsKey(item.ore_id))
						{
							oreIdToConfig.Add(item.ore_id, item);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AsteroidController] Ошибка загрузки ore конфига: {e.Message}");
			}
		}

		// Публичные геттеры для инспектора/кнопки
		public float Diameter => diameter;
		public string AsteroidId => gameObject.name;
		public string OreId
		{
			get
			{
				if (TryGetConfigFor(gameObject.name, out var cfg) && cfg != null)
				{
					return cfg.ore_id;
				}
				return string.Empty;
			}
		}
		public float OreDensity
		{
			get
			{
				// Костыль для реголита: у него нет руды, плотность фиксируем = 1
				if (TryGetConfigFor(gameObject.name, out var asteroidCfg) && asteroidCfg != null)
				{
					if (!string.IsNullOrEmpty(asteroidCfg.asteroid_id) &&
						asteroidCfg.asteroid_id.IndexOf("Реголит", StringComparison.Ordinal) >= 0)
					{
						return 1f;
					}
					if (!string.IsNullOrEmpty(asteroidCfg.ore_id) &&
						string.Equals(asteroidCfg.ore_id, "none", StringComparison.Ordinal))
					{
						return 1f;
					}
				}
				
				var oreId = OreId;
				if (!string.IsNullOrEmpty(oreId) && oreIdToConfig != null && oreIdToConfig.TryGetValue(oreId, out var ore))
				{
					return ore.density;
				}
				return 0f;
			}
		}
		public float HpFromM3
		{
			get
			{
				if (TryGetConfigFor(gameObject.name, out var cfg) && cfg != null)
				{
					return cfg.hp_from_m3;
				}
				return 0f;
			}
		}
		public float HpFromM2
		{
			get
			{
				if (TryGetConfigFor(gameObject.name, out var cfg) && cfg != null)
				{
					return cfg.hp_from_m2;
				}
				return 0f;
			}
		}
		public int GetAreaRounded()
		{
			var r = diameter * 0.5f;
			var area = Mathf.PI * r * r;
			return Mathf.RoundToInt(area);
		}
		public int GetVolumeRounded()
		{
			var r = diameter * 0.5f;
			var vol = (4f / 3f) * Mathf.PI * r * r * r;
			return Mathf.RoundToInt(vol);
		}
		public int GetMassRounded()
		{
			var area = GetAreaRounded();
			var density = OreDensity;
			var mass = area * density;
			return Mathf.RoundToInt(mass);
		}
		public int GetMaxHitPoints()
		{
			// Новая формула: по площади сечения, если задан hp_from_m2
			var k2 = HpFromM2;
			if (k2 > 0f)
			{
				var area = GetAreaRounded();
				var hp2 = area * k2;
				return Mathf.RoundToInt(hp2);
			}
			// Фолбэк на старую формулу по объёму (для старых конфигов)
			var vol = GetVolumeRounded();
			var k3 = HpFromM3;
			var hp = vol * k3;
			return Mathf.RoundToInt(hp);
		}
		public int CurrentHitPoints
		{
			get => currentHitPoints;
			set => currentHitPoints = Mathf.Clamp(value, 0, GetMaxHitPoints());
		}
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


