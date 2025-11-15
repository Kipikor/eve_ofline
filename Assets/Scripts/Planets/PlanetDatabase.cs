using System;
using System.Collections.Generic;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// База данных планет для симуляции экономики.
	/// Заполняется из JSON-конфигов (planet.json и др.) через редакторскую кнопку.
	/// </summary>
	[CreateAssetMenu(fileName = "planet_database", menuName = "Game/Planets/Planet Database", order = 10)]
	public class PlanetDatabase : ScriptableObject
	{
		[Serializable]
		public class PlanetRecord
		{
			[Header("Базовая информация")]
			public string idPlanet;
			public string planetType;
			
			[Header("Визуал")]
			public float scale = 1f;
			public Color color = Color.white;
			public string colorHex; // исходная строка из JSON для наглядности

			[Header("Стартовые ресурсы")]
			[TextArea] public string startResourceRaw;

			[Header("Параметры типа планеты (из planet_type.json)")]
			[Tooltip("Типы слотов процессов, как в process_slot_type (например: Universal,Mining,Social,Industrial,Scientific).")]
			public string processSlotTypeRaw;
			
			[Tooltip("Кол-во слотов по типам, как в process_slot_count (например: 1,1,3,1,1).")]
			public string processSlotCountRaw;
			
			[Tooltip("Базовые штрафы по слотам, как в process_slot_base_penalty (например: 30%,30%,0%,40%,30%).")]
			public string processSlotBasePenaltyRaw;
			
			[Tooltip("Базовый доход в тик: base_income_tik.")]
			public string baseIncomeTikRaw;
			
			[Tooltip("Постоянные потребности need_anytime.")]
			public string needAnytimeRaw;
			
			[Tooltip("Базовое потребление в тик: base_consumption_tik.")]
			public string baseConsumptionTikRaw;
		}

		[SerializeField] private List<PlanetRecord> planets = new List<PlanetRecord>();

		/// <summary>Список всех планет из конфигов.</summary>
		public IReadOnlyList<PlanetRecord> Planets => planets;

		/// <summary>Редактор устанавливает новые записи сюда.</summary>
		public void SetPlanets(List<PlanetRecord> newPlanets)
		{
			planets = newPlanets ?? new List<PlanetRecord>();
		}
	}
}


