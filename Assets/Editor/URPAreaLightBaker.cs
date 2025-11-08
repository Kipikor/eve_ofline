using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class URPAreaLightBaker
{
	[MenuItem("EVE Offline/URP: Сделать все Area Lights Baked (сцены сборки)")]
	public static void MakeAreaLightsBakedInBuildScenes()
	{
		var scenes = EditorBuildSettings.scenes;
		if (scenes == null || scenes.Length == 0)
		{
			Debug.LogWarning("Нет сцен в Build Settings.");
			return;
		}

		int changedTotal = 0;
		foreach (var s in scenes)
		{
			if (!s.enabled) continue;
			var path = s.path;
			if (string.IsNullOrEmpty(path)) continue;

			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			int changedInScene = MakeAreaLightsBakedInScene(scene);
			if (changedInScene > 0)
			{
				changedTotal += changedInScene;
				EditorSceneManager.MarkSceneDirty(scene);
				EditorSceneManager.SaveScene(scene);
			}
		}

		Debug.Log(changedTotal > 0
			? $"URP: Переведено Area Lights в Baked: {changedTotal}. Сцены сохранены."
			: "URP: Изменений не требовалось. Все Area Lights уже Baked или не найдены.");
	}

	private static int MakeAreaLightsBakedInScene(Scene scene)
	{
		int changed = 0;
		foreach (var root in scene.GetRootGameObjects())
		{
			var lights = root.GetComponentsInChildren<Light>(true);
			foreach (var l in lights)
			{
				if (l == null) continue;
				if (l.type == LightType.Rectangle || l.type == LightType.Disc)
				{
					if (l.lightmapBakeType != LightmapBakeType.Baked)
					{
						Undo.RecordObject(l, "Set Area Light to Baked");
						l.lightmapBakeType = LightmapBakeType.Baked;
						changed++;
					}
				}
			}
		}
		return changed;
	}
}






