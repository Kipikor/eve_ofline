using UnityEditor;
using UnityEngine;

namespace EditorTools
{
	public static class AsteroidSpriteOrderTool
	{
		private const string AsteroidFolder = "Assets/Prefab/asteroid";
		private const int TargetSortingOrder = 50;

		[MenuItem("Tools/Asteroids/Set Sprite Sorting Order = 50")]
		public static void SetSpriteSortingOrder()
		{
			var guids = AssetDatabase.FindAssets("t:Prefab", new[] { AsteroidFolder });
			int modifiedPrefabs = 0;
			int modifiedRenderers = 0;

			for (int gi = 0; gi < guids.Length; gi++)
			{
				var path = AssetDatabase.GUIDToAssetPath(guids[gi]);
				if (string.IsNullOrEmpty(path)) continue;

				var root = PrefabUtility.LoadPrefabContents(path);
				if (root == null) continue;

				bool changed = false;
				var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
				for (int i = 0; i < renderers.Length; i++)
				{
					var sr = renderers[i];
					if (sr == null) continue;
					if (sr.sortingOrder != TargetSortingOrder)
					{
						sr.sortingOrder = TargetSortingOrder;
						EditorUtility.SetDirty(sr);
						changed = true;
						modifiedRenderers++;
					}
				}

				if (changed)
				{
					PrefabUtility.SaveAsPrefabAsset(root, path);
					modifiedPrefabs++;
				}

				PrefabUtility.UnloadPrefabContents(root);
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[AsteroidSpriteOrderTool] Обновлено префабов: {modifiedPrefabs}, изменено SpriteRenderer: {modifiedRenderers}. Папка: {AsteroidFolder}. Значение sortingOrder = {TargetSortingOrder}");
		}
	}
}


