using UnityEngine;
using SkyBrawl.Tuning;

namespace SkyBrawl.Planes
{
    [CreateAssetMenu(fileName = "PlaneDefinition_New", menuName = "SkyBrawl/Plane Definition", order = 0)]
    public class PlaneDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used in saves. Never rename without migration.")]
        public string id = "plane_new";
        public string displayName = "New Plane";
        [TextArea] public string description;
        public Sprite preview;
        [Tooltip("Plane prefab to spawn. Must contain a PlaneController with PlaneTuningSO assigned.")]
        public GameObject prefab;
        [Tooltip("Base tuning. Runtime tuning is a clone of this with upgrades applied.")]
        public PlaneTuningSO baseTuning;
        public bool unlockedByDefault = true;
        public int unlockCost = 0;
        public Color[] availableColors = new Color[] {
            new Color(0.85f, 0.20f, 0.20f, 1f),  // red (default)
            new Color(0.20f, 0.45f, 0.85f, 1f),  // blue
            new Color(0.20f, 0.70f, 0.30f, 1f),  // green
            new Color(0.95f, 0.80f, 0.20f, 1f),  // yellow
            new Color(0.10f, 0.10f, 0.10f, 1f),  // black
        };
    }
}
