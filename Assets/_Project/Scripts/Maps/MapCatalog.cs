using UnityEngine;

namespace SkyBrawl.Maps
{
    [CreateAssetMenu(fileName = "MapCatalog", menuName = "SkyBrawl/Map Catalog", order = 1)]
    public class MapCatalog : ScriptableObject
    {
        public MapDefinition[] maps;

        public MapDefinition GetById(string id)
        {
            if (maps == null) return null;
            foreach (var m in maps) if (m != null && m.id == id) return m;
            return null;
        }
    }
}
