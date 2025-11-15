using System;
using System.Collections.Generic;
using System.IO;
using EveOffline.Planets;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetDatabase))]
public class PlanetDatabaseEditor : Editor
{
	private const string DefaultAssetPath = "Assets/Resources/planet_database.asset";

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Сборка из конфигов", EditorStyles.boldLabel);

		if (GUILayout.Button("Загрузить из Assets/Config/planet.json"))
		{
			var db = (PlanetDatabase)target;
			RebuildFromPlanetJson(db);
		}

		EditorGUILayout.Space();
		if (GUILayout.Button("Создать/найти planet_database.asset и загрузить из planet.json"))
		{
			var db = EnsureAsset();
			RebuildFromPlanetJson(db);
			EditorGUIUtility.PingObject(db);
		}
	}

	[MenuItem("Tools/Planets/Rebuild planet_database.asset from planet.json")]
	public static void MenuRebuildFromPlanetJson()
	{
		var db = EnsureAsset();
		RebuildFromPlanetJson(db);
		EditorGUIUtility.PingObject(db);
	}

	private static PlanetDatabase EnsureAsset()
	{
		var db = AssetDatabase.LoadAssetAtPath<PlanetDatabase>(DefaultAssetPath);
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
			db = ScriptableObject.CreateInstance<PlanetDatabase>();
			AssetDatabase.CreateAsset(db, DefaultAssetPath);
			AssetDatabase.SaveAssets();
		}
		return db;
	}

	[Serializable]
	private class PlanetJsonRecord
	{
		public string id_planet;
		public string planet_type;
		public string start_resource;
		public float scale;
		public string color;
	}

	[Serializable]
	private class PlanetJsonWrapper
	{
		public List<PlanetJsonRecord> items;
	}

	private static void RebuildFromPlanetJson(PlanetDatabase db)
	{
		if (db == null) return;

		try
		{
			string path = Path.Combine(Application.dataPath, "Config/planet.json");
			if (!File.Exists(path))
			{
				EditorUtility.DisplayDialog("Planet Database", "Не найден файл: Assets/Config/planet.json", "OK");
				return;
			}

			string json = File.ReadAllText(path);
			// В файле лежит чистый массив, оборачиваем его в объект для JsonUtility
			string wrapped = "{ \"items\": " + json + " }";
			var list = JsonUtility.FromJson<PlanetJsonWrapper>(wrapped);
			if (list == null || list.items == null)
			{
				EditorUtility.DisplayDialog("Planet Database", "Не удалось распарсить planet.json", "OK");
				return;
			}

			var newPlanets = new List<PlanetDatabase.PlanetRecord>(list.items.Count);
			for (int i = 0; i < list.items.Count; i++)
			{
				var src = list.items[i];
				if (string.IsNullOrEmpty(src.id_planet)) continue;

				var rec = new PlanetDatabase.PlanetRecord
				{
					idPlanet = src.id_planet,
					planetType = src.planet_type,
					startResourceRaw = src.start_resource,
					scale = src.scale > 0f ? src.scale : 1f,
					colorHex = src.color
				};

				// Парсим цвет из HEX, если задан
				if (!string.IsNullOrEmpty(src.color))
				{
					Color parsed;
					if (ColorUtility.TryParseHtmlString(src.color, out parsed))
					{
						rec.color = parsed;
					}
					else
					{
						rec.color = Color.white;
					}
				}

				newPlanets.Add(rec);
			}

			db.SetPlanets(newPlanets);
			EditorUtility.SetDirty(db);
			AssetDatabase.SaveAssets();

			EditorUtility.DisplayDialog("Planet Database", $"Успешно загружено планет: {newPlanets.Count}", "OK");
		}
		catch (Exception e)
		{
			Debug.LogError("[PlanetDatabase] Ошибка сборки: " + e);
			EditorUtility.DisplayDialog("Planet Database", "Ошибка: " + e.Message, "OK");
		}
	}
}


