using UnityEngine;

namespace SkyBrawl.Planes
{
    [CreateAssetMenu(fileName = "PlaneCatalog", menuName = "SkyBrawl/Plane Catalog", order = 1)]
    public class PlaneCatalog : ScriptableObject
    {
        public PlaneDefinition[] planes;

        public PlaneDefinition GetById(string id)
        {
            if (planes == null) return null;
            foreach (var p in planes) if (p != null && p.id == id) return p;
            return null;
        }
    }
}
