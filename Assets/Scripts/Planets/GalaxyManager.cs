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

		[System.Serializable]
		public class GalacticPriceEntry
		{
			public string resourceId;
			public string resourceName;
			public float lastPrice;
			public float currentPrice;
		}

		[Header("Среднегалактические цены")]
		[SerializeField] private List<GalacticPriceEntry> galacticPrices = new List<GalacticPriceEntry>();
		public IReadOnlyList<GalacticPriceEntry> GalacticPrices => galacticPrices;

		private float _ticksSinceLastPriceCollect;

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

			// Подписываемся на тики планетарного времени
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null)
			{
				tm.OnTick += OnTimeTicks;
			}
		}

		private void OnDestroy()
		{
			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm != null)
			{
				tm.OnTick -= OnTimeTicks;
			}
		}

		private void OnTimeTicks(uint tickCount)
		{
			if (tickCount == 0) return;

			var tm = PlanetTimeManager.TryGetExistingInstance();
			if (tm == null || tm.Constants == null) return;

			// Частота сбора из конфига:
			// "Частота сбора среднегалактической цены, раз во сколько тиков."
			float period = Mathf.Max(1f, tm.Constants.averagePriceCollectTicks);

			_ticksSinceLastPriceCollect += tickCount;

			if (_ticksSinceLastPriceCollect >= period)
			{
				_ticksSinceLastPriceCollect = 0f;
				RecalculateGalacticPrices();
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

		private void RecalculateGalacticPrices()
		{
			// 1. Собираем данные о ценах со всех планет
			var sums = new Dictionary<string, (float sum, int count)>(System.StringComparer.Ordinal);
			for (int i = 0; i < _planets.Count; i++)
			{
				var planet = _planets[i];
				if (planet == null) continue;
				var resources = planet.Resources;
				if (resources == null) continue;

				for (int r = 0; r < resources.Count; r++)
				{
					var res = resources[r];
					if (res == null || string.IsNullOrEmpty(res.resourceId)) continue;
					if (res.currentPrice <= 0f) continue;

					if (!sums.TryGetValue(res.resourceId, out var acc))
					{
						acc = (0f, 0);
					}
					acc.sum += res.currentPrice;
					acc.count += 1;
					sums[res.resourceId] = acc;
				}
			}

			// 2. Загружаем список всех ресурсов, чтобы иметь человеческие имена
			var resDb = Resources.Load<PlanetResourceDatabase>("planet_resource_database");
			var idToEntry = new Dictionary<string, GalacticPriceEntry>(System.StringComparer.Ordinal);
			for (int i = 0; i < galacticPrices.Count; i++)
			{
				var e = galacticPrices[i];
				if (e == null || string.IsNullOrEmpty(e.resourceId)) continue;
				idToEntry[e.resourceId] = e;
			}

			var newList = new List<GalacticPriceEntry>();
			if (resDb != null && resDb.Resources != null)
			{
				for (int i = 0; i < resDb.Resources.Count; i++)
				{
					var def = resDb.Resources[i];
					if (def == null || string.IsNullOrEmpty(def.resourceId)) continue;

					if (!idToEntry.TryGetValue(def.resourceId, out var entry) || entry == null)
					{
						entry = new GalacticPriceEntry
						{
							resourceId = def.resourceId,
							resourceName = def.resourceName,
							lastPrice = def.baseCost,
							currentPrice = def.baseCost
						};
					}

					if (string.Equals(def.resourceId, "PR_Credits", System.StringComparison.Ordinal))
					{
						// Кредиты: галактическая цена всегда базовая
						entry.lastPrice = def.baseCost;
						entry.currentPrice = def.baseCost;
					}
					else if (sums.TryGetValue(def.resourceId, out var acc) && acc.count > 0)
					{
						float sampleAvg = acc.sum / acc.count;
						float prev = entry.currentPrice > 0f ? entry.currentPrice : entry.lastPrice;
						float newAvg = prev > 0f ? (prev + sampleAvg) * 0.5f : sampleAvg;

						// Округляем по тому же шагу, что и цены планет
						var tm = PlanetTimeManager.TryGetExistingInstance();
						float step = (tm != null && tm.Constants != null && tm.Constants.minResourceShare > 0f)
							? tm.Constants.minResourceShare
							: 0.001f;
						newAvg = Mathf.Round(newAvg / step) * step;

						entry.lastPrice = entry.currentPrice;
						entry.currentPrice = Mathf.Max(0f, newAvg);
					}

					newList.Add(entry);
				}
			}

			galacticPrices = newList;

			// 3. После обновления среднегалактических цен сразу подрежем цены на планетах до допустимого диапазона
			ApplyPriceBoundsToPlanets();
		}

		private void ApplyPriceBoundsToPlanets()
		{
			if (_planets == null || _planets.Count == 0) return;
			if (galacticPrices == null || galacticPrices.Count == 0) return;

			for (int i = 0; i < _planets.Count; i++)
			{
				var planet = _planets[i];
				if (planet == null || planet.Resources == null) continue;

				var resList = planet.Resources;
				for (int r = 0; r < resList.Count; r++)
				{
					var rs = resList[r];
					if (rs == null || string.IsNullOrEmpty(rs.resourceId)) continue;

					rs.currentPrice = PlanetController.ClampPrice(rs.resourceId, rs.currentPrice);
				}
			}
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


