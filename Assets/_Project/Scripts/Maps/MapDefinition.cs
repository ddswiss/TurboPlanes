using UnityEngine;

namespace SkyBrawl.Maps
{
    [CreateAssetMenu(fileName = "MapDefinition_New", menuName = "SkyBrawl/Map Definition", order = 0)]
    public class MapDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used in saves. Never rename without migration.")]
        public string id = "map_new";
        public string displayName = "New Map";
        [TextArea] public string description;
        public Sprite preview;
        [Tooltip("Scene name (without .unity). Must be added to Build Settings.")]
        public string sceneName = "Map_New";
        public bool unlockedByDefault = true;
        public int unlockCost = 0;
    }
}
