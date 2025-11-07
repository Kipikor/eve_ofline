using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShowShipAndBlueBackground
{
	[MenuItem("EVE Offline/Показать корабль и вернуть синий фон камеры")] 
	public static void Execute()
	{
		// 1) Камера: стандартный синий фон и корректные 2D-настройки
		Camera cam = Camera.main;
		if (cam == null)
		{
			cam = Object.FindFirstObjectByType<Camera>();
		}
		if (cam != null)
		{
			Undo.RecordObject(cam, "Restore Blue Background");
			cam.orthographic = true;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color(0.192f, 0.301f, 0.474f); // стандартный unity blue
			cam.cullingMask = ~0; // Everything
			var t = cam.transform;
			Undo.RecordObject(t, "Move Camera for 2D");
			if (t.position.z > -0.1f) t.position = new Vector3(t.position.x, t.position.y, -10f);
		}
		else
		{
			Debug.LogWarning("Камера не найдена. Создай/выбери сцену с камерой.");
		}

		// 2) Корабль: гарантируем визуал
		var ship = Object.FindFirstObjectByType<EveOffline.Space.ShipController>();
		if (ship == null)
		{
			// Если контроллер не найден — создаём простой объект корабля
			var go = new GameObject("Ship");
			Undo.RegisterCreatedObjectUndo(go, "Create Ship");
			go.layer = 0; // Default
			var rb = go.AddComponent<Rigidbody2D>();
			rb.gravityScale = 0f;
			ship = go.AddComponent<EveOffline.Space.ShipController>();
			go.transform.position = Vector3.zero;
		}

		// Добавляем/проверяем визуал
		var srExisting = ship.GetComponent<SpriteRenderer>();
		if (srExisting == null)
		{
			var sr = ship.gameObject.AddComponent<SpriteRenderer>();
			sr.sprite = CreateColoredSprite(new Color(0.9f, 0.65f, 0.1f)); // яркий кирпич
			sr.sortingOrder = 0;
			ship.transform.localScale = new Vector3(2f, 1f, 1f);
		}

		// 3) Позиция в кадре
		if (cam != null)
		{
			ship.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
		}

		Debug.Log("Корабль показан, фон — стандартный синий. Если не видно — проверь сцену/слои/зум.");
	}

	private static Sprite CreateColoredSprite(Color color)
	{
		var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
		for (int y = 0; y < 2; y++)
		for (int x = 0; x < 2; x++)
			tex.SetPixel(x, y, color);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
	}
}


