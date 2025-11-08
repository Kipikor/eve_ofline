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

		private void Awake()
		{
			EnsureSectorConfigLoaded();
			FindRefsIfMissing();
			LoadSector(currentSectorId);
			EnsureRegistry();
			BuildOrResizePool();
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
				var pos = pa.instance.transform.position;
				if (!despawnRect.Contains(pos))
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
			bool needAny = false;
			foreach (var kv in targetCountByType)
			{
				activeByType.TryGetValue(kv.Key, out var a);
				if (a < kv.Value) { needAny = true; break; }
			}
			if (!needAny) return;

			// Берем первый неактивный, чей тип ещё не достиг квоты
			PooledAsteroid candidate = null;
			foreach (var pa in pool)
			{
				if (pa.active) continue;
				targetCountByType.TryGetValue(pa.asteroidId, out var target);
				activeByType.TryGetValue(pa.asteroidId, out var have);
				if (have < target)
				{
					candidate = pa;
					break;
				}
			}
			if (candidate == null) return;

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


