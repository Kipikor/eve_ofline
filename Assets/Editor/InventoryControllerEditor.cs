using UnityEditor;
using UnityEngine;
using UI.Inventory;

[CustomEditor(typeof(InventoryController))]
public class InventoryControllerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Отладка", EditorStyles.boldLabel);
		if (GUILayout.Button("Добавить случайный предмет"))
		{
			foreach (var t in targets)
			{
				var ctrl = t as InventoryController;
				if (ctrl == null) continue;
				Undo.RecordObject(ctrl, "Add Random Item");
				ctrl.EditorAddRandomItem();
				EditorUtility.SetDirty(ctrl);
			}
		}
	}
}


