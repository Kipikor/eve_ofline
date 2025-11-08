using UnityEditor;
using UnityEngine;
using Space.Weapons;
using System.Collections.Generic;

namespace EditorTools
{
	[CustomEditor(typeof(TurretMount))]
	public class TurretMountEditor : Editor
	{
		private string[] displayOptions;
		private string[] valueOptions;
		private int selectedIndex;
		private TurretPrefabRegistry registry;

		private void OnEnable()
		{
			ReloadOptions();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			var mount = (TurretMount)target;

			if (displayOptions == null) ReloadOptions();

			// Определяем текущий индекс
			string current = GetPrivateString(mount, "defaultTurretId");
			if (string.IsNullOrEmpty(current)) current = "";
			selectedIndex = 0;
			for (int i = 0; i < valueOptions.Length; i++)
			{
				if (string.Equals(valueOptions[i], current, System.StringComparison.Ordinal))
				{
					selectedIndex = i;
					break;
				}
			}

			EditorGUILayout.LabelField("Турели", EditorStyles.boldLabel);
			int newIndex = EditorGUILayout.Popup("Default Turret Id", selectedIndex, displayOptions);
			if (newIndex != selectedIndex)
			{
				selectedIndex = newIndex;
				string newValue = valueOptions[selectedIndex]; // "" = none
				Undo.RecordObject(mount, "Change Default Turret");
				SetPrivateString(mount, "defaultTurretId", newValue);
				EditorUtility.SetDirty(mount);
			}

			EditorGUILayout.HelpBox("Если выбран вариант (none), на точках без явного суффикса турель не ставится. Можно указать id в имени точки: turret_point:mining_turret_t1", MessageType.Info);

			serializedObject.ApplyModifiedProperties();
		}

		private void ReloadOptions()
		{
			registry = TurretPrefabRegistry.Load();
			var labels = new List<string>();
			var values = new List<string>();
			labels.Add("(none)");
			values.Add("");
			if (registry != null)
			{
				// Попробуем взять все id из реестра через скрытое поле entries
				var entriesField = typeof(TurretPrefabRegistry).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (entriesField != null)
				{
					var entries = entriesField.GetValue(registry) as System.Collections.IEnumerable;
					if (entries != null)
					{
						foreach (var e in entries)
						{
							var idField = e.GetType().GetField("turretId");
							var id = idField != null ? idField.GetValue(e) as string : null;
							if (!string.IsNullOrWhiteSpace(id))
							{
								labels.Add(id);
								values.Add(id);
							}
						}
					}
				}
			}
			displayOptions = labels.ToArray();
			valueOptions = values.ToArray();
		}

		private static string GetPrivateString(object obj, string fieldName)
		{
			var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			return f != null ? (f.GetValue(obj) as string ?? "") : "";
		}
		private static void SetPrivateString(object obj, string fieldName, string value)
		{
			var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (f != null) f.SetValue(obj, value);
		}
	}
}


