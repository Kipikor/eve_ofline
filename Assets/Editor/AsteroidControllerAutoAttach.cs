using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;
using Space;

namespace EditorTools
{
	public static class AsteroidControllerAutoAttach
	{
		private static readonly string[] SearchFolders = { "Assets/Prefab/asteroid" };
			private const float RootGravityScale = 0f;

		[MenuItem("Tools/Asteroids/Attach Controller to All Prefabs")]
		public static void AttachControllerToAllPrefabs()
		{
			var guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
			int processed = 0;
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				AttachForPrefabPath(path, ref processed);
			}

			if (processed > 0)
			{
				AssetDatabase.SaveAssets();
			}
			Debug.Log($"[AsteroidController] Обработано префабов: {processed}");
		}
	
			[MenuItem("Tools/Asteroids/Disable Gravity In Children")]
			public static void DisableGravityInChildrenForAllPrefabs()
			{
				var guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
				int processed = 0;
				foreach (var guid in guids)
				{
					var path = AssetDatabase.GUIDToAssetPath(guid);
					if (string.IsNullOrEmpty(path)) continue;
	
					var root = PrefabUtility.LoadPrefabContents(path);
					if (root == null) continue;
	
					bool changed = EnsureGravityDisabled(root);
					if (changed)
					{
						PrefabUtility.SaveAsPrefabAsset(root, path);
						processed++;
					}
	
					PrefabUtility.UnloadPrefabContents(root);
				}
	
				if (processed > 0)
				{
					AssetDatabase.SaveAssets();
				}
				Debug.Log($"[AsteroidController] Отключена гравитация у дочерних объектов в префабах: {processed}");
			}

		private static void AttachForPrefabPath(string path, ref int processed)
		{
			var root = PrefabUtility.LoadPrefabContents(path);
			if (root == null)
			{
				return;
			}

			bool changed = false;
			if (root.GetComponent<AsteroidController>() == null)
			{
				root.AddComponent<AsteroidController>();
				changed = true;
			}
	
				// Отключаем гравитацию у всех дочерних Rigidbody2D
				if (EnsureGravityDisabled(root))
				{
					changed = true;
				}

				// Переносим Rigidbody2D на корень, удаляя у детей
				if (EnsureRootHasRigidbodyAndChildrenDont(root))
				{
					changed = true;
				}

			if (changed)
			{
				PrefabUtility.SaveAsPrefabAsset(root, path);
				processed++;
			}

			PrefabUtility.UnloadPrefabContents(root);
		}
	
			public static bool EnsureGravityDisabled(GameObject root)
			{
				bool changed = false;
				var bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
				foreach (var rb in bodies)
				{
					if (rb != null && rb.gravityScale != 0f)
					{
						rb.gravityScale = 0f;
						EditorUtility.SetDirty(rb);
						changed = true;
					}
				}
				return changed;
			}

			private static bool EnsureRootHasRigidbodyAndChildrenDont(GameObject root)
			{
				bool changed = false;
				var rootRb = root.GetComponent<Rigidbody2D>();
				var allBodies = root.GetComponentsInChildren<Rigidbody2D>(true);
				Rigidbody2D source = null;
				foreach (var rb in allBodies)
				{
					if (rb != null && rb.gameObject != root && source == null)
					{
						source = rb;
					}
				}

				if (rootRb == null)
				{
					rootRb = Undo.AddComponent<Rigidbody2D>(root);
					changed = true;
				}

				// Копируем основные настройки с первого найденного детского rb
				if (source != null)
				{
					rootRb.bodyType = source.bodyType;
					rootRb.sharedMaterial = source.sharedMaterial;
					rootRb.useFullKinematicContacts = source.useFullKinematicContacts;
					rootRb.useAutoMass = source.useAutoMass;
					rootRb.mass = source.mass;
					rootRb.linearDamping = source.linearDamping;
					rootRb.angularDamping = source.angularDamping;
					rootRb.interpolation = source.interpolation;
					rootRb.sleepMode = source.sleepMode;
					rootRb.collisionDetectionMode = source.collisionDetectionMode;
					rootRb.constraints = source.constraints;
				}

				// Форсим гравитацию 0 на корне
				if (rootRb.gravityScale != RootGravityScale)
				{
					rootRb.gravityScale = RootGravityScale;
					EditorUtility.SetDirty(rootRb);
					changed = true;
				}

				// Удаляем rb у детей
				foreach (var rb in allBodies)
				{
					if (rb == null) continue;
					if (rb.gameObject == root) continue;
					Undo.DestroyObjectImmediate(rb);
					changed = true;
				}

				return changed;
			}
	}

	// Автоподключение при импорте/изменении префабов
	public class AsteroidPrefabPostprocessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			var processedPaths = new HashSet<string>();
			int processed = 0;
			foreach (var assetPath in importedAssets)
			{
				if (processedPaths.Contains(assetPath)) continue;
				if (!assetPath.StartsWith("Assets/Prefab/asteroid")) continue;
				if (!assetPath.EndsWith(".prefab")) continue;

				var root = PrefabUtility.LoadPrefabContents(assetPath);
				if (root == null) continue;

				bool changed = false;
				if (root.GetComponent<Space.AsteroidController>() == null)
				{
					root.AddComponent<Space.AsteroidController>();
					changed = true;
				}
	
					// Отключаем гравитацию у всех дочерних Rigidbody2D
					if (AsteroidControllerAutoAttach.EnsureGravityDisabled(root))
					{
						changed = true;
					}

				if (changed)
				{
					PrefabUtility.SaveAsPrefabAsset(root, assetPath);
					processed++;
				}

				PrefabUtility.UnloadPrefabContents(root);
				processedPaths.Add(assetPath);
			}

			if (processed > 0)
			{
				AssetDatabase.SaveAssets();
				Debug.Log($"[AsteroidController] Автоподключение: обновлено префабов: {processed}");
			}
		}
	}
}


