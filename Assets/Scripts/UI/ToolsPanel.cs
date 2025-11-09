using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	public class ToolsPanel : MonoBehaviour
	{
		[Header("Дети панели сверху-вниз (перетаскивай, чтобы менять порядок)")]
		[SerializeField] private List<RectTransform> items = new List<RectTransform>();

		[Header("Автонастройка")]
		[SerializeField] private bool autoCollectChildren = true;
		[SerializeField] private bool applyOrderOnValidate = true;

		private void Reset()
		{
			EnsureLayoutDefaults();
			CollectChildren();
			ApplyOrder();
		}

		private void OnValidate()
		{
			if (autoCollectChildren) CollectChildren();
			if (applyOrderOnValidate) ScheduleApplyOrder();
		}

		[ContextMenu("Collect Children")]
		public void CollectChildren()
		{
			if (items == null) items = new List<RectTransform>();
			items.Clear();
			for (int i = 0; i < transform.childCount; i++)
			{
				if (transform.GetChild(i) is RectTransform rt)
					items.Add(rt);
			}
		}

		#if UNITY_EDITOR
		private void ScheduleApplyOrder()
		{
			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (this == null) return;
				ApplyOrder();
			};
		}
		#endif

		[ContextMenu("Apply Order")]
		public void ApplyOrder()
		{
			for (int i = 0; i < items.Count; i++)
			{
				var rt = items[i];
				if (rt == null) continue;
				rt.SetSiblingIndex(i);
			}

			var rtRoot = transform as RectTransform;
			if (rtRoot != null)
				LayoutRebuilder.MarkLayoutForRebuild(rtRoot);
		}

		private void EnsureLayoutDefaults()
		{
			var vlg = GetComponent<VerticalLayoutGroup>();
			if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
			vlg.childAlignment = TextAnchor.UpperCenter;
			vlg.spacing = 8f;
			vlg.padding = new RectOffset(8, 8, 8, 8);
			// ширина контролируется панелью, высота — самими элементами (квадрат через AspectRatioFitter)
			vlg.childControlWidth = true;
			vlg.childControlHeight = false;
			vlg.childForceExpandWidth = true;
			vlg.childForceExpandHeight = false;
		}
	}
}


