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
			Debug.Log($"[TurretRegistry] Собрано записей: {entries.Count}");
		}
	}
}


