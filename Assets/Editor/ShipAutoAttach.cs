using UnityEditor;
using UnityEngine;
using Space.Weapons;

namespace EditorTools
{
	public static class ShipAutoAttach
	{
		private static readonly string[] ShipFolders = { "Assets/Prefab/ship" };

		[MenuItem("Tools/Ships/Attach TurretMount to All Ship Prefabs")]
		public static void AttachTurretMount()
		{
			var guids = AssetDatabase.FindAssets("t:Prefab", ShipFolders);
			int changed = 0;
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var root = PrefabUtility.LoadPrefabContents(path);
				if (root == null) continue;

				if (root.GetComponentInChildren<TurretMount>(true) == null)
				{
					root.AddComponent<TurretMount>();
					changed++;
				}

				PrefabUtility.SaveAsPrefabAsset(root, path);
				PrefabUtility.UnloadPrefabContents(root);
			}
			if (changed > 0)
			{
				Debug.Log($"[ShipAutoAttach] Добавлен TurretMount на {changed} префаб(ов).");
			}
			else
			{
				Debug.Log("[ShipAutoAttach] Все префабы уже содержат TurretMount.");
			}
		}
	}
}


