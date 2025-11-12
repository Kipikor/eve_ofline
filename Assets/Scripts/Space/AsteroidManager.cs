using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Space
{
	public class AsteroidManager : MonoBehaviour
	{
		[Serializable]
		private class SectorConfigEntry
		{
			public string sector_id;
			public string sector_name;
			public int cost_start;
			public string asteroid_id;
			public string asteroid_count;
			public float rarefaction_asteroid;
			public float speed_asteroid_min;
			public float speed_asteroid_max;
			public float asteroid_diameter_min;
			public float asteroid_diameter_max;
		}

		[Serializable]
		private class SectorConfigContainer
		{
			public List<SectorConfigEntry> items;
		}

		[Header("Ссылки (автопоиск)")]
		[SerializeField] private Transform shipTransform;
		[SerializeField] private Transform cameraTransform;

		[Header("Параметры спавна (по границам камеры)")]
		[SerializeField] private float cameraSpawnMargin = 8f;
		[SerializeField] private float cameraDespawnMargin = 20f;
		[SerializeField] private int spawnAttemptsPerUpdate = 3;

		[Header("Визуализация")]
		[SerializeField] private bool drawCameraBounds = true;
		[SerializeField] private bool drawSpawnBounds = true;
		[SerializeField] private bool drawDespawnBounds = true;

		[Header("Сектор")]
		[SerializeField] private string currentSectorId = "Солнечная_система";

		private const string SectorConfigRelativePath = "Config/sector.json";

		private static Dictionary<string, SectorConfigEntry> sectorIdToConfig;
		private static bool sectorConfigLoadTried;

		private AsteroidPrefabRegistry prefabRegistry;

		private class PooledAsteroid
		{
			public string asteroidId;
			public GameObject instance;
			public AsteroidController controller;
			public Rigidbody2D body;
			public bool active;
			public float diameter;
		}

		private readonly List<PooledAsteroid> pool = new List<PooledAsteroid>();
		private readonly Dictionary<string, int> targetCountByType = new Dictionary<string, int>(StringComparer.Ordinal);

		private float sectorRarefaction = 1.2f;
		private float sectorSpeedMin = 1.1f;
		private float sectorSpeedMax = 1.9f;
		private float sectorDiameterMin = 2f;
		private float sectorDiameterMax = 80f;

		[Header("Начальная генерация")]
		[SerializeField] private bool fillAllAtStart = true;
		private bool initialFillDone;

		private void Awake()
		{
			EnsureSectorConfigLoaded();
			FindRefsIfMissing();
			LoadSector(currentSectorId);
			EnsureRegistry();
			BuildOrResizePool();
			// Выполним начальную заливку сразу, без ограничений спавнов в секунду
			if (fillAllAtStart) InitialFillAllArea();
		}

		private void Update()
		{
			DespawnFarAsteroids();
			for (int i = 0; i < spawnAttemptsPerUpdate; i++)
			{
				TrySpawnOne();
			}
		}

		private void FindRefsIfMissing()
		{
			if (shipTransform == null)
			{
				// Поиск по компоненту ShipController (в проекте он в пространстве имён EveOffline.Space)
				EveOffline.Space.ShipController ship = null;
#if UNITY_2023_1_OR_NEWER
				ship = UnityEngine.Object.FindFirstObjectByType<EveOffline.Space.ShipController>();
				if (ship == null) ship = UnityEngine.Object.FindAnyObjectByType<EveOffline.Space.ShipController>();
#else
				var ships = UnityEngine.Object.FindObjectsOfType<EveOffline.Space.ShipController>();
				if (ships != null && ships.Length > 0) ship = ships[0];
#endif
				if (ship != null) shipTransform = ship.transform;
			}
			if (cameraTransform == null)
			{
				// Камеру «пришиваем» к кораблю для логики спавна
				if (shipTransform != null) cameraTransform = shipTransform;
				else
				{
					var cam = Camera.main;
					if (cam != null) cameraTransform = cam.transform;
				}
			}
		}

		private void EnsureRegistry()
		{
			prefabRegistry = AsteroidPrefabRegistry.Load();
			if (prefabRegistry == null)
			{
				Debug.LogWarning("[AsteroidManager] Не найден реестр префабов. Меню Tools → Asteroids → Rebuild Prefab Registry.");
			}
		}

		private void LoadSector(string sectorId)
		{
			if (string.IsNullOrEmpty(sectorId) || sectorIdToConfig == null || !sectorIdToConfig.TryGetValue(sectorId, out var cfg))
			{
				Debug.LogWarning($"[AsteroidManager] Сектор '{sectorId}' не найден в конфиге. Использую дефолтные параметры.");
				targetCountByType.Clear();
				sectorRarefaction = 1.2f;
				sectorSpeedMin = 1.1f;
				sectorSpeedMax = 1.9f;
				sectorDiameterMin = 2f;
				sectorDiameterMax = 80f;
				return;
			}

			sectorRarefaction = Mathf.Max(0.1f, cfg.rarefaction_asteroid);
			sectorSpeedMin = cfg.speed_asteroid_min;
			sectorSpeedMax = Mathf.Max(sectorSpeedMin, cfg.speed_asteroid_max);
			sectorDiameterMin = Mathf.Max(0f, cfg.asteroid_diameter_min);
			sectorDiameterMax = Mathf.Max(sectorDiameterMin, cfg.asteroid_diameter_max);

			targetCountByType.Clear();
			var ids = (cfg.asteroid_id ?? string.Empty).Split(',');
			var countsStr = (cfg.asteroid_count ?? string.Empty).Split(',');
			for (int i = 0; i < ids.Length; i++)
			{
				var id = ids[i].Trim();
				int count = 0;
				if (i < countsStr.Length) int.TryParse(countsStr[i].Trim(), out count);
				if (count > 0 && !string.IsNullOrEmpty(id))
				{
					targetCountByType[id] = count;
				}
			}
		}

		private void DestroySafe(GameObject go)
		{
			if (go == null) return;
			if (Application.isPlaying) Destroy(go);
			else DestroyImmediate(go);
		}

		private void ClearAllAsteroids()
		{
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (pa != null && pa.instance != null)
				{
					DestroySafe(pa.instance);
				}
			}
			pool.Clear();
		}

		private void BuildOrResizePool()
		{
			// Удалим лишние
			for (int i = pool.Count - 1; i >= 0; i--)
			{
				var pa = pool[i];
				// Если тип больше не нужен — уничтожаем объект
				if (!targetCountByType.ContainsKey(pa.asteroidId))
				{
					if (pa.instance != null)
					{
						DestroySafe(pa.instance);
					}
					pool.RemoveAt(i);
				}
			}

			// Текущее количество по типам
			var currentByType = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var pa in pool)
			{
				if (!currentByType.ContainsKey(pa.asteroidId)) currentByType[pa.asteroidId] = 0;
				currentByType[pa.asteroidId]++;
			}

			// Добираем недостающие
			foreach (var kv in targetCountByType)
			{
				var id = kv.Key;
				var need = kv.Value;
				currentByType.TryGetValue(id, out var have);
				for (int i = have; i < need; i++)
				{
					var go = CreateAsteroidInstance(id);
					if (go == null) break;
					var pa = new PooledAsteroid
					{
						asteroidId = id,
						instance = go,
						controller = go.GetComponent<AsteroidController>(),
						body = go.GetComponent<Rigidbody2D>(),
						active = false,
						diameter = 0f
					};
					go.SetActive(false);
					pool.Add(pa);
				}
			}
		}

		private GameObject CreateAsteroidInstance(string asteroidId)
		{
			if (prefabRegistry == null)
			{
				EnsureRegistry();
				if (prefabRegistry == null) return null;
			}
			var prefab = prefabRegistry.Get(asteroidId);
			if (prefab == null)
			{
				Debug.LogWarning($"[AsteroidManager] В реестре нет префаба '{asteroidId}'");
				return null;
			}
			var go = Instantiate(prefab, transform);
			go.name = asteroidId;
			return go;
		}

		private void DespawnFarAsteroids()
		{
			var camPos = cameraTransform != null ? cameraTransform.position : (shipTransform != null ? shipTransform.position : Vector3.zero);
			var cam = Camera.main != null ? Camera.main : (cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null);
			Rect worldRect = GetCameraWorldRect(camPos, cam);
			Rect despawnRect = ExpandRect(worldRect, cameraDespawnMargin);
			foreach (var pa in pool)
			{
				if (!pa.active || pa.instance == null) continue;
				var pos3 = pa.instance.transform.position;
				var pos2 = new Vector2(pos3.x, pos3.y);
				if (!despawnRect.Contains(pos2))
				{
					DisableAsteroid(pa);
				}
			}
		}

		private void DisableAsteroid(PooledAsteroid pa)
		{
			if (pa.instance != null)
			{
				pa.instance.SetActive(false);
			}
			pa.active = false;
			pa.diameter = 0f;
		}

		private void TrySpawnOne()
		{
			// Если активных уже достаточно, выходим
			var activeByType = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var pa in pool)
			{
				if (pa.active)
				{
					if (!activeByType.ContainsKey(pa.asteroidId)) activeByType[pa.asteroidId] = 0;
					activeByType[pa.asteroidId]++;
				}
			}
			// Собираем список всех НЕАКТИВНЫХ экземпляров, чьи типы ещё не достигли своей квоты
			var reserveIndices = new List<int>();
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (!pa.active && pa.instance != null)
				{
					// Проверяем квоту типа
					targetCountByType.TryGetValue(pa.asteroidId, out var targetForType);
					activeByType.TryGetValue(pa.asteroidId, out var activeForType);
					if (activeForType < targetForType)
					{
						reserveIndices.Add(i);
					}
				}
			}
			if (reserveIndices.Count == 0) return;
			int chosenIndex = reserveIndices[UnityEngine.Random.Range(0, reserveIndices.Count)];
			var candidate = pool[chosenIndex];

			// Сгенерируем диаметр (независимо от контроллера), чтобы проверить редукцию плотности
			var diameter = UnityEngine.Random.Range(sectorDiameterMin, sectorDiameterMax);
			var radius = diameter * 0.5f;

			// Выбираем позицию в одном из колец (корабль/камера)
			if (!TryPickSpawnPosition(radius, out var spawnPos)) return;

			// Проверяем разреженность: нет ли других астероидов слишком близко
			if (!IsFarFromOthers(spawnPos, radius * sectorRarefaction)) return;

			// Активируем и настраиваем
			EnableAndSetup(candidate, spawnPos, diameter);
		}

		// Единовременная заливка области при старте:
		// - Заполняем весь красный прямоугольник (despawnRect), игнорируя обычное правило "только за синим"
		// - Не спавним в круге вокруг корабля радиусом ≈ 2 размеров корабля
		// - Соблюдаем разрежённость и квоты типов
		private void InitialFillAllArea()
		{
			if (initialFillDone) return;
			initialFillDone = true;
			if (cameraTransform == null && shipTransform == null) FindRefsIfMissing();

			// Для стартового заполнения прямоугольник берём по размерам камеры, но центрируем ПО КОРАБЛЮ
			var cam = Camera.main != null ? Camera.main : (cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null);
			Vector3 shipPos = shipTransform != null ? shipTransform.position
				: (cameraTransform != null ? cameraTransform.position : Vector3.zero);
			var worldRect = GetCameraWorldRect(shipPos, cam);
			var despawnRect = ExpandRect(worldRect, cameraDespawnMargin);

			// Оценим радиус исключения вокруг корабля (≈ 2 размеров корабля)
			float exclusionRadius = 0f;
			{
				if (shipTransform != null)
				{
					var box = shipTransform.GetComponent<BoxCollider2D>();
					if (box != null)
					{
						// Полудиагональ коробки как базовый радиус
						float baseR = 0.5f * Mathf.Sqrt(box.size.x * box.size.x + box.size.y * box.size.y);
						exclusionRadius = Mathf.Max(0f, baseR * 2f);
					}
					else
					{
						exclusionRadius = 2f; // безопасный минимум
					}
				}
			}

			// Подсчёт активных и целевых по типам
			var activeByType = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var pa in pool)
			{
				if (pa.active)
				{
					if (!activeByType.ContainsKey(pa.asteroidId)) activeByType[pa.asteroidId] = 0;
					activeByType[pa.asteroidId]++;
				}
			}

			// Список резервов, которые ещё можно активировать по квоте
			var candidates = new List<int>();
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (pa == null || pa.instance == null || pa.active) continue;
				targetCountByType.TryGetValue(pa.asteroidId, out var target);
				activeByType.TryGetValue(pa.asteroidId, out var active);
				if (active < target) candidates.Add(i);
			}
			if (candidates.Count == 0) return;

			// Перемешаем для равномерности
			for (int i = 0; i < candidates.Count; i++)
			{
				int j = UnityEngine.Random.Range(i, candidates.Count);
				(candidates[i], candidates[j]) = (candidates[j], candidates[i]);
			}

			// Пытаемся расставить все допустимые резервы сразу
			for (int ci = 0; ci < candidates.Count; ci++)
			{
				int idx = candidates[ci];
				if (idx < 0 || idx >= pool.Count) continue;
				var pa = pool[idx];
				if (pa == null || pa.instance == null || pa.active) continue;

				// Проверим квоту на каждом шаге
				targetCountByType.TryGetValue(pa.asteroidId, out var target);
				activeByType.TryGetValue(pa.asteroidId, out var active);
				if (active >= target) continue;

				// Выбираем диаметр и позицию в пределах despawnRect, исключая круг у корабля
				float diameter = UnityEngine.Random.Range(sectorDiameterMin, sectorDiameterMax);
				float radius = diameter * 0.5f;

				Vector3 pos;
				bool placed = false;
				for (int attempt = 0; attempt < 48 && !placed; attempt++)
				{
					float x = UnityEngine.Random.Range(despawnRect.xMin, despawnRect.xMax);
					float y = UnityEngine.Random.Range(despawnRect.yMin, despawnRect.yMax);
					pos = new Vector3(x, y, 0f);
					// Исключаем пузырь у корабля
					if (exclusionRadius > 0f && (new Vector2(pos.x - shipPos.x, pos.y - shipPos.y)).sqrMagnitude < exclusionRadius * exclusionRadius)
					{
						continue;
					}
					// Разрежённость
					if (!IsFarFromOthers(pos, radius * sectorRarefaction)) continue;

					EnableAndSetup(pa, pos, diameter);
					placed = true;
					// Обновим счётчик
					activeByType.TryGetValue(pa.asteroidId, out active);
					activeByType[pa.asteroidId] = active + 1;
				}
			}
		}

		private bool TryPickSpawnPosition(float asteroidRadius, out Vector3 pos)
		{
			pos = Vector3.zero;
			if (cameraTransform == null && shipTransform == null) return false;
			var center = cameraTransform != null ? cameraTransform.position : shipTransform.position;
			var cam = Camera.main != null ? Camera.main : (cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null);
			var worldRect = GetCameraWorldRect(center, cam);
			var spawnRect = ExpandRect(worldRect, cameraSpawnMargin);
			var despawnRect = ExpandRect(worldRect, cameraDespawnMargin);

			// Выбираем равномерно точку в области между синим (spawnRect) и красным (despawnRect)
			for (int i = 0; i < 32; i++)
			{
				float x = UnityEngine.Random.Range(despawnRect.xMin, despawnRect.xMax);
				float y = UnityEngine.Random.Range(despawnRect.yMin, despawnRect.yMax);
				var p = new Vector2(x, y);
				if (!spawnRect.Contains(p))
				{
					pos = new Vector3(x, y, 0f);
					return true;
				}
			}

			// Фоллбек: если по каким-то причинам не нашли (например, margins перепутаны), вернём центр верхней стороны красного прямоугольника
			pos = new Vector3((despawnRect.xMin + despawnRect.xMax) * 2f * 0.5f, despawnRect.yMax, 0f);
			return true;
		}

		private bool TryPickInRing(Vector3 center, float inner, float outer, out Vector3 pos)
		{
			pos = Vector3.zero;
			if (outer <= inner) return false;
			// Одно равномерное попадание в кольцо
			var ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
			var r = Mathf.Sqrt(UnityEngine.Random.Range(inner * inner, outer * outer)); // равномерно по площади кольца
			var offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * r;
			pos = center + offset;
			return true;
		}

		private bool IsFarFromOthers(Vector3 candidatePos, float minDistance)
		{
			var minSqr = minDistance * minDistance;
			foreach (var pa in pool)
			{
				if (!pa.active || pa.instance == null) continue;
				var d2 = (pa.instance.transform.position - candidatePos).sqrMagnitude;
				if (d2 < minSqr) return false;
			}
			return true;
		}

		private void EnableAndSetup(PooledAsteroid pa, Vector3 position, float diameter)
		{
			var go = pa.instance;
			if (go == null) return;
			go.transform.position = position;

			// Настройка контроллера
			if (pa.controller == null) pa.controller = go.GetComponent<AsteroidController>();
			if (pa.controller != null)
			{
				// Прямо задаем диаметр и активируем подходящий size
				pa.controller.SetDiameter(diameter);
			}

			// Включаем объект
			go.SetActive(true);
			pa.active = true;
			pa.diameter = diameter;

			// Задаём скорость вниз с отклонением ±5°
			if (pa.body == null) pa.body = go.GetComponent<Rigidbody2D>();
			if (pa.body != null)
			{
				var baseAngleDeg = -90f;
				var delta = UnityEngine.Random.Range(-5f, 5f);
				var angRad = (baseAngleDeg + delta) * Mathf.Deg2Rad;
				var speed = UnityEngine.Random.Range(sectorSpeedMin, sectorSpeedMax);
				var vx = Mathf.Cos(angRad) * speed;
				var vy = Mathf.Sin(angRad) * speed;
				pa.body.linearVelocity = new Vector2(vx, vy);
				// Случайное вращение ±5 град/с
				pa.body.angularVelocity = UnityEngine.Random.Range(-5f, 5f);
			}
		}

		// Публичный API для точечного спавна конкретных детей (используется при расколе)
		public bool TrySpawnSpecific(string asteroidId, Vector3 position, float diameter, Vector2 velocity, float angularVelocity)
		{
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (pa == null) continue;
				if (pa.asteroidId != asteroidId) continue;
				if (pa.active) continue;
				if (pa.instance == null) continue;

				// Настраиваем
				pa.instance.transform.position = position;
				if (pa.controller == null) pa.controller = pa.instance.GetComponent<AsteroidController>();
				if (pa.controller != null)
				{
					pa.controller.SetDiameter(diameter);
				}
				pa.instance.SetActive(true);
				pa.active = true;
				pa.diameter = diameter;
				if (pa.body == null) pa.body = pa.instance.GetComponent<Rigidbody2D>();
				if (pa.body != null)
				{
					pa.body.linearVelocity = velocity;
					pa.body.angularVelocity = angularVelocity;
				}
				return true;
			}
			return false;
		}

		// Уведомление от контроллера, что инстанс отключился (например, при разрушении)
		public void NotifyDisabledInstance(GameObject instance)
		{
			if (instance == null) return;
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (pa == null || pa.instance == null) continue;
				if (pa.instance == instance)
				{
					pa.active = false;
					pa.diameter = 0f;
					return;
				}
			}
		}

		private static void EnsureSectorConfigLoaded()
		{
			if (sectorIdToConfig != null) return;
			if (sectorConfigLoadTried) return;
			sectorConfigLoadTried = true;
			try
			{
				var fullPath = Path.Combine(Application.dataPath, SectorConfigRelativePath);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[AsteroidManager] Конфиг секторов не найден: {fullPath}");
					return;
				}
				var json = File.ReadAllText(fullPath);
				if (string.IsNullOrWhiteSpace(json))
				{
					Debug.LogWarning("[AsteroidManager] Конфиг секторов пустой.");
					return;
				}
				var wrapped = "{ \"items\": " + json + " }";
				var container = JsonUtility.FromJson<SectorConfigContainer>(wrapped);
				if (container == null || container.items == null || container.items.Count == 0) return;
				sectorIdToConfig = new Dictionary<string, SectorConfigEntry>(StringComparer.Ordinal);
				foreach (var e in container.items)
				{
					if (e == null || string.IsNullOrEmpty(e.sector_id)) continue;
					if (!sectorIdToConfig.ContainsKey(e.sector_id)) sectorIdToConfig.Add(e.sector_id, e);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[AsteroidManager] Ошибка загрузки sector.json: {ex.Message}");
			}
		}

		// Публичное API
		public string CurrentSectorId
		{
			get => currentSectorId;
			set
			{
				if (currentSectorId == value) return;
				currentSectorId = value;
				// Полная пересборка пула при смене сектора
				LoadSector(currentSectorId);
				ClearAllAsteroids();
				BuildOrResizePool();
			}
		}

		public float CameraSpawnMargin => cameraSpawnMargin;

		[Serializable]
		public struct TypeStats
		{
			public string asteroidId;
			public int active;
			public int reserve;
			public int total;
			public int target;
		}

		public List<TypeStats> GetTypeStats()
		{
			var activeByType = new Dictionary<string, int>(StringComparer.Ordinal);
			var totalByType = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < pool.Count; i++)
			{
				var pa = pool[i];
				if (pa == null) continue;
				if (!totalByType.ContainsKey(pa.asteroidId)) totalByType[pa.asteroidId] = 0;
				totalByType[pa.asteroidId]++;
				if (pa.active)
				{
					if (!activeByType.ContainsKey(pa.asteroidId)) activeByType[pa.asteroidId] = 0;
					activeByType[pa.asteroidId]++;
				}
			}

			var result = new List<TypeStats>();
			// включаем все типы из пула и из целей
			var keys = new HashSet<string>(totalByType.Keys, StringComparer.Ordinal);
			foreach (var k in targetCountByType.Keys) keys.Add(k);
			foreach (var id in keys)
			{
				totalByType.TryGetValue(id, out var total);
				activeByType.TryGetValue(id, out var active);
				targetCountByType.TryGetValue(id, out var target);
				result.Add(new TypeStats
				{
					asteroidId = id,
					active = active,
					reserve = Math.Max(0, total - active),
					total = total,
					target = target
				});
			}
			result.Sort((a, b) => string.Compare(a.asteroidId, b.asteroidId, StringComparison.Ordinal));
			return result;
		}

		private void OnDrawGizmosSelected()
		{
			var center = cameraTransform != null ? cameraTransform.position : (shipTransform != null ? shipTransform.position : (Vector3?)null);
			if (!center.HasValue) return;
			var cam = Camera.main != null ? Camera.main : (cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null);
			Rect baseRect = GetCameraWorldRect(center.Value, cam);
			if (drawCameraBounds) DrawRect(baseRect, new Color(0.4f, 0.9f, 0.4f, 0.9f));
			if (drawSpawnBounds) DrawRect(ExpandRect(baseRect, cameraSpawnMargin), new Color(0.2f, 0.6f, 1f, 0.9f));
			if (drawDespawnBounds) DrawRect(ExpandRect(baseRect, cameraDespawnMargin), new Color(1f, 0.3f, 0.3f, 0.9f));
		}

		private static Rect GetCameraWorldRect(Vector3 camCenter, Camera cam)
		{
			// Если камеры нет — предположим ортографическую камеру 16:9 с halfHeight=5
			float halfH = 5f;
			float halfW = 5f * 16f / 9f;
			if (cam != null && cam.orthographic)
			{
				halfH = cam.orthographicSize;
				halfW = halfH * cam.aspect;
			}
			var min = new Vector2(camCenter.x - halfW, camCenter.y - halfH);
			var size = new Vector2(halfW * 2f, halfH * 2f);
			return new Rect(min, size);
		}

		private static Rect ExpandRect(Rect r, float margin)
		{
			return new Rect(r.xMin - margin, r.yMin - margin, r.width + margin * 2f, r.height + margin * 2f);
		}

		private static void DrawRect(Rect r, Color color)
		{
			Gizmos.color = color;
			Vector3 a = new Vector3(r.xMin, r.yMin, 0f);
			Vector3 b = new Vector3(r.xMax, r.yMin, 0f);
			Vector3 c = new Vector3(r.xMax, r.yMax, 0f);
			Vector3 d = new Vector3(r.xMin, r.yMax, 0f);
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
		}
	}
}


