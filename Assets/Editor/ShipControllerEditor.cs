using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EveOffline.Space;

[CustomEditor(typeof(ShipController))]
public class ShipControllerEditor : Editor
{
	private string[] _shipIds;

	private void OnEnable()
	{
		_shipIds = LoadShipIds();
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		// Draw Ship Id as popup
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

		// Draw the rest excluding m_Script and shipId
		DrawPropertiesExcluding(serializedObject, "m_Script", "shipId");

		serializedObject.ApplyModifiedProperties();

		if (GUILayout.Button("Обновить список Ship Id"))
		{
			_shipIds = LoadShipIds();
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
}


