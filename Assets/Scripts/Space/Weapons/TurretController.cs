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
			var cam = Camera.main;
			if (cam == null) return;
			Vector3 targetWorld;
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
	}
}


