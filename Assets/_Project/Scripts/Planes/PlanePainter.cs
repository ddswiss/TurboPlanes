using UnityEngine;

namespace SkyBrawl.Planes
{
    /// <summary>
    /// Applies a base color tint to all renderers on the plane. Wire up to UI later.
    /// </summary>
    public class PlanePainter : MonoBehaviour
    {
        public void Apply(Color color)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
