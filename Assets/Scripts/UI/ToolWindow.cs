using UnityEngine;
using UnityEngine.Events;

namespace UI
{
	[DisallowMultipleComponent]
	public class ToolWindow : MonoBehaviour
	{
		[Tooltip("Событие состояния окна: true = открыто, false = закрыто")]
		public UnityEvent<bool> visibilityChanged = new UnityEvent<bool>();

		private void OnEnable()
		{
			visibilityChanged.Invoke(true);
		}

		private void OnDisable()
		{
			visibilityChanged.Invoke(false);
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


