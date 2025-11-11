using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveOffline.Game
{
	public static class AutoBootstrap
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void OnAfterSceneLoad()
		{
			var scene = SceneManager.GetActiveScene();
			if (!scene.IsValid()) return;
			if (!string.Equals(scene.name, SceneNames.Space, System.StringComparison.Ordinal)) return;

			if (Object.FindFirstObjectByType<EveOffline.Industry.ProductionModuleRunner>(FindObjectsInactive.Exclude) != null) return;

			var go = new GameObject("ProductionModuleRunner");
			go.AddComponent<EveOffline.Industry.ProductionModuleRunner>();
		}
	}
}


