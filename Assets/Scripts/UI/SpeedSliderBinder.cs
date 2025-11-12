using EveOffline.Game;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EveOffline.UI
{
    public class SpeedSliderBinder : MonoBehaviour
    {
        private Slider slider;
        private TimeScaleController timeScaleController;
        private bool isBound;

        private void Awake()
        {
            EnsureRefs();
        }

        private void OnEnable()
        {
            EnsureRefs();
            BindIfNeeded();
            SyncFromController();
        }

        private void OnDisable()
        {
            if (slider != null && isBound)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
                isBound = false;
            }
        }

        private void Update()
        {
            // Если время изменилось внешне (например, пауза), подтягиваем значение на слайдер
            SyncFromController();

            // Блокируем управление слайдером с клавиатуры (WASD/стрелки) — убираем фокус при нажатии
            if (slider != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == slider.gameObject)
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                                   kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
#else
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
#endif
            }
        }

        private void EnsureRefs()
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
                if (slider != null)
                {
                    slider.minValue = 0f;
                    slider.maxValue = 5f;
                    // Не трогаем Whole Numbers — оставляем как выставлено в инспекторе

                    // Отключаем навигацию по клавиатуре для слайдера
                    var nav = slider.navigation;
                    nav.mode = Navigation.Mode.None;
                    slider.navigation = nav;
                }
                else
                {
                    Debug.LogWarning("[SpeedSliderBinder] На объекте не найден UI Slider.", this);
                }
            }

            if (timeScaleController == null)
            {
                timeScaleController = Object.FindFirstObjectByType<TimeScaleController>(FindObjectsInactive.Include);
                if (timeScaleController == null)
                {
                    Debug.LogWarning("[SpeedSliderBinder] TimeScaleController не найден в сцене.", this);
                }
            }
        }

        private void BindIfNeeded()
        {
            if (slider != null && timeScaleController != null && !isBound)
            {
                slider.onValueChanged.AddListener(OnSliderChanged);
                isBound = true;
            }
        }

        private void OnSliderChanged(float value)
        {
            if (timeScaleController != null)
            {
                timeScaleController.SetFromSlider(value);
            }
        }

        private void SyncFromController()
        {
            if (slider == null || timeScaleController == null)
            {
                return;
            }

            float current = timeScaleController != null ? timeScaleController.TargetTimeScale : 1f;
            if (Mathf.Abs(slider.value - current) > 0.0005f)
            {
                slider.SetValueWithoutNotify(current);
            }
        }
    }
}


