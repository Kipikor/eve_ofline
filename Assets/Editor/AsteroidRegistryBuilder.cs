using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Space;

namespace EditorTools
{
	public static class AsteroidRegistryBuilder
	{
		private const string RegistryResourcesPath = "Assets/Resources";
		private const string RegistryAssetPath = "Assets/Resources/AsteroidPrefabRegistry.asset";
		private static readonly string[] PrefabFolders = { "Assets/Prefab/asteroid" };

		[MenuItem("Tools/Asteroids/Rebuild Prefab Registry")]
		public static void Rebuild()
		{
			EnsureResourcesFolder();
			var registry = LoadOrCreateRegistry();
			var entries = BuildEntries();
			registry.SetEntries(entries);
			EditorUtility.SetDirty(registry);
			AssetDatabase.SaveAssets();
			Debug.Log($"[AsteroidRegistry] Обновлено записей: {entries.Count}");
		}

		private static void EnsureResourcesFolder()
		{
			if (!AssetDatabase.IsValidFolder(RegistryResourcesPath))
			{
				var parent = "Assets";
				var name = "Resources";
				AssetDatabase.CreateFolder(parent, name);
			}
		}

		private static AsteroidPrefabRegistry LoadOrCreateRegistry()
		{
			var registry = AssetDatabase.LoadAssetAtPath<AsteroidPrefabRegistry>(RegistryAssetPath);
			if (registry == null)
			{
				registry = ScriptableObject.CreateInstance<AsteroidPrefabRegistry>();
				AssetDatabase.CreateAsset(registry, RegistryAssetPath);
			}
			return registry;
		}

		private static List<AsteroidPrefabRegistry.Entry> BuildEntries()
		{
			var list = new List<AsteroidPrefabRegistry.Entry>();
			var guids = AssetDatabase.FindAssets("t:Prefab", PrefabFolders);
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null) continue;
				list.Add(new AsteroidPrefabRegistry.Entry
				{
					asteroidId = prefab.name,
					prefab = prefab
				});
			}
			return list;
		}
	}

	public class AsteroidRegistryPostprocessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			bool needRebuild = false;
			foreach (var p in importedAssets)
			{
				if (p.StartsWith("Assets/Prefab/asteroid") && p.EndsWith(".prefab")) { needRebuild = true; break; }
			}
			foreach (var p in movedAssets)
			{
				if (p.StartsWith("Assets/Prefab/asteroid") && p.EndsWith(".prefab")) { needRebuild = true; break; }
			}
			foreach (var p in movedFromAssetPaths)
			{
				if (p.StartsWith("Assets/Prefab/asteroid") && p.EndsWith(".prefab")) { needRebuild = true; break; }
			}
			if (needRebuild)
			{
				AsteroidRegistryBuilder.Rebuild();
			}
		}
	}
}


