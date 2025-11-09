using UnityEngine;
using UnityEngine.Events;

namespace UI
{
	[DisallowMultipleComponent]
	public class ToolWindow : MonoBehaviour
	{
		[Tooltip("Событие состояния окна: true = открыто, false = закрыто")]
		public UnityEvent<bool> visibilityChanged = new UnityEvent<bool>();

		[Header("Input")]
		[Tooltip("Блокировать ли обработку мыши у игрока, пока окно открыто")]
		[SerializeField] private bool blockMouseInput = true;

		private void OnEnable()
		{
			visibilityChanged.Invoke(true);
			if (blockMouseInput) UiInput.PushMouseBlock();
		}

		private void OnDisable()
		{
			visibilityChanged.Invoke(false);
			if (blockMouseInput) UiInput.PopMouseBlock();
		}

		public void Show()
		{
			if (!gameObject.activeSelf) gameObject.SetActive(true);
		}

		public void Hide()
		{
			if (gameObject.activeSelf) gameObject.SetActive(false);
		}

		public void Toggle()
		{
			gameObject.SetActive(!gameObject.activeSelf);
		}
	}
}


