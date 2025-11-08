using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class TurretShooter : MonoBehaviour
	{
		[Header("Firing")]
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private float projectileSpeed = 20f;
		[SerializeField] private float fireRate = 4f; // выстр/сек
		[SerializeField] private bool fireAllMuzzles = true; // если false — циклически по одному
		[SerializeField] private bool holdMouseToFire = true;

		private readonly List<Transform> muzzles = new List<Transform>();
		private int muzzleIndex;
		private float nextFireTime;

		private void Awake()
		{
			FindMuzzles();
		}

		private void FindMuzzles()
		{
			muzzles.Clear();
			var root = transform;
			var stack = new Stack<Transform>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				var t = stack.Pop();
				if (t != root && t.name.StartsWith("muzzle"))
				{
					muzzles.Add(t);
				}
				for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
			}
			if (muzzles.Count == 0)
			{
				Debug.LogWarning($"[TurretShooter] Не найдены точки \"muzzle\" под {gameObject.name}. Добавьте дочерние трансформы с именем начинающимся на \"muzzle\".", this);
			}
		}

		private void Update()
		{
			bool wantFire = false;
#if ENABLE_INPUT_SYSTEM
			var mouse = Mouse.current;
			if (mouse != null)
			{
				wantFire = holdMouseToFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
			}
#else
			wantFire = holdMouseToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
#endif
			if (!wantFire) return;
			if (Time.time < nextFireTime) return;
			nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0f);
			FireNow();
		}

		public void FireNow()
		{
			if (muzzles.Count == 0) return;
			if (projectilePrefab == null)
			{
				Debug.LogWarning("[TurretShooter] projectilePrefab не задан. Выстрел отменён.", this);
				return;
			}

			if (fireAllMuzzles)
			{
				for (int i = 0; i < muzzles.Count; i++)
				{
					SpawnProjectile(muzzles[i]);
				}
			}
			else
			{
				var t = muzzles[muzzleIndex % muzzles.Count];
				muzzleIndex = (muzzleIndex + 1) % muzzles.Count;
				SpawnProjectile(t);
			}
		}

		private void SpawnProjectile(Transform muzzle)
		{
			var go = Instantiate(projectilePrefab);
			var proj = go.GetComponent<TurretProjectile>();
			if (proj == null) proj = go.AddComponent<TurretProjectile>();
			// Владелец — корневой объект корабля (ShipController в родителях)
			EveOffline.Space.ShipController owner = GetComponentInParent<EveOffline.Space.ShipController>();
			if (owner != null) proj.SetOwner(owner.transform);
			Vector2 pos = muzzle.position;
			Vector2 dir = muzzle.up;
			proj.Launch(pos, dir, projectileSpeed);
		}
	}
}


