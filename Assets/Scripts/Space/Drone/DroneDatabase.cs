using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EveOffline.Space.Drone
{
	public static class DroneDatabase
	{
		[Serializable]
		public class DroneDef
		{
			public string drone_id;
			public string name;
			public float cost;
			public string descr;
			public string prefab;              // имя префаба, например analog_drone_t1
			public int can_transport;
			public int can_heal;
			public int can_damage;
			public string weapon;
			public string max_count;
			public float shield_hp;
			public float mass;                 // тонны (считаем как у корабля)
			public float radius_from_ship;     // м
			public float grab_max_diameter;    // м — максимальный диаметр астероида для захвата
			public float grab_max_m3;
			public float shield_regen;
			public float shield_cd;
			public float armor_hp;
			public float structure_hp;
			public float acceleration;         // кН
			public float rotation;             // кН·м
			public float max_speed;            // м/с
			public float in_cargo_size;
			public float need_drone_throughput;
		}

		private static Dictionary<string, DroneDef> idToDef;
		private static List<string> cachedIds;

		public static IReadOnlyList<string> GetAllIds()
		{
			EnsureLoaded();
			return cachedIds;
		}

		public static DroneDef Get(string id)
		{
			EnsureLoaded();
			if (string.IsNullOrEmpty(id)) return null;
			idToDef.TryGetValue(id, out var def);
			return def;
		}

		private static void EnsureLoaded()
		{
			if (idToDef != null) return;
			idToDef = new Dictionary<string, DroneDef>(StringComparer.Ordinal);
			cachedIds = new List<string>();
			try
			{
				// Resources (если есть)
				TextAsset ta = Resources.Load<TextAsset>("Config/drone");
				string json = ta != null ? ta.text : null;

#if UNITY_EDITOR
				if (string.IsNullOrEmpty(json))
				{
					// Ищем файл по проекту
					string[] guids = AssetDatabase.FindAssets("drone t:TextAsset");
					for (int gi = 0; gi < guids.Length; gi++)
					{
						string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
						if (!path.EndsWith("drone.json", StringComparison.OrdinalIgnoreCase)) continue;
						var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
						if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) { json = asset.text; break; }
					}
				}
#else
				if (string.IsNullOrEmpty(json))
				{
					// Файл в StreamingAssets/или IO (по необходимости)
					string path = Path.Combine(Application.dataPath, "Config/drone.json");
					if (File.Exists(path)) json = File.ReadAllText(path);
				}
#endif
				if (string.IsNullOrEmpty(json)) return;
				var defs = JsonUtility.FromJson<Wrapper<DroneDef>>(Wrap(json))?.items;
				if (defs == null) return;
				for (int i = 0; i < defs.Length; i++)
				{
					var d = defs[i];
					if (d == null || string.IsNullOrEmpty(d.drone_id)) continue;
					if (!idToDef.ContainsKey(d.drone_id))
					{
						idToDef.Add(d.drone_id, d);
						cachedIds.Add(d.drone_id);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[DroneDatabase] load error: {e}");
			}
		}

		public static GameObject LoadPrefab(DroneDef def)
		{
			if (def == null || string.IsNullOrEmpty(def.prefab)) return null;

			// Resources
			var go = Resources.Load<GameObject>($"Prefab/drone/{def.prefab}");
			if (go != null) return go;

#if UNITY_EDITOR
			// Ожидаемый путь
			string path = $"Assets/Prefab/drone/{def.prefab}.prefab";
			var byPath = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (byPath != null) return byPath;

			// Поиск по имени
			string[] guids = AssetDatabase.FindAssets(def.prefab + " t:Prefab");
			for (int gi = 0; gi < guids.Length; gi++)
			{
				string p = AssetDatabase.GUIDToAssetPath(guids[gi]);
				var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
				if (asset != null && string.Equals(asset.name, def.prefab, StringComparison.OrdinalIgnoreCase)) return asset;
			}
#endif
			return null;
		}

		[Serializable]
		private class Wrapper<TT> { public TT[] items; }
		private static string Wrap(string json) => "{\"items\":" + json + "}";
	}
}


