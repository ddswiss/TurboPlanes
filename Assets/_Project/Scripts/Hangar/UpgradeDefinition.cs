using UnityEngine;

namespace SkyBrawl.Hangar
{
    public enum UpgradeKind { Engine, Wings, Maneuverability }

    [CreateAssetMenu(fileName = "Upgrade_New", menuName = "SkyBrawl/Upgrade Definition", order = 0)]
    public class UpgradeDefinition : ScriptableObject
    {
        public string id = "upgrade_new";
        public string displayName;
        public UpgradeKind kind;
        public int[] tierCosts = new int[] { 100, 250, 500 };
        [Tooltip("Multiplier applied to the relevant tuning field, indexed by tier (0 = no upgrade).")]
        public float[] tierMultipliers = new float[] { 1.0f, 1.1f, 1.25f, 1.5f };
    }
}
