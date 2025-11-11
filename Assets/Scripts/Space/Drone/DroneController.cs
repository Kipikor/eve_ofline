using System;
using UnityEngine;

namespace EveOffline.Space.Drone
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Rigidbody2D))]
	public class DroneController : MonoBehaviour
	{
		[Header("Runtime (auto)")]
		[SerializeField] private EveOffline.Space.ShipController owner;
		[SerializeField] private float massTons;
		[SerializeField] private float accelerationKN;
		[SerializeField] private float maxSpeedMS;
		[SerializeField] private float maxRadiusFromShip;
		[SerializeField] private float maxAngularSpeedDegPerSec;
		[SerializeField] private float grabMaxDiameter = 5f;
		[SerializeField] private int carryingVolumeM3 = 0;
		[SerializeField] private string carryingOreId = "";
		[SerializeField] private Vector2 velocity;

		[Header("Debug")]
		[SerializeField] private bool enableLogging = true;
		private DroneState lastLoggedState;

		private enum DroneState { Orbit, Seek, ToAsteroid, ReturnToShip }
		[SerializeField] private DroneState state = DroneState.Orbit;
		private global::Space.AsteroidController targetAsteroid;

		private static readonly System.Collections.Generic.HashSet<global::Space.AsteroidController> Claimed = new System.Collections.Generic.HashSet<global::Space.AsteroidController>();

		private static void PurgeClaimed()
		{
			if (Claimed.Count == 0) return;
			// Актуальные цели, на которые реально кто-то летит сейчас
			var activeTargets = new System.Collections.Generic.HashSet<global::Space.AsteroidController>();
			var drones = UnityEngine.Object.FindObjectsByType<DroneController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			for (int i = 0; i < drones.Length; i++)
			{
				var d = drones[i];
				if (d != null && d.targetAsteroid != null && d.targetAsteroid.gameObject.activeInHierarchy)
				{
					activeTargets.Add(d.targetAsteroid);
				}
			}

			// Удаляем записи на неактивные/уничтоженные и «осиротевшие» цели
			var toRemove = new System.Collections.Generic.List<global::Space.AsteroidController>();
			foreach (var a in Claimed)
			{
				if (a == null || !a.gameObject.activeInHierarchy || !activeTargets.Contains(a))
				{
					toRemove.Add(a);
				}
			}
			for (int i = 0; i < toRemove.Count; i++) Claimed.Remove(toRemove[i]);
		}

		[Header("Orbit")]
		[SerializeField] private float orbitRadius;               // м
		[SerializeField] private float orbitAngularSpeedDeg = 35; // град/с
		[SerializeField] private float orbitPhaseDeg;             // стартовая фаза

		private Rigidbody2D body;

		public void Setup(EveOffline.Space.ShipController shipOwner, float massTons, float accelerationKN, float maxSpeedMS, float maxRadiusFromShip, float baseShipRadius, float grabMaxDiameter)
		{
			this.owner = shipOwner;
			this.massTons = massTons;
			this.accelerationKN = accelerationKN;
			this.maxSpeedMS = maxSpeedMS;
			this.maxRadiusFromShip = maxRadiusFromShip;
			this.grabMaxDiameter = Mathf.Max(0f, grabMaxDiameter);
			this.maxAngularSpeedDegPerSec = Mathf.Max(0f, accelerationKN); // по требованию: скорость поворота = acceleration (град/с)

			body = GetComponent<Rigidbody2D>();
			body.gravityScale = 0f;
			body.mass = Mathf.Max(0.01f, massTons);
			body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
			body.bodyType = RigidbodyType2D.Kinematic; // двигаемся без инерции
			body.linearVelocity = Vector2.zero;
			body.angularVelocity = 0f;
			velocity = Vector2.zero;

			// logging suppressed (only capture/unload logs remain)

			// Убедимся, что есть коллайдер-триггер для пересечений
			var anyCol = GetComponent<Collider2D>();
			if (anyCol == null) anyCol = gameObject.AddComponent<CircleCollider2D>();
			anyCol.isTrigger = true;
			if (anyCol is CircleCollider2D cc)
			{
				cc.radius = Mathf.Max(0.75f, baseShipRadius * 0.15f); // небольшой, но уверенный радиус захвата/выгрузки
			}

			// Радиус орбиты ~ 1.5..2.0 размеров корабля
			float k = UnityEngine.Random.Range(1.5f, 2.0f);
			orbitRadius = Mathf.Max(1f, baseShipRadius * k);
			orbitPhaseDeg = UnityEngine.Random.Range(0f, 360f);

			DisableCollisions();
		}

		private void Awake()
		{
			body = GetComponent<Rigidbody2D>();
			if (body != null)
			{
				body.gravityScale = 0f;
				body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
			}
			DisableCollisions();
		}

		private void DisableCollisions()
		{
			// Дрон ни с чем не сталкивается: убираем коллайдеры или переводим их в триггер
			var cols = GetComponentsInChildren<Collider2D>(true);
			for (int i = 0; i < cols.Length; i++)
			{
				cols[i].isTrigger = true;
			}
			// Слой не меняем: лазеры игнорируют дронов по коду в LaserShooter
		}

		private void FixedUpdate()
		{
			if (owner == null || body == null) return;

			Vector2 ownerPos = owner.transform.position;
			Vector2 myPos = transform.position;
			Vector2 toOwner = ownerPos - myPos;
			float dist = toOwner.magnitude;

			// Если ушли за рабочий радиус — возвращаемся внутрь
			if (maxRadiusFromShip > 0f && dist > maxRadiusFromShip)
			{
				Vector2 dir = toOwner.normalized;
				Vector2 target = ownerPos + dir * (maxRadiusFromShip * 0.9f);
				SteerKinematicTo(target);
			}
			else
			{
				switch (state)
				{
					case DroneState.Orbit:
						// Кружим по орбите и ищем цель
						orbitPhaseDeg += orbitAngularSpeedDeg * Time.fixedDeltaTime;
						{
							float rad = orbitPhaseDeg * Mathf.Deg2Rad;
							Vector2 orbitOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
							Vector2 target = ownerPos + orbitOffset;
							SteerKinematicTo(target);
						}
						TryAcquireTarget(ownerPos);
						break;

					case DroneState.Seek:
					case DroneState.ToAsteroid:
						if (targetAsteroid == null || !targetAsteroid.gameObject.activeInHierarchy)
						{
							ClearTarget();
							state = DroneState.Orbit;
							break;
						}
						SteerKinematicTo(targetAsteroid.transform.position);
						break;

					case DroneState.ReturnToShip:
						SteerKinematicTo(ownerPos);
						// Фолбэк: выгрузка по близости, если триггер/вход пропущен
						float unloadDist = Mathf.Max(0.75f, GetShipBaseRadiusCached() * 0.5f);
						if (Vector2.Distance(body.position, ownerPos) <= unloadDist)
						{
							TryUnloadToShip();
						}
						break;
				}
			}

			// Перемещение по рассчитанной скорости
			Vector2 newPos = body.position + velocity * Time.fixedDeltaTime;
			body.MovePosition(newPos);

			// state-change logging suppressed
		}

		private void SteerKinematicTo(Vector2 target)
		{
			Vector2 myPos = transform.position;
			Vector2 desired = (target - myPos);
			if (desired.sqrMagnitude < 0.0001f) return;

			Vector2 dir = desired.normalized;

			// Плавное изменение скорости: acceleration = скорость набора/торможения (м/с^2, но применяем как Δv/с)
			float accel = Mathf.Max(0f, accelerationKN);
			Vector2 desiredVel = dir * Mathf.Max(0f, maxSpeedMS);
			velocity = Vector2.MoveTowards(velocity, desiredVel, accel * Time.fixedDeltaTime);

			// Дискретный поворот к целевому направлению с ограничением скорости (град/с).
			// Приоритетно смотрим по направлению движения; если почти стоим — по направлению к цели.
			Vector2 faceDir = velocity.sqrMagnitude > 0.001f ? velocity.normalized : dir;
			float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
			float currentAngle = body.rotation;
			float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
			float maxStep = Mathf.Max(0f, maxAngularSpeedDegPerSec) * Time.fixedDeltaTime;
			float step = Mathf.Clamp(delta, -maxStep, maxStep);
			float newAngle = currentAngle + step;
			body.MoveRotation(newAngle);
			body.angularVelocity = 0f;
		}

		private float _cachedShipRadius;
		private float GetShipBaseRadiusCached()
		{
			if (_cachedShipRadius <= 0f && owner != null)
			{
				// Оценка по коллайдеру
				var box = owner.GetComponent<BoxCollider2D>();
				if (box != null)
				{
					_cachedShipRadius = 0.5f * Mathf.Sqrt(box.size.x * box.size.x + box.size.y * box.size.y);
				}
				if (_cachedShipRadius <= 0f) _cachedShipRadius = 1.0f;
			}
			return _cachedShipRadius > 0f ? _cachedShipRadius : 1.0f;
		}

		private void TryAcquireTarget(Vector2 ownerPos)
		{
			PurgeClaimed();
			if (carryingVolumeM3 > 0) { state = DroneState.ReturnToShip; return; }

			// Ищем подходящие астероиды в радиусе
			var asteroids = UnityEngine.Object.FindObjectsByType<global::Space.AsteroidController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			float bestShipDist = float.MaxValue;
			float bestDroneDist = float.MaxValue;
			global::Space.AsteroidController best = null;
			for (int i = 0; i < asteroids.Length; i++)
			{
				var a = asteroids[i];
				if (a == null || !a.gameObject.activeInHierarchy) continue;
				// Пропускаем реголит
				if (!string.IsNullOrEmpty(a.AsteroidId) && a.AsteroidId.IndexOf("Реголит", StringComparison.Ordinal) >= 0) continue;
				// Диаметр
				if (a.Diameter > grabMaxDiameter) continue;
				// В радиусе работы от корабля
				float d = Vector2.Distance(ownerPos, a.transform.position);
				if (maxRadiusFromShip > 0f && d > maxRadiusFromShip) continue;
				// Уже помечен
				if (Claimed.Contains(a)) continue;
				// Приоритет: ближе к кораблю; при равенстве — ближе к дрону
				float meDist = Vector2.Distance((Vector2)transform.position, (Vector2)a.transform.position);
				if (d < bestShipDist || (Mathf.Approximately(d, bestShipDist) && meDist < bestDroneDist))
				{
					bestShipDist = d;
					bestDroneDist = meDist;
					best = a;
				}
			}
			if (best != null)
			{
				Claimed.Add(best);
				targetAsteroid = best;
				state = DroneState.ToAsteroid;
				// acquire logging suppressed
			}
		}

		private void ClearTarget()
		{
			if (targetAsteroid != null) { Claimed.Remove(targetAsteroid); }
			targetAsteroid = null;
		}

		private void OnTriggerEnter2D(Collider2D other)
		{
			// Захват астероида
			if (targetAsteroid != null && other != null && other.GetComponentInParent<global::Space.AsteroidController>() == targetAsteroid)
			{
				// Записать объём и ore_id
				carryingVolumeM3 = Mathf.Max(0, targetAsteroid.GetVolumeRounded());
				carryingOreId = targetAsteroid.OreId;
				if (enableLogging) Log($"Capture asteroid: {targetAsteroid.AsteroidId} ore={carryingOreId} m3={carryingVolumeM3}");

				// Отключить астероид и вернуть в пул
				var go = targetAsteroid.gameObject;
				go.SetActive(false);
				var manager = UnityEngine.Object.FindFirstObjectByType<global::Space.AsteroidManager>();
				if (manager != null) manager.NotifyDisabledInstance(go);

				ClearTarget();
				state = DroneState.ReturnToShip;
				return;
			}

			// Выгрузка в корабль
			if (owner != null && other != null && other.GetComponentInParent<EveOffline.Space.ShipController>() == owner)
			{
				TryUnloadToShip();
			}
		}

		private void OnTriggerStay2D(Collider2D other)
		{
			// Повторяем логику на каждый кадр пересечения для надёжности
			if (targetAsteroid != null && other != null && other.GetComponentInParent<global::Space.AsteroidController>() == targetAsteroid)
			{
				carryingVolumeM3 = Mathf.Max(0, targetAsteroid.GetVolumeRounded());
				carryingOreId = targetAsteroid.OreId;
				if (enableLogging) Log($"Capture (stay) asteroid: {targetAsteroid.AsteroidId} ore={carryingOreId} m3={carryingVolumeM3}");
				var go = targetAsteroid.gameObject;
				go.SetActive(false);
				var manager = UnityEngine.Object.FindFirstObjectByType<global::Space.AsteroidManager>();
				if (manager != null) manager.NotifyDisabledInstance(go);
				ClearTarget();
				state = DroneState.ReturnToShip;
				return;
			}

			if (owner != null && other != null && other.GetComponentInParent<EveOffline.Space.ShipController>() == owner)
			{
				TryUnloadToShip();
			}
		}

		private void TryUnloadToShip()
		{
			if (carryingVolumeM3 <= 0 || string.IsNullOrEmpty(carryingOreId)) { state = DroneState.Orbit; return; }

			// Сколько единиц поместится по объёму
			float unitM3 = OreDb.GetCagro(carryingOreId);
			if (unitM3 <= 0.0001f) unitM3 = 1f;
			int wantUnits = Mathf.Max(1, Mathf.FloorToInt(carryingVolumeM3 / unitM3));

			// Ищем инвентарь: сперва Singleton, затем активные/неактивные
			var inv = global::UI.Inventory.InventoryController.Instance;
			if (inv == null) inv = UnityEngine.Object.FindFirstObjectByType<global::UI.Inventory.InventoryController>(FindObjectsInactive.Exclude);
			if (inv == null) inv = UnityEngine.Object.FindAnyObjectByType<global::UI.Inventory.InventoryController>(FindObjectsInactive.Include);
#if UNITY_EDITOR
			if (inv == null)
			{
				var all = Resources.FindObjectsOfTypeAll(typeof(global::UI.Inventory.InventoryController));
				for (int i = 0; i < all.Length; i++)
				{
					var c = all[i] as global::UI.Inventory.InventoryController;
					if (c != null && c.gameObject.scene.IsValid()) { inv = c; break; }
				}
			}
#endif
			if (inv == null)
			{
				// если нет инвентаря — просто обнулим груз
				carryingVolumeM3 = 0;
				carryingOreId = "";
				state = DroneState.Orbit;
				return;
			}

			int added = inv.AddItem(carryingOreId, wantUnits);
			if (enableLogging) Log($"Unload: ore={carryingOreId} want={wantUnits} added={added}");
			if (added > 0)
			{
				int usedM3 = Mathf.RoundToInt(added * unitM3);
				carryingVolumeM3 = Mathf.Max(0, carryingVolumeM3 - usedM3);
				if (enableLogging) Log($"After unload: remaining_m3={carryingVolumeM3}");
			}

			if (carryingVolumeM3 <= 0)
			{
				carryingOreId = "";
				state = DroneState.Orbit;
				if (enableLogging) Log("Unload complete → Orbit");
			}
			else
			{
				// Остаток не влез — будем пытаться ещё
				state = DroneState.ReturnToShip;
				// partial unload logging suppressed
			}
		}

		private void OnDrawGizmosSelected()
		{
			if (owner == null) return;
			Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
			DrawCircle(owner.transform.position, maxRadiusFromShip, 64);
		}

		private void DrawCircle(Vector3 center, float radius, int segments)
		{
			if (radius <= 0f || segments < 3) return;
			Vector3 prev = center + new Vector3(radius, 0f, 0f);
			for (int i = 1; i <= segments; i++)
			{
				float ang = (i / (float)segments) * Mathf.PI * 2f;
				Vector3 next = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
				Gizmos.DrawLine(prev, next);
				prev = next;
			}
		}

		// Простой локальный парсер ore.json для получения cagro
		private static class OreDb
		{
			private static System.Collections.Generic.Dictionary<string, float> idToCagro;
			private static bool loaded;

			public static float GetCagro(string oreId)
			{
				EnsureLoaded();
				if (idToCagro != null && idToCagro.TryGetValue(oreId ?? "", out var v)) return v;
				return 1f;
			}

			private static void EnsureLoaded()
			{
				if (loaded) return;
				loaded = true;
				idToCagro = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal);
				try
				{
					var full = System.IO.Path.Combine(Application.dataPath, "Config/ore.json");
					if (!System.IO.File.Exists(full)) return;
					var json = System.IO.File.ReadAllText(full);
					string wrapped = "{ \"items\": " + json + " }";
					var list = JsonUtility.FromJson<Wrapper>(wrapped);
					if (list?.items == null) return;
					for (int i = 0; i < list.items.Length; i++)
					{
						var it = list.items[i];
						if (it == null || string.IsNullOrEmpty(it.ore_id)) continue;
						if (!idToCagro.ContainsKey(it.ore_id)) idToCagro.Add(it.ore_id, Mathf.Max(0.0001f, it.cagro));
					}
				}
				catch { }
			}

			[Serializable] private class OreDef { public string ore_id; public float cagro; }
			[Serializable] private class Wrapper { public OreDef[] items; }
		}

		private void Log(string msg)
		{
			Debug.Log($"[Drone:{gameObject.name}] {msg}", this);
		}
	}
}


