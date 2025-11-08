using UnityEngine;
using UnityEditor;
using Space;

namespace EditorTools
{
	[CustomEditor(typeof(AsteroidController))]
	public class AsteroidControllerEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// Поле диаметра
			var diameterProp = serializedObject.FindProperty("diameter");
			if (diameterProp != null)
			{
				EditorGUILayout.PropertyField(diameterProp, new GUIContent("Диаметр"));
			}

			var controller = (AsteroidController)target;

			EditorGUILayout.Space(8);
			EditorGUILayout.LabelField("Генерация диаметра", EditorStyles.boldLabel);

			var min = EditorGUILayout.FloatField("Мин. диаметр", controller.GenerateMinDiameter);
			var max = EditorGUILayout.FloatField("Макс. диаметр", controller.GenerateMaxDiameter);

			if (min != controller.GenerateMinDiameter)
			{
				Undo.RecordObject(controller, "Изменение мин. диаметра генерации");
				controller.GenerateMinDiameter = min;
				EditorUtility.SetDirty(controller);
			}
			if (max != controller.GenerateMaxDiameter)
			{
				Undo.RecordObject(controller, "Изменение макс. диаметра генерации");
				controller.GenerateMaxDiameter = max;
				EditorUtility.SetDirty(controller);
			}

			if (GUILayout.Button("Сгенерировать"))
			{
				var tr = controller.transform;
				Undo.RecordObjects(new Object[] { controller, tr }, "Генерация диаметра астероида");
				controller.RegenerateRandom();
				EditorUtility.SetDirty(controller);
				EditorUtility.SetDirty(tr);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}


