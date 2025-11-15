using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Контроллер транспорта между планетами.
	/// Пока заглушка: хранит связи по ID, позже сюда добавим расчёт маршрутов и логистику.
	/// </summary>
	[DisallowMultipleComponent]
	public class TransportController : MonoBehaviour
	{
		[Header("Идентификатор маршрута/транспорта")]
		[SerializeField] private string transportId;

		[Header("Связанные планеты (ID из конфигов)")]
		[SerializeField] private string fromPlanetId;
		[SerializeField] private string toPlanetId;

		public string TransportId => transportId;
		public string FromPlanetId => fromPlanetId;
		public string ToPlanetId => toPlanetId;

		private void Awake()
		{
			// Гарантируем наличие базовых менеджеров
			_ = PlanetTimeManager.Instance;
			_ = GalaxyManager.Instance;

			if (string.IsNullOrWhiteSpace(transportId))
			{
				Debug.LogWarning($"[TransportController] У объекта '{name}' не задан transportId. Будет сложно отладить этот маршрут.");
			}

			if (string.IsNullOrWhiteSpace(fromPlanetId) || string.IsNullOrWhiteSpace(toPlanetId))
			{
				Debug.LogWarning($"[TransportController] У объекта '{name}' не заполнены fromPlanetId/toPlanetId. Транспорт не знает, между чем и чем ездить.");
			}
		}
	}
}


