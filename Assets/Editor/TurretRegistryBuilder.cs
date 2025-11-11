using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Space.Weapons;

namespace EditorTools
{
	public static class TurretRegistryBuilder
	{
		private const string RegistryResourcesPath = "Assets/Resources";
		private const string RegistryAssetPath = "Assets/Resources/TurretPrefabRegistry.asset";
		private static readonly string[] PrefabFolders = { "Assets/Prefab/turret" };
		private const string PrefKeyLastCount = "TurretRegistry_LastCount";

		[MenuItem("Tools/Turrets/Rebuild Turret Prefab Registry")]
		public static void Rebuild()
		{
			if (!AssetDatabase.IsValidFolder(RegistryResourcesPath))
			{
				AssetDatabase.CreateFolder("Assets", "Resources");
			}

			var registry = AssetDatabase.LoadAssetAtPath<TurretPrefabRegistry>(RegistryAssetPath);
			if (registry == null)
			{
				registry = ScriptableObject.CreateInstance<TurretPrefabRegistry>();
				AssetDatabase.CreateAsset(registry, RegistryAssetPath);
			}

			var entries = new List<TurretPrefabRegistry.Entry>();
			var guids = AssetDatabase.FindAssets("t:Prefab", PrefabFolders);
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null) continue;
				entries.Add(new TurretPrefabRegistry.Entry
				{
					turretId = prefab.name,
					prefab = prefab
				});
			}
			registry.SetEntries(entries);
			EditorUtility.SetDirty(registry);
			AssetDatabase.SaveAssets();

			// Лог показываем только при реальном изменении количества, чтобы не спамить консоль
			int prevCount = EditorPrefs.GetInt(PrefKeyLastCount, -1);
			if (prevCount != entries.Count)
			{
				Debug.Log($"[TurretRegistry] Собрано записей: {entries.Count}");
				EditorPrefs.SetInt(PrefKeyLastCount, entries.Count);
			}
		}
	}

	// Автосборка при изменениях в папке с турелями
	public class TurretRegistryPostprocessor : AssetPostprocessor
	{
		private static readonly string WatchFolder = "Assets/Prefab/turret";
		private static bool pendingRebuild;

		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			bool needRebuild = false;
			for (int i = 0; i < importedAssets.Length; i++)
			{
				var p = importedAssets[i];
				if (p.StartsWith(WatchFolder) && p.EndsWith(".prefab")) { needRebuild = true; break; }
			}
			if (!needRebuild)
			{
				for (int i = 0; i < movedAssets.Length; i++)
				{
					var p = movedAssets[i];
					if (p.StartsWith(WatchFolder) && p.EndsWith(".prefab")) { needRebuild = true; break; }
				}
			}
			if (!needRebuild)
			{
				for (int i = 0; i < movedFromAssetPaths.Length; i++)
				{
					var p = movedFromAssetPaths[i];
					if (p.StartsWith(WatchFolder) && p.EndsWith(".prefab")) { needRebuild = true; break; }
				}
			}

			if (needRebuild)
			{
				// Дебаунс: собрать один раз после пачки импортов
				if (pendingRebuild) return;
				pendingRebuild = true;
				EditorApplication.delayCall += () =>
				{
					pendingRebuild = false;
					TurretRegistryBuilder.Rebuild();
				};
			}
		}
	}
}


