using UnityEngine;
using SkyBrawl.Player;

namespace SkyBrawl.Maps
{
    /// <summary>
    /// Soft map boundary. When the player flies beyond a defined radius from this
    /// GameObject (the map center), gradually steers them back by writing into
    /// PlaneController.externalYawBias. The bias scales with how far past the boundary
    /// they are, so close to the edge it's a nudge and far out it's a hard turn.
    ///
    /// Place this on the map's "Environment" root or on a dedicated empty at the map
    /// center. The XZ position of this transform defines the center; Y is ignored.
    /// </summary>
    public class MapBoundary : MonoBehaviour
    {
        [Tooltip("Radius in meters within which no auto-return is applied. Outside this, the plane is steered back to center.")]
        [SerializeField] private float boundaryRadius = 1000f;

        [Tooltip("Distance past the boundary at which the auto-return reaches full strength.")]
        [SerializeField] private float fullStrengthOver = 300f;

        [Tooltip("Maximum yaw bias added to the plane's input (in input units; 1 = full A/D press). Clamped to +/-this.")]
        [SerializeField] private float maxYawBias = 1.2f;

        [Tooltip("Visualize the boundary in the Scene view.")]
        [SerializeField] private bool drawGizmo = true;

        private PlaneController _plane;
        private float _retryTimer;

        private void Update()
        {
            // Find the plane lazily — it spawns at runtime
            if (_plane == null)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    _plane = FindFirstObjectByType<PlaneController>();
                    _retryTimer = 0.5f;
                }
                if (_plane == null) return;
            }

            Vector3 center = transform.position;
            Vector3 planePos = _plane.transform.position;

            // Horizontal distance only (XZ plane). Vertical position doesn't matter here.
            float dx = planePos.x - center.x;
            float dz = planePos.z - center.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);

            float overshoot = distXZ - boundaryRadius;
            if (overshoot <= 0f)
            {
                _plane.externalYawBias = 0f;
                return;
            }

            // Strength ramps in over `fullStrengthOver` meters past the boundary
            float strength = Mathf.Clamp01(overshoot / Mathf.Max(1f, fullStrengthOver));

            // Determine which way to turn: compute angle between the plane's forward
            // and the direction back toward center, signed by the world-up axis.
            Vector3 toCenter = -new Vector3(dx, 0f, dz).normalized;
            Vector3 fwd = _plane.transform.forward;
            fwd.y = 0f; fwd.Normalize();
            // Signed angle: positive = need to turn right (yaw +), negative = turn left (yaw -)
            float signedAngle = Vector3.SignedAngle(fwd, toCenter, Vector3.up);
            // 0..1 magnitude based on how off-heading we are (180 = pointing dead away)
            float angleMag = Mathf.Clamp01(Mathf.Abs(signedAngle) / 180f);

            float bias = Mathf.Sign(signedAngle) * angleMag * strength * maxYawBias;
            _plane.externalYawBias = bias;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            // Use a vertical disc to show the boundary on the ground plane
            const int seg = 64;
            Vector3 c = transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.7f);
            Vector3 prev = c + new Vector3(boundaryRadius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * boundaryRadius, 0f, Mathf.Sin(a) * boundaryRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.4f);
            float outerR = boundaryRadius + fullStrengthOver;
            prev = c + new Vector3(outerR, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * outerR, 0f, Mathf.Sin(a) * outerR);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
