using System;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// Менеджер времени для симуляции планет.
	/// Работает отдельно от основного геймплея и не требует ручной привязки в сцене.
	/// </summary>
	[DisallowMultipleComponent]
	public class PlanetTimeManager : MonoBehaviour
	{
		private static PlanetTimeManager _instance;

		/// <summary>
		/// Глобальный доступ к менеджеру времени. При первом обращении создаёт объект автоматически.
		/// </summary>
		public static PlanetTimeManager Instance
		{
			get
			{
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

		[Header("Скорость симуляции")]
		[Tooltip("Множитель скорости симуляции. 1 = в реальном времени, >1 = ускорение.")]
		[SerializeField] private float simulationSpeed = 1f;

		/// <summary>Текущее «внутриигровое» время в днях.</summary>
		public float ElapsedDays { get; private set; }

		/// <summary>Событие тика симуляции (передаётся дельта в днях).</summary>
		public event Action<float> OnTick;

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

			if (simulationSpeed <= 0f)
			{
				simulationSpeed = 1f;
			}
		}

		private void Update()
		{
			// Простая заглушка: считаем, что 1 реальная секунда = 1 внутриигровой час
			// 24 часа = 1 день
			float deltaDays = (Time.deltaTime * simulationSpeed) / (60f * 60f * 24f);
			if (deltaDays <= 0f)
			{
				return;
			}

			ElapsedDays += deltaDays;
			OnTick?.Invoke(deltaDays);
		}
	}
}


