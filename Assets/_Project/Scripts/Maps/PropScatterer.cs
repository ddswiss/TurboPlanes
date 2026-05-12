using UnityEngine;

namespace SkyBrawl.Maps
{
    /// <summary>
    /// Edit-time scatterer: places N instances of a prefab inside a circular area
    /// centered on this transform, optionally raycasting down to snap each
    /// instance to the terrain. Spawned objects become real children that can
    /// be selected, moved, or deleted individually. Scatter is triggered from
    /// the custom inspector — nothing happens at runtime, so there is zero
    /// gameplay overhead.
    /// </summary>
    public class PropScatterer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Prefab to scatter. Becomes children of this GameObject.")]
        public GameObject prefab;

        [Header("Distribution")]
        [Tooltip("Number of instances to spawn when Scatter is clicked.")]
        [Min(0)] public int count = 20;

        [Tooltip("Radius (m) of the circular scatter area, centered on this transform.")]
        [Min(0f)] public float radius = 80f;

        [Tooltip("Random seed. 0 = different result each click. Any other value = repeatable layout.")]
        public int seed = 0;

        [Header("Ground snap")]
        [Tooltip("Raycast each instance down to whatever it hits, instead of placing at this transform's Y.")]
        public bool snapToGround = true;

        [Tooltip("Layers considered ground for the snap raycast. Defaults to everything.")]
        public LayerMask groundMask = ~0;

        [Tooltip("Vertical offset added after snap (so props don't sink into the surface).")]
        public float yOffset = 0f;

        [Header("Variation")]
        [Tooltip("Min/max uniform scale multiplier applied to each instance.")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);

        [Tooltip("Randomize Y rotation across 0–360°.")]
        public bool randomYaw = true;

        [Tooltip("Slight random tilt off vertical (degrees). 0 = perfectly upright.")]
        [Range(0f, 30f)] public float maxTilt = 0f;

        [Tooltip("Align each instance's up axis to the terrain normal at the hit point (only used when Snap To Ground is on). Random yaw still applies around the local-up.")]
        public bool alignToSurface = false;

        [Header("Gizmo")]
        public bool drawGizmo = true;
        public Color gizmoColor = new Color(0.3f, 1f, 0.5f, 0.5f);

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;
            const int seg = 64;
            Vector3 c = transform.position;
            Vector3 prev = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
