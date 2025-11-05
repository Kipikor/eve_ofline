using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EveOffline.Space
{
    public class SpaceTimer : MonoBehaviour
    {
        private Text uiText;
        private TMP_Text tmpText;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool useUnscaledTime = false;

        private bool isRunning;
        private float elapsedSeconds;

        private void OnEnable()
        {
            if (autoStart)
            {
                isRunning = true;
            }
            UpdateLabel();
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsedSeconds += delta;
            if (elapsedSeconds < 0f)
            {
                elapsedSeconds = 0f;
            }
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            int totalMilliseconds = Mathf.FloorToInt(elapsedSeconds * 1000f);
            if (totalMilliseconds < 0) totalMilliseconds = 0;
            int seconds = totalMilliseconds / 1000;
            int milliseconds = totalMilliseconds % 1000;

            string formatted = $"{seconds:00}:{milliseconds:000}";

            if (tmpText == null)
            {
                tmpText = GetComponent<TMP_Text>();
            }
            if (uiText == null)
            {
                uiText = GetComponent<Text>();
            }

            if (tmpText != null)
            {
                tmpText.text = formatted;
            }
            else if (uiText != null)
            {
                uiText.text = formatted;
            }
        }

        public void ResetTimer()
        {
            elapsedSeconds = 0f;
            UpdateLabel();
        }

        public void StartTimer()
        {
            isRunning = true;
        }

        public void StopTimer()
        {
            isRunning = false;
        }

        public void SetRunning(bool running)
        {
            isRunning = running;
        }

        public void SetUseUnscaled(bool useUnscaled)
        {
            useUnscaledTime = useUnscaled;
        }
    }
}


