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


