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

		// Рисуем все поля, КРОМЕ служебных списков, которые оформляем ниже
		DrawPropertiesExcluding(serializedObject, "processSlots", "resources");

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
					EditorGUILayout.TextField(nameProp.stringValue, GUILayout.MinWidth(140));

					// Редактируемое значение штрафа в формате "140%"
					string penaltyText = $"{penaltyProp.floatValue:0.##}%";
					GUI.enabled = true;
					string newPenaltyText = EditorGUILayout.TextField(penaltyText, GUILayout.Width(80));

					// Парсим число обратно, допускаем ввод без знака % или с ним
					if (newPenaltyText != penaltyText && !string.IsNullOrEmpty(newPenaltyText))
					{
						string raw = newPenaltyText.Trim();
						if (raw.EndsWith("%"))
							raw = raw.Substring(0, raw.Length - 1);
						if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
							    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
						{
							penaltyProp.floatValue = parsed;
						}
					}

					EditorGUILayout.EndHorizontal();
				}
			}

			EditorGUI.indentLevel--;
		}

		// Ресурсы планеты
		EditorGUILayout.Space();
		bool showResources = EditorGUILayout.Foldout(true, "Ресурсы планеты", true);
		if (showResources)
		{
			EditorGUI.indentLevel++;
			var resProp = serializedObject.FindProperty("resources");
			if (resProp != null)
			{
				if (resProp.arraySize == 0)
				{
					EditorGUILayout.HelpBox("Ресурсы ещё не сгенерированы. Убедись, что собраны planet_database и planet_resource_database.", MessageType.Info);
				}
				for (int i = 0; i < resProp.arraySize; i++)
				{
					var element = resProp.GetArrayElementAtIndex(i);
					var idProp = element.FindPropertyRelative("resourceId");
					var nameProp = element.FindPropertyRelative("resourceName");
					var currentProp = element.FindPropertyRelative("currentAmount");

					if (nameProp == null || currentProp == null) continue;

					EditorGUILayout.BeginHorizontal();

					// Нередактируемое имя ресурса (или ID, если имя пустое)
					GUI.enabled = false;
					string label = string.IsNullOrEmpty(nameProp.stringValue) ? idProp.stringValue : nameProp.stringValue;
					EditorGUILayout.TextField(label, GUILayout.MinWidth(140));
					GUI.enabled = true;

					// Редактируемое текущее количество
					float value = currentProp.floatValue;
					value = EditorGUILayout.FloatField(value, GUILayout.Width(80));
					currentProp.floatValue = value;

					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUI.indentLevel--;
		}

		serializedObject.ApplyModifiedProperties();
	}
}


