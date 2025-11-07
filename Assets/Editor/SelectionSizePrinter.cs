using UnityEditor;
using UnityEngine;

public static class SelectionSizePrinter
{
	[MenuItem("EVE Offline/Вывести размер выделенного объекта (м)")]
	public static void PrintSelectedObjectSize()
	{
		var go = Selection.activeGameObject;
		if (go == null)
		{
			Debug.LogWarning("Ничего не выбрано.");
			return;
		}

		if (TryGetRenderersBounds(go, out var bounds) || TryGetCollider2DBounds(go, out bounds))
		{
			float width = bounds.size.x;
			float height = bounds.size.y;
			Debug.Log($"Объект: {go.name} | Ширина: {width:F3} м | Высота: {height:F3} м");
			return;
		}

		if (TryGetRectTransformSizeWorld(go, out var widthRt, out var heightRt))
		{
			Debug.Log($"Объект: {go.name} | Ширина: {widthRt:F3} м | Высота: {heightRt:F3} м");
			return;
		}

		Debug.LogWarning($"Не удалось определить размеры для '{go.name}'. Нет Renderer/Collider2D/RectTransform.");
	}

	private static bool TryGetRenderersBounds(GameObject go, out Bounds combined)
	{
		var renderers = go.GetComponentsInChildren<Renderer>();
		if (renderers != null && renderers.Length > 0)
		{
			combined = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
			{
				combined.Encapsulate(renderers[i].bounds);
			}
			return true;
		}
		combined = default;
		return false;
	}

	private static bool TryGetCollider2DBounds(GameObject go, out Bounds combined)
	{
		var colliders = go.GetComponentsInChildren<Collider2D>();
		if (colliders != null && colliders.Length > 0)
		{
			combined = colliders[0].bounds;
			for (int i = 1; i < colliders.Length; i++)
			{
				combined.Encapsulate(colliders[i].bounds);
			}
			return true;
		}
		combined = default;
		return false;
	}

	private static bool TryGetRectTransformSizeWorld(GameObject go, out float width, out float height)
	{
		var rt = go.GetComponent<RectTransform>();
		if (rt == null)
		{
			width = height = 0f;
			return false;
		}
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners); // 0:BL, 1:TL, 2:TR, 3:BR
		width = Vector3.Distance(corners[0], corners[3]);
		height = Vector3.Distance(corners[0], corners[1]);
		return true;
	}
}


