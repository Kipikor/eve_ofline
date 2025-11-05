using EveOffline.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveOffline.Space
{
    public class SpaceController : MonoBehaviour
    {
        private PauseManager pauseManager;

        private void Awake()
        {
            if (pauseManager == null)
            {
                pauseManager = Object.FindFirstObjectByType<PauseManager>(FindObjectsInactive.Include);
            }
        }

        public void OnReturnToStation()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Station);
        }
    }
}


