using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space
{
	[CreateAssetMenu(fileName = "AsteroidPrefabRegistry", menuName = "Space/Asteroid Prefab Registry")]
	public class AsteroidPrefabRegistry : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			public string asteroidId;
			public GameObject prefab;
		}

		[SerializeField] private List<Entry> entries = new List<Entry>();

		private Dictionary<string, GameObject> idToPrefab;

		public GameObject Get(string asteroidId)
		{
			if (string.IsNullOrEmpty(asteroidId)) return null;
			EnsureMap();
			idToPrefab.TryGetValue(asteroidId, out var prefab);
			return prefab;
		}

		public IReadOnlyList<Entry> Entries => entries;

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
				if (e == null || string.IsNullOrEmpty(e.asteroidId) || e.prefab == null) continue;
				if (!idToPrefab.ContainsKey(e.asteroidId))
				{
					idToPrefab.Add(e.asteroidId, e.prefab);
				}
			}
		}

		public static AsteroidPrefabRegistry Load()
		{
			return Resources.Load<AsteroidPrefabRegistry>("AsteroidPrefabRegistry");
		}
	}
}


