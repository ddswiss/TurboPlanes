using UnityEngine;
using SkyBrawl.Core;
using SkyBrawl.Player;

namespace SkyBrawl.Maps
{
    /// <summary>
    /// Trigger volume placed over a runway. While the plane is inside, the player can
    /// hold the negative throttle (LCtrl) to bleed all the way to 0 km/h, and the
    /// hangar auto-opens at full stop. Releasing throttle brings the plane back up to
    /// cruise via the standard flight model. Outside the zone, the normal minSpeed
    /// (which already respects the speed multiplier) is enforced again.
    ///
    /// Setup: attach to a GameObject with a BoxCollider (isTrigger). Position over the
    /// runway. The plane's hull trigger drives OnTriggerEnter/Stay/Exit.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LandingZone : MonoBehaviour
    {
        [Header("Deceleration feel")]
        [Tooltip("Multiplier applied to the plane's throttleAccel while inside the zone. <1 = slower decel/accel for a longer, more controllable landing roll. 0.4 = ~2.5× slower than normal flight.")]
        [Range(0.1f, 1f)] [SerializeField] private float landingThrottleAccelMul = 0.2f;

        [Header("Tilt safety")]
        [Tooltip("Max ROLL/BANK degrees (left or right wing tilt) the plane can have inside the zone before triggering an instant crash.")]
        [Range(0f, 90f)] [SerializeField] private float maxLandingTilt = 60f;

        [Tooltip("Max PITCH degrees (nose up or nose down) the plane can have inside the zone before triggering an instant crash.")]
        [Range(0f, 90f)] [SerializeField] private float maxLandingPitch = 45f;

        [Header("Stop detection")]
        [Tooltip("Speed (game units) below which the plane is considered fully stopped. Hangar opens at this point.")]
        [Range(0f, 2f)] [SerializeField] private float stopSpeedThreshold = 0.10f;

        [Tooltip("Speed (game units) the plane resumes at when the hangar closes. Must be above stopSpeedThreshold or the plane re-lands instantly. ~0.28 = 1 km/h displayed.")]
        [SerializeField] private float takeoffSpeed = 1f / 3.6f;

        [Header("Runway snap")]
        [Tooltip("Vertical clearance added on top of the terrain surface (or spawnPosition.y as a fallback) when pinning the plane to the runway. Should match the plane's hull radius so terrain push-out lines up with the snap and there's no teleport when the constraint releases at speed >= 15.")]
        [SerializeField] private float runwayClearance = 2.5f;

        [Tooltip("Max speed (m/s) the plane glides toward the runway-Y target when the snap is engaged. Higher = snappier, lower = smoother glide. Used by Mathf.MoveTowards on Y.")]
        [SerializeField] private float runwaySnapSpeed = 6f;

        [Header("Spawn pose (used by GameManager when entering the map and when the hangar closes)")]
        [Tooltip("World position the plane is teleported to when entering the map (lands in hangar) and when the hangar closes (taxi start of runway).")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(-827f, 125f, 337f);
        [Tooltip("Euler rotation applied to the plane on spawn / hangar close. Should face along the runway away from the start.")]
        [SerializeField] private Vector3 spawnRotationEuler = new Vector3(0f, 270f, 0f);

        public Quaternion SpawnRotation => Quaternion.Euler(spawnRotationEuler);

        [Header("Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(0.4f, 1f, 0.4f, 0.20f);

        private BoxCollider _trigger;
        private PlaneController _plane;
        private bool _planeInside;

        // Saved state so we can restore on exit (slider changes during the hangar visit
        // are reapplied via GameManager.RefreshActivePlaneUpgrades for minSpeed/cruise/max).
        private float _savedMinSpeed;
        private float _savedThrottleAccel;
        private bool _savedActive;

        private void Awake()
        {
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.HangarClosed += OnHangarClosed;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.HangarClosed -= OnHangarClosed;
        }

        private void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponentInParent<PlaneController>();
            if (pc == null || pc.Tuning == null) return;
            // If a different plane than we previously tracked enters (swap-plane in hangar),
            // reset our saved-state so the new plane gets a fresh tuning capture + override.
            if (pc != _plane) _savedActive = false;
            _plane = pc;
            _planeInside = true;

            // Save tuning only on the FIRST entry of a session (ForceLand may have already
            // overridden the values; re-saving would capture the override and lose the originals).
            if (!_savedActive)
            {
                _savedMinSpeed      = pc.Tuning.minSpeed;
                _savedThrottleAccel = pc.Tuning.throttleAccel;
                _savedActive = true;
                pc.Tuning.minSpeed     = 0f;
                pc.Tuning.throttleAccel = _savedThrottleAccel * landingThrottleAccelMul;
            }
            pc.IgnoreTerrainCollision = true;
        }

        private void FixedUpdate()
        {
            if (!_planeInside || _plane == null) return;
            if (_plane.IsLanded) return;  // already landed; idle until hangar closes

            // Roll check (banking) — uses CurrentBankAngle (visual) since the flight model
            // itself doesn't roll the logical transform.
            float rollDeg  = Mathf.Abs(_plane.CurrentBankAngle);
            // Pitch check (nose up/down) — derived from the logical forward vector's Y
            // component. forward.y = sin(pitch), so asin gives the pitch angle in radians.
            float pitchDeg = Mathf.Abs(Mathf.Asin(Mathf.Clamp(_plane.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg);
            if (rollDeg > maxLandingTilt || pitchDeg > maxLandingPitch)
            {
                var crash = _plane.GetComponent<SkyBrawl.Player.PlaneCrashHandler>();
                if (crash != null) crash.TriggerCrash();
                return;  // skip everything else this tick — plane is being respawned
            }

            // Couple Y-constraint AND terrain-collision-ignore to the steering-lock state.
            //
            //   Locked (currentSpeed < 15): plane is "on the runway". Pin Y to the runway,
            //     ignore terrain push-out (otherwise it'd fight the snap and cause jitter).
            //
            //   Unlocked (currentSpeed >= 15): plane has lift-off speed. Release Y so the
            //     player can climb out by pressing pitch. Re-enable terrain collision so
            //     the plane can't sneak through the ground by pitching down.
            bool grounded = _plane.IsSteeringLocked;
            _plane.IgnoreTerrainCollision = grounded;
            if (grounded)
            {
                ConstrainToRunwayY();
            }

            // Stopped → land. Use a very small threshold so it only fires at true 0.
            if (_plane.State.currentSpeed <= stopSpeedThreshold)
            {
                Land();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var pc = other.GetComponentInParent<PlaneController>();
            if (pc == null || pc != _plane) return;

            // If the plane left the zone below the steering-lock threshold (default 15
            // game units), that means it taxied/drifted out instead of taking off properly
            // — count as a crash. Skip the check while IsLanded is true (plane is being
            // teleported during respawn, not actually flying).
            bool tooSlow = pc.IsSteeringLocked && !pc.IsLanded;

            RestoreTuning();
            pc.IgnoreTerrainCollision = false;
            _planeInside = false;

            if (tooSlow)
            {
                var crash = pc.GetComponent<SkyBrawl.Player.PlaneCrashHandler>();
                if (crash != null) crash.TriggerCrash();
            }
        }

        private void ConstrainToRunwayY()
        {
            if (_plane == null) return;
            // Target = terrain surface + clearance. Matches what PlaneCrashHandler's
            // terrain push-out would settle on, so when the constraint releases at
            // currentSpeed >= 15 there's no Y teleport. Falls back to spawnPosition.y if
            // no terrain is in the scene.
            float targetY;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 p = _plane.transform.position;
                float terrainY = terrain.SampleHeight(p) + terrain.transform.position.y;
                targetY = terrainY + runwayClearance;
            }
            else
            {
                targetY = spawnPosition.y + runwayClearance;
            }
            // Smooth move instead of hard snap — avoids the visible teleport when the
            // plane crosses the speed-15 boundary or first touches the runway.
            float curY = _plane.transform.position.y;
            float maxStep = runwaySnapSpeed * Time.fixedDeltaTime;
            float newY = Mathf.MoveTowards(curY, targetY, maxStep);
            _plane.ConstrainGroundY(newY);
        }

        private void Land()
        {
            if (_plane == null) return;
            _plane.SetCurrentSpeed(0f);
            _plane.IsLanded = true;
            ConstrainToRunwayY();   // glue plane in place before hangar opens
            if (GameManager.Instance != null) GameManager.Instance.OpenHangar();
        }

        private void OnHangarClosed()
        {
            if (_plane == null || !_plane.IsLanded) return;
            // Teleport plane back to runway start so the player always begins their take-off
            // run from the same place, then resume at takeoffSpeed (above stopSpeedThreshold
            // so re-landing doesn't immediately fire). Throttle = 0 → target = cruise, so the
            // plane accelerates naturally from this seed value.
            _plane.SnapTo(spawnPosition, SpawnRotation);
            _plane.IsLanded = false;
            _plane.SetCurrentSpeed(takeoffSpeed);
        }

        /// <summary>Force the plane into the landed state at the runway start. Called by
        /// GameManager.SpawnSelectedPlane so the player begins each map session in the
        /// hangar at the runway, ready to customize and take off.</summary>
        public void ForceLand(PlaneController pc)
        {
            if (pc == null || pc.Tuning == null) return;
            // If this is a different plane than we last tracked (swap-plane in hangar),
            // reset our saved-state so the override is re-applied to the new plane's
            // fresh tuning values.
            if (pc != _plane) _savedActive = false;
            _plane = pc;
            _planeInside = true;

            if (!_savedActive)
            {
                _savedMinSpeed      = pc.Tuning.minSpeed;
                _savedThrottleAccel = pc.Tuning.throttleAccel;
                _savedActive = true;
                pc.Tuning.minSpeed     = 0f;
                pc.Tuning.throttleAccel = _savedThrottleAccel * landingThrottleAccelMul;
            }

            pc.SnapTo(spawnPosition, SpawnRotation);
            pc.SetCurrentSpeed(0f);
            pc.IsLanded = true;
            pc.IgnoreTerrainCollision = true;
            if (GameManager.Instance != null) GameManager.Instance.OpenHangar();
        }

        private void RestoreTuning()
        {
            if (_plane == null || _plane.Tuning == null || !_savedActive) return;
            // Restore throttleAccel directly (it's not tied to the speed multiplier).
            _plane.Tuning.throttleAccel = _savedThrottleAccel;
            // Re-apply the menu speed slider so cruise/min/max are right even if the
            // user changed the slider in the hangar.
            if (GameManager.Instance != null) GameManager.Instance.RefreshActivePlaneUpgrades();
            else _plane.Tuning.minSpeed = _savedMinSpeed;
            _savedActive = false;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = gizmoColor;
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = prev;
        }
    }
}
