using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Player;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Displays the current speed of a target plane in km/h on a TextMeshPro UI element.
    /// Attach to any GameObject in the scene; assign the plane and the text field in the inspector.
    /// </summary>
    public class SpeedHUD : MonoBehaviour
    {
        [Tooltip("The plane whose speed is shown.")]
        [SerializeField] private PlaneController plane;

        [Tooltip("The TextMeshPro UI text that displays the speed.")]
        [SerializeField] private TMP_Text speedText;

        [Tooltip("Multiplier applied to raw speed before display. Game units are arbitrary; ~3.6 makes 'fast' arcade speeds read like km/h.")]
        [SerializeField] private float displayMultiplier = 3.6f;

        [Tooltip("How smoothly the displayed value catches up to the actual speed. Higher = snappier.")]
        [SerializeField] private float smoothing = 8f;

        private float _displayedValue;

        private void Update()
        {
            if (plane == null || speedText == null) return;

            float target = plane.State.currentSpeed * displayMultiplier;
            _displayedValue = Mathf.Lerp(_displayedValue, target, smoothing * Time.deltaTime);

            speedText.text = $"{Mathf.RoundToInt(_displayedValue)} KM/H";
        }
    }
}
