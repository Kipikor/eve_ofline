using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Inventory
{
	[DisallowMultipleComponent]
	public class InventoryController : MonoBehaviour
	{
		[Header("Capacity")]
		[SerializeField] private bool useShipCargoHolding = true;
		[SerializeField, Min(0f)] private float capacityM3 = 750f;

		[Header("UI Refs (filled by builder)")]
		[SerializeField] private RectTransform gridContent;
		[SerializeField] private GridLayoutGroup gridLayout;
		[SerializeField] private Image capacityFill;
		[SerializeField] private TMP_Text capacityText;

		[Header("Grid Settings")]
		[SerializeField] private Vector2 cellSize = new Vector2(96, 112);
		[SerializeField] private Vector2 cellSpacing = new Vector2(8, 8);
		[SerializeField] private bool autoColumns = true;
		[SerializeField] private int columns = 3; // используется, если autoColumns = false

		[Header("Item Layout")]
		[SerializeField, Min(0f)] private float iconBottomPadding = 20f;
		[SerializeField] private Vector2 countOffset = new Vector2(-6f, 6f);
		[SerializeField, Min(12f)] private float nameHeight = 20f;

		[Header("Runtime")]
		[SerializeField] private List<StackEntry> stacks = new List<StackEntry>();

		[Header("Editor Tools")]
		[SerializeField, Min(1)] private int debugRandomMin = 1;
		[SerializeField, Min(1)] private int debugRandomMax = 100;
		[SerializeField] private bool editorAutoRefresh = false; // авто-перестройка UI при изменениях в инспекторе
		[SerializeField] private bool controlBarByAnchors = true; // управлять заливкой барчика через anchors (иначе использовать Image.fillAmount)

		[Serializable]
		public class StackEntry
		{
			public string itemId;
			public int amount;
		}

		#region Mineral DB
		[Serializable] private class MineralDef { public string item_id; public string item_name; public string item_icon; public float cagro; public float cost; public string item_descr; }
		[Serializable] private class MineralList { public List<MineralDef> items; }
		private static Dictionary<string, MineralDef> _db;

		private static void EnsureDb()
		{
			if (_db != null) return;
			_db = new Dictionary<string, MineralDef>(StringComparer.Ordinal);
			try
			{
				var path = Path.Combine(Application.dataPath, "Config/mineral.json");
				if (!File.Exists(path)) return;
				var json = File.ReadAllText(path);
				var wrapped = "{ \"items\": " + json + " }";
				var list = JsonUtility.FromJson<MineralList>(wrapped);
				if (list?.items == null) return;
				foreach (var m in list.items) if (!string.IsNullOrEmpty(m.item_id) && !_db.ContainsKey(m.item_id)) _db.Add(m.item_id, m);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Inventory] Mineral DB load error: {e}");
			}
		}

		private static MineralDef GetDef(string id)
		{
			EnsureDb();
			if (id == null) return null;
			_db.TryGetValue(id, out var def);
			return def;
		}

		private static Sprite LoadIcon(string iconKey)
		{
			if (string.IsNullOrEmpty(iconKey)) return null;
			// try Resources first (recommended to place icons under Assets/Resources/Sprites/Mineral/)
			var s = Resources.Load<Sprite>($"Sprites/Mineral/{iconKey}");
#if UNITY_EDITOR
			if (s == null)
			{
				// Fallback to editor-time asset lookup
				string basePath = "Assets/Sprites/Mineral/" + iconKey;
				string[] exts = { ".png", ".psb", ".jpg", ".jpeg" };
				foreach (var ext in exts)
				{
					var p = basePath + ext;
					var byPath = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
					if (byPath != null) return byPath;
				}
				// search by name
				var guids = UnityEditor.AssetDatabase.FindAssets(iconKey + " t:Sprite");
				foreach (var g in guids)
				{
					var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
					var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
					if (asset != null && asset.name == iconKey) return asset;
				}
			}
#endif
			return s;
		}
		#endregion

		private void Awake()
		{
			if (useShipCargoHolding)
			{
				EveOffline.Space.ShipController ship = null;
#if UNITY_2023_1_OR_NEWER
				ship = UnityEngine.Object.FindFirstObjectByType<EveOffline.Space.ShipController>(FindObjectsInactive.Exclude);
				if (ship == null) ship = UnityEngine.Object.FindAnyObjectByType<EveOffline.Space.ShipController>(FindObjectsInactive.Exclude);
#else
				var all = Resources.FindObjectsOfTypeAll(typeof(EveOffline.Space.ShipController));
				for (int i = 0; i < all.Length; i++)
				{
					var candidate = all[i] as EveOffline.Space.ShipController;
					if (candidate != null && candidate.gameObject.scene.IsValid())
					{
						ship = candidate;
						break;
					}
				}
#endif
				if (ship != null)
				{
					capacityM3 = Mathf.Max(0f, ship.CargoHolding);
				}
			}
			ApplyGridSettings();
			RefreshUI();
		}

		private void OnValidate()
		{
			ApplyGridSettings();
			#if UNITY_EDITOR
			if (editorAutoRefresh)
			{
				UnityEditor.EditorApplication.delayCall += () =>
				{
					if (this == null) return;
					RefreshUI();
				};
			}
			#endif
		}

		private void ApplyGridSettings()
		{
			if (gridLayout != null)
			{
				gridLayout.cellSize = cellSize;
				gridLayout.spacing = cellSpacing;
				gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
				gridLayout.constraintCount = Mathf.Max(1, autoColumns ? ComputeAutoColumns() : columns);
				gridLayout.childAlignment = TextAnchor.UpperLeft;
			}
		}

		private int ComputeAutoColumns()
		{
			var target = gridContent != null ? gridContent : (transform as RectTransform);
			if (target == null) return Mathf.Max(1, columns);
			float totalWidth = target.rect.width;
			var padding = gridLayout != null ? gridLayout.padding : new RectOffset();
			float available = Mathf.Max(0f, totalWidth - padding.left - padding.right);
			float step = cellSize.x + cellSpacing.x;
			if (step <= 0.0001f) return Mathf.Max(1, columns);
			// "+ cellSpacing.x" позволяет уместить последнюю ячейку без лишнего шага
			int cols = Mathf.FloorToInt((available + cellSpacing.x) / step);
			return Mathf.Max(1, cols);
		}

		private void OnRectTransformDimensionsChange()
		{
			// Пересчитать количество колонок при изменении размеров панели (и в редакторе, и в рантайме)
			if (gridLayout == null) return;
			if (autoColumns)
			{
				int newCols = Mathf.Max(1, ComputeAutoColumns());
				if (gridLayout.constraintCount != newCols)
				{
					gridLayout.constraintCount = newCols;
					if (gridContent != null) UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(gridContent);
				}
			}
		}

		#region Inventory API
		public float Capacity => capacityM3;
		public float UsedVolume => ComputeUsedVolume();

		public int AddItem(string itemId, int amount)
		{
			var def = GetDef(itemId);
			if (def == null) return 0;
			int toAdd = Mathf.Max(0, amount);
			if (toAdd == 0) return 0;
			float free = Mathf.Max(0f, capacityM3 - UsedVolume);
			int canFit = def.cagro > 0.00001f ? Mathf.FloorToInt(free / def.cagro) : toAdd;
			int addCount = Mathf.Clamp(toAdd, 0, canFit);
			if (addCount <= 0) return 0;

			var stack = stacks.Find(s => s.itemId == itemId);
			if (stack == null)
			{
				stacks.Add(new StackEntry { itemId = itemId, amount = addCount });
			}
			else
			{
				stack.amount += addCount;
			}
			RefreshUI();
			return addCount;
		}

		public int RemoveItem(string itemId, int amount)
		{
			var s = stacks.Find(x => x.itemId == itemId);
			if (s == null || amount <= 0) return 0;
			int take = Mathf.Min(s.amount, amount);
			s.amount -= take;
			if (s.amount <= 0) stacks.Remove(s);
			RefreshUI();
			return take;
		}

		private float ComputeUsedVolume()
		{
			float acc = 0f;
			foreach (var s in stacks)
			{
				var def = GetDef(s.itemId);
				if (def == null) continue;
				acc += def.cagro * s.amount;
			}
			return acc;
		}
		#endregion

		#region UI
		public void RefreshUI()
		{
			// header
			if (capacityFill != null)
			{
				float k = capacityM3 > 0.0001f ? Mathf.Clamp01(UsedVolume / capacityM3) : 0f;
				if (controlBarByAnchors)
				{
					var frt = capacityFill.rectTransform;
					// якоря: слева 0, справа k
					frt.anchorMin = new Vector2(0f, 0f);
					frt.anchorMax = new Vector2(k, 1f);
					// оставляем небольшие внутренние поля
					frt.offsetMin = new Vector2(2f, 2f);
					frt.offsetMax = new Vector2(-2f, -2f);
				}
				else
				{
					// вариант через Image.fillAmount (если нужно оставить Filled-спрайт)
					capacityFill.type = Image.Type.Filled;
					capacityFill.fillMethod = Image.FillMethod.Horizontal;
					capacityFill.fillAmount = k;
				}
			}
			if (capacityText != null)
			{
				capacityText.text = $"{UsedVolume:0.##}/{capacityM3:0.##}";
			}
			// grid
			if (gridContent == null) return;
			for (int i = gridContent.childCount - 1; i >= 0; i--)
			{
				var child = gridContent.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(child);
				else DestroyImmediate(child);
			}
			foreach (var s in stacks)
			{
				var def = GetDef(s.itemId);
				if (def == null) continue;
				CreateItemView(def, s.amount);
			}

			// Пересборка лэйаута, чтобы элементы гарантированно стали видны
			var rt = gridContent as RectTransform;
			if (rt != null)
			{
				UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
				if (rt.parent is RectTransform prt) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
			}
		}

		private void CreateItemView(MineralDef def, int amount)
		{
			var go = new GameObject($"Item_{def.item_id}", typeof(RectTransform));
			go.transform.SetParent(gridContent, false);
			var rt = (RectTransform)go.transform;
			rt.anchorMin = new Vector2(0, 1);
			rt.anchorMax = new Vector2(0, 1);
			rt.sizeDelta = cellSize;
			// гарантируем понятный размер для GridLayoutGroup
			var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
			le.preferredWidth = cellSize.x;
			le.preferredHeight = cellSize.y;

			// Icon
			var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
			iconGO.transform.SetParent(rt, false);
			var iconRT = (RectTransform)iconGO.transform;
			iconRT.anchorMin = new Vector2(0, 0); // leave bottom space for name
			iconRT.anchorMax = new Vector2(1, 1);
			iconRT.offsetMin = new Vector2(0, iconBottomPadding);
			iconRT.offsetMax = new Vector2(0, 0);
			var img = iconGO.GetComponent<Image>();
			img.sprite = LoadIcon(def.item_icon);
			img.preserveAspect = true;
			img.raycastTarget = false;

			// Count (bottom-right on icon)
			var countGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
			var countRT = countGO.GetComponent<RectTransform>();
			countGO.transform.SetParent(iconRT, false);
			countRT.anchorMin = new Vector2(1, 0);
			countRT.anchorMax = new Vector2(1, 0);
			countRT.pivot = new Vector2(1, 0);
			countRT.anchoredPosition = countOffset;
			var countTxt = countGO.GetComponent<TextMeshProUGUI>();
			countTxt.raycastTarget = false;
			countTxt.alignment = TextAlignmentOptions.BottomRight;
			countTxt.enableAutoSizing = false;
			countTxt.fontSize = 18f;
			countTxt.text = FormatCount(amount);

			// Name (bottom full width)
			var nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
			var nameRT = nameGO.GetComponent<RectTransform>();
			nameGO.transform.SetParent(rt, false);
			nameRT.anchorMin = new Vector2(0, 0);
			nameRT.anchorMax = new Vector2(1, 0);
			nameRT.pivot = new Vector2(0.5f, 0);
			nameRT.sizeDelta = new Vector2(0, nameHeight);
			var nameTxt = nameGO.GetComponent<TextMeshProUGUI>();
			nameTxt.raycastTarget = false;
			nameTxt.alignment = TextAlignmentOptions.Midline;
			nameTxt.enableAutoSizing = false;
			nameTxt.fontSize = 16f;
			nameTxt.text = def.item_name ?? def.item_id;
		}

		private static string FormatCount(int amount)
		{
			if (amount >= 1000000) return (amount / 1000000f).ToString("0.#") + "M";
			if (amount >= 1000) return (amount / 1000f).ToString("0.#") + "k";
			return amount.ToString();
		}
		#endregion

#if UNITY_EDITOR
		[ContextMenu("Add Random Item (Debug)")]
		public void EditorAddRandomItem()
		{
			EnsureDb();
			if (_db == null || _db.Count == 0) return;
			// random id (устойчивый выбор по индексу)
			var keys = new List<string>(_db.Keys);
			int idx = UnityEngine.Random.Range(0, keys.Count);
			string id = keys[idx];
			if (string.IsNullOrEmpty(id)) return;

			// random amount within [min, max]
			int min = Mathf.Max(1, debugRandomMin);
			int max = Mathf.Max(min, debugRandomMax);
			int amount = UnityEngine.Random.Range(min, max + 1);

			AddItem(id, amount);
			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}


