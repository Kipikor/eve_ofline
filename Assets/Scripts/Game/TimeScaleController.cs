using UnityEngine;

namespace EveOffline.Game
{
    public class TimeScaleController : MonoBehaviour
    {
        [Range(0f, 5f)]
        [SerializeField] private float targetTimeScale = 1f;

        private const float BaseFixedDeltaTime = 0.02f;

        public float TargetTimeScale
        {
            get => targetTimeScale;
            set
            {
                targetTimeScale = Mathf.Clamp(value, 0f, 5f);
                Apply();
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            Time.timeScale = targetTimeScale;
            Time.fixedDeltaTime = BaseFixedDeltaTime * Mathf.Max(targetTimeScale, 0.0001f);
        }

        public void SetFromSlider(float value)
        {
            TargetTimeScale = value;
        }
    }
}


