using System.Collections.Generic;
using UnityEngine;

namespace Space.Weapons
{
	[DisallowMultipleComponent]
	public class TurretMount : MonoBehaviour
	{
		[Tooltip("ID турели из реестра. По умолчанию mining_turret_t1")]
		[SerializeField] private string defaultTurretId = "mining_turret_t1";

		private TurretPrefabRegistry registry;

		private void Awake()
		{
			registry = TurretPrefabRegistry.Load();
			if (registry == null)
			{
				Debug.LogWarning("[TurretMount] Не найден TurretPrefabRegistry (Resources/TurretPrefabRegistry).");
			}
			EnsureAllMounts();
		}

		private void EnsureAllMounts()
		{
			var points = FindTurretPoints(transform);
			foreach (var p in points)
			{
				EnsureTurretAtPoint(p);
			}
		}

		private static List<Transform> FindTurretPoints(Transform root)
		{
			var list = new List<Transform>();
			var stack = new Stack<Transform>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				var t = stack.Pop();
				if (t != root && t.name.StartsWith("turret_point"))
				{
					list.Add(t);
				}
				for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
			}
			return list;
		}

		private void EnsureTurretAtPoint(Transform point)
		{
			// Уже есть турель?
			if (point.GetComponentInChildren<TurretController>(true) != null) return;

			// Поддержка имени вида "turret_point:mining_turret_t1"
			string id = defaultTurretId;
			var name = point.name;
			var split = name.Split(':');
			if (split.Length == 2 && !string.IsNullOrWhiteSpace(split[1]))
			{
				id = split[1].Trim();
			}

			// Опция "без турели"
			if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "none", System.StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			var prefab = registry != null ? registry.Get(id) : null;
			if (prefab == null)
			{
				Debug.LogWarning($"[TurretMount] Не найден префаб для '{id}'.");
				return;
			}

			var turret = Instantiate(prefab, point);
			turret.name = id;
			turret.transform.localPosition = Vector3.zero;
			turret.transform.localRotation = Quaternion.identity;
		}
	}
}


