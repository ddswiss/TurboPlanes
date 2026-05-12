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

        [Header("Speed-scaled hull")]
        [Tooltip("Maximum hull radius (used at speeds >= hullRadiusScaleEnd, and as the solid radius during the ragdoll tumble). Bigger = more aggressive crash detection at high speed and more reliable terrain collision while tumbling.")]
        [SerializeField] private float hullRadiusMax = 5f;
        [Tooltip("Below this speed (game units), hull radius stays at the base hullRadius — keeps runway taxi/takeoff alignment correct.")]
        [SerializeField] private float hullRadiusScaleStart = 30f;
        [Tooltip("At this speed (and above), hull radius reaches hullRadiusMax.")]
        [SerializeField] private float hullRadiusScaleEnd = 70f;

        [Header("Crash thresholds")]
        [Tooltip("Above this speed (game units), an impact at the lenient angle threshold is a crash.")]
        [SerializeField] private float crashSpeedThreshold = 20f;
        [Tooltip("Min cos(angle between forward and -normal) needed to count a hit as head-on AT crashSpeedThreshold. Higher = stricter (must be more head-on). 1 = perfectly head-on, 0 = perpendicular slide.")]
        [Range(0f, 1f)] [SerializeField] private float crashAngleCosThreshold = 0.30f;
        [Tooltip("At this speed (and above), the angle threshold relaxes to crashAngleCosThresholdHigh — high-speed impacts crash at much shallower angles, no matter the impact direction.")]
        [SerializeField] private float crashSpeedHigh = 60f;
        [Tooltip("Min cos(angle) at speeds >= crashSpeedHigh. Lower = even glancing high-speed hits count as crash. 0 = anything counts.")]
        [Range(0f, 1f)] [SerializeField] private float crashAngleCosThresholdHigh = 0.05f;

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

        private void Update()
        {
            // Don't fight the ragdoll-mode radius set in CrashAndRespawn while a crash is in progress.
            if (_isCrashing || _hullTrigger == null || _plane == null) return;

            // Speed-scaled hull radius: small at runway / low speeds (preserves taxi alignment),
            // grows toward hullRadiusMax as the plane reaches top speed (catches grazing impacts).
            float speed = _plane.State.currentSpeed;
            float t = Mathf.InverseLerp(hullRadiusScaleStart, hullRadiusScaleEnd, speed);
            _hullTrigger.radius = Mathf.Lerp(hullRadius, hullRadiusMax, t);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isCrashing) return;
            if (Time.time - _activatedAt < spawnGrace) return;             // grace period after spawn/respawn
            if (other.isTrigger) return;                                   // ignore triggers (HangarTriggerZone etc.)
            if (other.transform.IsChildOf(transform)) return;              // ignore our own hierarchy

            Vector3 closest;
            Vector3 normal;
            float dist;

            // TerrainCollider.ClosestPoint returns the input position (Unity limitation), which
            // would early-return below. Compute closest point + normal from the heightfield instead.
            if (other is TerrainCollider tc)
            {
                // LandingZone takes over Y management while the plane is on the runway —
                // skip terrain push-out so it doesn't fight the snap and cause jitter.
                if (_plane.IgnoreTerrainCollision) return;
                var terrain = tc.GetComponent<Terrain>();
                if (terrain == null) return;
                Vector3 pos = transform.position;
                float surfaceY = terrain.SampleHeight(pos) + terrain.transform.position.y;
                float clearance = pos.y - surfaceY;
                if (clearance >= _hullTrigger.radius) return;  // hull entirely above surface — no overlap
                closest = new Vector3(pos.x, surfaceY, pos.z);

                // Use the ACTUAL terrain surface normal (not Vector3.up) so flying horizontally
                // into a slope counts as a head-on crash. Without this, dot(forward, -up) ≈ 0
                // and the plane just "drives up" inclines no matter how fast.
                var td = terrain.terrainData;
                Vector3 tPos = terrain.transform.position;
                float u = Mathf.Clamp01((pos.x - tPos.x) / td.size.x);
                float v = Mathf.Clamp01((pos.z - tPos.z) / td.size.z);
                normal = td.GetInterpolatedNormal(u, v);
                dist = Mathf.Max(0f, clearance);
            }
            else
            {
                closest = other.ClosestPoint(transform.position);
                Vector3 outward = transform.position - closest;
                dist = outward.magnitude;

                // If ClosestPoint returns our own position, we're INSIDE the collider's bounds.
                // For a huge non-uniform-scale SphereCollider (e.g. flattened island base) this is a
                // false-positive: we're not actually touching the visible mesh. Ignore.
                if (dist < 0.05f) return;
                normal = outward / dist;
            }

            float speed     = _plane.State.currentSpeed;
            float impactCos = Vector3.Dot(transform.forward, -normal);

            // Speed-scaled angle threshold: at low speed (>= crashSpeedThreshold) the
            // angle requirement is lenient; as speed climbs toward crashSpeedHigh, the
            // threshold drops so even shallow glancing hits crash.
            float t = Mathf.InverseLerp(crashSpeedThreshold, crashSpeedHigh, speed);
            float effectiveCos = Mathf.Lerp(crashAngleCosThreshold, crashAngleCosThresholdHigh, t);
            bool isHard = speed > crashSpeedThreshold && impactCos > effectiveCos;
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

        /// <summary>External trigger for a crash + respawn (e.g. LandingZone tilt check).
        /// No-op if a crash is already in progress.</summary>
        public void TriggerCrash()
        {
            if (_isCrashing) return;
            StartCoroutine(CrashAndRespawn());
        }

        private IEnumerator CrashAndRespawn()
        {
            _isCrashing = true;

            // Capture current forward velocity from the FlightModel before handing off to physics
            Vector3 capturedVelocity = transform.forward * _plane.State.currentSpeed * crashVelocityScale;

            // CRITICAL: switch the hull collider to a NON-trigger so the dynamic rigidbody
            // physically collides with terrain (mesas, island, ocean). While it's a trigger,
            // the ragdoll falls through everything.
            // Also inflate to the maximum hull radius for the tumble — the small detection
            // radius used in slow flight (e.g. 1.54) is too small for a fast tumbling
            // rigidbody to reliably collide with the terrain heightfield (tunneling).
            _hullTrigger.radius = Mathf.Max(hullRadius, hullRadiusMax);
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

            // Back to trigger mode for flight-time detection. Restore the (smaller) detection
            // radius too — we only inflated it for the ragdoll tumble.
            _hullTrigger.radius = hullRadius;
            _hullTrigger.isTrigger = true;

            _plane.RespawnAt(pos, rot);
            _plane.enabled = true;
            _isCrashing = false;
            _activatedAt = Time.time; // re-arm spawn grace so respawn doesn't instant-crash

            // If the active map has a LandingZone, drop the respawned plane into it landed
            // and open the hangar — same flow as initial map load. Player resumes at the
            // runway instead of high above the AirportSpawn.
            var landing = FindFirstObjectByType<SkyBrawl.Maps.LandingZone>();
            if (landing != null) landing.ForceLand(_plane);
        }
    }
}
