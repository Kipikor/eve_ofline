using EveOffline.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveOffline.Station
{
    public class StationController : MonoBehaviour
    {
        public void OnLaunchToSpace()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Space);
        }

        public void OnBackToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.MainMenu);
        }
    }
}


