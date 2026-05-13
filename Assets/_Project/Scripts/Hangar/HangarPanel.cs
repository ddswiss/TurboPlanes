using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Core;

namespace SkyBrawl.Hangar
{
    /// <summary>
    /// Hangar controller. Single-screen layout: plane navigator on the left (handled by
    /// PlaneNavigatorUI), upgrade sliders on the right (SpeedAdjusterUI instances), skill
    /// points readout in the header (SkillPointsHUD). Tab buttons are no longer used —
    /// both panels are visible simultaneously. The old tab fields are kept on the
    /// component so the prefab's serialized references don't break; they're just inert.
    /// </summary>
    public class HangarPanel : MonoBehaviour
    {
        [Header("Content panels (both shown at once now)")]
        [SerializeField] private GameObject planePanel;
        [SerializeField] private GameObject colorPanel;     // currently unused; hidden by default
        [SerializeField] private GameObject upgradePanel;

        [Header("Bottom buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;

        [Header("Deprecated (kept to preserve prefab refs)")]
        [SerializeField] private Button planeTabButton;
        [SerializeField] private Button colorTabButton;
        [SerializeField] private Button upgradeTabButton;
        [SerializeField] private Transform planeListContent;
        [SerializeField] private GameObject planeButtonTemplate;
        [SerializeField] private Transform colorGridContent;
        [SerializeField] private GameObject colorButtonTemplate;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => GameManager.Instance?.CloseHangar());
            if (quitButton  != null) quitButton.onClick.AddListener(() => GameManager.Instance?.QuitToMenu());
            // Tab buttons (if still wired in the prefab) become no-ops in the new layout.
            if (planeTabButton   != null) planeTabButton.gameObject.SetActive(false);
            if (colorTabButton   != null) colorTabButton.gameObject.SetActive(false);
            if (upgradeTabButton != null) upgradeTabButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Show plane + upgrade together; color panel stays hidden (out of scope for the new layout).
            if (planePanel   != null) planePanel.SetActive(true);
            if (colorPanel   != null) colorPanel.SetActive(false);
            if (upgradePanel != null) upgradePanel.SetActive(true);
        }
    }
}
