using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI;

namespace EditorTools
{
	public static class PanelToolsBuilder
	{
		private const string PanelName = "Panel_tools";
		private const string ButtonPrefabPath = "Assets/Prefab/UI/Button.prefab";

		[MenuItem("Tools/UI/Build Panel_tools")]
		public static void BuildPanel()
		{
			var panelGO = GameObject.Find(PanelName);
			if (panelGO == null)
			{
				EditorUtility.DisplayDialog("Panel not found", $"Не найден объект '{PanelName}' на сцене.", "OK");
				return;
			}

			var panelRT = panelGO.GetComponent<RectTransform>();
			if (panelRT == null)
			{
				EditorUtility.DisplayDialog("Wrong object", $"'{PanelName}' должен быть UI RectTransform.", "OK");
				return;
			}

			Undo.RegisterFullObjectHierarchyUndo(panelGO, "Build Panel_tools");

			// Ensure CanvasGroup (optional)
			if (panelGO.GetComponent<CanvasRenderer>() == null && panelGO.GetComponent<Image>() == null)
			{
				// add transparent image to ensure raycast area if needed
				var img = panelGO.AddComponent<Image>();
				img.color = new Color(0, 0, 0, 0);
			}

			// Ensure VerticalLayoutGroup via ToolsPanel
			var toolsPanel = panelGO.GetComponent<ToolsPanel>();
			if (toolsPanel == null)
			{
				toolsPanel = panelGO.AddComponent<ToolsPanel>();
			}

			// Load button prefab
			var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
			if (buttonPrefab == null)
			{
				EditorUtility.DisplayDialog("Prefab not found", $"Не найден префаб кнопки:\n{ButtonPrefabPath}", "OK");
				return;
			}

			// Ensure existing buttons have layout & binding
			var existingButtons = panelGO.GetComponentsInChildren<Button>(true);
			foreach (var b in existingButtons)
			{
				if (b == null) continue;
				var rt = b.GetComponent<RectTransform>();
				SetupButtonAsSquare(rt);
				EnsureToggleBinding(b.gameObject);
			}

			// Auto-create buttons for each ToolWindow in scene if there is no bound button yet
			var windows = FindAllToolWindows();
			foreach (var win in windows)
			{
				if (HasButtonForWindow(panelGO, win)) continue;
				var btn = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, panelGO.scene);
				btn.name = $"Btn_{win.gameObject.name}";
				var btnRT = btn.GetComponent<RectTransform>();
				btnRT.SetParent(panelRT, false);
				SetupButtonAsSquare(btnRT);
				EnsureToggleBinding(btnRT.gameObject);
				// bind and label
				var binding = btn.GetComponent<UI.ToolToggleBinding>();
				binding.targetWindow = win;
				SetButtonLabel(btn, win.gameObject.name);
			}

			// Refresh and order list
			toolsPanel.CollectChildren();
			toolsPanel.ApplyOrder();

			EditorUtility.SetDirty(panelGO);
			EditorUtility.DisplayDialog("Panel built", "Панель собрана. Кнопки выровнены и квадратизированы.", "OK");
		}

		private static ToolWindow[] FindAllToolWindows()
		{
			#if UNITY_2023_1_OR_NEWER
			return Object.FindObjectsByType<ToolWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			#elif UNITY_2020_1_OR_NEWER
			return Object.FindObjectsOfType<ToolWindow>(true);
			#else
			var all = Resources.FindObjectsOfTypeAll<ToolWindow>();
			var list = new List<ToolWindow>();
			foreach (var w in all)
			{
				if (w != null && w.gameObject.scene.IsValid()) list.Add(w);
			}
			return list.ToArray();
			#endif
		}

		private static void SetupButtonAsSquare(RectTransform btn)
		{
			if (btn == null) return;
			// stretch horizontally to parent, keep some padding handled by VerticalLayoutGroup
			btn.anchorMin = new Vector2(0, btn.anchorMin.y);
			btn.anchorMax = new Vector2(1, btn.anchorMax.y);
			btn.offsetMin = new Vector2(0, btn.offsetMin.y);
			btn.offsetMax = new Vector2(0, btn.offsetMax.y);

			// Ensure LayoutElement (optional min height)
			var le = btn.GetComponent<LayoutElement>();
			if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
			le.minHeight = 0;           // let aspect define height
			le.preferredHeight = 0;     // no fixed height
			le.flexibleHeight = 0;
			le.flexibleWidth = 1;

			// Add/ensure AspectRatioFitter on root button: width controls height (square)
			var arf = btn.GetComponent<AspectRatioFitter>();
			if (arf == null) arf = btn.gameObject.AddComponent<AspectRatioFitter>();
			arf.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
			arf.aspectRatio = 1f;

			// Ensure child Image/Text anchors fill (optional)
			for (int i = 0; i < btn.childCount; i++)
			{
				if (btn.GetChild(i) is RectTransform rt)
				{
					rt.anchorMin = new Vector2(0, 0);
					rt.anchorMax = new Vector2(1, 1);
					rt.offsetMin = Vector2.zero;
					rt.offsetMax = Vector2.zero;
				}
			}
		}
		
		private static void EnsureToggleBinding(GameObject btn)
		{
			// If object already has a Button (Selectable), do NOT add Toggle.
			// Binding will use Button.onClick to toggle the window.
			var button = btn.GetComponent<Button>();
			var toggle = btn.GetComponent<Toggle>();
			if (toggle == null && button == null)
			{
				// Pure graphic container -> add Toggle so we can show on/off state
				toggle = btn.AddComponent<Toggle>();
				toggle.transition = Selectable.Transition.None;
				Graphic g = btn.GetComponent<Graphic>();
				if (g == null)
				{
					var img = btn.GetComponent<Image>();
					if (img == null) img = btn.AddComponent<Image>();
					g = img;
				}
				toggle.targetGraphic = g;
			}

			// Ensure binding
			var binding = btn.GetComponent<UI.ToolToggleBinding>();
			if (binding == null)
			{
				binding = btn.AddComponent<UI.ToolToggleBinding>();
				// default tint colors: off = original, on = slightly brighter
				if (binding.tintGraphics == null || binding.tintGraphics.Length == 0)
				{
					var g = btn.GetComponent<Graphic>();
					if (g != null) binding.tintGraphics = new[] { g };
					binding.offColor = g != null ? g.color : Color.white;
					binding.onColor = binding.offColor * 1.1f;
					binding.onColor.a = binding.offColor.a;
				}
			}
		}

		private static bool HasButtonForWindow(GameObject panel, ToolWindow w)
		{
			var bindings = panel.GetComponentsInChildren<UI.ToolToggleBinding>(true);
			foreach (var b in bindings)
			{
				if (b != null && b.targetWindow == w) return true;
			}
			return false;
		}

		private static void SetButtonLabel(GameObject btn, string label)
		{
			var tmp = btn.GetComponentInChildren<TMP_Text>(true);
			if (tmp != null) { tmp.text = label; return; }
			var txt = btn.GetComponentInChildren<Text>(true);
			if (txt != null) txt.text = label;
		}
	}
}


