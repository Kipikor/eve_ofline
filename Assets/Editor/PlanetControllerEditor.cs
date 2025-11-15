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
					var recipeProp = element.FindPropertyRelative("currentRecipeId");
					var ticksProp = element.FindPropertyRelative("ticksRemaining");

					if (nameProp == null || penaltyProp == null || recipeProp == null || ticksProp == null) continue;

					EditorGUILayout.BeginHorizontal();

					// Тип слота (не редактируемый)
					GUI.enabled = false;
					EditorGUILayout.TextField(nameProp.stringValue, GUILayout.MinWidth(120));

					GUI.enabled = true;

					// Штраф (редактируемый, без знака %)
					float penalty = penaltyProp.floatValue;
					penalty = EditorGUILayout.FloatField(penalty, GUILayout.Width(60));
					penaltyProp.floatValue = penalty;

					// Текущий рецепт (не редактируемый)
					GUI.enabled = false;
					EditorGUILayout.TextField(string.IsNullOrEmpty(recipeProp.stringValue) ? "-" : recipeProp.stringValue, GUILayout.MinWidth(140));
					GUI.enabled = true;

					// Тиков до окончания (редактируемый)
					int ticks = ticksProp.intValue;
					ticks = EditorGUILayout.IntField(ticks, GUILayout.Width(60));
					ticksProp.intValue = Mathf.Max(0, ticks);

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
					var targetProp = element.FindPropertyRelative("targetAmount");
					var warningProp = element.FindPropertyRelative("warningAmount");
					var basePriceProp = element.FindPropertyRelative("basePrice"); // оставляем на будущее, но не рисуем отдельно
					var currentPriceProp = element.FindPropertyRelative("currentPrice");

					if (nameProp == null || currentProp == null || targetProp == null || warningProp == null || currentPriceProp == null) continue;

					float current = currentProp.floatValue;
					float target = targetProp.floatValue;
					float warning = warningProp.floatValue;

					// Выбираем цвет имени ресурса по запасам
					Color oldColor = GUI.color;
					if (target > 0f && current >= target)
					{
						GUI.color = Color.green;
					}
					else
					{
						float lowThreshold = warning > 0f ? warning : target;
						if (lowThreshold > 0f && current < lowThreshold)
						{
							GUI.color = Color.red;
						}
						else if (target > 0f && current < target)
						{
							GUI.color = Color.yellow;
						}
					}

					EditorGUILayout.BeginHorizontal();

					// Нередактируемое имя ресурса (или ID, если имя пустое)
					GUI.enabled = false;
					string label = string.IsNullOrEmpty(nameProp.stringValue) ? idProp.stringValue : nameProp.stringValue;
					EditorGUILayout.TextField(label, GUILayout.MinWidth(140));
					GUI.enabled = true;

					GUI.color = oldColor;

					// Редактируемое текущее количество
					float value = current;
					value = EditorGUILayout.FloatField(value, GUILayout.Width(80));
					currentProp.floatValue = value;

					// Нередактируемый целевой запас
					GUI.enabled = false;
					EditorGUILayout.FloatField(target, GUILayout.Width(80));
					GUI.enabled = true;

					// Текущая цена (редактируемая, базовая не показывается отдельно)
					float currentPrice = currentPriceProp.floatValue;
					currentPrice = EditorGUILayout.FloatField(currentPrice, GUILayout.Width(80));
					currentPriceProp.floatValue = Mathf.Max(0f, currentPrice);

					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUI.indentLevel--;
		}

		serializedObject.ApplyModifiedProperties();
	}
}


