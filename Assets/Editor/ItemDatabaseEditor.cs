using System;
using System.Collections.Generic;
using System.IO;
using Data;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
	private const string DefaultAssetPath = "Assets/Resources/all_item.asset";

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		// Выбор источников
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Источники данных", EditorStyles.boldLabel);
		LoadPrefsOnce();
		var newMinerals = EditorGUILayout.ToggleLeft("Минералы (Assets/Config/mineral.json)", s_importMinerals);
		var newOres = EditorGUILayout.ToggleLeft("Руды (Assets/Config/ore.json)", s_importOres);
		if (newMinerals != s_importMinerals)
		{
			s_importMinerals = newMinerals;
			EditorPrefs.SetBool(PrefMinerals, s_importMinerals);
		}
		if (newOres != s_importOres)
		{
			s_importOres = newOres;
			EditorPrefs.SetBool(PrefOres, s_importOres);
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Сборка базы", EditorStyles.boldLabel);
		if (GUILayout.Button("Собрать из mineral.json"))
		{
			RebuildFromMinerals((ItemDatabase)target);
		}
		if (GUILayout.Button("Собрать из ore.json"))
		{
			RebuildFromOre((ItemDatabase)target);
		}
		if (GUILayout.Button("Собрать выбранное"))
		{
			RebuildCombined((ItemDatabase)target, s_importMinerals, s_importOres);
		}

		EditorGUILayout.Space();
		if (GUILayout.Button("Создать/найти all_item.asset и собрать"))
		{
			var db = EnsureAsset();
			RebuildFromMinerals(db);
			EditorGUIUtility.PingObject(db);
		}
		if (GUILayout.Button("Создать/найти all_item.asset и собрать из ore.json"))
		{
			var db = EnsureAsset();
			RebuildFromOre(db);
			EditorGUIUtility.PingObject(db);
		}
		if (GUILayout.Button("Создать/найти all_item.asset и собрать выбранное"))
		{
			var db = EnsureAsset();
			RebuildCombined(db, s_importMinerals, s_importOres);
			EditorGUIUtility.PingObject(db);
		}
	}

	[MenuItem("Tools/Items/Rebuild all_item.asset from mineral.json")]
	public static void MenuRebuild()
	{
		var db = EnsureAsset();
		RebuildFromMinerals(db);
		EditorGUIUtility.PingObject(db);
	}

	[MenuItem("Tools/Items/Rebuild all_item.asset from ore.json")]
	public static void MenuRebuildFromOre()
	{
		var db = EnsureAsset();
		RebuildFromOre(db);
		EditorGUIUtility.PingObject(db);
	}

	[MenuItem("Tools/Items/Rebuild all_item.asset (selected sources)")]
	public static void MenuRebuildCombined()
	{
		LoadPrefsOnce();
		var db = EnsureAsset();
		RebuildCombined(db, s_importMinerals, s_importOres);
		EditorGUIUtility.PingObject(db);
	}

	private static ItemDatabase EnsureAsset()
	{
		var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DefaultAssetPath);
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
			db = ScriptableObject.CreateInstance<ItemDatabase>();
			AssetDatabase.CreateAsset(db, DefaultAssetPath);
			AssetDatabase.SaveAssets();
		}
		return db;
	}

	// mineral.json поддержка
	[Serializable] private class MineralDef { public string item_id; public string item_name; public string item_icon; public float cagro; public float cost; public string item_descr; }
	[Serializable] private class MineralList { public List<MineralDef> items; }

	private static void RebuildFromMinerals(ItemDatabase db)
	{
		if (db == null) return;

		try
		{
			string mineralsPath = Path.Combine(Application.dataPath, "Config/mineral.json");
			if (!File.Exists(mineralsPath))
			{
				EditorUtility.DisplayDialog("all_item", "Не найден файл: Assets/Config/mineral.json", "OK");
				return;
			}

			string json = File.ReadAllText(mineralsPath);
			var wrapped = "{ \"items\": " + json + " }";
			var list = JsonUtility.FromJson<MineralList>(wrapped);
			if (list == null || list.items == null)
			{
				EditorUtility.DisplayDialog("all_item", "Не удалось распарсить mineral.json", "OK");
				return;
			}

			var newItems = new List<ItemDatabase.ItemRecord>(list.items.Count);
			for (int i = 0; i < list.items.Count; i++)
			{
				var m = list.items[i];
				if (string.IsNullOrEmpty(m.item_id)) continue;
				var rec = new ItemDatabase.ItemRecord
				{
					id = m.item_id,
					name = string.IsNullOrEmpty(m.item_name) ? m.item_id : m.item_name,
					iconKey = m.item_icon,
					cargo = m.cagro,
					cost = m.cost,
					description = m.item_descr,
					iconSprite = LoadIconSprite(m.item_icon)
				};
				newItems.Add(rec);
			}

			db.SetItems(newItems);
			EditorUtility.SetDirty(db);
			AssetDatabase.SaveAssets();
			EditorUtility.DisplayDialog("all_item", $"Успешно собрано: {newItems.Count} предметов.", "OK");
		}
		catch (Exception e)
		{
			Debug.LogError("[all_item] Ошибка сборки: " + e);
			EditorUtility.DisplayDialog("all_item", "Ошибка: " + e.Message, "OK");
		}
	}

	private static Sprite LoadIconSprite(string iconKey)
	{
		if (string.IsNullOrEmpty(iconKey)) return null;

		// Сначала пробуем Resources
		Sprite s = Resources.Load<Sprite>($"Sprites/Mineral/{iconKey}");
		if (s != null) return s;

		// Затем — точные пути в проекте
		string basePath = "Assets/Sprites/Mineral/" + iconKey;
		string[] exts = { ".png", ".psb", ".jpg", ".jpeg" };
		for (int i = 0; i < exts.Length; i++)
		{
			var p = basePath + exts[i];
			var byPath = AssetDatabase.LoadAssetAtPath<Sprite>(p);
			if (byPath != null) return byPath;
		}

		// По имени среди всех спрайтов
		var guids = AssetDatabase.FindAssets(iconKey + " t:Sprite");
		for (int gi = 0; gi < guids.Length; gi++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
			var asset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (asset != null && asset.name == iconKey) return asset;
		}
		return null;
	}

	// ore.json поддержка
	[Serializable] private class OreDef { public string ore_id; public string ore_name; public string ore_icon; public string ore_descr; public float cagro; public float cost; }
	[Serializable] private class OreList { public List<OreDef> items; }

	private static void RebuildFromOre(ItemDatabase db)
	{
		if (db == null) return;

		try
		{
			string orePath = Path.Combine(Application.dataPath, "Config/ore.json");
			if (!File.Exists(orePath))
			{
				EditorUtility.DisplayDialog("all_item", "Не найден файл: Assets/Config/ore.json", "OK");
				return;
			}

			string json = File.ReadAllText(orePath);
			var wrapped = "{ \"items\": " + json + " }";
			var list = JsonUtility.FromJson<OreList>(wrapped);
			if (list == null || list.items == null)
			{
				EditorUtility.DisplayDialog("all_item", "Не удалось распарсить ore.json", "OK");
				return;
			}

			var newItems = new List<ItemDatabase.ItemRecord>(list.items.Count);
			for (int i = 0; i < list.items.Count; i++)
			{
				var m = list.items[i];
				if (string.IsNullOrEmpty(m.ore_id)) continue;
				var rec = new ItemDatabase.ItemRecord
				{
					id = m.ore_id,
					name = string.IsNullOrEmpty(m.ore_name) ? m.ore_id : m.ore_name,
					iconKey = m.ore_icon,
					cargo = m.cagro,
					cost = m.cost,
					description = m.ore_descr,
					iconSprite = LoadOreIconSprite(m.ore_icon)
				};
				newItems.Add(rec);
			}

			db.SetItems(newItems);
			EditorUtility.SetDirty(db);
			AssetDatabase.SaveAssets();
			EditorUtility.DisplayDialog("all_item", $"Успешно собрано из ore.json: {newItems.Count} предметов.", "OK");
		}
		catch (Exception e)
		{
			Debug.LogError("[all_item] Ошибка сборки из ore.json: " + e);
			EditorUtility.DisplayDialog("all_item", "Ошибка: " + e.Message, "OK");
		}
	}

	private static Sprite LoadOreIconSprite(string iconKey)
	{
		if (string.IsNullOrEmpty(iconKey)) return null;

		// Сначала пробуем Resources
		Sprite s = Resources.Load<Sprite>($"Sprites/Asteroid/{iconKey}");
		if (s != null) return s;

		// Затем — точные пути в проекте
		string basePath = "Assets/Sprites/Asteroid/" + iconKey;
		string[] exts = { ".png", ".psb", ".jpg", ".jpeg" };
		for (int i = 0; i < exts.Length; i++)
		{
			var p = basePath + exts[i];
			var byPath = AssetDatabase.LoadAssetAtPath<Sprite>(p);
			if (byPath != null) return byPath;
		}

		// По имени среди всех спрайтов
		var guids = AssetDatabase.FindAssets(iconKey + " t:Sprite");
		for (int gi = 0; gi < guids.Length; gi++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
			var asset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (asset != null && asset.name == iconKey) return asset;
		}
		return null;
	}

	// Комбинированная сборка
	private const string PrefMinerals = "all_item_import_minerals";
	private const string PrefOres = "all_item_import_ores";
	private static bool s_prefsLoaded;
	private static bool s_importMinerals = true;
	private static bool s_importOres = true;

	private static void LoadPrefsOnce()
	{
		if (s_prefsLoaded) return;
		s_importMinerals = EditorPrefs.GetBool(PrefMinerals, true);
		s_importOres = EditorPrefs.GetBool(PrefOres, true);
		s_prefsLoaded = true;
	}

	private static void RebuildCombined(ItemDatabase db, bool includeMinerals, bool includeOres)
	{
		if (db == null) return;

		var idToItem = new Dictionary<string, ItemDatabase.ItemRecord>();
		int imported = 0;

		// Минералы
		if (includeMinerals)
		{
			try
			{
				string mineralsPath = Path.Combine(Application.dataPath, "Config/mineral.json");
				if (File.Exists(mineralsPath))
				{
					string json = File.ReadAllText(mineralsPath);
					var wrapped = "{ \"items\": " + json + " }";
					var list = JsonUtility.FromJson<MineralList>(wrapped);
					if (list != null && list.items != null)
					{
						for (int i = 0; i < list.items.Count; i++)
						{
							var m = list.items[i];
							if (string.IsNullOrEmpty(m.item_id)) continue;
							var rec = new ItemDatabase.ItemRecord
							{
								id = m.item_id,
								name = string.IsNullOrEmpty(m.item_name) ? m.item_id : m.item_name,
								iconKey = m.item_icon,
								cargo = m.cagro,
								cost = m.cost,
								description = m.item_descr,
								iconSprite = LoadIconSprite(m.item_icon)
							};
							idToItem[rec.id] = rec; // минералы можно перекрывать рудами ниже
							imported++;
						}
					}
				}
				else
				{
					Debug.LogWarning("all_item: Не найден файл минералов Assets/Config/mineral.json");
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[all_item] Ошибка чтения mineral.json: " + e);
			}
		}

		// Руды
		if (includeOres)
		{
			try
			{
				string orePath = Path.Combine(Application.dataPath, "Config/ore.json");
				if (File.Exists(orePath))
				{
					string json = File.ReadAllText(orePath);
					var wrapped = "{ \"items\": " + json + " }";
					var list = JsonUtility.FromJson<OreList>(wrapped);
					if (list != null && list.items != null)
					{
						for (int i = 0; i < list.items.Count; i++)
						{
							var m = list.items[i];
							if (string.IsNullOrEmpty(m.ore_id)) continue;
							var rec = new ItemDatabase.ItemRecord
							{
								id = m.ore_id,
								name = string.IsNullOrEmpty(m.ore_name) ? m.ore_id : m.ore_name,
								iconKey = m.ore_icon,
								cargo = m.cagro,
								cost = m.cost,
								description = m.ore_descr,
								iconSprite = LoadOreIconSprite(m.ore_icon)
							};
							idToItem[rec.id] = rec; // руда перекрывает совпадающие id минералов
							imported++;
						}
					}
				}
				else
				{
					Debug.LogWarning("all_item: Не найден файл руд Assets/Config/ore.json");
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[all_item] Ошибка чтения ore.json: " + e);
			}
		}

		var result = new List<ItemDatabase.ItemRecord>(idToItem.Values);
		db.SetItems(result);
		EditorUtility.SetDirty(db);
		AssetDatabase.SaveAssets();
		EditorUtility.DisplayDialog("all_item", $"Собрано (выбранное): {result.Count} предметов.", "OK");
	}
}


