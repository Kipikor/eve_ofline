using System;
using System.IO;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Менеджер времени для симуляции планет.
	/// Работает отдельными "тиками" с интервалом в секундах.
	/// Интервал по умолчанию берётся из Config/planet_const.json
	/// ("Тик происходит каждые сколько секунд?").
	/// </summary>
	[DisallowMultipleComponent]
	public class PlanetTimeManager : MonoBehaviour
	{
		private static PlanetTimeManager _instance;
		private static bool _applicationQuitting;

		private const string PlanetConstFileRelative = "Config/planet_const.json";
		private const string TickIntervalName = "Тик происходит каждые сколько секунд?";
		private const string MinResourceShareName = "Минимальная доля ресурсов";
		private const string CreditDecayName = "Естественная убыть PR_Credits со счёта каждый тик от доли существующих, касается всех кто может накапливать PR_Credits";
		private const string PopulationDecayName = "Естественная убыль населения на планетах каждый тик от всех имеющихся";
		private const string ReserveTicksAheadName = "На сколько тиков вперёд запасает ресурсов планета(целевой запас). Так же планеты не будет продавать ресурс если его текущий запас ниже целевого.";
		private const string ReservePenaltyTicksAheadName = "Штрафы за отсутствие запасов начинают начисляться если их меньше чем на сколько тиков вперёд";
		private const string PriceDecreasePerTickName = "На какое значение в тик планеты понижает свою цену при избытке товара(запасы текущие больше необходимых), относительно текущей цены планеты";
		private const string PriceIncreasePerTickName = "На какое значение в тик планета повышает свою цену при дефиите(запасы текущие ниже необходимыых) товара, относительно текущей цены планеты";
		private const string AveragePriceCollectTicksName = "Частота сбора среднегалактической цены, раз во сколько тиков. Берётся цена на каждый товар у каждой планеты, находиться средний и получается среднегалактическая цена.";

		[Serializable]
		public class ConstantsData
		{
			[InspectorName("Тик происходит каждые сколько секунд?")]
			[Tooltip("Тик происходит каждые сколько секунд?")]
			public float secondsPerTick = 4.1f;

			[InspectorName("Минимальная доля ресурсов")]
			[Tooltip("Минимальная доля ресурсов")]
			public float minResourceShare = 0.001f;

			[InspectorName("Естественная убыть PR_Credits со счёта каждый тик от доли существующих, касается всех кто может накапливать PR_Credits")]
			[Tooltip("Естественная убыть PR_Credits со счёта каждый тик от доли существующих, касается всех кто может накапливать PR_Credits")]
			public float creditDecayPerTick = 0.01f;

			[InspectorName("Естественная убыль населения на планетах каждый тик от всех имеющихся")]
			[Tooltip("Естественная убыль населения на планетах каждый тик от всех имеющихся")]
			public float populationDecayPerTick = 0.05f;

			[InspectorName("На сколько тиков вперёд запасает ресурсов планета(целевой запас). Так же планеты не будет продавать ресурс если его текущий запас ниже целевого.")]
			[Tooltip("На сколько тиков вперёд запасает ресурсов планета(целевой запас). Так же планеты не будет продавать ресурс если его текущий запас ниже целевого.")]
			public float reserveTicksAhead = 150f;

			[InspectorName("Штрафы за отсутствие запасов начинают начисляться если их меньше чем на сколько тиков вперёд")]
			[Tooltip("Штрафы за отсутствие запасов начинают начисляться если их меньше чем на сколько тиков вперёд")]
			public float reservePenaltyTicksAhead = 75f;

			[InspectorName("На какое значение в тик планеты понижает свою цену при избытке товара(запасы текущие больше необходимых), относительно текущей цены планеты")]
			[Tooltip("На какое значение в тик планеты понижает свою цену при избытке товара(запасы текущие больше необходимых), относительно текущей цены планеты")]
			public float priceDecreasePerTick = 0.01f;

			[InspectorName("На какое значение в тик планета повышает свою цену при дефиите(запасы текущие ниже необходимыых) товара, относительно текущей цены планеты")]
			[Tooltip("На какое значение в тик планета повышает свою цену при дефиите(запасы текущие ниже необходимыых) товара, относительно текущей цены планеты")]
			public float priceIncreasePerTick = 0.02f;

			[InspectorName("Частота сбора среднегалактической цены, раз во сколько тиков. Берётся цена на каждый товар у каждой планеты, находиться средний и получается среднегалактическая цена.")]
			[Tooltip("Частота сбора среднегалактической цены, раз во сколько тиков. Берётся цена на каждый товар у каждой планеты, находиться средний и получается среднегалактическая цена.")]
			public float averagePriceCollectTicks = 20f;
		}

		[SerializeField] private ConstantsData constants = new ConstantsData();
		public ConstantsData Constants => constants;

		private DateTime _constLastWriteTimeUtc;
		private float _constReloadTimer;

		/// <summary>
		/// Глобальный доступ к менеджеру времени. При первом обращении создаёт объект автоматически.
		/// </summary>
		public static PlanetTimeManager Instance
		{
			get
			{
				if (_applicationQuitting)
				{
					return null;
				}

				if (_instance == null)
				{
					_instance = FindFirstObjectByType<PlanetTimeManager>(FindObjectsInactive.Include);
					if (_instance == null)
					{
						var go = new GameObject("PlanetTimeManager");
						_instance = go.AddComponent<PlanetTimeManager>();
					}
				}

				return _instance;
			}
		}

		/// <summary>
		/// Возвращает уже существующий экземпляр, не создавая новый.
		/// Используется при отписке, чтобы не плодить объекты при закрытии сцены.
		/// </summary>
		public static PlanetTimeManager TryGetExistingInstance()
		{
			if (_instance != null) return _instance;
			if (_applicationQuitting) return null;

			_instance = FindFirstObjectByType<PlanetTimeManager>(FindObjectsInactive.Include);
			return _instance;
		}

		[Tooltip("Использовать реальное время (unscaled) или учитывать Time.timeScale.")]
		[SerializeField]
		private bool useUnscaledTime = false;

		/// <summary>Количество тиков, прошедших с момента запуска симуляции.</summary>
		public long ElapsedTicks { get; private set; }

		/// <summary>Событие тика симуляции (кол-во тиков за этот кадр, обычно 1 или 0).</summary>
		public event Action<uint> OnTick;

		private float _tickIntervalSeconds = 10f;
		private float _timeAccumulator;

		private void Awake()
		{
			// Синглтон без необходимости ручной настройки
			if (_instance != null && _instance != this)
			{
				Debug.LogWarning("[PlanetTimeManager] Обнаружен дублирующийся экземпляр, лишний будет уничтожен.");
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);

			// Подтягиваем константы из planet_const.json
			LoadConstantsFromConfig();
			RecalculateTickInterval();
		}

		private void OnApplicationQuit()
		{
			_applicationQuitting = true;
		}

		private void OnValidate()
		{
			if (constants != null && constants.secondsPerTick <= 0f)
			{
				constants.secondsPerTick = 0.0001f;
			}
			RecalculateTickInterval();
		}

		private void RecalculateTickInterval()
		{
			float s = (constants != null && constants.secondsPerTick > 0f)
				? constants.secondsPerTick
				: 1f;
			_tickIntervalSeconds = s > 0f ? s : float.PositiveInfinity;
		}

		private void Update()
		{
			UpdateConstantsFromConfigIfChanged();

			if (_tickIntervalSeconds <= 0f || float.IsInfinity(_tickIntervalSeconds))
				return;

			float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
			if (dt <= 0f) return;

			_timeAccumulator += dt;
			if (_timeAccumulator < _tickIntervalSeconds)
				return;

			uint ticksThisFrame = (uint)Mathf.FloorToInt(_timeAccumulator / _tickIntervalSeconds);
			if (ticksThisFrame == 0)
				return;

			_timeAccumulator -= ticksThisFrame * _tickIntervalSeconds;

			ElapsedTicks += ticksThisFrame;
			OnTick?.Invoke(ticksThisFrame);
		}

		private void LoadConstantsFromConfig()
		{
			// Значения по умолчанию уже заданы в сериализуемой структуре constants.
			try
			{
				string fullPath = Path.Combine(Application.dataPath, PlanetConstFileRelative);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[PlanetTimeManager] Не найден файл констант планет: Assets/{PlanetConstFileRelative}");
					return;
				}

				var lines = File.ReadAllLines(fullPath);
				_constLastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath);

				constants.secondsPerTick = TryReadFloatConst(lines, TickIntervalName, constants.secondsPerTick);
				constants.minResourceShare = TryReadFloatConst(lines, MinResourceShareName, constants.minResourceShare);
				constants.creditDecayPerTick = TryReadFloatConst(lines, CreditDecayName, constants.creditDecayPerTick);
				constants.populationDecayPerTick = TryReadFloatConst(lines, PopulationDecayName, constants.populationDecayPerTick);
				constants.reserveTicksAhead = TryReadFloatConst(lines, ReserveTicksAheadName, constants.reserveTicksAhead);
				constants.reservePenaltyTicksAhead = TryReadFloatConst(lines, ReservePenaltyTicksAheadName, constants.reservePenaltyTicksAhead);
				constants.priceDecreasePerTick = TryReadFloatConst(lines, PriceDecreasePerTickName, constants.priceDecreasePerTick);
				constants.priceIncreasePerTick = TryReadFloatConst(lines, PriceIncreasePerTickName, constants.priceIncreasePerTick);
				constants.averagePriceCollectTicks = TryReadFloatConst(lines, AveragePriceCollectTicksName, constants.averagePriceCollectTicks);
			}
			catch (Exception e)
			{
				Debug.LogError("[PlanetTimeManager] Ошибка чтения planet_const.json: " + e);
			}
		}

		private void UpdateConstantsFromConfigIfChanged()
		{
			_constReloadTimer += Time.unscaledDeltaTime;
			if (_constReloadTimer < 0.5f) return;
			_constReloadTimer = 0f;

			try
			{
				string fullPath = Path.Combine(Application.dataPath, PlanetConstFileRelative);
				if (!File.Exists(fullPath))
				{
					return;
				}

				var writeTime = File.GetLastWriteTimeUtc(fullPath);
				if (writeTime <= _constLastWriteTimeUtc) return;

				// Файл изменился — перечитываем константы
				LoadConstantsFromConfig();
				RecalculateTickInterval();
			}
			catch (Exception e)
			{
				Debug.LogError("[PlanetTimeManager] Ошибка обновления констант: " + e);
			}
		}

		private static float TryReadFloatConst(string[] lines, string name, float fallback)
		{
			if (lines == null || lines.Length == 0 || string.IsNullOrEmpty(name)) return fallback;

			for (int i = 0; i < lines.Length; i++)
			{
				if (!lines[i].Contains(name)) continue;

				for (int j = i + 1; j < Mathf.Min(lines.Length, i + 6); j++)
				{
					if (!lines[j].Contains("\"Значение\"")) continue;
					int colonIndex = lines[j].IndexOf(':');
					if (colonIndex < 0) continue;

					string raw = lines[j].Substring(colonIndex + 1);
					raw = raw.Replace(",", string.Empty);
					raw = raw.Replace("\"", string.Empty);
					raw = raw.Replace("}", string.Empty);
					raw = raw.Trim();

					if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
						    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
					{
						if (parsed >= 0f)
							return parsed;
						return fallback;
					}
				}
			}

			return fallback;
		}
	}
}


