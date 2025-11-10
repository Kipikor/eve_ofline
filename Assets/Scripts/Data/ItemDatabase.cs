using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
	[CreateAssetMenu(fileName = "all_item", menuName = "Game/All Items", order = 1)]
	public class ItemDatabase : ScriptableObject
	{
		[Serializable]
		public class ItemRecord
		{
			public string id;
			public string name;
			public string iconKey;
			public float cargo; // м^3 на единицу
			public float cost;
			[TextArea] public string description;
			public Sprite iconSprite;
		}

		[SerializeField] private List<ItemRecord> items = new List<ItemRecord>();
		public IReadOnlyList<ItemRecord> Items => items;

		// редактор будет вызывать
		public void SetItems(List<ItemRecord> newItems)
		{
			items = newItems ?? new List<ItemRecord>();
		}
	}
}


