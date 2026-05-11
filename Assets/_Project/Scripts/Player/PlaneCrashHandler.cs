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
        [SerializeField] private float respawnDelay = 1.5f;
        [Tooltip("Multiplier applied to the captured forward speed when handing it to the dynamic rigidbody.")]
        [SerializeField] private float crashVelocityScale = 1f;
        [Tooltip("Initial random tumble (degrees/sec roughly).")]
        [SerializeField] private float crashTumbleSpin = 360f;

        [Tooltip("Grace period (seconds) after enable during which crashes are ignored. Prevents instant respawn loops at spawn.")]
        [SerializeField] private float spawnGrace = 0.75f;

        private PlaneController _plane;
        private Rigidbody _rb;
        private SphereCollider _hullTrigger;
        private bool _isCrashing;
        private float _activatedAt;

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

        private void OnEnable()
        {
            _activatedAt = Time.time;
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isCrashing) return;
            if (Time.time - _activatedAt < spawnGrace) return;             // grace period after spawn/respawn
            if (other.isTrigger) return;                                   // ignore triggers (HangarTriggerZone etc.)
            if (other.transform.IsChildOf(transform)) return;              // ignore our own hierarchy

            Vector3 closest = other.ClosestPoint(transform.position);
            Vector3 outward = transform.position - closest;
            float   dist    = outward.magnitude;

            // If ClosestPoint returns our own position, we're INSIDE the collider's bounds.
            // For a huge non-uniform-scale SphereCollider (e.g. flattened island base) this is a
            // false-positive: we're not actually touching the visible mesh. Ignore.
            if (dist < 0.05f) return;

            Vector3 normal  = outward / dist;
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

            // CRITICAL: switch the hull collider to a NON-trigger so the dynamic rigidbody
            // physically collides with terrain (mesas, island, ocean). While it's a trigger,
            // the ragdoll falls through everything.
            _hullTrigger.isTrigger = false;

            // Disable flight control and switch to dynamic physics for the tumble
            _plane.enabled = false;
            _rb.isKinematic = false;
            _rb.useGravity  = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // avoid tunnelling thin colliders at high fall speed
            _rb.linearVelocity  = capturedVelocity;
            _rb.angularVelocity = Random.insideUnitSphere * (crashTumbleSpin * Mathf.Deg2Rad);

            // Wait while tumbling
            yield return new WaitForSeconds(respawnDelay);

            // Find spawn and respawn
            var spawn = GameObject.Find("AirportSpawn");
            Vector3    pos = spawn != null ? spawn.transform.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            // Reset rigidbody before re-enabling the controller. Order matters:
            // collision detection mode must be Discrete BEFORE isKinematic = true.
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.isKinematic = true;
            _rb.useGravity  = false;

            // Back to trigger mode for flight-time detection
            _hullTrigger.isTrigger = true;

            _plane.RespawnAt(pos, rot);
            _plane.enabled = true;
            _isCrashing = false;
            _activatedAt = Time.time; // re-arm spawn grace so respawn doesn't instant-crash
        }
    }
}
