using EveOffline.Planets;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetController))]
public class PlanetControllerEditor : Editor
{
	private static bool s_showProcessSlots = true;

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		// Рисуем все поля, КРОМЕ списка слотов процессов — его оформляем отдельно ниже
		DrawPropertiesExcluding(serializedObject, "processSlots");

		EditorGUILayout.Space();
		s_showProcessSlots = EditorGUILayout.Foldout(s_showProcessSlots, "Процессные слоты", true);
		if (s_showProcessSlots)
		{
			EditorGUI.indentLevel++;

			var slotsProp = serializedObject.FindProperty("processSlots");
			if (slotsProp != null)
			{
				if (slotsProp.arraySize == 0)
				{
					EditorGUILayout.HelpBox("Слоты ещё не сгенерированы. Убедись, что у планеты задан planetId и собран planet_database.", MessageType.Info);
				}
				for (int i = 0; i < slotsProp.arraySize; i++)
				{
					var element = slotsProp.GetArrayElementAtIndex(i);
					var nameProp = element.FindPropertyRelative("slotName");
					var penaltyProp = element.FindPropertyRelative("penaltyPercent");

					if (nameProp == null || penaltyProp == null) continue;

					EditorGUILayout.BeginHorizontal();

					// Нередактируемое имя слота
					GUI.enabled = false;
					EditorGUILayout.TextField(nameProp.stringValue, GUILayout.MinWidth(120));

					// Нередактируемое значение штрафа в формате "140%"
					string penaltyText = $"{penaltyProp.floatValue:0.##}%";
					EditorGUILayout.TextField(penaltyText, GUILayout.Width(80));
					GUI.enabled = true;

					EditorGUILayout.EndHorizontal();
				}
			}

			EditorGUI.indentLevel--;
		}

		serializedObject.ApplyModifiedProperties();
	}
}


