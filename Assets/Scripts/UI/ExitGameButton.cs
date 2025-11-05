using UnityEngine;

namespace EveOffline.UI
{
    public class ExitGameButton : MonoBehaviour
    {
        public void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}


