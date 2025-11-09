using UnityEngine;
using UnityEngine.UI;

namespace UI
{
	[DisallowMultipleComponent]
	public class ToolToggleBinding : MonoBehaviour
	{
		[Tooltip("Окно, которым управляет эта кнопка/переключатель")]
		public ToolWindow targetWindow;

		[Header("Внешний вид при вкл/выкл")]
		public Graphic[] tintGraphics;
		public Color onColor = new Color(0.9f, 0.9f, 0.9f, 1f);
		public Color offColor = new Color(1f, 1f, 1f, 1f);

		private Toggle toggle;
		private Button button;
		private bool updating;

		private void Awake()
		{
			toggle = GetComponent<Toggle>();
			button = GetComponent<Button>();

			if (tintGraphics == null || tintGraphics.Length == 0)
			{
				var g = GetComponent<Graphic>();
				if (g != null) tintGraphics = new[] { g };
			}
		}

		private void OnEnable()
		{
			if (toggle != null) toggle.onValueChanged.AddListener(OnToggleChanged);
			if (button != null) button.onClick.AddListener(OnButtonClick);
			if (targetWindow != null) targetWindow.visibilityChanged.AddListener(OnWindowVisibilityChanged);
			SyncFromWindow();
			ApplyTint();
		}

		private void OnDisable()
		{
			if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
			if (button != null) button.onClick.RemoveListener(OnButtonClick);
			if (targetWindow != null) targetWindow.visibilityChanged.RemoveListener(OnWindowVisibilityChanged);
		}

		private void OnButtonClick()
		{
			if (targetWindow == null) return;
			// переключаем состояние окна
			targetWindow.Toggle();
			// визуально обновить подсветку
			ApplyTint();
		}

		private void OnToggleChanged(bool isOn)
		{
			if (updating) return;
			if (targetWindow != null)
			{
				if (isOn) targetWindow.Show();
				else targetWindow.Hide();
			}
			ApplyTint();
		}

		private void OnWindowVisibilityChanged(bool visible)
		{
			if (toggle != null)
			{
				updating = true;
				toggle.isOn = visible;
				updating = false;
			}
			ApplyTint();
		}

		private void SyncFromWindow()
		{
			if (targetWindow == null) return;
			if (toggle != null)
			{
				updating = true;
				toggle.isOn = targetWindow.gameObject.activeSelf;
				updating = false;
			}
		}

		private void ApplyTint()
		{
			if (tintGraphics == null) return;
			bool on = toggle != null ? toggle.isOn : (targetWindow != null && targetWindow.gameObject.activeSelf);
			var c = on ? onColor : offColor;
			foreach (var g in tintGraphics)
			{
				if (g == null) continue;
				g.color = c;
			}
		}
	}
}


