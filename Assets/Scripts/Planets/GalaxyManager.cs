using System.Collections.Generic;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Менеджер галактики: хранит список планет и будет управлять их взаимодействиями.
	/// Сейчас это заглушка с автоматической регистрацией планет.
	/// </summary>
	[DisallowMultipleComponent]
	public class GalaxyManager : MonoBehaviour
	{
		private static GalaxyManager _instance;

		[Header("Параметры галактики")]
		[SerializeField, Min(0f)] private float galaxyRadiusUnityMeters = 500f;

		/// <summary>Радиус галактики в юнити-метрах (берётся из planet_const.json при генерации).</summary>
		public float GalaxyRadiusUnityMeters => galaxyRadiusUnityMeters;

		/// <summary>Есть ли уже созданный экземпляр без его авто-создания.</summary>
		public static bool HasInstance => _instance != null;

		/// <summary>Глобальный доступ к менеджеру галактики. При первом обращении создаёт объект автоматически.</summary>
		public static GalaxyManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = FindFirstObjectByType<GalaxyManager>(FindObjectsInactive.Include);
					if (_instance == null)
					{
						var go = new GameObject("GalaxyManager");
						_instance = go.AddComponent<GalaxyManager>();
					}
				}

				return _instance;
			}
		}

		private readonly List<PlanetController> _planets = new List<PlanetController>();

		/// <summary>Список всех зарегистрированных планет в текущей сессии.</summary>
		public IReadOnlyList<PlanetController> Planets => _planets;

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Debug.LogWarning("[GalaxyManager] Обнаружен дублирующийся экземпляр, лишний будет уничтожен.");
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);

			// На старте автоматически подхватываем уже существующие планеты в сцене
			var existingPlanets = FindObjectsByType<PlanetController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < existingPlanets.Length; i++)
			{
				RegisterPlanet(existingPlanets[i]);
			}
		}

		/// <summary>Регистрирует планету в менеджере галактики (если ещё не зарегистрирована).</summary>
		public void RegisterPlanet(PlanetController planet)
		{
			if (planet == null) return;
			if (_planets.Contains(planet)) return;

			_planets.Add(planet);
		}

		/// <summary>Убирает планету из менеджера галактики.</summary>
		public void UnregisterPlanet(PlanetController planet)
		{
			if (planet == null) return;
			_planets.Remove(planet);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Редакторский метод для установки радиуса галактики из конфигов.
		/// </summary>
		public void EditorSetGalaxyRadius(float radius)
		{
			galaxyRadiusUnityMeters = Mathf.Max(0f, radius);
		}
#endif
	}
}


