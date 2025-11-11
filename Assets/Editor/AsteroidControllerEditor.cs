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

			var controller = (AsteroidController)target;

			// Статистика (только чтение, кроме текущего HP)
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Статистика", EditorStyles.boldLabel);
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.FloatField("Диаметр (м)", controller.Diameter);
			EditorGUILayout.IntField("Площадь сечения (м²)", controller.GetAreaRounded());
			EditorGUILayout.IntField("Объём (м³)", controller.GetVolumeRounded());
			EditorGUILayout.FloatField("Плотность (из ore)", controller.OreDensity);
			{
				float k2 = controller.HpFromM2;
				if (k2 > 0f)
					EditorGUILayout.FloatField("HP/м² (из asteroid)", k2);
				else
					EditorGUILayout.FloatField("HP/м³ (из asteroid, legacy)", controller.HpFromM3);
			}
			EditorGUILayout.IntField("Масса (кг)", controller.GetMassRounded());
			EditorGUILayout.IntField("HP максимум", controller.GetMaxHitPoints());
			EditorGUI.EndDisabledGroup();

			// Текущее HP (редактируемое)
			EditorGUILayout.Space(2);
			var currentHpProp = serializedObject.FindProperty("currentHitPoints");
			if (currentHpProp != null)
			{
				int newHp = EditorGUILayout.IntField("HP текущее", currentHpProp.intValue);
				newHp = Mathf.Clamp(newHp, 0, controller.GetMaxHitPoints());
				if (newHp != currentHpProp.intValue)
				{
					Undo.RecordObject(controller, "Изменение текущего HP");
					currentHpProp.intValue = newHp;
					EditorUtility.SetDirty(controller);
				}
			}

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

			EditorGUILayout.Space(6);
			if (GUILayout.Button("Разрушить (тест)"))
			{
				Undo.RecordObject(controller, "Разрушение астероида (тест)");
				controller.ApplyDamage(controller.GetMaxHitPoints());
				EditorUtility.SetDirty(controller);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}


