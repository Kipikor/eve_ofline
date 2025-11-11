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

			private Space.AsteroidController currentTarget;
			private float nextRetargetTime;

			public Space.AsteroidController CurrentTarget => currentTarget;
			public bool HasTarget => currentTarget != null && currentTarget.gameObject.activeInHierarchy;

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
		}

		private void LateUpdate()
		{
			if (rotationPivot == null) return;

			Vector3 targetWorld;
			if (autoAim)
			{
				if (Time.time >= nextRetargetTime || !HasTarget)
				{
					nextRetargetTime = Time.time + Mathf.Max(0.05f, retargetEvery);
					currentTarget = AcquireTarget();
				}
				if (!HasTarget) return;
				targetWorld = currentTarget.transform.position;
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


