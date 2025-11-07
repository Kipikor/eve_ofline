#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class ParentColliderHighlighter2D : MonoBehaviour
{
	public Color color = Color.yellow;

	void OnDrawGizmos()
	{
		var active = Selection.activeTransform;
		if (!active || active == transform || !active.IsChildOf(transform)) return;
		Handles.color = color;
		using (new Handles.DrawingScope(transform.localToWorldMatrix))
		{
			foreach (var col in GetComponents<Collider2D>()) Draw(col);
		}
	}

	static void Draw(Collider2D c)
	{
		if (!c || !c.enabled) return;
		switch (c)
		{
			case BoxCollider2D b:
				Handles.DrawWireCube(b.offset, b.size);
				break;
			case CircleCollider2D cc:
				Handles.DrawWireDisc(cc.offset, Vector3.forward, cc.radius);
				break;
			case PolygonCollider2D p:
				for (int i = 0; i < p.pathCount; i++)
				{
					var path = p.GetPath(i);
					var v = new Vector3[path.Length + 1];
					for (int j = 0; j < path.Length; j++) v[j] = (Vector3)path[j] + (Vector3)p.offset;
					v[path.Length] = v[0];
					Handles.DrawAAPolyLine(2f, v);
				}
				break;
			case CapsuleCollider2D cap:
				Collider2DDrawUtil.DrawCapsule(cap.offset, cap.size, cap.direction);
				break;
		}
	}
}

public static class ParentColliderHighlighter2DMenu
{
	[MenuItem("EVE Offline/Добавить подсветку 2D на выбранный объект")]
	private static void AddToSelected()
	{
		var t = Selection.activeTransform;
		if (!t)
		{
			Debug.LogWarning("Ничего не выбрано.");
			return;
		}
		if (!t.GetComponent<ParentColliderHighlighter2D>()) t.gameObject.AddComponent<ParentColliderHighlighter2D>();
		Selection.activeGameObject = t.gameObject;
		Debug.Log("ParentColliderHighlighter2D добавлен на выбранный объект.");
	}
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class GlobalParentColliderHighlighter2D
{
	private const string MenuPath = "EVE Offline/Подсвечивать родительские 2D-коллайдеры";
	private const string PrefKey = "EVE_AlwaysHighlightParent2DColliders";

	static GlobalParentColliderHighlighter2D()
	{
		EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, Enabled);
		SceneView.duringSceneGui += OnSceneGUI;
	}

	private static bool Enabled
	{
		get => EditorPrefs.GetBool(PrefKey, false);
		set => EditorPrefs.SetBool(PrefKey, value);
	}

	[MenuItem(MenuPath)]
	private static void Toggle()
	{
		Enabled = !Enabled;
		Menu.SetChecked(MenuPath, Enabled);
		SceneView.RepaintAll();
	}

	[MenuItem(MenuPath, true)]
	private static bool ToggleValidate()
	{
		Menu.SetChecked(MenuPath, Enabled);
		return true;
	}

	private static void OnSceneGUI(SceneView sceneView)
	{
		if (!Enabled) return;
		var active = Selection.activeTransform;
		if (!active) return;

		// Найти ближайшего родителя с Collider2D
		var parent = active.parent;
		while (parent != null)
		{
			var cols = parent.GetComponents<Collider2D>();
			if (cols != null && cols.Length > 0)
			{
				// Красный круг радиусом 0.5 м у родителя
				var prevColor = Handles.color;
				Handles.color = Color.red;
				Handles.DrawWireDisc(parent.position, Vector3.forward, 0.5f);
				Handles.color = prevColor;

				DrawParentColliders(parent, cols);
				break;
			}
			parent = parent.parent;
		}
	}

	private static void DrawParentColliders(Transform parent, Collider2D[] colliders)
	{
		Handles.color = Color.yellow;
		using (new Handles.DrawingScope(parent.localToWorldMatrix))
		{
			foreach (var c in colliders)
			{
				if (!c || !c.enabled) continue;
				switch (c)
				{
					case BoxCollider2D b:
						Handles.DrawWireCube(b.offset, b.size);
						break;
					case CircleCollider2D cc:
						Handles.DrawWireDisc(cc.offset, Vector3.forward, cc.radius);
						break;
					case PolygonCollider2D p:
						for (int i = 0; i < p.pathCount; i++)
						{
							var path = p.GetPath(i);
							var v = new Vector3[path.Length + 1];
							for (int j = 0; j < path.Length; j++) v[j] = (Vector3)path[j] + (Vector3)p.offset;
							v[path.Length] = v[0];
							Handles.DrawAAPolyLine(2f, v);
						}
						break;
					case CapsuleCollider2D cap:
						Collider2DDrawUtil.DrawCapsule(cap.offset, cap.size, cap.direction);
						break;
					case EdgeCollider2D e:
						var pts = e.points;
						if (pts != null && pts.Length > 1)
						{
							var vv = new Vector3[pts.Length];
							for (int j = 0; j < pts.Length; j++) vv[j] = (Vector3)pts[j] + (Vector3)e.offset;
							Handles.DrawAAPolyLine(2f, vv);
						}
						break;
				}
			}
		}
	}
}
#endif

#region Utility
static class Collider2DDrawUtil
{
	public static void DrawCapsule(Vector2 offset, Vector2 size, CapsuleDirection2D direction)
	{
		float halfWidth = size.x * 0.5f;
		float halfHeight = size.y * 0.5f;
		Vector3 center = (Vector3)offset;

		if (direction == CapsuleDirection2D.Vertical)
		{
			float radius = halfWidth;
			float straight = Mathf.Max(0f, size.y - 2f * radius);
			if (straight <= 0f)
			{
				Handles.DrawWireDisc(center, Vector3.forward, radius);
				return;
			}

			Vector3 topCenter = center + Vector3.up * (straight * 0.5f);
			Vector3 bottomCenter = center - Vector3.up * (straight * 0.5f);

			Vector3 leftTop = new Vector3(offset.x - radius, topCenter.y, 0f);
			Vector3 rightTop = new Vector3(offset.x + radius, topCenter.y, 0f);
			Vector3 leftBottom = new Vector3(offset.x - radius, bottomCenter.y, 0f);
			Vector3 rightBottom = new Vector3(offset.x + radius, bottomCenter.y, 0f);

			Handles.DrawLine(leftBottom, leftTop);
			Handles.DrawLine(rightBottom, rightTop);
			Handles.DrawWireArc(topCenter, Vector3.forward, Vector3.right, 180f, radius);
			Handles.DrawWireArc(bottomCenter, Vector3.forward, Vector3.left, 180f, radius);
		}
		else // Horizontal
		{
			float radius = halfHeight;
			float straight = Mathf.Max(0f, size.x - 2f * radius);
			if (straight <= 0f)
			{
				Handles.DrawWireDisc(center, Vector3.forward, radius);
				return;
			}

			Vector3 rightCenter = center + Vector3.right * (straight * 0.5f);
			Vector3 leftCenter = center - Vector3.right * (straight * 0.5f);

			Vector3 topRight = new Vector3(rightCenter.x, offset.y + radius, 0f);
			Vector3 bottomRight = new Vector3(rightCenter.x, offset.y - radius, 0f);
			Vector3 topLeft = new Vector3(leftCenter.x, offset.y + radius, 0f);
			Vector3 bottomLeft = new Vector3(leftCenter.x, offset.y - radius, 0f);

			Handles.DrawLine(topLeft, topRight);
			Handles.DrawLine(bottomLeft, bottomRight);
			Handles.DrawWireArc(rightCenter, Vector3.forward, Vector3.up, 180f, radius);
			Handles.DrawWireArc(leftCenter, Vector3.forward, Vector3.down, 180f, radius);
		}
	}
}
#endregion

#endif


