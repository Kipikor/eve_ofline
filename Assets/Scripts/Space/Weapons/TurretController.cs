using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class TurretController : MonoBehaviour
	{
		[SerializeField] private float aimSpeedDegPerSec = 720f;
		[SerializeField] private bool instantAim = true;
		private Transform rotationPivot; // автоматически используется родительский turret_point (или сам объект)

			[Header("Auto Mode")]
			[SerializeField] private bool autoAim = false;
			[SerializeField, Min(0f)] private float targetDiameterMin = 5f;
			[SerializeField, Min(0.05f)] private float retargetEvery = 0.5f;
			[SerializeField, Min(0f)] private float maxTargetDistance = 0f; // 0 = без ограничения

			[Header("Prediction")]
			[SerializeField] private bool usePrediction = true;
			[SerializeField, Min(0f)] private float projectileSpeedOverride = 0f; // 0 = взять из TurretShooter
			[SerializeField, Min(0f)] private float maxLeadSeconds = 2.0f;
			[SerializeField] private bool debugDrawLead = false;

			[Header("Firing Gate")]
			[SerializeField, Min(0f)] private float fireAngleToleranceDeg = 4f;

			private Space.AsteroidController currentTarget;
			private float nextRetargetTime;
			private TurretShooter shooter;
			private EveOffline.Space.ShipController ownerShip;
			private bool isAimedNow;

			public Space.AsteroidController CurrentTarget => currentTarget;
			public bool HasTarget => currentTarget != null && currentTarget.gameObject.activeInHierarchy;
			public bool IsAimed => isAimedNow;

		private void Awake()
		{
			if (rotationPivot == null)
			{
				// Если турель смонтирована, её родитель — turret_point: вращаем именно его
				if (transform.parent != null && transform.parent.name.StartsWith("turret_point"))
				{
					rotationPivot = transform.parent;
				}
				else
				{
					rotationPivot = transform;
				}
			}

			// Попробуем найти шутер для чтения скорости пули
			shooter = GetComponentInChildren<TurretShooter>(true);
			ownerShip = GetComponentInParent<EveOffline.Space.ShipController>();

			// Применим стартовые настройки от корабля и подпишемся на изменения
			ApplyWeaponSettingsFromShip();
			if (ownerShip != null) ownerShip.WeaponSettingsChanged += ApplyWeaponSettingsFromShip;
		}

		private void OnDestroy()
		{
			if (ownerShip != null) ownerShip.WeaponSettingsChanged -= ApplyWeaponSettingsFromShip;
		}

		private void ApplyWeaponSettingsFromShip()
		{
			if (ownerShip == null) return;
			autoAim = ownerShip.WeaponAutoAim;
			usePrediction = ownerShip.WeaponAutoPrediction;
			fireAngleToleranceDeg = ownerShip.WeaponFireAngleToleranceDeg;
		}

		private void LateUpdate()
		{
			if (rotationPivot == null) return;
			isAimedNow = false;

			Vector3 targetWorld;
			if (autoAim)
			{
				if (Time.time >= nextRetargetTime || !HasTarget)
				{
					nextRetargetTime = Time.time + Mathf.Max(0.05f, retargetEvery);
					currentTarget = AcquireTarget();
				}
				if (!HasTarget) return;

				// Предиктивное упреждение для снарядов
				if (usePrediction)
				{
					Vector2 origin = rotationPivot.position;
					Vector2 targetPos = currentTarget.transform.position;
					var rb = currentTarget.GetComponent<Rigidbody2D>();
					Vector2 targetVel = rb != null ? rb.linearVelocity : Vector2.zero;

					float projectileSpeed = projectileSpeedOverride > 0f ? projectileSpeedOverride
						: (shooter != null ? Mathf.Max(0f, shooter.ProjectileSpeed) : 0f);

					Vector2 lead;
					if (TrySolveIntercept(origin, targetPos, targetVel, projectileSpeed, maxLeadSeconds, out lead))
					{
						targetWorld = new Vector3(lead.x, lead.y, currentTarget.transform.position.z);
						if (debugDrawLead)
						{
							Debug.DrawLine(origin, targetWorld, Color.cyan, 0f, false);
						}
					}
					else
					{
						targetWorld = currentTarget.transform.position;
					}
				}
				else
				{
					targetWorld = currentTarget.transform.position;
				}
			}
			else
			{
				var cam = Camera.main;
				if (cam == null) return;
#if ENABLE_INPUT_SYSTEM
				var mouse = Mouse.current;
				if (mouse == null) return;
				var mousePos = mouse.position.ReadValue(); // Vector2
				var depth = cam.WorldToScreenPoint(rotationPivot.position).z;
				var mouseScreen = new Vector3(mousePos.x, mousePos.y, depth);
				targetWorld = cam.ScreenToWorldPoint(mouseScreen);
#else
				var mouseScreen = Input.mousePosition;
				mouseScreen.z = cam.WorldToScreenPoint(rotationPivot.position).z;
				targetWorld = cam.ScreenToWorldPoint(mouseScreen);
#endif
			}

			Vector2 dir = (Vector2)(targetWorld - rotationPivot.position);
			if (dir.sqrMagnitude < 0.0001f) return;
			float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
			float current = rotationPivot.eulerAngles.z;

			// Оценка наведения до применения поворота
			float err = Mathf.Abs(Mathf.DeltaAngle(current, targetAngle));
			isAimedNow = err <= Mathf.Max(0f, fireAngleToleranceDeg);

			if (instantAim)
			{
				rotationPivot.rotation = Quaternion.Euler(0, 0, targetAngle);
			}
			else
			{
				float next = Mathf.MoveTowardsAngle(current, targetAngle, aimSpeedDegPerSec * Time.deltaTime);
				rotationPivot.rotation = Quaternion.Euler(0, 0, next);
			}
		}

		// Решение перехвата: ||r + v*t|| = s*t
		private static bool TrySolveIntercept(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVel, float projectileSpeed, float maxT, out Vector2 impactPoint)
		{
			impactPoint = targetPos;
			if (projectileSpeed <= 0.001f)
			{
				return false;
			}

			Vector2 r = targetPos - shooterPos;
			float vv = Vector2.Dot(targetVel, targetVel);
			float rr = Vector2.Dot(r, r);
			float rv = Vector2.Dot(r, targetVel);
			float s2 = projectileSpeed * projectileSpeed;

			// Квадратное: (vv - s^2) t^2 + 2(rv) t + rr = 0
			float a = vv - s2;
			float b = 2f * rv;
			float c = rr;

			float t;
			if (Mathf.Abs(a) < 1e-6f)
			{
				// Линейный случай: s ~= |v| → t = -c / b
				if (Mathf.Abs(b) < 1e-6f) return false;
				t = -c / b;
				if (t <= 0f) return false;
			}
			else
			{
				float disc = b * b - 4f * a * c;
				if (disc < 0f) return false;
				float sqrt = Mathf.Sqrt(disc);
				float t1 = (-b - sqrt) / (2f * a);
				float t2 = (-b + sqrt) / (2f * a);
				// Нужен наименьший положительный корень
				t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
				if (t <= 0f) return false;
			}

			if (maxT > 0f) t = Mathf.Min(t, maxT);
			impactPoint = targetPos + targetVel * t;
			return true;
		}

		private Space.AsteroidController AcquireTarget()
		{
			var asteroids = UnityEngine.Object.FindObjectsByType<Space.AsteroidController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			if (asteroids == null || asteroids.Length == 0) return null;
			Vector2 origin = rotationPivot.position;

			// 1) Непреголит, диаметр >= порога
			var best = PickNearest(asteroids, origin, preferRegolith: false);
			if (best != null) return best;

			// 2) Реголит, диаметр >= порога
			return PickNearest(asteroids, origin, preferRegolith: true);
		}

		private Space.AsteroidController PickNearest(Space.AsteroidController[] asteroids, Vector2 origin, bool preferRegolith)
		{
			float bestDist = float.MaxValue;
			Space.AsteroidController best = null;
			for (int i = 0; i < asteroids.Length; i++)
			{
				var a = asteroids[i];
				if (a == null || !a.gameObject.activeInHierarchy) continue;
				if (a.Diameter < Mathf.Max(0f, targetDiameterMin)) continue;

				bool isReg = IsRegolith(a);
				if (preferRegolith != isReg) continue;

				float d = Vector2.Distance(origin, (Vector2)a.transform.position);
				if (maxTargetDistance > 0f && d > maxTargetDistance) continue;
				if (d < bestDist)
				{
					bestDist = d;
					best = a;
				}
			}
			return best;
		}

		private static bool IsRegolith(Space.AsteroidController a)
		{
			if (a == null) return false;
			string id = a.AsteroidId ?? "";
			string ore = a.OreId ?? "";
			// реголит — если имя содержит "Реголит" или ore_id == "none"
			if (id.IndexOf("Реголит", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (string.Equals(ore, "none", System.StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}
	}
}


