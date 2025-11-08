using UnityEngine;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class LaserBeam : MonoBehaviour
	{
		private LineRenderer lr;
		private ParticleSystem hitFxInstance;

		public void Initialize(Color color, float width, GameObject hitEffectPrefab, float hitEffectLifetime, Transform parentForFx, string sortingLayer, int sortingOrder)
		{
			if (lr == null)
			{
				lr = gameObject.AddComponent<LineRenderer>();
				lr.positionCount = 2;
				lr.useWorldSpace = true;
				lr.numCapVertices = 2;
				lr.numCornerVertices = 2;
				var sh = Shader.Find("Sprites/Default");
				if (sh == null) sh = Shader.Find("Unlit/Color");
				lr.material = new Material(sh);
				lr.textureMode = LineTextureMode.Stretch;
				lr.alignment = LineAlignment.View;
				lr.loop = false;
			}
			lr.startColor = lr.endColor = color;
			lr.startWidth = lr.endWidth = Mathf.Max(0.001f, width);
			if (!string.IsNullOrEmpty(sortingLayer)) lr.sortingLayerName = sortingLayer;
			lr.sortingOrder = sortingOrder;
			lr.enabled = true;

			if (hitEffectPrefab != null && hitFxInstance == null)
			{
				var go = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity, parentForFx);
				hitFxInstance = go.GetComponent<ParticleSystem>();
				if (hitFxInstance == null) hitFxInstance = go.AddComponent<ParticleSystem>();
				Destroy(go, Mathf.Max(0.05f, hitEffectLifetime));
			}
		}

		public void SetSegment(Vector3 from, Vector3 to, Vector3 forwardDir)
		{
			if (lr == null) return;
			// На всякий случай всегда держим только 2 точки
			if (lr.positionCount != 2) lr.positionCount = 2;
			lr.loop = false;
			lr.SetPosition(0, from);
			lr.SetPosition(1, to);
			if (hitFxInstance != null)
			{
				var tr = hitFxInstance.transform;
				tr.position = to;
				if (forwardDir.sqrMagnitude > 0.0001f)
				{
					float ang = Mathf.Atan2(forwardDir.y, forwardDir.x) * Mathf.Rad2Deg - 90f;
					tr.rotation = Quaternion.Euler(0, 0, ang);
				}
			}
		}

		public void SetVisible(bool v)
		{
			if (lr != null) lr.enabled = v;
			if (!v && hitFxInstance != null)
			{
				// скрыть FX, если он ещё жив
				var em = hitFxInstance.emission;
				em.enabled = false;
			}
		}
	}
}


