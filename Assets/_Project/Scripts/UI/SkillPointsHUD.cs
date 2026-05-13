using UnityEngine;
using TMPro;
using SkyBrawl.Core;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Displays the player's remaining skill points in the hangar header. Listens to the
    /// HangarEvents bus so it auto-refreshes when any upgrade is purchased/refunded or the
    /// selected plane changes (the latter is just to be safe — skill points are shared, not
    /// per-plane, so plane switches don't actually change the number).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class SkillPointsHUD : MonoBehaviour
    {
        [SerializeField] private string format = "Skill Points: {0}";
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (label == null) label = GetComponent<TMP_Text>();
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

        private void Refresh()
        {
            if (label == null) return;
            int sp = GameManager.Instance?.Profile?.skillPoints ?? 0;
            label.text = string.Format(format, sp);
        }
    }
}
