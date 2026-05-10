using UnityEngine;
using System.Collections;

namespace SkyBrawl.Player
{
    /// <summary>
    /// Detects terrain collisions on the plane (kinematic Rigidbody driven by FlightModel)
    /// using a trigger collider, and either slides along the surface or triggers a crash
    /// + ragdoll-style tumble + respawn at the AirportSpawn.
    ///
    /// Heuristic for "hard" crash vs glance:
    ///   - hard if  forward-dot-(-normal) > crashAngleCosThreshold  AND  speed > crashSpeedThreshold
    ///   - otherwise the plane is gently pushed back along the surface normal (slide)
    ///
    /// Auto-creates a SphereCollider trigger on Awake if none is present so the plane
    /// prefab doesn't need manual collider setup.
    /// </summary>
    [RequireComponent(typeof(PlaneController))]
    [RequireComponent(typeof(Rigidbody))]
    public class PlaneCrashHandler : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Radius of the hull trigger collider that auto-spawns if missing.")]
        [SerializeField] private float hullRadius = 4f;

        [Header("Crash thresholds")]
        [Tooltip("Above this speed (game units), an aligned impact is considered a crash.")]
        [SerializeField] private float crashSpeedThreshold = 30f;
        [Tooltip("dot(forward, -normal) above this is considered head-on enough to crash. 1 = pure head-on, 0 = perpendicular.")]
        [Range(0f, 1f)] [SerializeField] private float crashAngleCosThreshold = 0.4f;

        [Header("Ragdoll + Respawn")]
        [Tooltip("How many seconds to tumble before respawning the plane.")]
        [SerializeField] private float respawnDelay = 3f;
        [Tooltip("Multiplier applied to the captured forward speed when handing it to the dynamic rigidbody.")]
        [SerializeField] private float crashVelocityScale = 1f;
        [Tooltip("Initial random tumble (degrees/sec roughly).")]
        [SerializeField] private float crashTumbleSpin = 360f;

        private PlaneController _plane;
        private Rigidbody _rb;
        private SphereCollider _hullTrigger;
        private bool _isCrashing;

        private void Awake()
        {
            _plane = GetComponent<PlaneController>();
            _rb    = GetComponent<Rigidbody>();

            // Ensure a trigger collider exists on the plane root
            _hullTrigger = GetComponent<SphereCollider>();
            if (_hullTrigger == null)
            {
                _hullTrigger = gameObject.AddComponent<SphereCollider>();
                _hullTrigger.radius = hullRadius;
                _hullTrigger.isTrigger = true;
                _hullTrigger.center = Vector3.zero;
            }
            else
            {
                _hullTrigger.isTrigger = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isCrashing) return;
            // Ignore our own hierarchy
            if (other.transform.IsChildOf(transform)) return;

            Vector3 closest = other.ClosestPoint(transform.position);
            Vector3 outward = transform.position - closest;
            float   dist    = outward.magnitude;
            Vector3 normal  = dist > 0.0001f ? outward / dist : -transform.forward;

            float speed     = _plane.State.currentSpeed;
            float impactCos = Vector3.Dot(transform.forward, -normal);

            bool isHard = speed > crashSpeedThreshold && impactCos > crashAngleCosThreshold;
            if (isHard)
            {
                StartCoroutine(CrashAndRespawn());
                return;
            }

            // Glance: push the plane out so its hull is no longer penetrating.
            float penetration = _hullTrigger.radius - dist;
            if (penetration > 0f)
            {
                Vector3 corrected = transform.position + normal * penetration;
                transform.position = corrected;
                _plane.SyncStatePosition(corrected);
            }
        }

        private IEnumerator CrashAndRespawn()
        {
            _isCrashing = true;

            // Capture current forward velocity from the FlightModel before handing off to physics
            Vector3 capturedVelocity = transform.forward * _plane.State.currentSpeed * crashVelocityScale;

            // Disable flight control and switch to dynamic physics for the tumble
            _plane.enabled = false;
            _rb.isKinematic = false;
            _rb.useGravity  = true;
            _rb.linearVelocity  = capturedVelocity;
            _rb.angularVelocity = Random.insideUnitSphere * (crashTumbleSpin * Mathf.Deg2Rad);

            // Wait while tumbling
            yield return new WaitForSeconds(respawnDelay);

            // Find spawn and respawn
            var spawn = GameObject.Find("AirportSpawn");
            Vector3    pos = spawn != null ? spawn.transform.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            // Reset rigidbody before re-enabling the controller
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity  = false;

            _plane.RespawnAt(pos, rot);
            _plane.enabled = true;
            _isCrashing = false;
        }
    }
}
