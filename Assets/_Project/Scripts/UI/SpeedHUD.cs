using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Player;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Displays the current speed of a target plane in km/h on a TextMeshPro UI element.
    /// If `plane` is unassigned, falls back to FindFirstObjectByType so the HUD works even
    /// when the plane is spawned at runtime (e.g. by GameManager.SpawnSelectedPlane).
    /// </summary>
    public class SpeedHUD : MonoBehaviour
    {
        [Tooltip("The plane whose speed is shown. Leave empty to auto-find the active plane in the scene.")]
        [SerializeField] private PlaneController plane;

        [Tooltip("The TextMeshPro UI text that displays the speed.")]
        [SerializeField] private TMP_Text speedText;

        [Tooltip("Multiplier applied to raw speed before display. Game units are arbitrary; ~3.6 makes 'fast' arcade speeds read like km/h.")]
        [SerializeField] private float displayMultiplier = 3.6f;

        [Tooltip("How smoothly the displayed value catches up to the actual speed. Higher = snappier.")]
        [SerializeField] private float smoothing = 8f;

        private float _displayedValue;
        private float _retryTimer;

        private void Update()
        {
            if (speedText == null) return;

            // Auto-find plane if not assigned. Retry every 0.5s while still null
            // (handles late-spawned planes after a scene transition).
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

            float target = plane.State.currentSpeed * displayMultiplier;
            _displayedValue = Mathf.Lerp(_displayedValue, target, smoothing * Time.deltaTime);

            speedText.text = $"{Mathf.RoundToInt(_displayedValue)} KM/H";
        }
    }
}
