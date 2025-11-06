using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EveOffline.Space
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Rigidbody2D))]
	public class ShipController : MonoBehaviour
	{
		[Header("Config Source")]
		[SerializeField] private TextAsset shipConfigJson;

		[Header("Tuning")]
		[SerializeField] private float acceleration = 25f; // м/с^2 для плавного разгона/торможения

		private Rigidbody2D shipBody;
		private float maxLinearSpeedMetersPerSecond = 6f;
		private float rotationSpeedDegreesPerSecond = 90f;
		private bool isAlignToMouseEnabled;

		[Serializable]
		private class ShipRecord
		{
			public string id;
			public string name;
			public string descr;
			public string sprite;
			public float cost;
			public float shield_hp;
			public float shield_regen;
			public float shield_cd;
			public float armor_hp;
			public float structure_hp;
			public float power;
			public float cpu;
			public float speed;      // м/с
			public float rotation;   // градусы/сек
		}

		private void Awake()
		{
			shipBody = GetComponent<Rigidbody2D>();
			shipBody.gravityScale = 0f;
			shipBody.freezeRotation = true; // поворачиваем вручную

			LoadShipConfig();
		}

		private void LoadShipConfig()
		{
			if (shipConfigJson == null || string.IsNullOrWhiteSpace(shipConfigJson.text))
			{
				return;
			}

			try
			{
				var records = JsonArrayHelper.FromJson<ShipRecord>(shipConfigJson.text);
				if (records != null && records.Length > 0)
				{
					// Берем первую запись (по умолчанию "condor")
					maxLinearSpeedMetersPerSecond = Mathf.Max(0f, records[0].speed);
					rotationSpeedDegreesPerSecond = Mathf.Max(0f, records[0].rotation);
				}
			}
			catch (Exception)
			{
				// Если парсинг не удался — оставим значения по умолчанию
			}
		}

		private void Update()
		{
			var keyboard = Keyboard.current;
			if (keyboard != null)
			{
				if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame)
				{
					isAlignToMouseEnabled = !isAlignToMouseEnabled;
				}
			}
		}

		private void FixedUpdate()
		{
			UpdateMovement();
			UpdateRotationToMouseIfEnabled();
		}

		private void UpdateMovement()
		{
			Vector2 inputDirection = ReadWasdDirection();
			Vector2 desiredVelocity = inputDirection * maxLinearSpeedMetersPerSecond;

			float maxDelta = Mathf.Max(0f, acceleration) * Time.fixedDeltaTime;
			shipBody.linearVelocity = Vector2.MoveTowards(shipBody.linearVelocity, desiredVelocity, maxDelta);
		}

		private static Vector2 ReadWasdDirection()
		{
			var keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return Vector2.zero;
			}

			Vector2 dir = Vector2.zero;
			if (keyboard.wKey.isPressed) dir.y += 1f;
			if (keyboard.sKey.isPressed) dir.y -= 1f;
			if (keyboard.aKey.isPressed) dir.x -= 1f;
			if (keyboard.dKey.isPressed) dir.x += 1f;
			return dir.sqrMagnitude > 1f ? dir.normalized : dir;
		}

		private void UpdateRotationToMouseIfEnabled()
		{
			if (!isAlignToMouseEnabled)
			{
				return;
			}

			var mouse = Mouse.current;
			var mainCamera = Camera.main;
			if (mouse == null || mainCamera == null)
			{
				return;
			}

			Vector3 mouseScreen = mouse.position.ReadValue();
			Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);
			Vector2 toMouse = (Vector2)(mouseWorld - transform.position);

			if (toMouse.sqrMagnitude < 0.0001f)
			{
				return;
			}

			float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - 90f; // нос корабля вверх
			float newAngle = Mathf.MoveTowardsAngle(shipBody.rotation, targetAngle, rotationSpeedDegreesPerSecond * Time.fixedDeltaTime);
			shipBody.MoveRotation(newAngle);
		}
	}

	internal static class JsonArrayHelper
	{
		[Serializable]
		private class Wrapper<T>
		{
			public T[] items;
		}

		public static T[] FromJson<T>(string json)
		{
			if (string.IsNullOrEmpty(json)) return Array.Empty<T>();
			string wrapped = "{\"items\":" + json + "}";
			var data = JsonUtility.FromJson<Wrapper<T>>(wrapped);
			return data?.items ?? Array.Empty<T>();
		}
	}
}
