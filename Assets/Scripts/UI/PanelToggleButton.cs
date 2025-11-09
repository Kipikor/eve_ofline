using UnityEngine;
using UnityEngine.UI;

namespace UI
{
	/// <summary>
	/// Универсальный биндер для кнопки панели инструментов.
	/// - Перетащи сюда ссылку на окно (GameObject с ToolWindow) или сам ToolWindow.
	/// - Скрипт сам покажет/скроет окно по клику и подсветит кнопку как "включенную", когда окно открыто.
	/// Работает и с Button, и с Toggle (если на объекте есть Toggle, будет использован его isOn).
	/// </summary>
	[DisallowMultipleComponent]
	public class PanelToggleButton : MonoBehaviour
	{
		[Header("Назначение")]
		[SerializeField] private GameObject targetPanel;   // корневой объект окна (можно не задавать, если указан targetWindow)
		[SerializeField] private ToolWindow targetWindow;  // опционально: если не задан, возьмётся из targetPanel (или будет добавлен)

		[Header("Внешний вид")]
		[SerializeField] private Graphic[] tintGraphics;   // элементы, которым меняем цвет
		[SerializeField] private Color onColor = new Color(0.9f, 0.9f, 0.9f, 1f);
		[SerializeField] private Color offColor = Color.white;

		private Button button;
		private Toggle toggle;
		private bool syncing;

		private void Awake()
		{
			button = GetComponent<Button>();
			toggle = GetComponent<Toggle>(); // не добавляем автоматически, используем если уже есть

			if (targetWindow == null && targetPanel != null)
			{
				targetWindow = targetPanel.GetComponent<ToolWindow>();
				if (targetWindow == null)
				{
					// добавляем тонкий наблюдатель за открытием/закрытием
					targetWindow = targetPanel.AddComponent<ToolWindow>();
				}
			}

			if (tintGraphics == null || tintGraphics.Length == 0)
			{
				var g = GetComponent<Graphic>();
				if (g != null) tintGraphics = new[] { g };
			}
		}

		private void OnEnable()
		{
			if (button != null) button.onClick.AddListener(OnButtonClick);
			if (toggle != null) toggle.onValueChanged.AddListener(OnToggleChanged);
			if (targetWindow != null) targetWindow.visibilityChanged.AddListener(OnWindowVisibilityChanged);
			SyncFromWindow();
			ApplyTint();
		}

		private void OnDisable()
		{
			if (button != null) button.onClick.RemoveListener(OnButtonClick);
			if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
			if (targetWindow != null) targetWindow.visibilityChanged.RemoveListener(OnWindowVisibilityChanged);
		}

		private void OnButtonClick()
		{
			if (targetWindow == null) return;
			targetWindow.Toggle();
			// если есть Toggle — синхронизируем его состояние
			if (toggle != null)
			{
				syncing = true;
				toggle.isOn = targetWindow.gameObject.activeSelf;
				syncing = false;
			}
			ApplyTint();
		}

		private void OnToggleChanged(bool isOn)
		{
			if (syncing) return;
			if (targetWindow == null) return;
			if (isOn) targetWindow.Show(); else targetWindow.Hide();
			ApplyTint();
		}

		private void OnWindowVisibilityChanged(bool visible)
		{
			if (toggle != null)
			{
				syncing = true;
				toggle.isOn = visible;
				syncing = false;
			}
			ApplyTint();
		}

		private void SyncFromWindow()
		{
			if (targetWindow == null) return;
			if (toggle != null)
			{
				syncing = true;
				toggle.isOn = targetWindow.gameObject.activeSelf;
				syncing = false;
			}
		}

		private void ApplyTint()
		{
			if (tintGraphics == null || tintGraphics.Length == 0) return;
			bool on = targetWindow != null && targetWindow.gameObject.activeSelf;
			if (toggle != null) on = toggle.isOn;
			var c = on ? onColor : offColor;
			foreach (var g in tintGraphics)
			{
				if (g == null) continue;
				g.color = c;
			}
		}
	}
}


