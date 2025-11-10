using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;

namespace EveOffline.Space
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(BoxCollider2D))]
	public class ShipController : MonoBehaviour
	{
		[Header("Config")]
		[SerializeField] private string shipId;

		[Header("Camera")]
		[SerializeField] private bool attachCameraToShip = true;
		[SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 0f, -10f);
		[SerializeField] private bool enableZoom = true;
		[SerializeField] private float minOrthoSize = 2f;
		[SerializeField] private float maxOrthoSize = 30f;
		[SerializeField] private float zoomSpeed = 5f;
		[SerializeField] private bool clampZoomToSpawnBounds = true;
		private float baseOrthoSize;
		private float baseHalfWidth;

		private float acceleration = 25f; // из JSON (значение по умолчанию)
		private float shipWidthMeters = 1f;  // из JSON
		private float shipHeightMeters = 1f; // из JSON
		private string spriteKey;            // из JSON
		private float spriteScale = 1f;      // из JSON
		private float shipOffsetX;           // из JSON
		private float shipOffsetY;           // из JSON

		[Header("Cargo")]
		[SerializeField] private float cargoHolding = 750f; // м^3
		public float CargoHolding => cargoHolding;

		private Rigidbody2D shipBody;
		private float maxLinearSpeedMetersPerSecond = 6f;
		private float rotationTorqueKiloNewtonMeters = 2500f; // момент, кН·м
		private bool isAlignToMouseEnabled;
		private Toggle modeMouseToggle;
		
		public enum ForceOrientation
		{
			Local,  // усилия прикладываются относительно корабля (нос = вперёд)
			World   // усилия прикладываются относительно экрана/мира (W=вверх экрана)
		}
		
		[Header("Движение")]
		[SerializeField] private ForceOrientation forceOrientation = ForceOrientation.Local;
		// режим поворота к мыши контролируется чекбоксом в сцене (mode_mouse_orientatioon)

		[Serializable]
		private class ShipRecord
		{
			public string id;
			public string name;
			public string descr;
			public float mass;           // тонны
			public string sprite;
			public string Sprite_ship;   // альтернативный ключ для спрайта
			public float cost;
			public float shield_hp;
			public float shield_regen;
			public float shield_cd;
			public float armor_hp;
			public float structure_hp;
			public float power;
			public float cpu;
			public float speed;      // м/с (максимальная скорость)
			public float rotation;   // кН·м (момент)
			public float acceleration; // кН (тяга)
			public float accel;        // альтернативное имя
			public float width;        // м
			public float height;       // м
			public float size_x;       // альтернативное имя ширины
			public float size_y;       // альтернативное имя высоты
			public float offset_x;     // смещение коллайдера X (м)
			public float offset_y;     // смещение коллайдера Y (м)
			public float offsetX;      // альтернативное имя
			public float offsetY;      // альтернативное имя
			public float sprite_scale; // масштаб спрайта
			public float cargo_holding; // м^3
		}

		private void Awake()
		{
			shipBody = GetComponent<Rigidbody2D>();
			shipBody.gravityScale = 0f;
			shipBody.freezeRotation = false; // вращение физикой

			LoadShipConfig();
			ApplySizeAndVisuals();

			// Ищем чекбокс режима по имени
			var go = GameObject.Find("mode_mouse_orientatioon");
			if (go != null)
			{
				modeMouseToggle = go.GetComponent<Toggle>();
				if (modeMouseToggle != null)
				{
					// Берём начальное значение из UI и далее слушаем изменения
					isAlignToMouseEnabled = modeMouseToggle.isOn;
					modeMouseToggle.onValueChanged.AddListener(OnModeMouseToggleChanged);
				}
			}

			AttachCameraIfConfigured();
			CacheCameraBaseSize();
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			// В редакторе не меняем сам sprite, чтобы не ловить SendMessage в OnValidate
			LoadShipConfig();
			ApplySizeAndVisuals(changeSprite: false);

			// При изменении настроек поддержим позицию камеры
			if (attachCameraToShip)
			{
				var cam = Camera.main;
				if (cam != null && cam.transform.parent == transform)
				{
					cam.transform.localPosition = cameraLocalOffset;
					cam.transform.localRotation = Quaternion.identity;
				}
			}
			CacheCameraBaseSize();
		}
#endif

		private void LoadShipConfig()
		{
			string shipConfigText = TryLoadShipConfigText();
			if (string.IsNullOrWhiteSpace(shipConfigText)) return;

			try
			{
				var records = JsonArrayHelper.FromJson<ShipRecord>(shipConfigText);
				if (records != null && records.Length > 0)
				{
					int index = 0;
					if (!string.IsNullOrWhiteSpace(shipId))
					{
						for (int i = 0; i < records.Length; i++)
						{
							if (string.Equals(records[i].id, shipId, StringComparison.OrdinalIgnoreCase))
							{
								index = i;
								break;
							}
						}
					}

					maxLinearSpeedMetersPerSecond = Mathf.Max(0f, records[index].speed);
					rotationTorqueKiloNewtonMeters = Mathf.Max(0f, records[index].rotation);
					acceleration = records[index].acceleration != 0f ? records[index].acceleration : (records[index].accel != 0f ? records[index].accel : acceleration); // кН
					shipWidthMeters = records[index].width != 0f ? records[index].width : (records[index].size_x != 0f ? records[index].size_x : shipWidthMeters);
					shipHeightMeters = records[index].height != 0f ? records[index].height : (records[index].size_y != 0f ? records[index].size_y : shipHeightMeters);
					shipOffsetX = records[index].offset_x != 0f ? records[index].offset_x : (records[index].offsetX != 0f ? records[index].offsetX : 0f);
					shipOffsetY = records[index].offset_y != 0f ? records[index].offset_y : (records[index].offsetY != 0f ? records[index].offsetY : 0f);
					spriteKey = !string.IsNullOrWhiteSpace(records[index].Sprite_ship) ? records[index].Sprite_ship : records[index].sprite;
					spriteScale = records[index].sprite_scale != 0f ? records[index].sprite_scale : 1f;
					// Масса: в проекте трактуем напрямую как ТОННЫ, без пересчёта
					if (records[index].mass > 0f)
					{
						if (shipBody == null) shipBody = GetComponent<Rigidbody2D>();
						if (shipBody != null) shipBody.mass = records[index].mass;
					}

					// Грузовой отсек
					if (records[index].cargo_holding > 0f)
					{
						cargoHolding = records[index].cargo_holding;
					}
				}
			}
			catch (Exception)
			{
				// Если парсинг не удался — оставим значения по умолчанию
			}
		}

		private void ApplySizeAndVisuals(bool changeSprite = true)
		{
			// Коллайдер
			var box = GetComponent<BoxCollider2D>();
			if (box != null)
			{
				box.size = new Vector2(Mathf.Max(0.01f, shipWidthMeters), Mathf.Max(0.01f, shipHeightMeters));
				box.offset = new Vector2(shipOffsetX, shipOffsetY);
			}

			// Спрайт: берём дочерний объект "sprite" если есть; иначе первый SpriteRenderer в детях
			SpriteRenderer spriteRenderer = null;
			var spriteChild = transform.Find("sprite");
			if (spriteChild != null) spriteRenderer = spriteChild.GetComponent<SpriteRenderer>();
			if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			if (spriteRenderer != null)
			{
				// Назначаем спрайт только не в OnValidate
				if (changeSprite && !string.IsNullOrWhiteSpace(spriteKey))
				{
					var loaded = TryLoadSprite(spriteKey);
					if (loaded != null)
					{
						spriteRenderer.sprite = loaded;
					}
				}

				// Масштабирование спрайта по значению из JSON
				float scale = spriteScale <= 0f ? 1f : spriteScale;
				spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
			}
		}

		private static Sprite TryLoadSprite(string key)
		{
			// Пробуем из Resources по имени и с префиксом Sprites/
			Sprite s = Resources.Load<Sprite>(key);
			if (s == null) s = Resources.Load<Sprite>("Sprites/" + key);
			if (s != null) return s;

#if UNITY_EDITOR
			// Сначала пробуем по ожидаемому пути
			string basePath = "Assets/Sprites/ship/" + key;
			string[] exts = new[] { ".png", ".psb", ".jpg", ".jpeg" };
			for (int ei = 0; ei < exts.Length; ei++)
			{
				var p = basePath + exts[ei];
				var byPath = AssetDatabase.LoadAssetAtPath<Sprite>(p);
				if (byPath != null) return byPath;
			}

			// Если не нашли — ищем по имени во всём проекте
			string[] guids = AssetDatabase.FindAssets(key + " t:Sprite");
			for (int gi = 0; gi < guids.Length; gi++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
				var asset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
				if (asset != null && string.Equals(asset.name, key, StringComparison.OrdinalIgnoreCase))
				{
					return asset;
				}
			}
#endif

			return null;
		}

#if UNITY_EDITOR
		public void EditorReloadConfig()
		{
			LoadShipConfig();
			// В редакторе не меняем спрайт, чтобы избегать SendMessage в неподходящие моменты
			ApplySizeAndVisuals(changeSprite: false);
		}
#endif

		private static string TryLoadShipConfigText()
		{
			// 1) Resources (если файл находится в Assets/Resources/Config/ship.json или Assets/Resources/ship.json)
			TextAsset ta = Resources.Load<TextAsset>("Config/ship");
			if (ta == null) ta = Resources.Load<TextAsset>("ship");
			if (ta != null && !string.IsNullOrWhiteSpace(ta.text)) return ta.text;

#if UNITY_EDITOR
			// 2) В редакторе ищем ship.json по проекту
			string[] guids = AssetDatabase.FindAssets("ship t:TextAsset");
			for (int gi = 0; gi < guids.Length; gi++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
				if (!path.EndsWith("ship.json", StringComparison.OrdinalIgnoreCase)) continue;
				var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) return asset.text;
			}
#endif

			return null;
		}

		private void Update()
		{
			var keyboard = Keyboard.current;
			if (keyboard != null)
			{
				if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame)
				{
					isAlignToMouseEnabled = !isAlignToMouseEnabled;
					if (modeMouseToggle != null)
					{
						modeMouseToggle.SetIsOnWithoutNotify(isAlignToMouseEnabled);
					}
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
			Vector2 worldDirection;
			if (forceOrientation == ForceOrientation.Local)
			{
				// Локальная ориентация сил (вдоль корпуса)
				worldDirection =
					(Vector2)transform.right * inputDirection.x +
					(Vector2)transform.up * inputDirection.y;
			}
			else
			{
				// Мировая ориентация сил (относительно экрана)
				worldDirection = new Vector2(inputDirection.x, inputDirection.y);
			}
			// Тяга: используем кН напрямую (масса в тоннах), без пересчёта единиц
			float thrustUnits = Mathf.Max(0f, acceleration); // кН

			if (worldDirection.sqrMagnitude > 0.0001f)
			{
				worldDirection.Normalize();
				shipBody.AddForce(worldDirection * thrustUnits, ForceMode2D.Force);
			}
			else
			{
				// Автоторможение: прикладываем силу против скорости
				Vector2 v = shipBody.linearVelocity;
				if (v.sqrMagnitude > 0.0004f)
				{
					Vector2 brakeDir = -v.normalized;
					shipBody.AddForce(brakeDir * thrustUnits, ForceMode2D.Force);
				}
				else
				{
					shipBody.linearVelocity = Vector2.zero;
				}
			}

			// Ограничение максимальной скорости
			float speed = shipBody.linearVelocity.magnitude;
			if (speed > maxLinearSpeedMetersPerSecond && maxLinearSpeedMetersPerSecond > 0f)
			{
				shipBody.linearVelocity = shipBody.linearVelocity.normalized * maxLinearSpeedMetersPerSecond;
			}
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
			// Если режим ориентации выключен ИЛИ мышь заблокирована UI — не целимся на мышь, а демпфируем вращение
			if (!isAlignToMouseEnabled || global::UI.UiInput.IsMouseBlocked)
			{
				// Автодемпфирование вращения при отсутствии управления (кН·м напрямую)
				float torqueUnits = Mathf.Max(0f, rotationTorqueKiloNewtonMeters);
				float av = shipBody.angularVelocity; // град/с
				if (Mathf.Abs(av) > 0.1f)
				{
					float sign = av > 0f ? -1f : 1f;
					shipBody.AddTorque(sign * torqueUnits, ForceMode2D.Force);
				}
				else
				{
					shipBody.angularVelocity = 0f;
				}
				return;
			}

			var mouse = Mouse.current;
			var mainCamera = Camera.main;
			if (mouse == null || mainCamera == null)
			{
				return;
			}

			Vector3 mouseScreen = mouse.position.ReadValue();
			// Конвертируем координаты мыши на ту же глубину, что и корабль
			mouseScreen.z = mainCamera.WorldToScreenPoint(transform.position).z;
			Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);
			Vector2 toMouse = (Vector2)(mouseWorld - transform.position);

			if (toMouse.sqrMagnitude < 0.0001f)
			{
				return;
			}

			float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - 90f; // нос корабля вверх
			float currentAngle = shipBody.rotation;
			float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
			float torqueUnits2 = Mathf.Max(0f, rotationTorqueKiloNewtonMeters);
			float signTorque = Mathf.Sign(delta);
			if (Mathf.Abs(delta) > 0.5f)
			{
				shipBody.AddTorque(signTorque * torqueUnits2, ForceMode2D.Force);
			}
			else
			{
				shipBody.angularVelocity = 0f;
			}
		}

		private void OnDestroy()
		{
			if (modeMouseToggle != null)
			{
				modeMouseToggle.onValueChanged.RemoveListener(OnModeMouseToggleChanged);
			}
		}

		private void OnModeMouseToggleChanged(bool isOn)
		{
			isAlignToMouseEnabled = isOn;
		}

		private void AttachCameraIfConfigured()
		{
			if (!attachCameraToShip) return;
			var cam = Camera.main;
			if (cam == null) return;
			var camTr = cam.transform;
			// Камера не должна вращаться вместе с кораблём — держим её отдельно и только переносим
			if (camTr.parent == transform)
			{
				// Отсоединяем, чтобы не наследовать вращение
				camTr.SetParent(null, true);
			}
			SetCameraWorldTransform(cam);
		}

		private void LateUpdate()
		{
			if (!attachCameraToShip) return;
			var cam = Camera.main;
			if (cam == null) return;
			SetCameraWorldTransform(cam);

			// Блокируем масштабирование мышью, если UI захватывает мышь
			if (enableZoom && !global::UI.UiInput.IsMouseBlocked)
			{
				HandleZoom(cam);
			}
		}

		private void SetCameraWorldTransform(Camera cam)
		{
			// Позиция = позиция корабля + фиксированный мировой сдвиг, поворот фиксированный
			var shipPos = transform.position;
			var worldPos = new Vector3(shipPos.x + cameraLocalOffset.x, shipPos.y + cameraLocalOffset.y, cameraLocalOffset.z);
			var camTr = cam.transform;
			camTr.position = worldPos;
			camTr.rotation = Quaternion.identity;
		}

		private void HandleZoom(Camera cam)
		{
			float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
			var mouse = UnityEngine.InputSystem.Mouse.current;
			if (mouse != null) scroll = mouse.scroll.ReadValue().y * 0.01f;
#endif

			if (!Mathf.Approximately(scroll, 0f))
			{
				float target = cam.orthographicSize - scroll * zoomSpeed;
				float maxAllowed = ComputeMaxOrthoAllowed(cam);
				cam.orthographicSize = Mathf.Clamp(target, Mathf.Max(0.01f, minOrthoSize), maxAllowed);
			}
		}

		private void CacheCameraBaseSize()
		{
			var cam = Camera.main;
			if (cam == null) return;
			baseOrthoSize = cam.orthographicSize <= 0f ? 5f : cam.orthographicSize;
			baseHalfWidth = baseOrthoSize * (cam.orthographic ? cam.aspect : (16f / 9f));
		}

		private float ComputeMaxOrthoAllowed(Camera cam)
		{
			float maxAllowed = maxOrthoSize;
			if (clampZoomToSpawnBounds)
			{
				var am = UnityEngine.Object.FindFirstObjectByType<global::Space.AsteroidManager>();
				if (am == null) am = UnityEngine.Object.FindAnyObjectByType<global::Space.AsteroidManager>();
				if (am != null)
				{
					// Базовый прямоугольник base → синий = base + margin. Камера не должна превышать по высоте H0 + margin и по ширине W0 + margin.
					float margin = am.CameraSpawnMargin;
					float byHeight = baseOrthoSize + margin;
					float byWidth = (baseHalfWidth + margin) / Mathf.Max(0.01f, (cam.orthographic ? cam.aspect : (16f / 9f)));
					maxAllowed = Mathf.Min(maxAllowed, Mathf.Min(byHeight, byWidth));
				}
			}
			return Mathf.Max(minOrthoSize, maxAllowed);
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

