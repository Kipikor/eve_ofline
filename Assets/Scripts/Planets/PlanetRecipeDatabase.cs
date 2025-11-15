using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Рецепты процессов для планет. Читаются из Config/planet_recipes.json при первом обращении.
	/// </summary>
	public static class PlanetRecipeDatabase
	{
		[Serializable]
		public class PlanetRecipe
		{
			public string id;
			public int slotUniversal;
			public int slotMining;
			public int slotSocial;
			public int slotIndustrial;
			public int slotScientific;

			public int processTicks;

			public string inResourceRaw;
			public string outResourceRaw;

			public Dictionary<string, float> inResources;
			public Dictionary<string, float> outResources;

			public bool CanUseSlot(string slotName)
			{
				if (string.IsNullOrEmpty(slotName)) return false;
				if (slotName.Contains("Universal", StringComparison.OrdinalIgnoreCase)) return slotUniversal > 0;
				if (slotName.Contains("Mining", StringComparison.OrdinalIgnoreCase)) return slotMining > 0;
				if (slotName.Contains("Social", StringComparison.OrdinalIgnoreCase)) return slotSocial > 0;
				if (slotName.Contains("Industrial", StringComparison.OrdinalIgnoreCase)) return slotIndustrial > 0;
				if (slotName.Contains("Scientific", StringComparison.OrdinalIgnoreCase)) return slotScientific > 0;
				return false;
			}
		}

		private static bool _loaded;
		private static readonly List<PlanetRecipe> _recipes = new List<PlanetRecipe>();
		private static readonly Dictionary<string, PlanetRecipe> _byId = new Dictionary<string, PlanetRecipe>(StringComparer.Ordinal);

		public static IReadOnlyList<PlanetRecipe> Recipes
		{
			get
			{
				EnsureLoaded();
				return _recipes;
			}
		}

		public static PlanetRecipe GetById(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;
			EnsureLoaded();
			_byId.TryGetValue(id, out var r);
			return r;
		}

		private static void EnsureLoaded()
		{
			if (_loaded) return;
			_loaded = true;
			_recipes.Clear();
			_byId.Clear();

			try
			{
				string path = Path.Combine(Application.dataPath, "Config/planet_recipes.json");
				if (!File.Exists(path))
				{
					Debug.LogWarning("[PlanetRecipeDatabase] Не найден файл: Assets/Config/planet_recipes.json");
					return;
				}

				string json = File.ReadAllText(path);
				var wrapped = "{ \"items\": " + json + " }";
				var container = JsonUtility.FromJson<PlanetRecipeJsonWrapper>(wrapped);
				if (container == null || container.items == null) return;

				for (int i = 0; i < container.items.Count; i++)
				{
					var src = container.items[i];
					if (src == null || string.IsNullOrEmpty(src.id_planet_recipe)) continue;

					var r = new PlanetRecipe
					{
						id = src.id_planet_recipe,
						slotUniversal = src.process_slot_Universal,
						slotMining = src.process_slot_Mining,
						slotSocial = src.process_slot_Social,
						slotIndustrial = src.process_slot_Industrial,
						slotScientific = src.process_slot_Scientific,
						processTicks = Mathf.Max(1, src.process_tik_timer),
						inResourceRaw = src.in_resource,
						outResourceRaw = src.out_resource,
						inResources = ParseResources(src.in_resource),
						outResources = ParseResources(src.out_resource)
					};

					_recipes.Add(r);
					_byId[r.id] = r;
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[PlanetRecipeDatabase] Ошибка загрузки: " + e);
			}
		}

		[Serializable]
		private class PlanetRecipeJson
		{
			public string id_planet_recipe;
			public int process_slot_Universal;
			public int process_slot_Mining;
			public int process_slot_Social;
			public int process_slot_Industrial;
			public int process_slot_Scientific;
			public string in_resource;
			public int process_tik_timer;
			public string out_resource;
		}

		[Serializable]
		private class PlanetRecipeJsonWrapper
		{
			public List<PlanetRecipeJson> items;
		}

		private static Dictionary<string, float> ParseResources(string raw)
		{
			var result = new Dictionary<string, float>(StringComparer.Ordinal);
			if (string.IsNullOrWhiteSpace(raw)) return result;

			var entries = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < entries.Length; i++)
			{
				var part = entries[i].Trim();
				if (string.IsNullOrEmpty(part)) continue;
				int idx = part.IndexOf(':');
				if (idx <= 0 || idx >= part.Length - 1) continue;

				string id = part.Substring(0, idx).Trim();
				string valueRaw = part.Substring(idx + 1).Trim();

				if (string.IsNullOrEmpty(id)) continue;
				if (!float.TryParse(valueRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float amount))
				{
					continue;
				}

				result[id] = amount;
			}

			return result;
		}
	}
}


