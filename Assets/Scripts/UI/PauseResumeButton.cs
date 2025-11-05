using EveOffline.Game;
using UnityEngine;

namespace EveOffline.UI
{
    public class PauseResumeButton : MonoBehaviour
    {
        private PauseManager pauseManager;

        public void Configure(PauseManager manager)
        {
            pauseManager = manager;
        }

        public void Resume()
        {
            if (pauseManager == null)
            {
                pauseManager = Object.FindFirstObjectByType<PauseManager>(UnityEngine.FindObjectsInactive.Include);
            }
            if (pauseManager != null)
            {
                pauseManager.SetPaused(false);
            }
        }
    }
}


