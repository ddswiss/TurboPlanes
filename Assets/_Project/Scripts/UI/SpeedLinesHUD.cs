using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Player;
using SkyBrawl.Tuning;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Drives the SpeedLines UI shader's _Intensity property based on the plane's
    /// current speed, scaled into the cruise→max range. Auto-finds the active
    /// plane if not assigned. The shader handles its own time-based shimmer; this
    /// component only changes how loud the effect is.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpeedLinesHUD : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Fullscreen UI Image whose material uses TurboPlanes/UI/SpeedLines.")]
        [SerializeField] private Image image;

        [Tooltip("Plane to read speed from. Auto-found at runtime if left empty.")]
        [SerializeField] private PlaneController plane;

        [Tooltip("Tuning ScriptableObject. Drag any SpeedLinesTuning_* asset.")]
        [SerializeField] private SpeedLinesTuningSO tuning;

        [Header("Toggle")]
        [Tooltip("Master enable. Toggle off in inspector or via SetEffectEnabled to kill the effect entirely (e.g. for testing).")]
        [SerializeField] private bool effectEnabled = true;

        [Header("Boost hook")]
        [Tooltip("External multiplier on top of the speed-driven intensity. Defaults to 1.0; bump to 1.5+ from a future boost system to make the lines bloom even harder during boost.")]
        public float boostMultiplier = 1.0f;

        private Material _matInstance;
        private static readonly int IntensityID   = Shader.PropertyToID("_Intensity");
        private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");

        private float _displayedIntensity;
        private float _intensityVelocity;
        private float _retryTimer;

        public bool EffectEnabled
        {
            get => effectEnabled;
            set => SetEffectEnabled(value);
        }

        public void SetEffectEnabled(bool on)
        {
            effectEnabled = on;
            if (!on && _matInstance != null)
            {
                _displayedIntensity = 0f;
                _intensityVelocity  = 0f;
                _matInstance.SetFloat(IntensityID, 0f);
            }
        }

        private void Awake()
        {
            if (image == null) return;
            // Clone the material so runtime changes never dirty the shared asset.
            if (image.material != null)
            {
                _matInstance = new Material(image.material);
                image.material = _matInstance;
            }
        }

        private void OnEnable()
        {
            // Reset so we don't flash last-session intensity for a frame.
            _displayedIntensity = 0f;
            _intensityVelocity  = 0f;
            if (_matInstance != null) _matInstance.SetFloat(IntensityID, 0f);
        }

        private void Update()
        {
            if (_matInstance == null) return;

            if (!effectEnabled)
            {
                if (_displayedIntensity != 0f)
                {
                    _displayedIntensity = 0f;
                    _matInstance.SetFloat(IntensityID, 0f);
                }
                return;
            }

            if (plane == null)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    plane = FindFirstObjectByType<PlaneController>();
                    _retryTimer = 0.5f;
                }
                if (plane == null) return;
            }

            // Aspect can change on window resize.
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 1.7777f;
            _matInstance.SetFloat(AspectRatioID, aspect);

            float cruise = plane.Tuning != null ? plane.Tuning.cruiseSpeed : 1f;
            float top    = plane.Tuning != null ? plane.Tuning.maxSpeed    : cruise;
            float range  = Mathf.Max(0.0001f, top - cruise);

            // 0 at cruise, 1 at top speed.
            float over = Mathf.Clamp01((plane.State.currentSpeed - cruise) / range);

            float maxIntensity = tuning != null ? tuning.maxIntensity : 0.85f;
            float threshold    = tuning != null ? tuning.threshold    : 0.05f;
            float ramp         = tuning != null ? tuning.ramp         : 1f;
            float smoothTime   = tuning != null ? tuning.smoothTime   : 0.2f;

            float target;
            if (over <= threshold)
            {
                target = 0f;
            }
            else
            {
                float n = (over - threshold) / Mathf.Max(0.0001f, 1f - threshold);
                target = Mathf.Pow(Mathf.Clamp01(n), ramp) * maxIntensity * Mathf.Max(0f, boostMultiplier);
            }

            // Smoothed approach with explicit time constant — independent of frame rate.
            _displayedIntensity = Mathf.SmoothDamp(_displayedIntensity, target, ref _intensityVelocity, smoothTime);
            _matInstance.SetFloat(IntensityID, _displayedIntensity);
        }

        private void OnDestroy()
        {
            if (_matInstance != null) Destroy(_matInstance);
        }
    }
}
