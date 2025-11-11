using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UI.Inventory;
using UnityEngine;

namespace EveOffline.Industry
{
	[DisallowMultipleComponent]
	public class ProductionModuleRunner : MonoBehaviour
	{
		private InventoryController inventory;
		private EveOffline.Space.ShipController ship;

		[Serializable]
		private class Recipe
		{
			public string module_production_id;
			public int sort_order;
			public string name;
			public string descr;
			public string in_production;
			public float timer_production;
			public string production_out;
		}

		[Serializable]
		private class RecipeList { public List<Recipe> items; }

		private List<Recipe> recipes;
		private bool isProducing;
		private System.Random rng;

		private void Awake()
		{
			rng = new System.Random();
			inventory = InventoryController.Instance;
			if (inventory == null)
			{
#if UNITY_2023_1_OR_NEWER
				inventory = UnityEngine.Object.FindFirstObjectByType<InventoryController>(FindObjectsInactive.Exclude);
				if (inventory == null) inventory = UnityEngine.Object.FindAnyObjectByType<InventoryController>(FindObjectsInactive.Exclude);
#else
				var all = Resources.FindObjectsOfTypeAll(typeof(InventoryController));
				for (int i = 0; i < all.Length; i++)
				{
					var candidate = all[i] as InventoryController;
					if (candidate != null && candidate.gameObject.scene.IsValid()) { inventory = candidate; break; }
				}
#endif
			}

			if (inventory == null)
			{
				Debug.LogWarning("[Production] Не найден InventoryController — промышленный модуль отключён.");
				enabled = false;
				return;
			}

			ship = FindShip();
			if (ship == null)
			{
				Debug.LogWarning("[Production] Не найден ShipController — промышленный модуль отключён.");
				enabled = false;
				return;
			}

			LoadRecipes();
			if (recipes == null || recipes.Count == 0)
			{
				Debug.LogWarning("[Production] Рецепты не загружены или пустые — промышленный модуль отключён.");
				enabled = false;
				return;
			}

			StartCoroutine(RunLoop());
		}

		private EveOffline.Space.ShipController FindShip()
		{
#if UNITY_2023_1_OR_NEWER
			var s = UnityEngine.Object.FindFirstObjectByType<EveOffline.Space.ShipController>(FindObjectsInactive.Exclude);
			if (s == null) s = UnityEngine.Object.FindAnyObjectByType<EveOffline.Space.ShipController>(FindObjectsInactive.Exclude);
			return s;
#else
			var all = Resources.FindObjectsOfTypeAll(typeof(EveOffline.Space.ShipController));
			for (int i = 0; i < all.Length; i++)
			{
				var candidate = all[i] as EveOffline.Space.ShipController;
				if (candidate != null && candidate.gameObject.scene.IsValid()) return candidate;
			}
			return null;
#endif
		}

		private void LoadRecipes()
		{
			try
			{
				string json = null;
				var ta = Resources.Load<TextAsset>("Config/production_recipes");
				if (ta == null) ta = Resources.Load<TextAsset>("production_recipes");
				if (ta != null && !string.IsNullOrWhiteSpace(ta.text)) json = ta.text;
#if UNITY_EDITOR
				if (string.IsNullOrEmpty(json))
				{
					string[] guids = UnityEditor.AssetDatabase.FindAssets("production_recipes t:TextAsset");
					for (int gi = 0; gi < guids.Length; gi++)
					{
						string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[gi]);
						if (!path.EndsWith("production_recipes.json", StringComparison.OrdinalIgnoreCase)) continue;
						var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
						if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) { json = asset.text; break; }
					}
				}
#else
				if (string.IsNullOrEmpty(json))
				{
					string path = Path.Combine(Application.dataPath, "Config/production_recipes.json");
					if (File.Exists(path)) json = File.ReadAllText(path);
				}
#endif
				if (string.IsNullOrWhiteSpace(json))
				{
					recipes = new List<Recipe>();
					return;
				}
				var wrapped = "{ \"items\": " + json + " }";
				var list = JsonUtility.FromJson<RecipeList>(wrapped);
				recipes = list != null && list.items != null ? list.items : new List<Recipe>();
			}
			catch (Exception e)
			{
				Debug.LogWarning("[Production] Ошибка загрузки production_recipes.json: " + e.Message);
				recipes = new List<Recipe>();
			}
		}

		private IEnumerator RunLoop()
		{
			while (true)
			{
				if (!isProducing)
				{
					TryStartAnyRecipe();
				}
				yield return new WaitForSeconds(0.5f);
			}
		}

		private void TryStartAnyRecipe()
		{
			string moduleId = ship != null ? ship.InnateModuleId : null;
			if (string.IsNullOrEmpty(moduleId)) return;
			if (recipes == null || recipes.Count == 0) return;

			var candidates = new List<Recipe>();
			for (int i = 0; i < recipes.Count; i++)
			{
				var r = recipes[i];
				if (r == null) continue;
				if (!string.Equals(r.module_production_id, moduleId, StringComparison.Ordinal)) continue;
				if (CanSatisfyInputs(r)) candidates.Add(r);
			}

			if (candidates.Count == 0) return;

			int idx = rng.Next(candidates.Count);
			var chosen = candidates[idx];
			if (TryConsumeInputs(chosen, out var consumed))
			{
				StartCoroutine(ProduceRoutine(chosen, consumed));
			}
		}

		private bool CanSatisfyInputs(Recipe r)
		{
			if (r == null || string.IsNullOrEmpty(r.in_production)) return false;
			var inputs = ParseItems(r.in_production);
			if (inputs == null || inputs.Count == 0) return false;
			foreach (var kv in inputs)
			{
				if (inventory.GetItemCount(kv.Key) < kv.Value) return false;
			}
			return true;
		}

		private bool TryConsumeInputs(Recipe r, out Dictionary<string, int> consumed)
		{
			consumed = new Dictionary<string, int>(StringComparer.Ordinal);
			var inputs = ParseItems(r.in_production);
			if (inputs == null || inputs.Count == 0) return false;

			foreach (var kv in inputs)
			{
				int need = Mathf.Max(0, kv.Value);
				if (need == 0) continue;
				int taken = inventory.RemoveItem(kv.Key, need);
				consumed[kv.Key] = taken;
				if (taken != need)
				{
					// откат
					foreach (var back in consumed)
					{
						if (back.Value > 0) inventory.AddItem(back.Key, back.Value);
					}
					return false;
				}
			}
			return true;
		}

		private IEnumerator ProduceRoutine(Recipe r, Dictionary<string, int> consumed)
		{
			isProducing = true;
			float wait = r != null && r.timer_production > 0f ? r.timer_production : 0f;
			if (wait > 0f) yield return new WaitForSeconds(wait);

			var outputs = ParseItems(r.production_out);
			if (outputs != null)
			{
				foreach (var kv in outputs)
				{
					if (kv.Value > 0) inventory.AddItem(kv.Key, kv.Value);
				}
			}
			isProducing = false;
		}

		private static Dictionary<string, int> ParseItems(string spec)
		{
			if (string.IsNullOrWhiteSpace(spec)) return null;
			var map = new Dictionary<string, int>(StringComparer.Ordinal);
			var pairs = spec.Split(',');
			for (int i = 0; i < pairs.Length; i++)
			{
				var p = pairs[i]?.Trim();
				if (string.IsNullOrEmpty(p)) continue;
				int colon = p.IndexOf(':');
				if (colon <= 0 || colon >= p.Length - 1) continue;
				string id = p.Substring(0, colon).Trim();
				string n = p.Substring(colon + 1).Trim();
				if (string.IsNullOrEmpty(id)) continue;
				if (!int.TryParse(n, out int amount)) continue;
				if (!map.ContainsKey(id)) map.Add(id, Mathf.Max(0, amount));
			}
			return map;
		}
	}
}


