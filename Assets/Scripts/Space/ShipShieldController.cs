using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EveOffline.Space
{
	[DisallowMultipleComponent]
	public class ShipShieldController : MonoBehaviour
	{
		[Header("Shield (auto from ship.json)")]
		[SerializeField] private float shieldHpMax = 0f;
		[SerializeField] private float shieldRegenPerSec = 0f;
		[SerializeField] private float shieldRadiusMeters = 0f;
		[SerializeField] private float shieldCooldownSeconds = 0f;

		[Header("Tuning")]
		[SerializeField, Min(0f)] private float minDamageSpeed = 1.0f; // м/с, ниже — урон не наносится
		[SerializeField] private Color shieldColor = new Color(0.2f, 0.6f, 1f, 0.35f);
		[SerializeField, Min(3)] private int circleSegments = 64;
		[SerializeField, Min(0f)] private float lineWidth = 0.12f;

		// Runtime
		[SerializeField] private float shieldHpCurrent = 0f;
		[SerializeField] private bool shieldActive = false;
		private float cooldownUntilTime = 0f;

		private Transform shieldRoot;
		private CircleCollider2D shieldCollider;
		private LineRenderer lineRenderer;
		private ShipController ship;

		private void Awake()
		{
			ship = GetComponent<ShipController>();
			LoadShieldFromConfig();
			EnsureShieldObjects();
			ApplyShieldVisual();
			EnableShieldImmediate(shieldHpCurrent > 0f);
		}

		private void Update()
		{
			// Реген щита всегда идёт (даже в КД), но не выше максимума
			if (shieldHpCurrent < shieldHpMax && shieldRegenPerSec > 0f)
			{
				shieldHpCurrent = Mathf.Min(shieldHpMax, shieldHpCurrent + shieldRegenPerSec * Time.deltaTime);
			}

			// Выход из КД
			if (!shieldActive && Time.time >= cooldownUntilTime && shieldHpCurrent > 0f)
			{
				EnableShieldImmediate(true);
			}

			// Поддержка визуализации радиуса (на случай изменения конфигов в рантайме)
			if (shieldRoot != null)
			{
				shieldRoot.localPosition = Vector3.zero;
			}
		}

		public void OnShieldCollision(Collision2D collision)
		{
			if (!shieldActive) return;

			var otherRb = collision.rigidbody;
			var asteroid = collision.transform.GetComponentInParent<global::Space.AsteroidController>();
			if (asteroid == null) return;

			float relSpeed = collision.relativeVelocity.magnitude;
			if (relSpeed < Mathf.Max(0f, minDamageSpeed)) return;

			// Массы (в "тонах" проекта): корабля и астероида
			float m1 = 1f; // ship
			float m2 = 1f; // asteroid
			var shipRb = ship != null ? ship.GetComponent<Rigidbody2D>() : null;
			if (shipRb != null && shipRb.mass > 0f) m1 = shipRb.mass;
			if (otherRb != null && otherRb.mass > 0f) m2 = otherRb.mass;
			else
			{
				// Фолбэк, если у астероида нет ригидбади
				try { m2 = Mathf.Max(1f, asteroid.GetMassRounded()); } catch { m2 = 1f; }
			}

			// Приведённая масса μ = (m1*m2)/(m1+m2)
			float mu = (m1 > 0f && m2 > 0f) ? (m1 * m2) / (m1 + m2) : Mathf.Max(m1, m2);
			// Кинетическая энергия удара E = 0.5 * μ * v^2
			// В наших единицах: масса в "тоннах" → 1 (т·м²/с²) = 1 кДж.
			float energyTotalKJ = 0.5f * mu * relSpeed * relSpeed;
			// Делим поровну между телами → энергия, приходящаяся на каждое тело (в кДж)
			float energyEachKJ = energyTotalKJ * 0.5f;
			// 1 кДж = 1 урон: астероид получает полный урон, щит — урон/100
			int asteroidDamage = Mathf.Max(1, Mathf.RoundToInt(energyEachKJ));
			int shieldDamage = Mathf.Max(1, Mathf.RoundToInt(energyEachKJ / 100f));

			// Лог удара
			Debug.Log($"[ShieldHit] v={relSpeed:0.###} m_ship={m1:0.###} m_ast={m2:0.###} mu={mu:0.###} E_total={energyTotalKJ:0.###}kJ dmg_each={energyEachKJ:0.###}kJ dmg_ast={asteroidDamage} dmg_shield={shieldDamage}", this);

			// Наносим урон астероиду
			asteroid.ApplyDamage(asteroidDamage);

			// Урон щиту
			shieldHpCurrent -= shieldDamage;
			if (shieldHpCurrent <= 0f)
			{
				shieldHpCurrent = 0f;
				EnterCooldown();
			}
			UpdateShieldColor();
		}

		private void EnterCooldown()
		{
			cooldownUntilTime = Time.time + Mathf.Max(0f, shieldCooldownSeconds);
			EnableShieldImmediate(false);
		}

		private void EnableShieldImmediate(bool enabledNow)
		{
			shieldActive = enabledNow;
			if (shieldCollider != null) shieldCollider.enabled = enabledNow;
			if (lineRenderer != null) lineRenderer.enabled = enabledNow;
			UpdateShieldColor();
		}

		private void LoadShieldFromConfig()
		{
			// Попытаемся прочитать ship.json по тем же правилам, что и ShipController
			string text = TryLoadShipConfigText();
			if (string.IsNullOrWhiteSpace(text))
			{
				// нет конфига — выключим щит
				shieldHpMax = 0f;
				shieldRegenPerSec = 0f;
				shieldRadiusMeters = 0f;
				shieldCooldownSeconds = 0f;
				shieldHpCurrent = 0f;
				return;
			}

			try
			{
				var records = JsonArrayHelper.FromJson<ShipRecordMinimal>(text);
				if (records != null && records.Length > 0)
				{
					int idx = 0;
					string idWanted = ship != null ? GetPrivateFieldShipId(ship) : null;
					if (!string.IsNullOrWhiteSpace(idWanted))
					{
						for (int i = 0; i < records.Length; i++)
						{
							if (string.Equals(records[i].id, idWanted, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
						}
					}

					shieldHpMax = Mathf.Max(0f, records[idx].shield_hp);
					shieldRegenPerSec = Mathf.Max(0f, records[idx].shield_regen);
					shieldCooldownSeconds = Mathf.Max(0f, records[idx].shield_cd);

					// В ship.json радиус хранится как shield_radius
					shieldRadiusMeters = Mathf.Max(0f, records[idx].shield_radius);

					// Текущее значение по умолчанию — максимум
					shieldHpCurrent = shieldHpMax;
				}
			}
			catch
			{
				// оставляем значения по умолчанию
			}
		}

		private static string TryLoadShipConfigText()
		{
			TextAsset ta = Resources.Load<TextAsset>("Config/ship");
			if (ta == null) ta = Resources.Load<TextAsset>("ship");
			if (ta != null && !string.IsNullOrWhiteSpace(ta.text)) return ta.text;

#if UNITY_EDITOR
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

		[Serializable]
		private class ShipRecordMinimal
		{
			public string id;
			public float shield_hp;
			public float shield_regen;
			public float shield_radius;
			public float shield_cd;
		}

		private static string GetPrivateFieldShipId(ShipController sc)
		{
			// ShipController.shipId — приватное поле; попытаемся достать через SerializedObject в редакторе, иначе вернём null
#if UNITY_EDITOR
			if (!Application.isPlaying && sc != null)
			{
				var so = new SerializedObject(sc);
				var sp = so.FindProperty("shipId");
				if (sp != null) return sp.stringValue;
			}
#endif
			// В рантайме нет доступа — вернём null, используем первую запись из ship.json
			return null;
		}

		private void EnsureShieldObjects()
		{
			// Корневой объект щита
			var existing = transform.Find("shield");
			shieldRoot = existing != null ? existing : new GameObject("shield").transform;
			shieldRoot.SetParent(transform, false);
			shieldRoot.localPosition = Vector3.zero;
			shieldRoot.localRotation = Quaternion.identity;

			// Коллайдер
			shieldCollider = shieldRoot.GetComponent<CircleCollider2D>();
			if (shieldCollider == null) shieldCollider = shieldRoot.gameObject.AddComponent<CircleCollider2D>();
			shieldCollider.isTrigger = false; // физически существует
			shieldCollider.usedByEffector = false;
			shieldCollider.radius = Mathf.Max(0f, shieldRadiusMeters);

			// Релей коллизий
			var relay = shieldRoot.GetComponent<ShipShieldHitRelay>();
			if (relay == null) relay = shieldRoot.gameObject.AddComponent<ShipShieldHitRelay>();
			relay.owner = this;

			// Линия круга
			lineRenderer = shieldRoot.GetComponent<LineRenderer>();
			if (lineRenderer == null) lineRenderer = shieldRoot.gameObject.AddComponent<LineRenderer>();
			lineRenderer.useWorldSpace = false;
			lineRenderer.loop = true;
			lineRenderer.positionCount = Mathf.Max(3, circleSegments);
			lineRenderer.widthMultiplier = Mathf.Max(0f, lineWidth);
			lineRenderer.material = GetDefaultLineMaterial();

			RebuildCircle();
			UpdateShieldColor();
		}

		private void ApplyShieldVisual()
		{
			if (shieldCollider != null) shieldCollider.radius = Mathf.Max(0f, shieldRadiusMeters);
			RebuildCircle();
			UpdateShieldColor();
		}

		private void RebuildCircle()
		{
			if (lineRenderer == null) return;
			int count = Mathf.Max(3, circleSegments);
			if (lineRenderer.positionCount != count) lineRenderer.positionCount = count;
			float r = Mathf.Max(0f, shieldRadiusMeters);
			for (int i = 0; i < count; i++)
			{
				float t = (i / (float)count) * Mathf.PI * 2f;
				float x = Mathf.Cos(t) * r;
				float y = Mathf.Sin(t) * r;
				lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
			}
		}

		private void UpdateShieldColor()
		{
			if (lineRenderer == null) return;
			float k = shieldHpMax > 0f ? Mathf.Clamp01(shieldHpCurrent / shieldHpMax) : 0f;
			Color c = shieldColor;
			c.a = Mathf.Lerp(0.15f, shieldColor.a, k);
			lineRenderer.startColor = c;
			lineRenderer.endColor = c;
		}

		private static Material GetDefaultLineMaterial()
		{
			// Создадим простой материал для LineRenderer, если не задан
			var mat = new Material(Shader.Find("Sprites/Default"));
			return mat;
		}

		// Вспомогательный компонент на дочернем объекте с коллайдером щита
		[DisallowMultipleComponent]
		private class ShipShieldHitRelay : MonoBehaviour
		{
			[NonSerialized] public ShipShieldController owner;

			private void OnCollisionEnter2D(Collision2D collision)
			{
				if (owner != null) owner.OnShieldCollision(collision);
			}
		}
	}
}



