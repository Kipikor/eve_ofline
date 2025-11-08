using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Space;
using System.IO;

namespace EditorTools
{
	[CustomEditor(typeof(AsteroidManager))]
	public class AsteroidManagerEditor : Editor
	{
		private string[] sectorIds;
		private int selectedIndex;

		private void OnEnable()
		{
			ReloadSectors();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var manager = (AsteroidManager)target;

			EditorGUILayout.PropertyField(serializedObject.FindProperty("shipTransform"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraTransform"));

			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Параметры спавна (границы камеры)", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraSpawnMargin"), new GUIContent("Отступ спавна от границ камеры"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraDespawnMargin"), new GUIContent("Отступ отключения от границ камеры"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnAttemptsPerUpdate"), new GUIContent("Попыток спавна за кадр"));

			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Визуализация зон", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("drawCameraBounds"), new GUIContent("Рисовать границы камеры"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("drawSpawnBounds"), new GUIContent("Рисовать зону спавна"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("drawDespawnBounds"), new GUIContent("Рисовать зону отключения"));

			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Сектор", EditorStyles.boldLabel);
			if (sectorIds != null && sectorIds.Length > 0)
			{
				selectedIndex = Mathf.Max(0, System.Array.IndexOf(sectorIds, manager.CurrentSectorId));
				int newIndex = EditorGUILayout.Popup("Текущий сектор", selectedIndex, sectorIds);
				if (newIndex != selectedIndex)
				{
					Undo.RecordObject(manager, "Смена сектора");
					selectedIndex = newIndex;
					manager.CurrentSectorId = sectorIds[selectedIndex];
					EditorUtility.SetDirty(manager);
				}
			}
			else
			{
				EditorGUILayout.HelpBox("Не удалось прочитать список секторов из Config/sector.json", MessageType.Warning);
				if (GUILayout.Button("Перечитать сектора"))
				{
					ReloadSectors();
				}
			}

			EditorGUILayout.Space(6);
			if (GUILayout.Button("Rebuild Prefab Registry"))
			{
				AsteroidRegistryBuilder.Rebuild();
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void ReloadSectors()
		{
			try
			{
				var full = Path.Combine(Application.dataPath, "Config/sector.json");
				if (!File.Exists(full))
				{
					sectorIds = new string[0];
					return;
				}
				var json = File.ReadAllText(full);
				var wrapped = "{ \"items\": " + json + " }";
				var container = JsonUtility.FromJson<SectorContainerEditor>(wrapped);
				if (container == null || container.items == null)
				{
					sectorIds = new string[0];
					return;
				}
				var list = new List<string>();
				foreach (var e in container.items)
				{
					if (e != null && !string.IsNullOrEmpty(e.sector_id))
					{
						list.Add(e.sector_id);
					}
				}
				sectorIds = list.ToArray();
			}
			catch
			{
				sectorIds = new string[0];
			}
		}

		[System.Serializable]
		private class SectorEntryEditor
		{
			public string sector_id;
		}
		[System.Serializable]
		private class SectorContainerEditor
		{
			public List<SectorEntryEditor> items;
		}
	}
}


