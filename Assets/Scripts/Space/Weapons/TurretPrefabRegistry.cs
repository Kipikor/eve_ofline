using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space.Weapons
{
	[CreateAssetMenu(fileName = "TurretPrefabRegistry", menuName = "Space/Turret Prefab Registry")]
	public class TurretPrefabRegistry : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			public string turretId;
			public GameObject prefab;
		}

		[SerializeField] private List<Entry> entries = new List<Entry>();
		private Dictionary<string, GameObject> idToPrefab;

		public GameObject Get(string turretId)
		{
			if (string.IsNullOrEmpty(turretId)) return null;
			EnsureMap();
			idToPrefab.TryGetValue(turretId, out var prefab);
			return prefab;
		}

		public void SetEntries(List<Entry> newEntries)
		{
			entries = newEntries ?? new List<Entry>();
			idToPrefab = null;
		}

		private void EnsureMap()
		{
			if (idToPrefab != null) return;
			idToPrefab = new Dictionary<string, GameObject>(StringComparer.Ordinal);
			foreach (var e in entries)
			{
				if (e == null || string.IsNullOrEmpty(e.turretId) || e.prefab == null) continue;
				if (!idToPrefab.ContainsKey(e.turretId))
				{
					idToPrefab.Add(e.turretId, e.prefab);
				}
			}
		}

		public static TurretPrefabRegistry Load()
		{
			return Resources.Load<TurretPrefabRegistry>("TurretPrefabRegistry");
		}
	}
}


