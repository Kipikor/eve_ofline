using UnityEngine;
using UnityEngine.InputSystem;

namespace EveOffline.Game
{
    public class PauseManager : MonoBehaviour
    {
        private GameObject pauseUiRoot;
        private TimeScaleController timeScaleController;

        private float lastNonZeroScale = 1f;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            SetPaused(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            bool isPaused = Time.timeScale <= 0.0001f;
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            // Обновляем ссылки на случай, если UI добавили/переименовали позже
            EnsureReferences();

            if (paused)
            {
                lastNonZeroScale = Mathf.Max(timeScaleController != null ? timeScaleController.TargetTimeScale : Time.timeScale, 1f);
                if (timeScaleController != null)
                {
                    timeScaleController.TargetTimeScale = 0f;
                }
                else
                {
                    Time.timeScale = 0f;
                }
            }
            else
            {
                if (timeScaleController != null)
                {
                    timeScaleController.TargetTimeScale = lastNonZeroScale;
                }
                else
                {
                    Time.timeScale = lastNonZeroScale;
                }
            }

            if (pauseUiRoot != null)
            {
                pauseUiRoot.SetActive(paused);
            }
            else if (paused)
            {
                Debug.LogWarning("[PauseManager] PauseUI не найден при попытке показать меню паузы. Убедитесь, что корневой объект UI называется 'PauseUI' или на нём висит PauseMenuController.", this);
            }
        }

        public void OnResumeButton()
        {
            SetPaused(false);
        }

        private void EnsureReferences()
        {
            if (timeScaleController == null)
            {
                timeScaleController = GetComponent<TimeScaleController>();
                if (timeScaleController == null)
                {
                    timeScaleController = Object.FindFirstObjectByType<TimeScaleController>(FindObjectsInactive.Include);
                    if (timeScaleController == null)
                    {
                        // предупреждение ниже лишь если реально потребуется в SetPaused
                    }
                }
            }

            if (pauseUiRoot == null)
            {
                // Пытаемся найти UI паузы по контроллеру меню или имени
                var pmc = Object.FindFirstObjectByType<EveOffline.UI.PauseMenuController>(FindObjectsInactive.Include);
                if (pmc != null)
                {
                    pauseUiRoot = pmc.gameObject;
                }
                else
                {
                    var go = GameObject.Find("PauseUI");
                    if (go != null)
                    {
                        pauseUiRoot = go;
                    }
                }
            }
        }
    }
}


