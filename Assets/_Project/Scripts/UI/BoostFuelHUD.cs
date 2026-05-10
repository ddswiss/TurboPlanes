using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Player;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Drives a UI Image (Filled type) and an optional label to display the player plane's
    /// current boost fuel. Falls back to FindFirstObjectByType when no plane is assigned.
    /// </summary>
    public class BoostFuelHUD : MonoBehaviour
    {
        [Tooltip("The plane whose boost fuel is shown. Leave empty to auto-find at runtime.")]
        [SerializeField] private PlaneController plane;
        [Tooltip("UI Image (image type = Filled) whose fillAmount we drive 0..1.")]
        [SerializeField] private Image fillBar;
        [Tooltip("Optional color when fuel is full (BoostFuelRatio01 = 1).")]
        [SerializeField] private Color fullColor  = new Color(0.30f, 0.85f, 0.55f, 1f);
        [Tooltip("Optional color when fuel is empty (BoostFuelRatio01 = 0).")]
        [SerializeField] private Color emptyColor = new Color(0.85f, 0.30f, 0.20f, 1f);
        [SerializeField] private float smoothing = 10f;

        private float _displayedFill = 1f;
        private float _retryTimer;

        private void Update()
        {
            if (fillBar == null) return;

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

            float target = plane.BoostFuelRatio01;
            _displayedFill = Mathf.Lerp(_displayedFill, target, smoothing * Time.deltaTime);
            fillBar.fillAmount = _displayedFill;
            fillBar.color = Color.Lerp(emptyColor, fullColor, _displayedFill);
        }
    }
}
