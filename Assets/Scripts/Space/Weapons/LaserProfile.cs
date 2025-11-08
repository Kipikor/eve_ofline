using UnityEngine;

namespace Space.Weapons
{
	[CreateAssetMenu(fileName = "LaserProfile", menuName = "Space/Weapons/Laser Profile")]
	public class LaserProfile : ScriptableObject
	{
		[Header("Визуал")]
		public Color beamColor = Color.cyan;
		[Min(0.001f)] public float beamWidth = 0.06f;
		[Min(1f)] public float maxDistance = 25f;

		[Header("Урон")]
		[Min(0f)] public float damagePerSecond = 30f;

		[Header("Эффекты")]
		public GameObject hitEffectPrefab;
		[Min(0.05f)] public float hitEffectLifetime = 1.5f;
	}
}


