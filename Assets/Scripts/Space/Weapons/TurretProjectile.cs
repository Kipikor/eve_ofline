using UnityEngine;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class TurretProjectile : MonoBehaviour
	{
		[SerializeField] private float lifeTime = 5f;
		[SerializeField] private float speed = 20f;
		[SerializeField] private float damage = 1f;
		[SerializeField] private GameObject hitEffectPrefab;
		[SerializeField] private float hitEffectLifetime = 2f;

		private float deathTime;
		private Rigidbody2D body;
		private Collider2D hitbox;
		private Transform ownerRoot;
		private Collider2D[] ownerCollidersCache;

		public float Damage => damage;

		private void Awake()
		{
			body = GetComponent<Rigidbody2D>();
			if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
			body.gravityScale = 0f;
			body.bodyType = RigidbodyType2D.Dynamic;
			// Надёжная детекция для быстрых пуль без лучей
			body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

			hitbox = GetComponent<Collider2D>();
			if (hitbox == null)
			{
				var circle = gameObject.AddComponent<CircleCollider2D>();
				// НЕ триггер — используем реальные столкновения
				circle.isTrigger = false;
				circle.radius = 0.05f;
				hitbox = circle;
			}
			else
			{
				hitbox.isTrigger = false;
			}
			deathTime = Time.time + Mathf.Max(0.1f, lifeTime);
		}

		public void SetOwner(Transform owner)
		{
			ownerRoot = owner;
			// Закэшируем коллайдеры владельца и отключим столкновения
			if (ownerRoot != null)
			{
				ownerCollidersCache = ownerRoot.GetComponentsInChildren<Collider2D>(true);
				if (hitbox != null && ownerCollidersCache != null)
				{
					for (int i = 0; i < ownerCollidersCache.Length; i++)
					{
						if (ownerCollidersCache[i] != null)
						{
							Physics2D.IgnoreCollision(hitbox, ownerCollidersCache[i], true);
						}
					}
				}
			}
		}

		public void Launch(Vector2 position, Vector2 direction, float initialSpeed)
		{
			transform.position = position;
			if (direction.sqrMagnitude > 0.0001f)
			{
				direction.Normalize();
			}
			else
			{
				direction = Vector2.up;
			}
			float v = initialSpeed > 0f ? initialSpeed : speed;
			body.linearVelocity = direction * v;
			float z = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
			transform.rotation = Quaternion.Euler(0f, 0f, z);
		}

		private void Update()
		{
			if (Time.time >= deathTime)
			{
				Destroy(gameObject);
			}
		}

		private void SpawnHitEffect(Vector2 position, Vector2 direction, Transform parent)
		{
			if (hitEffectPrefab == null) return;
			float ang = direction.sqrMagnitude > 0.0001f
				? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f
				: 0f;
			var rot = Quaternion.Euler(0f, 0f, ang);
			var fx = Object.Instantiate(hitEffectPrefab, position, rot, parent);
			// Чтобы эффект ехал вместе с объектом: переводим симуляцию в Local
			var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
			for (int i = 0; i < systems.Length; i++)
			{
				var ps = systems[i];
				var main = ps.main;
				main.simulationSpace = ParticleSystemSimulationSpace.Local;
			}
			Object.Destroy(fx, Mathf.Max(0.1f, hitEffectLifetime));
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (collision == null) return;
			if (ownerRoot != null && collision.transform != null && collision.transform.IsChildOf(ownerRoot)) return;
			var contact = collision.GetContact(0);
			Vector2 point = contact.point;
			// Ориентация эффекта — по вектору полёта пули (а не по нормали поверхности)
			Vector2 flightDir = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
				? body.linearVelocity.normalized
				: (Vector2)(transform.up);
			// Применяем урон по астероиду (если он есть)
			var asteroid = collision.transform != null ? collision.transform.GetComponentInParent<Space.AsteroidController>() : null;
			if (asteroid == null) return;
			asteroid.ApplyDamage(Mathf.RoundToInt(damage));
			// Вращаем эффект по направлению пули, прицепляем к астероиду чтобы ехал вместе с ним
			SpawnHitEffect(point, flightDir, asteroid.transform);
			Destroy(gameObject);
		}
	}
}


