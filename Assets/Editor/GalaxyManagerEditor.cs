using System;
using System.Collections.Generic;
using System.IO;
using EveOffline.Planets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GalaxyManager))]
public class GalaxyManagerEditor : Editor
{
	private const string PlanetConstRadiusName = "Радиус галактики юнити метры";
	private const string PlanetConstPath = "Assets/Config/planet_const.json";
	private const string PlanetPrefabPath = "Assets/Prefab/planet/planet.prefab";

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		var manager = (GalaxyManager)target;

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Галактический генератор", EditorStyles.boldLabel);

		float radius;
		bool hasRadius = TryReadGalaxyRadius(out radius);
		EditorGUILayout.LabelField("Радиус (из planet_const.json):",
			hasRadius ? radius.ToString("0.##") + " юнити метров" : "не найден, будет использовано значение по умолчанию");

		if (GUILayout.Button("Сгенерировать / пересоздать галактику"))
		{
			GenerateGalaxy(manager, hasRadius ? radius : manager.GalaxyRadiusUnityMeters);
		}
	}

	private static bool TryReadGalaxyRadius(out float radius)
	{
		radius = 0f;
		string fullPath = Path.Combine(Application.dataPath, "Config/planet_const.json");
		if (!File.Exists(fullPath))
		{
			Debug.LogWarning($"[GalaxyManager] Не найден файл констант планет: {PlanetConstPath}");
			return false;
		}

		try
		{
			var lines = File.ReadAllLines(fullPath);
			for (int i = 0; i < lines.Length; i++)
			{
				if (!lines[i].Contains(PlanetConstRadiusName)) continue;

				for (int j = i + 1; j < Mathf.Min(lines.Length, i + 6); j++)
				{
					if (!lines[j].Contains("\"Значение\"")) continue;
					int colonIndex = lines[j].IndexOf(':');
					if (colonIndex < 0) continue;

					string raw = lines[j].Substring(colonIndex + 1);
					raw = raw.Replace(",", string.Empty);
					raw = raw.Replace("\"", string.Empty);
					raw = raw.Replace("}", string.Empty);
					raw = raw.Trim();

					if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radius))
					{
						return true;
					}
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogError("[GalaxyManager] Ошибка чтения planet_const.json: " + e);
		}

		return false;
	}

	private static void GenerateGalaxy(GalaxyManager manager, float radius)
	{
		if (manager == null) return;

		// 1. Загружаем базу планет из Resources
		var db = Resources.Load<PlanetDatabase>("planet_database");
		if (db == null)
		{
			EditorUtility.DisplayDialog("Galaxy Manager", "Не найден PlanetDatabase: Resources/planet_database.asset.\nСначала собери его из planet.json.", "OK");
			return;
		}

		// 2. Загружаем префаб планеты
		var planetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanetPrefabPath);
		if (planetPrefab == null)
		{
			EditorUtility.DisplayDialog("Galaxy Manager", $"Не найден префаб планеты: {PlanetPrefabPath}", "OK");
			return;
		}

		Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Rebuild Galaxy");

		// 3. Обновляем радиус в самом менеджере
		manager.EditorSetGalaxyRadius(radius);

		// 4. Находим/создаём корневой объект для списка планет
		var planetListGo = GameObject.Find("список планет");
		if (planetListGo == null)
		{
			planetListGo = new GameObject("список планет");
			Undo.RegisterCreatedObjectUndo(planetListGo, "Create Planet List Root");
		}

		// 5. Удаляем старые планеты в списке планет
		for (int i = planetListGo.transform.childCount - 1; i >= 0; i--)
		{
			var child = planetListGo.transform.GetChild(i);
			if (child == null) continue;
			var pc = child.GetComponent<PlanetController>();
			if (pc != null)
			{
				Undo.DestroyObjectImmediate(child.gameObject);
			}
		}

		// 6. Расставляем новые планеты
		var records = db.Planets;
		if (records == null || records.Count == 0)
		{
			EditorUtility.DisplayDialog("Galaxy Manager", "В PlanetDatabase нет планет для генерации.", "OK");
			return;
		}

		var placedPositions = new List<Vector2>();
		float safeRadius = Mathf.Max(0f, radius);
		float minDist = safeRadius * 0.05f; // минимум 5% от радиуса галактики

		for (int i = 0; i < records.Count; i++)
		{
			var rec = records[i];
			if (rec == null || string.IsNullOrEmpty(rec.idPlanet)) continue;

			// Позиция — равномерно по площади диска радиуса radius
			// + правило: новая планета не ближе minDist к уже созданным
			Vector2 chosenPos = Vector2.zero;
			bool found = false;
			const int MaxTries = 64;
			for (int tryIndex = 0; tryIndex < MaxTries; tryIndex++)
			{
				float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
				float dist = Mathf.Sqrt(UnityEngine.Random.value) * safeRadius;
				var candidate = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

				bool tooClose = false;
				for (int pi = 0; pi < placedPositions.Count; pi++)
				{
					if (Vector2.Distance(candidate, placedPositions[pi]) < minDist)
					{
						tooClose = true;
						break;
					}
				}

				if (!tooClose)
				{
					chosenPos = candidate;
					found = true;
					break;
				}
			}

			// Если за разумное число попыток не нашли достаточно далёкую точку,
			// берём последнего кандидата: так мы всё равно создадим галактику,
			// но может быть небольшое нарушение дистанции.
			if (!found && placedPositions.Count > 0)
			{
				Debug.LogWarning("[GalaxyManager] Не удалось обеспечить минимальную дистанцию между всеми планетами, некоторые будут ближе 1% радиуса.");
			}

			placedPositions.Add(chosenPos);

			var instance = (GameObject)PrefabUtility.InstantiatePrefab(planetPrefab, planetListGo.transform);
			if (instance == null) continue;

			Undo.RegisterCreatedObjectUndo(instance, "Create Planet");

			instance.name = rec.idPlanet;

			var pos = new Vector3(chosenPos.x, chosenPos.y, 0f);
			instance.transform.localPosition = pos;

			// Масштаб планеты
			float scale = rec.scale <= 0f ? 1f : rec.scale;
			instance.transform.localScale = Vector3.one * scale;

			// Цвет круга (дочерний SpriteRenderer)
			var sr = instance.GetComponentInChildren<SpriteRenderer>();
			if (sr != null)
			{
				if (rec.color != default)
					sr.color = rec.color;
			}

			// Планетный контроллер: прописываем id планеты в приватное поле через SerializedObject
			var controller = instance.GetComponent<PlanetController>();
			if (controller != null)
			{
				var so = new SerializedObject(controller);
				var prop = so.FindProperty("planetId");
				if (prop != null)
				{
					prop.stringValue = rec.idPlanet;
					so.ApplyModifiedPropertiesWithoutUndo();
				}
			}
		}

		EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
	}
}


