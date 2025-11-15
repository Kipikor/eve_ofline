using System;
using System.Collections.Generic;
using UnityEngine;

namespace EveOffline.Planets
{
	/// <summary>
	/// База данных ресурсов для планетарной экономики.
	/// Заполняется из planet_resource.json через редактор.
	/// </summary>
	[CreateAssetMenu(fileName = "planet_resource_database", menuName = "Game/Planets/Planet Resource Database", order = 11)]
	public class PlanetResourceDatabase : ScriptableObject
	{
		[Serializable]
		public class ResourceRecord
		{
			public string resourceId;
			public string resourceName;
			public float baseCost;
		}

		[SerializeField] private List<ResourceRecord> resources = new List<ResourceRecord>();

		/// <summary>Все ресурсы из конфигов.</summary>
		public IReadOnlyList<ResourceRecord> Resources => resources;

		/// <summary>Редактор устанавливает сюда новые записи.</summary>
		public void SetResources(List<ResourceRecord> newResources)
		{
			resources = newResources ?? new List<ResourceRecord>();
		}
	}
}


