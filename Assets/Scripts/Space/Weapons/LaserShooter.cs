using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class LaserShooter : MonoBehaviour
	{
		[Header("Beam Visual")]
		[SerializeField] private Color beamColor = Color.cyan;
		[SerializeField, Min(0.001f)] private float beamWidth = 0.06f;
		[SerializeField, Min(1f)] private float maxDistance = 25f;

		[Header("Damage")]
		[SerializeField, Min(0f)] private float damagePerSecond = 30f;

		[Header("Hit FX")]
		[SerializeField] private GameObject hitEffectPrefab;
		[SerializeField, Min(0.05f)] private float hitEffectLifetime = 1.5f;

		[Header("Sorting")]
		[SerializeField] private string sortingLayerName = "Default";
		[SerializeField] private int sortingOrder = 60;

		[SerializeField] private bool holdMouseToFire = true;
		[SerializeField] private LayerMask hitMask = ~0; // по умолчанию все слои
		[SerializeField, Min(0f)] private float originOffset = 0.08f; // смещение старта луча вперёд от дула

		private struct BeamData
		{
			public Transform muzzle;
			public LineRenderer lr;
		}

		private readonly List<BeamData> beams = new List<BeamData>();
		private Collider2D[] ownerColliders;

		private void Awake()
		{
			ownerColliders = GetComponentInParent<Transform>()?.GetComponentsInChildren<Collider2D>(true);
			SetupBeams();
		}

		private void SetupBeams()
		{
			beams.Clear();
			var muzzles = FindMuzzlesOrCreateAuto();
			for (int i = 0; i < muzzles.Count; i++)
			{
				var lrGo = new GameObject("laser_lr_" + i);
				lrGo.transform.SetParent(transform, false);
				var lr = lrGo.AddComponent<LineRenderer>();
				lr.positionCount = 2;
				lr.useWorldSpace = true;
				lr.numCapVertices = 2;
				lr.numCornerVertices = 2;
				lr.loop = false;
				var sh = Shader.Find("Sprites/Default");
				if (sh == null) sh = Shader.Find("Unlit/Color");
				lr.material = new Material(sh);
				lr.textureMode = LineTextureMode.Stretch;
				lr.alignment = LineAlignment.View;
				lr.startColor = lr.endColor = beamColor;
				lr.startWidth = lr.endWidth = beamWidth;
				if (!string.IsNullOrEmpty(sortingLayerName)) lr.sortingLayerName = sortingLayerName;
				lr.sortingOrder = sortingOrder;
				lr.enabled = false;
				beams.Add(new BeamData { muzzle = muzzles[i], lr = lr });
			}
		}

		private List<Transform> FindMuzzlesOrCreateAuto()
		{
			var list = new List<Transform>();
			var root = transform;
			var stack = new Stack<Transform>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				var t = stack.Pop();
				if (t != root && t.name.StartsWith("muzzle"))
				{
					list.Add(t);
				}
				for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
			}
			if (list.Count == 0)
			{
				var auto = new GameObject("muzzle_auto").transform;
				auto.SetParent(transform, false);
				list.Add(auto);
			}
			return list;
		}

		private void Update()
		{
			bool wantFire = false;
#if ENABLE_INPUT_SYSTEM
			var mouse = Mouse.current;
			if (mouse != null) wantFire = holdMouseToFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
#else
			wantFire = holdMouseToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
#endif

			if (!wantFire) { SetVisible(false); return; }
			FireContinuous();
		}

		private void SetVisible(bool v)
		{
			for (int i = 0; i < beams.Count; i++) if (beams[i].lr != null) beams[i].lr.enabled = v;
		}

		private void FireContinuous()
		{
			int damageTick = Mathf.Max(1, Mathf.RoundToInt(damagePerSecond * Time.deltaTime));

			for (int i = 0; i < beams.Count; i++)
			{
				var data = beams[i];
				if (data.muzzle == null || data.lr == null) continue;
				data.lr.enabled = true;

				Vector2 origin = data.muzzle.position;
				Vector2 dir = data.muzzle.up;
				float dist = maxDistance;

				// Смещаем старт, чтобы не «упираться» в собственный носик/коллайдер
				origin += dir * originOffset;

				// Временно отключаем «старты внутри коллайдеров» для чистого результата
				bool prevQ = Physics2D.queriesStartInColliders;
				Physics2D.queriesStartInColliders = false;
				var hits = Physics2D.RaycastAll(origin, dir, dist, hitMask);
				Physics2D.queriesStartInColliders = prevQ;

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
					// если не астероид — это препятствие для луча
					end = hit.point;
					break;
				}

				if (found != null)
				{
					found.ApplyDamage(damageTick);
					end = hitPoint;
					SpawnHitFx(found.transform, hitPoint, dir);
				}

				// Устанавливаем два конца сегмента
				if (data.lr.positionCount != 2) data.lr.positionCount = 2;
				data.lr.SetPosition(0, origin);
				data.lr.SetPosition(1, end);
			}
		}

		private void SpawnHitFx(Transform parent, Vector2 point, Vector2 dir)
		{
			if (hitEffectPrefab == null || parent == null) return;
			float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
			var fx = Instantiate(hitEffectPrefab, point, Quaternion.Euler(0, 0, ang), parent);
			var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
			for (int i = 0; i < systems.Length; i++)
			{
				var main = systems[i].main;
				main.simulationSpace = ParticleSystemSimulationSpace.Local;
			}
			Destroy(fx, hitEffectLifetime);
		}

		private bool IsOwnerCollider(Collider2D col)
		{
			if (ownerColliders == null) return false;
			for (int i = 0; i < ownerColliders.Length; i++) if (ownerColliders[i] == col) return true;
			return false;
		}
	}
}


