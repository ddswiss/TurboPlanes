using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Core;
using SkyBrawl.Persistence;

namespace SkyBrawl.UI
{
    public enum AdjusterTarget
    {
        Speed,           // PlaneSaveData.speedStage          — cruise = base * (1 + 0.10*stage)
        BoostDuration,   // PlaneSaveData.boostDurationStage  — boost lasts 2 + stage seconds (stage 0..8)
        Boost,           // PlaneSaveData.boostStage          — max = cruise + 30 + 6*stage game units
        BoostRefill      // PlaneSaveData.boostRefillStage    — refill in 5 - 0.5*stage seconds (stage 0..5)
    }

    /// <summary>
    /// Stepped-slider UI (squares + plus/minus) that reads/writes one int field on
    /// the SELECTED plane's PlaneSaveData. Increments cost one skill point from the
    /// shared PlayerProfile pool; decrements refund one. Squares colors:
    ///   yellow  = upgrade owned at this tier
    ///   grey    = available to buy
    ///   black   = beyond this slider's maxStage (locked).
    /// When the player has no skill points left, the next purchasable square shows
    /// as black too — visually communicating "can't afford".
    /// </summary>
    public class SpeedAdjusterUI : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Image[] squares;
        [SerializeField] private TMP_Text percentLabel;

        [Header("Config")]
        [Tooltip("Which PlaneSaveData stage this slider controls.")]
        [SerializeField] private AdjusterTarget target = AdjusterTarget.Speed;
        [Tooltip("Prefix for the label, e.g. 'Cruise Speed' -> 'Cruise Speed: 100 KM/H'.")]
        [SerializeField] private string labelPrefix = "Cruise Speed";
        [Tooltip("Max stage this slider allows. Default 10 (full grid). Set lower for limited upgrades — e.g. BoostRefill max 5.")]
        [Range(1, 10)] [SerializeField] private int maxStage = 10;

        [Header("Colors")]
        [SerializeField] private Color filledColor   = new Color(0.95f, 0.85f, 0.20f, 1f); // yellow — owned
        [SerializeField] private Color emptyColor    = new Color(1f,    1f,    1f,    0.18f); // grey  — available
        [SerializeField] private Color disabledColor = new Color(0.05f, 0.05f, 0.05f, 0.80f); // black — locked / unaffordable

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(() => Step(-1));
            if (plusButton  != null) plusButton.onClick.AddListener(() => Step(+1));
        }

        private void OnEnable()
        {
            Refresh();
            HangarEvents.UpgradesOrPlaneChanged += Refresh;
        }

        private void OnDisable()
        {
            HangarEvents.UpgradesOrPlaneChanged -= Refresh;
        }

        private PlaneSaveData GetActivePlaneData()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return null;
            if (string.IsNullOrEmpty(gm.Profile.selectedPlaneId)) return null;
            return gm.Profile.GetOrCreatePlaneData(gm.Profile.selectedPlaneId);
        }

        private int GetStage()
        {
            var pd = GetActivePlaneData();
            if (pd == null) return 0;
            switch (target)
            {
                case AdjusterTarget.BoostDuration: return pd.boostDurationStage;
                case AdjusterTarget.Boost:         return pd.boostStage;
                case AdjusterTarget.BoostRefill:   return pd.boostRefillStage;
                case AdjusterTarget.Speed:
                default:                           return pd.speedStage;
            }
        }

        private void SetStage(int stage)
        {
            var pd = GetActivePlaneData();
            if (pd == null) return;
            switch (target)
            {
                case AdjusterTarget.BoostDuration: pd.boostDurationStage = stage; break;
                case AdjusterTarget.Boost:         pd.boostStage         = stage; break;
                case AdjusterTarget.BoostRefill:   pd.boostRefillStage   = stage; break;
                case AdjusterTarget.Speed:         pd.speedStage         = stage; break;
            }
            var gm = GameManager.Instance;
            gm.Profile.Save();
            gm.RefreshActivePlaneUpgrades();
            HangarEvents.RaiseUpgradesChanged();
        }

        private void Step(int delta)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return;

            int current = GetStage();
            int next    = Mathf.Clamp(current + delta, 0, maxStage);
            if (next == current) return;

            if (delta > 0)
            {
                // Buying — must have a skill point to spend.
                if (gm.Profile.skillPoints <= 0) return;
                gm.Profile.skillPoints--;
            }
            else
            {
                // Refunding — give the point back.
                gm.Profile.skillPoints++;
            }

            SetStage(next);
            // SetStage already raises UpgradesChanged which calls Refresh on every adjuster.
        }

        private void Refresh()
        {
            int stage   = Mathf.Clamp(GetStage(), 0, maxStage);
            int sp      = GameManager.Instance?.Profile?.skillPoints ?? 0;
            bool canBuy = sp > 0;

            if (squares != null)
            {
                for (int i = 0; i < squares.Length; i++)
                {
                    if (squares[i] == null) continue;
                    if (i >= maxStage)              squares[i].color = disabledColor;      // beyond max — permanently locked
                    else if (i < stage)             squares[i].color = filledColor;        // owned
                    else if (i == stage && !canBuy) squares[i].color = disabledColor;      // next-purchasable, but unaffordable
                    else                            squares[i].color = emptyColor;         // available
                }
            }

            if (plusButton  != null) plusButton.interactable  = stage < maxStage && canBuy;
            if (minusButton != null) minusButton.interactable = stage > 0;

            if (percentLabel != null)
            {
                string suffix;
                switch (target)
                {
                    case AdjusterTarget.BoostDuration:
                        suffix = (2 + stage) + "s";
                        break;
                    case AdjusterTarget.Boost:
                        suffix = "+" + (15 * stage) + " km/h";
                        break;
                    case AdjusterTarget.BoostRefill:
                        float refillSec = 5f - 0.5f * stage;
                        suffix = refillSec.ToString("0.#") + "s";
                        break;
                    case AdjusterTarget.Speed:
                    default:
                        // baseCruise=40, +10% per stage, displayMul=2.5 → 100 + 10*stage km/h.
                        int cruiseKmh = 100 + 10 * stage;
                        suffix = cruiseKmh + " KM/H";
                        break;
                }
                percentLabel.text = labelPrefix + ": " + suffix;
            }
        }
    }
}
