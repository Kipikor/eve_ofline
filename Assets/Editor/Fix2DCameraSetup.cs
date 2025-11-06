using UnityEditor;
using UnityEngine;

public static class Fix2DCameraSetup
{
	[MenuItem("EVE Offline/Починить 2D камеру (ортографическая + тестовый кирпич)")]
	public static void FixCameraAndSpawnTest()
	{
		Camera cam = Camera.main;
		if (cam == null)
		{
			cam = Object.FindFirstObjectByType<Camera>();
		}

		if (cam == null)
		{
			Debug.LogError("Камера не найдена в сцене.");
			return;
		}

		Undo.RecordObject(cam, "Fix 2D Camera");
		cam.orthographic = true;
		cam.nearClipPlane = 0.01f;
		cam.farClipPlane = 1000f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
		cam.cullingMask = ~0; // Everything

		var camTransform = cam.transform;
		Undo.RecordObject(camTransform, "Fix 2D Camera Transform");
		if (camTransform.position.z > -0.1f)
		{
			camTransform.position = new Vector3(camTransform.position.x, camTransform.position.y, -10f);
		}

		// Создаём тестовый кирпич, если его ещё нет
		const string testName = "TestBrick_2D";
		var existing = GameObject.Find(testName);
		if (existing == null)
		{
			var go = new GameObject(testName);
			Undo.RegisterCreatedObjectUndo(go, "Create Test Brick");
			var sr = go.AddComponent<SpriteRenderer>();
			sr.sprite = CreateColoredSprite(new Color(0.1f, 0.8f, 0.9f));
			sr.sortingOrder = 0;
			go.transform.position = new Vector3(camTransform.position.x, camTransform.position.y, 0f);
			go.transform.localScale = new Vector3(3f, 1.5f, 1f);
		}

		Debug.Log("Камера настроена под 2D. Добавлен тестовый кирпич перед камерой. Если он виден — рендер работает.");
	}

	private static Sprite CreateColoredSprite(Color color)
	{
		var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Point
		};
		for (int y = 0; y < 2; y++)
		{
			for (int x = 0; x < 2; x++)
			{
				tex.SetPixel(x, y, color);
			}
		}
		tex.Apply();
		var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
		return sprite;
	}
}
