using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class LaserShooter : MonoBehaviour
	{
		[SerializeField] private LaserProfile profile;
		[SerializeField] private bool holdMouseToFire = true;
		[SerializeField] private LayerMask hitMask = ~0; // по умолчанию все слои

		private readonly List<Transform> muzzles = new List<Transform>();
		private readonly List<LaserBeam> beams = new List<LaserBeam>();
		private Collider2D[] ownerColliders;
		private Transform ownerRoot;

		private void Awake()
		{
			FindMuzzles();
			ownerRoot = GetComponentInParent<Transform>();
			if (ownerRoot != null) ownerColliders = ownerRoot.GetComponentsInChildren<Collider2D>(true);
			EnsureBeams();
		}

		private void EnsureBeams()
		{
			beams.Clear();
			for (int i = 0; i < muzzles.Count; i++)
			{
				var go = new GameObject("laser_beam_" + i);
				go.transform.SetParent(transform, false);
				var lb = go.AddComponent<LaserBeam>();
				if (profile != null)
				{
					lb.Initialize(profile.beamColor, profile.beamWidth, profile.hitEffectPrefab, profile.hitEffectLifetime, transform);
				}
				lb.SetVisible(false);
				beams.Add(lb);
			}
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
		}

		private void Update()
		{
			if (profile == null || muzzles.Count == 0) { SetAllVisible(false); return; }

			bool wantFire = false;
#if ENABLE_INPUT_SYSTEM
			var mouse = Mouse.current;
			if (mouse != null) wantFire = holdMouseToFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
#else
			wantFire = holdMouseToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
#endif

			if (!wantFire)
			{
				SetAllVisible(false);
				return;
			}

			FireContinuous();
		}

		private void SetAllVisible(bool v)
		{
			for (int i = 0; i < beams.Count; i++) beams[i].SetVisible(v);
		}

		private void FireContinuous()
		{
			// Урон за кадр
			int damageTick = Mathf.RoundToInt(Mathf.Max(0f, profile.damagePerSecond) * Time.deltaTime);
			damageTick = Mathf.Max(1, damageTick);

			for (int i = 0; i < muzzles.Count; i++)
			{
				if (i >= beams.Count) continue;
				var muzzle = muzzles[i];
				var beam = beams[i];
				beam.SetVisible(true);

				Vector2 origin = muzzle.position;
				Vector2 dir = muzzle.up;
				float dist = profile.maxDistance;

				// Луч с фильтрацией: берём первый хит НЕ владельца
				var hits = Physics2D.RaycastAll(origin, dir, dist, hitMask);
				Vector2 end = origin + dir * dist;
				Space.AsteroidController found = null;
				Vector2 hitPoint = end;
				for (int h = 0; h < hits.Length; h++)
				{
					var hit = hits[h];
					if (hit.collider == null) continue;
					if (IsOwnerCollider(hit.collider)) continue;
					var ast = hit.collider.GetComponentInParent<Space.AsteroidController>();
					if (ast != null)
					{
						found = ast;
						hitPoint = hit.point;
						break;
					}
					// если не астероид — считаем препятствием для луча
					end = hit.point;
					break;
				}

				if (found != null)
				{
					found.ApplyDamage(damageTick);
					end = hitPoint;
				}

				beam.SetSegment(origin, end, dir);
			}
		}

		private bool IsOwnerCollider(Collider2D col)
		{
			if (ownerColliders == null) return false;
			for (int i = 0; i < ownerColliders.Length; i++)
			{
				if (ownerColliders[i] == col) return true;
			}
			return false;
		}
	}
}


