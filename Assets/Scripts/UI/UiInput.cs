using UnityEngine;

namespace UI
{
	/// <summary>
	/// Глобальный «шлюз» ввода для UI.
	/// Позволяет блокировать обработку событий мыши, когда открыты окна интерфейса.
	/// </summary>
	public static class UiInput
	{
		private static int mouseBlockCounter = 0;

		/// <summary>Возвращает true, если какой-либо UI заблокировал обработку мыши.</summary>
		public static bool IsMouseBlocked => mouseBlockCounter > 0;

		/// <summary>Включить блокировку ввода мыши (например, при открытии окна).</summary>
		public static void PushMouseBlock()
		{
			mouseBlockCounter++;
		}

		/// <summary>Снять блокировку ввода мыши (например, при закрытии окна).</summary>
		public static void PopMouseBlock()
		{
			if (mouseBlockCounter > 0) mouseBlockCounter--;
		}
	}
}


