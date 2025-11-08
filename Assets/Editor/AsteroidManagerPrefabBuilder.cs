using UnityEditor;
using UnityEngine;
using Space;

namespace EditorTools
{
	public static class AsteroidManagerPrefabBuilder
	{
		private const string PrefabFolder = "Assets/Prefab/manager";
		private const string PrefabPath = "Assets/Prefab/manager/AsteroidManager.prefab";

		[MenuItem("Tools/Asteroids/Create/Update AsteroidManager Prefab")]
		public static void CreateOrUpdateManagerPrefab()
		{
			EnsureFolder();

			GameObject temp = new GameObject("AsteroidManager");
			try
			{
				var manager = temp.AddComponent<AsteroidManager>();
				// Базовые разумные значения
				SetDefaults(manager);

				// Если уже есть префаб — обновим, иначе создадим
				var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
				if (existing == null)
				{
					PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
					Debug.Log($"[AsteroidManagerPrefab] Создан префаб: {PrefabPath}");
				}
				else
				{
					PrefabUtility.SaveAsPrefabAssetAndConnect(temp, PrefabPath, InteractionMode.AutomatedAction);
					Debug.Log($"[AsteroidManagerPrefab] Обновлён префаб: {PrefabPath}");
				}
			}
			finally
			{
				Object.DestroyImmediate(temp);
			}
			AssetDatabase.SaveAssets();
		}

		[MenuItem("Tools/Asteroids/Add AsteroidManager To Scene")]
		public static void AddManagerToScene()
		{
			// Если уже есть менеджер в сцене — выделим его
			var existing = Object.FindFirstObjectByType<AsteroidManager>();
			if (existing != null)
			{
				Selection.activeObject = existing.gameObject;
				EditorGUIUtility.PingObject(existing.gameObject);
				Debug.Log("[AsteroidManagerPrefab] В сцене уже есть AsteroidManager. Выделил существующий объект.");
				return;
			}

			CreateOrUpdateManagerPrefab();

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			if (prefab == null)
			{
				Debug.LogWarning($"[AsteroidManagerPrefab] Не удалось найти префаб по пути: {PrefabPath}");
				return;
			}

			var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			if (instance == null)
			{
				Debug.LogWarning("[AsteroidManagerPrefab] Не удалось инстанциировать префаб в сцене.");
				return;
			}

			instance.name = "AsteroidManager";
			var manager = instance.GetComponent<AsteroidManager>();
			if (manager != null)
			{
				SetDefaults(manager);
				// Попробуем сразу найти ссылки
				var cam = Camera.main;
				if (cam != null) manager.GetType().GetField("cameraTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(manager, cam.transform);

				// Поиск корабля по тегам
				var ship = GameObject.FindWithTag("Ship");
				if (ship == null) ship = GameObject.FindWithTag("Player");
				if (ship != null)
				{
					manager.GetType().GetField("shipTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(manager, ship.transform);
				}
			}

			Selection.activeObject = instance;
			EditorGUIUtility.PingObject(instance);
			Debug.Log("[AsteroidManagerPrefab] Добавлен AsteroidManager в сцену.");
		}

		private static void EnsureFolder()
		{
			if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
			{
				AssetDatabase.CreateFolder("Assets", "Prefab");
			}
			if (!AssetDatabase.IsValidFolder(PrefabFolder))
			{
				AssetDatabase.CreateFolder("Assets/Prefab", "manager");
			}
		}

		private static void SetDefaults(AsteroidManager manager)
		{
			// Через сериализатор не лезем — просто оставим значения по умолчанию, выставленные в самом компоненте.
			// Этот метод оставлен на будущее для централизованной настройки, если понадобится.
		}
	}
}


