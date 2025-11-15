using System;
using System.Collections.Generic;
using System.IO;
using EveOffline.Planets;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetResourceDatabase))]
public class PlanetResourceDatabaseEditor : Editor
{
	private const string DefaultAssetPath = "Assets/Resources/planet_resource_database.asset";

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Сборка из конфигов", EditorStyles.boldLabel);

		if (GUILayout.Button("Загрузить из Assets/Config/planet_resource.json"))
		{
			var db = (PlanetResourceDatabase)target;
			RebuildFromPlanetResourceJson(db);
		}

		EditorGUILayout.Space();
		if (GUILayout.Button("Создать/найти planet_resource_database.asset и загрузить"))
		{
			var db = EnsureAsset();
			RebuildFromPlanetResourceJson(db);
			EditorGUIUtility.PingObject(db);
		}
	}

	[MenuItem("Tools/Planets/Rebuild planet_resource_database.asset from planet_resource.json")]
	public static void MenuRebuildFromPlanetResourceJson()
	{
		var db = EnsureAsset();
		RebuildFromPlanetResourceJson(db);
		EditorGUIUtility.PingObject(db);
	}

	private static PlanetResourceDatabase EnsureAsset()
	{
		var db = AssetDatabase.LoadAssetAtPath<PlanetResourceDatabase>(DefaultAssetPath);
		if (db == null)
		{
			var dir = Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/");
			if (!AssetDatabase.IsValidFolder(dir))
			{
				var parts = dir.Split('/');
				string pathAcc = parts[0];
				for (int i = 1; i < parts.Length; i++)
				{
					string next = pathAcc + "/" + parts[i];
					if (!AssetDatabase.IsValidFolder(next))
					{
						AssetDatabase.CreateFolder(pathAcc, parts[i]);
					}
					pathAcc = next;
				}
			}
			db = ScriptableObject.CreateInstance<PlanetResourceDatabase>();
			AssetDatabase.CreateAsset(db, DefaultAssetPath);
			AssetDatabase.SaveAssets();
		}
		return db;
	}

	[Serializable]
	private class PlanetResourceJsonRecord
	{
		public string resource_id;
		public string resource_name;
		public float base_cost;
	}

	[Serializable]
	private class PlanetResourceJsonWrapper
	{
		public List<PlanetResourceJsonRecord> items;
	}

	private static void RebuildFromPlanetResourceJson(PlanetResourceDatabase db)
	{
		if (db == null) return;

		try
		{
			string path = Path.Combine(Application.dataPath, "Config/planet_resource.json");
			if (!File.Exists(path))
			{
				EditorUtility.DisplayDialog("Planet Resources", "Не найден файл: Assets/Config/planet_resource.json", "OK");
				return;
			}

			string json = File.ReadAllText(path);
			// В файле лежит чистый массив, оборачиваем его в объект для JsonUtility
			string wrapped = "{ \"items\": " + json + " }";
			var list = JsonUtility.FromJson<PlanetResourceJsonWrapper>(wrapped);
			if (list == null || list.items == null)
			{
				EditorUtility.DisplayDialog("Planet Resources", "Не удалось распарсить planet_resource.json", "OK");
				return;
			}

			var newResources = new List<PlanetResourceDatabase.ResourceRecord>(list.items.Count);
			for (int i = 0; i < list.items.Count; i++)
			{
				var src = list.items[i];
				if (string.IsNullOrEmpty(src.resource_id)) continue;

				var rec = new PlanetResourceDatabase.ResourceRecord
				{
					resourceId = src.resource_id,
					resourceName = src.resource_name,
					baseCost = src.base_cost
				};
				newResources.Add(rec);
			}

			db.SetResources(newResources);
			EditorUtility.SetDirty(db);
			AssetDatabase.SaveAssets();

			EditorUtility.DisplayDialog("Planet Resources", $"Успешно загружено ресурсов: {newResources.Count}", "OK");
		}
		catch (Exception e)
		{
			Debug.LogError("[PlanetResourceDatabase] Ошибка сборки: " + e);
			EditorUtility.DisplayDialog("Planet Resources", "Ошибка: " + e.Message, "OK");
		}
	}
}


