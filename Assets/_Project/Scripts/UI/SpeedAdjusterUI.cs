using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Core;

namespace SkyBrawl.UI
{
    public enum AdjusterTarget
    {
        Speed,           // PlayerProfile.speedStage          — cruise = base * (1 + 0.10*stage)
        BoostDuration,   // PlayerProfile.boostDurationStage  — boost lasts 2 + stage seconds (stage 0..8)
        Boost,           // PlayerProfile.boostStage          — max = cruise + 30 + 6*stage game units
        BoostRefill      // PlayerProfile.boostRefillStage    — refill in 5 - 0.5*stage seconds (stage 0..5)
    }

    /// <summary>
    /// Stepped-slider UI (squares + plus/minus) that reads/writes one int field on
    /// PlayerProfile. Range is per-target via the maxStage SerializeField. Default 0
    /// = no upgrade. Each step adds the upgrade effect described in AdjusterTarget.
    /// </summary>
    public class SpeedAdjusterUI : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Image[] squares;
        [SerializeField] private TMP_Text percentLabel;

        [Header("Config")]
        [Tooltip("Which PlayerProfile field this slider controls.")]
        [SerializeField] private AdjusterTarget target = AdjusterTarget.Speed;
        [Tooltip("Prefix for the label, e.g. 'Max Speed' -> 'Max Speed: 175 KM/H'.")]
        [SerializeField] private string labelPrefix = "Speed";
        [Tooltip("Max stage this slider allows. Default 10 (full grid). Set lower for limited upgrades — e.g. BoostRefill max 5.")]
        [Range(1, 10)] [SerializeField] private int maxStage = 10;

        [Header("Colors")]
        [SerializeField] private Color filledColor   = new Color(0.95f, 0.85f, 0.20f, 1f);
        [SerializeField] private Color emptyColor    = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color disabledColor = new Color(0.20f, 0.20f, 0.20f, 0.35f);

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(() => Step(-1));
            if (plusButton  != null) plusButton.onClick.AddListener(() => Step(+1));
        }

        private void OnEnable() => Refresh();

        private int GetStage()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return 0;
            switch (target)
            {
                case AdjusterTarget.BoostDuration: return gm.Profile.boostDurationStage;
                case AdjusterTarget.Boost:         return gm.Profile.boostStage;
                case AdjusterTarget.BoostRefill:   return gm.Profile.boostRefillStage;
                case AdjusterTarget.Speed:
                default:                           return gm.Profile.speedStage;
            }
        }

        private void SetStage(int stage)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return;
            switch (target)
            {
                case AdjusterTarget.BoostDuration: gm.Profile.boostDurationStage = stage; break;
                case AdjusterTarget.Boost:         gm.Profile.boostStage         = stage; break;
                case AdjusterTarget.BoostRefill:   gm.Profile.boostRefillStage   = stage; break;
                case AdjusterTarget.Speed:         gm.Profile.speedStage         = stage; break;
            }
            gm.Profile.Save();
            gm.RefreshActivePlaneUpgrades();
        }

        private void Step(int delta)
        {
            int s = Mathf.Clamp(GetStage() + delta, 0, maxStage);
            if (s == GetStage()) return;
            SetStage(s);
            Refresh();
        }

        private void Refresh()
        {
            int stage = Mathf.Clamp(GetStage(), 0, maxStage);

            if (squares != null)
            {
                for (int i = 0; i < squares.Length; i++)
                {
                    if (squares[i] == null) continue;
                    if (i >= maxStage)        squares[i].color = disabledColor; // beyond max — locked off
                    else if (i < stage)       squares[i].color = filledColor;
                    else                      squares[i].color = emptyColor;
                }
            }

            if (percentLabel != null)
            {
                string suffix;
                switch (target)
                {
                    case AdjusterTarget.BoostDuration:
                        // 2s + 1s per stage
                        suffix = (2 + stage) + "s";
                        break;
                    case AdjusterTarget.Boost:
                        // +6 game units per stage = +15 km/h with displayMultiplier 2.5
                        suffix = "+" + (15 * stage) + " km/h";
                        break;
                    case AdjusterTarget.BoostRefill:
                        // 5s - 0.5s per stage
                        float refillSec = 5f - 0.5f * stage;
                        suffix = refillSec.ToString("0.#") + "s";
                        break;
                    case AdjusterTarget.Speed:
                    default:
                        // Max speed at this stage assuming stock 30-unit boost gap.
                        // baseCruise=40, +10% per stage, +30 gap, displayMul=2.5
                        // → max km/h = 175 + 10*stage
                        int maxKmh = 175 + 10 * stage;
                        suffix = maxKmh + " KM/H";
                        break;
                }
                percentLabel.text = labelPrefix + ": " + suffix;
            }
        }
    }
}
