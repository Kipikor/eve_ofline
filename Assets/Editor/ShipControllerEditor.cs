using System;
using System.Collections.Generic;
using EveOffline.Space;
using EveOffline.Space.Drone;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipController))]
public class ShipControllerEditor : Editor
{
	private string[] _shipIds;
	private static string[] s_droneIds = new string[0];

	private void OnEnable()
	{
		_shipIds = LoadShipIds();
		EnsureDroneIds();
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		// Ship Id popup
		var shipIdProp = serializedObject.FindProperty("shipId");
		int currentIndex = 0;
		if (_shipIds != null && _shipIds.Length > 0)
		{
			for (int i = 0; i < _shipIds.Length; i++)
			{
				if (string.Equals(_shipIds[i], shipIdProp.stringValue, StringComparison.OrdinalIgnoreCase))
				{
					currentIndex = i;
					break;
				}
			}
			int newIndex = EditorGUILayout.Popup("Ship Id", currentIndex, _shipIds);
			if (newIndex >= 0 && newIndex < _shipIds.Length)
			{
				shipIdProp.stringValue = _shipIds[newIndex];
			}
		}
		else
		{
			EditorGUILayout.PropertyField(shipIdProp, new GUIContent("Ship Id"));
		}

		// Остальные свойства кроме m_Script, shipId и drones (дроны рисуем кастомно ниже)
		DrawPropertiesExcluding(serializedObject, "m_Script", "shipId", "drones");

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Дроны", EditorStyles.boldLabel);
		var ship = (ShipController)target;
		var dronesProp = serializedObject.FindProperty("drones");
		if (dronesProp != null)
		{
			for (int i = 0; i < dronesProp.arraySize; i++)
			{
				var elem = dronesProp.GetArrayElementAtIndex(i);
				var idProp = elem.FindPropertyRelative("droneId");
				var countProp = elem.FindPropertyRelative("count");

				EditorGUILayout.BeginHorizontal();
				int current = Mathf.Max(0, IndexOfDrone(idProp.stringValue));
				int next = EditorGUILayout.Popup(current, s_droneIds);
				if (next != current)
				{
					idProp.stringValue = s_droneIds[next];
				}
				countProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(countProp.intValue, GUILayout.Width(80)));
				if (GUILayout.Button("X", GUILayout.Width(20)))
				{
					dronesProp.DeleteArrayElementAtIndex(i);
					EditorGUILayout.EndHorizontal();
					break;
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("+ Добавить"))
			{
				int idx = dronesProp.arraySize;
				dronesProp.InsertArrayElementAtIndex(idx);
				var elem = dronesProp.GetArrayElementAtIndex(idx);
				elem.FindPropertyRelative("droneId").stringValue = s_droneIds.Length > 0 ? s_droneIds[0] : "";
				elem.FindPropertyRelative("count").intValue = 1;
			}
			if (GUILayout.Button("Очистить"))
			{
				dronesProp.ClearArray();
			}
			EditorGUILayout.EndHorizontal();
		}

		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space();
		if (GUILayout.Button("Обновить список Ship Id"))
		{
			_shipIds = LoadShipIds();
		}
		if (GUILayout.Button("Обновить список Drone Id"))
		{
			EnsureDroneIds();
		}
		if (GUILayout.Button("Перечитать конфиг корабля"))
		{
			foreach (var t in targets)
			{
				var ctrl = t as ShipController;
				if (ctrl == null) continue;
				Undo.RecordObject(ctrl, "Reload Ship Config");
				ctrl.EditorReloadConfig();
				EditorUtility.SetDirty(ctrl);
			}
		}
		if (GUILayout.Button("Переспавнить дронов (сцена)"))
		{
			var m = typeof(ShipController).GetMethod("SpawnDronesFromLoadout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			if (m != null) m.Invoke(ship, null);
		}
	}

	private static string[] LoadShipIds()
	{
		// Пытаемся найти ship.json в проекте
		string[] guids = AssetDatabase.FindAssets("ship t:TextAsset");
		for (int gi = 0; gi < guids.Length; gi++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
			if (!path.EndsWith("ship.json", StringComparison.OrdinalIgnoreCase)) continue;
			var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (asset == null || string.IsNullOrWhiteSpace(asset.text)) continue;

			var ids = ParseIds(asset.text);
			if (ids != null && ids.Length > 0) return ids;
		}
		return Array.Empty<string>();
	}

	[Serializable]
	private class Wrapper<T> { public T[] items; }
	[Serializable]
	private class ShipIdRecord { public string id; }

	private static string[] ParseIds(string json)
	{
		if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
		string wrapped = "{\"items\":" + json + "}";
		var data = JsonUtility.FromJson<Wrapper<ShipIdRecord>>(wrapped);
		if (data?.items == null) return Array.Empty<string>();
		List<string> result = new List<string>();
		for (int i = 0; i < data.items.Length; i++)
		{
			string id = data.items[i]?.id;
			if (string.IsNullOrEmpty(id)) continue;
			if (!result.Contains(id)) result.Add(id);
		}
		return result.ToArray();
	}

	private static void EnsureDroneIds()
	{
		IReadOnlyList<string> ids = DroneDatabase.GetAllIds();
		if (ids == null) { s_droneIds = new string[0]; return; }
		var arr = new string[ids.Count];
		for (int i = 0; i < arr.Length; i++) arr[i] = ids[i];
		s_droneIds = arr;
	}

	private static int IndexOfDrone(string id)
	{
		for (int i = 0; i < s_droneIds.Length; i++) if (s_droneIds[i] == id) return i;
		return 0;
	}
}

