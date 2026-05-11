using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Core;

namespace SkyBrawl.UI
{
    public enum AdjusterTarget
    {
        Speed,
        BoostDrain
    }

    /// <summary>
    /// Generic stepped-slider UI (10 squares + plus/minus buttons) that reads/writes
    /// a single int field on PlayerProfile. Default value 5 maps to 100%, each step
    /// is +/-20% (range 1..10 -> 0.2x..2.0x multiplier).
    ///
    /// Used both on the MainMenu (Speed target) and on the Hangar's Upgrade tab
    /// (Speed and BoostDrain targets).
    /// </summary>
    public class SpeedAdjusterUI : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Image[] squares;          // 10 entries, index 0 = leftmost
        [SerializeField] private TMP_Text percentLabel;

        [Header("Config")]
        [Tooltip("Which PlayerProfile field this slider controls.")]
        [SerializeField] private AdjusterTarget target = AdjusterTarget.Speed;
        [Tooltip("Prefix for the percent label. E.g. 'Speed' -> 'Speed: 100%'.")]
        [SerializeField] private string labelPrefix = "Speed";

        [Header("Colors")]
        [SerializeField] private Color filledColor = new Color(0.95f, 0.85f, 0.20f, 1f);
        [SerializeField] private Color emptyColor  = new Color(1f, 1f, 1f, 0.18f);

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(() => Step(-1));
            if (plusButton  != null) plusButton.onClick.AddListener(() => Step(+1));
        }

        private void OnEnable() => Refresh();

        private int GetStage()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return 5;
            switch (target)
            {
                case AdjusterTarget.BoostDrain: return gm.Profile.boostDrainStage;
                case AdjusterTarget.Speed:
                default:                        return gm.Profile.speedStage;
            }
        }

        private void SetStage(int stage)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return;
            switch (target)
            {
                case AdjusterTarget.BoostDrain: gm.Profile.boostDrainStage = stage; break;
                case AdjusterTarget.Speed:      gm.Profile.speedStage      = stage; break;
            }
            gm.Profile.Save();
            // Apply immediately to the active plane so hangar tweaks take effect without
            // needing a respawn. (RefreshActivePlaneUpgrades is a no-op if no plane is spawned.)
            gm.RefreshActivePlaneUpgrades();
        }

        private void Step(int delta)
        {
            int s = Mathf.Clamp(GetStage() + delta, 1, 10);
            if (s == GetStage()) return;
            SetStage(s);
            Refresh();
        }

        private void Refresh()
        {
            int stage = Mathf.Clamp(GetStage(), 1, 10);

            if (squares != null)
            {
                for (int i = 0; i < squares.Length; i++)
                    if (squares[i] != null) squares[i].color = (i < stage) ? filledColor : emptyColor;
            }

            if (percentLabel != null)
            {
                float mult = 1f + (stage - 5) * 0.2f;
                percentLabel.text = labelPrefix + ": " + Mathf.RoundToInt(mult * 100f) + "%";
            }
        }
    }
}
