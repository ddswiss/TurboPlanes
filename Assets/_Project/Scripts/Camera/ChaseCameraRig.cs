using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyBrawl.CameraRig
{
    /// <summary>
    /// Chase + orbit camera.
    ///
    /// - When RIGHT MOUSE BUTTON is NOT held: standard chase camera (sits at `offset`
    ///   in the target's local space, follows position/rotation, leans into roll).
    /// - When RMB is held: world-space spherical orbit around the target. Position
    ///   snaps directly to the sphere point each frame (no lerp), so fast mouse drags
    ///   stay on the sphere arc instead of chord-cutting through the interior.
    ///
    /// Releasing RMB lerps smoothly back to the chase pose.
    /// WASD still controls the plane regardless of camera mode.
    /// </summary>
    public class ChaseCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Chase")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -10f);
        [SerializeField] private float positionLerp = 12f;
        [SerializeField] private float rotationLerp = 8f;
        [Tooltip("How much the camera leans into the plane's roll in CHASE mode only. 0 = camera stays upright. Free-look always uses world up.")]
        [Range(0f, 1f)] public float rollFollow = 0.4f;

        [Header("Free Look (RMB) — spherical orbit around the plane")]
        [Tooltip("Mouse sensitivity in degrees per pixel of mouse delta.")]
        [SerializeField] private float freeLookSensitivity = 0.25f;
        [Tooltip("Pitch clamp (degrees). + = camera above plane, - = camera below.")]
        [SerializeField] private float freeLookPitchMin = -80f;
        [SerializeField] private float freeLookPitchMax =  80f;
        [Tooltip("How quickly the orbit radius lerps from the current camera distance toward the standard |offset| radius. Higher = settles faster. 0 = stays at entry distance.")]
        [SerializeField] private float radiusSettleSpeed = 2.5f;
        [Tooltip("Duration of the smooth transition from chase pose to orbit pose when RMB is pressed. After this elapses, the camera snaps to the sphere each frame so fast drags trace clean arcs.")]
        [SerializeField] private float pressTransitionDuration = 0.15f;

        // World-space spherical orbit angles, updated by mouse during free look.
        private float _orbitYaw;
        private float _orbitPitch;
        private float _orbitRadius;  // smoothly lerps toward |offset| while orbiting
        private bool _wasFreeCamActive;

        // Press transition: smoothly slide from chase pose to orbit pose at the start
        // of a free-look session, then snap for the rest of the drag.
        private float _pressT;          // 0..1, 1 means transition complete
        private Vector3 _pressStartPos;
        private Quaternion _pressStartRot;

        // Z-key toggle. Free cam is active if Z is toggled ON or RMB is held.
        private bool _zToggled;

        // Cached rigidbody on the target — used to detect ragdoll state (non-kinematic = crashed).
        private Rigidbody _targetRb;

        private void LateUpdate()
        {
            if (target == null) return;

            // Lazy-cache the target's rigidbody so we can detect ragdoll state below.
            if (_targetRb == null) _targetRb = target.GetComponent<Rigidbody>();

            // RAGDOLL / CRASH state: target's rb is dynamic (non-kinematic). Freeze the
            // camera position so it doesn't follow the tumbling wreck (would lag, clip
            // into terrain, and look weird). Just rotate slowly to keep the wreck framed
            // until the respawn happens.
            bool isCrashed = _targetRb != null && !_targetRb.isKinematic;
            if (isCrashed)
            {
                Vector3 toTarget = target.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 3f * Time.deltaTime);
                }
                // Reset orbit/transition state so re-entry on respawn is clean
                _wasFreeCamActive = false;
                _pressT = 0f;
                _zToggled = false; // turn off free-cam if it was on (avoids confusing camera-jerk on respawn)
                return;
            }

            // Toggle Z (keyboard) on press transitions.
            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
            {
                _zToggled = !_zToggled;
            }

            bool rmbHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool freeCamActive = rmbHeld || _zToggled;

            // First frame of entering free look: snapshot current camera yaw/pitch
            // AND distance so the orbit starts from where the chase camera was
            // (no visible jump in position, angle, or radius). Also capture the
            // current pose for the press transition that softens any rotation
            // up-vector change (e.g. rolled chase -> level orbit horizon).
            if (freeCamActive && !_wasFreeCamActive)
            {
                Vector3 rel = transform.position - target.position;
                float currentDistance = rel.magnitude;
                if (currentDistance > 0.01f)
                {
                    _orbitYaw   = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
                    _orbitPitch = Mathf.Asin(Mathf.Clamp(rel.y / currentDistance, -1f, 1f)) * Mathf.Rad2Deg;
                    _orbitRadius = currentDistance;
                }
                _pressStartPos = transform.position;
                _pressStartRot = transform.rotation;
                _pressT = 0f;
            }
            _wasFreeCamActive = freeCamActive;

            if (freeCamActive)
            {
                // --- ORBIT mode: snap to the exact sphere position each frame ---
                Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                _orbitYaw   += delta.x * freeLookSensitivity;
                _orbitPitch -= delta.y * freeLookSensitivity; // mouse-up looks up
                _orbitPitch  = Mathf.Clamp(_orbitPitch, freeLookPitchMin, freeLookPitchMax);

                // Gradually settle the orbit radius toward the standard chase distance.
                // Starts at the actual camera-to-plane distance captured on RMB press,
                // so there's no snap if the chase camera was lagging at a different distance.
                float targetRadius = Mathf.Max(0.01f, offset.magnitude);
                _orbitRadius = Mathf.Lerp(_orbitRadius, targetRadius, radiusSettleSpeed * Time.deltaTime);

                float yawRad   = _orbitYaw   * Mathf.Deg2Rad;
                float pitchRad = _orbitPitch * Mathf.Deg2Rad;
                Vector3 sphereDir = new Vector3(
                    Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                    Mathf.Sin(pitchRad),
                    Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
                );
                Vector3 orbitPos = target.position + sphereDir * _orbitRadius;
                Quaternion orbitRot = Quaternion.LookRotation(-sphereDir, Vector3.up);

                // Advance the press transition. During the first `pressTransitionDuration`
                // seconds, smoothly blend from the chase pose to the orbit pose so any
                // rotation up-vector change (rolled chase -> level orbit) doesn't snap.
                // After the transition, snap each frame so fast drags trace clean arcs.
                if (_pressT < 1f)
                {
                    _pressT = Mathf.Min(1f, _pressT + Time.deltaTime / Mathf.Max(0.001f, pressTransitionDuration));
                    float eased = Mathf.SmoothStep(0f, 1f, _pressT);
                    transform.position = Vector3.Lerp(_pressStartPos, orbitPos, eased);
                    transform.rotation = Quaternion.Slerp(_pressStartRot, orbitRot, eased);
                }
                else
                {
                    transform.position = orbitPos;
                    transform.rotation = orbitRot;
                }
            }
            else
            {
                // --- CHASE mode: smoothly lerp toward the local-space chase pose ---
                Vector3 chasePos = target.TransformPoint(offset);
                transform.position = Vector3.Lerp(transform.position, chasePos, positionLerp * Time.deltaTime);

                Vector3 lookDir = (target.position - transform.position).normalized;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    Vector3 upRef = Vector3.Lerp(Vector3.up, target.up, rollFollow);
                    Quaternion lookRot = Quaternion.LookRotation(lookDir, upRef);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationLerp * Time.deltaTime);
                }
            }
        }

        public void SetTarget(Transform t)
        {
            target   = t;
            _targetRb = t != null ? t.GetComponent<Rigidbody>() : null;
        }
    }
}
