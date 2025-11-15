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

		[Header("Тики времени")]
		[Tooltip("Интервал между тиками в секундах. По умолчанию берётся из Config/planet_const.json (\"Тик происходит каждые сколько секунд?\").")]
		[SerializeField]
		private float secondsPerTick = 4.1f;

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

			// Подтягиваем интервал тиков из planet_const.json, если получается
			float configInterval;
			if (TryReadTickIntervalFromConfig(out configInterval) && configInterval > 0f)
			{
				secondsPerTick = configInterval;
			}
			else if (secondsPerTick <= 0f)
			{
				secondsPerTick = 1f;
			}

			RecalculateTickInterval();
		}

		private void OnApplicationQuit()
		{
			_applicationQuitting = true;
		}

		private void OnValidate()
		{
			if (secondsPerTick <= 0f)
			{
				secondsPerTick = 0.0001f;
			}
			RecalculateTickInterval();
		}

		private void RecalculateTickInterval()
		{
			_tickIntervalSeconds = secondsPerTick > 0f ? secondsPerTick : float.PositiveInfinity;
		}

		private void Update()
		{
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

		private static bool TryReadTickIntervalFromConfig(out float secondsPerTick)
		{
			secondsPerTick = 0f;

			try
			{
				string fullPath = Path.Combine(Application.dataPath, PlanetConstFileRelative);
				if (!File.Exists(fullPath))
				{
					Debug.LogWarning($"[PlanetTimeManager] Не найден файл констант планет: Assets/{PlanetConstFileRelative}");
					return false;
				}

				var lines = File.ReadAllLines(fullPath);
				for (int i = 0; i < lines.Length; i++)
				{
					if (!lines[i].Contains(TickIntervalName)) continue;

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

						if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secondsPerTick))
						{
							return true;
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[PlanetTimeManager] Ошибка чтения planet_const.json: " + e);
			}

			return false;
		}
	}
}


