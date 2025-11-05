using EveOffline.Game;
using UnityEngine;
using UnityEngine.UI;

namespace EveOffline.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        private PauseManager pauseManager;
        private TimeScaleController timeScaleController;
        private Slider speedSlider;
        private bool sliderBound;

        private void Awake()
        {
            if (pauseManager == null)
            {
                pauseManager = Object.FindFirstObjectByType<PauseManager>(UnityEngine.FindObjectsInactive.Include);
                if (pauseManager == null)
                {
                    Debug.LogWarning("[PauseMenuController] PauseManager не найден.", this);
                }
            }

            if (timeScaleController == null)
            {
                timeScaleController = Object.FindFirstObjectByType<TimeScaleController>(UnityEngine.FindObjectsInactive.Include);
                if (timeScaleController == null)
                {
                    Debug.LogWarning("[PauseMenuController] TimeScaleController не найден.", this);
                }
            }
        }

        private void OnEnable() {}

        public void OnResume()
        {
            if (pauseManager != null)
            {
                pauseManager.SetPaused(false);
            }
        }

        public void OnSpeedChanged(float value)
        {
            if (timeScaleController != null)
            {
                timeScaleController.SetFromSlider(value);
            }
        }
    }
}


