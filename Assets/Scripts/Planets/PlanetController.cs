using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Контроллер одной планеты в экономической симуляции.
	/// Пока это заглушка, позже сюда добавим загрузку конфигов и логику ресурсов.
	/// </summary>
	[DisallowMultipleComponent]
	public class PlanetController : MonoBehaviour
	{
		[Header("Идентификатор планеты")]
		[Tooltip("ID планеты из конфигов (planet.json / planet_type.json и др.).")]
		[SerializeField] private string planetId;

		/// <summary>ID планеты из конфигов.</summary>
		public string PlanetId => planetId;

		private void Awake()
		{
			// Гарантируем наличие менеджера времени и галактики без ручной привязки
			_ = PlanetTimeManager.Instance;
			_ = GalaxyManager.Instance;

			if (string.IsNullOrWhiteSpace(planetId))
			{
				Debug.LogWarning($"[PlanetController] У объекта '{name}' не задан planetId. Конфиг не сможет подцепиться.");
			}
			else
			{
				GalaxyManager.Instance.RegisterPlanet(this);
			}
		}

		private void OnDestroy()
		{
			if (!string.IsNullOrWhiteSpace(planetId) && GalaxyManager.HasInstance)
			{
				GalaxyManager.Instance.UnregisterPlanet(this);
			}
		}
	}
}


