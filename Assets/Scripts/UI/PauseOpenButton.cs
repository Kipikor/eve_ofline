using EveOffline.Game;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EveOffline.UI
{
    public class PauseOpenButton : MonoBehaviour
    {
        private PauseManager pauseManager;

        public void Configure(PauseManager manager)
        {
            pauseManager = manager;
        }

        public void Open()
        {
            // Защита 1: если компонент висит на Slider или клик пришёл со слайдера — игнорируем
            if (GetComponent<Slider>() != null) return;
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (current != null && current.GetComponentInParent<Slider>() != null) return;

            // Защита 2: если это кнопка, но событие пришло не от этой кнопки — игнорируем
            var btn = GetComponent<Button>();
            if (btn != null && current != this.gameObject) return;
            if (pauseManager == null)
            {
                pauseManager = Object.FindFirstObjectByType<PauseManager>(FindObjectsInactive.Include);
            }
            if (pauseManager != null)
            {
                pauseManager.SetPaused(true);
            }
        }
    }
}


