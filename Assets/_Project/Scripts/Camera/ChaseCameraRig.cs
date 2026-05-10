using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyBrawl.CameraRig
{
    /// <summary>
    /// Chase camera that smoothly follows the target plane.
    ///
    /// Hold RIGHT MOUSE BUTTON to free-look — mouse delta orbits the camera around the
    /// target without affecting plane controls (WASD still flies the plane). Release RMB
    /// to return to the chase pose. Cursor visibility is preserved during free-look.
    /// </summary>
    public class ChaseCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Position")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -10f);
        [SerializeField] private float positionLerp = 5f;

        [Header("Rotation")]
        [SerializeField] private float rotationLerp = 4f;
        [Tooltip("How much the camera leans into the plane's roll. 0 = camera stays upright, 1 = matches plane.")]
        [Range(0f, 1f)] public float rollFollow = 0.4f;

        [Header("Free Look (RMB)")]
        [Tooltip("Mouse sensitivity in degrees per pixel of mouse delta.")]
        [SerializeField] private float freeLookSensitivity = 0.25f;
        [Tooltip("Pitch clamp for free look (degrees). Prevents flipping over.")]
        [SerializeField] private float freeLookPitchMin = -75f;
        [SerializeField] private float freeLookPitchMax =  75f;
        [Tooltip("How quickly free-look offsets decay back to 0 after releasing RMB.")]
        [SerializeField] private float freeLookReturnLerp = 6f;

        // Accumulated free-look offsets (relative to chase pose), in degrees.
        private float _freeYaw;
        private float _freePitch;
        private bool _wasRightHeld;

        private void LateUpdate()
        {
            if (target == null) return;

            bool rmbHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;

            if (rmbHeld)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _freeYaw   += delta.x * freeLookSensitivity;
                _freePitch -= delta.y * freeLookSensitivity; // inverted: pull down = look up
                _freePitch = Mathf.Clamp(_freePitch, freeLookPitchMin, freeLookPitchMax);
            }
            else
            {
                // Smoothly decay free-look offsets back to neutral
                float k = freeLookReturnLerp * Time.deltaTime;
                _freeYaw   = Mathf.Lerp(_freeYaw,   0f, k);
                _freePitch = Mathf.Lerp(_freePitch, 0f, k);
            }
            _wasRightHeld = rmbHeld;

            // Compute chase pose (in target's local space) and then layer the free-look orbit on top.
            // Step 1: build a base rotation that follows yaw/pitch of the target plus partial roll.
            Vector3 fwd = target.forward;
            Quaternion baseRot = Quaternion.LookRotation(fwd, Vector3.Lerp(Vector3.up, target.up, rollFollow));

            // Step 2: apply user yaw/pitch as an additional rotation around the target.
            Quaternion freeRot = Quaternion.AngleAxis(_freeYaw, Vector3.up) * Quaternion.AngleAxis(_freePitch, Vector3.right);
            // The free rotation is applied in the target's frame so it orbits the plane consistently
            Quaternion combinedRot = baseRot * freeRot;

            // Step 3: compute the desired camera position as offset rotated by the combined rotation, around the target.
            Vector3 worldOffset = combinedRot * offset;
            Vector3 desiredPos = target.position + worldOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, positionLerp * Time.deltaTime);

            // Step 4: face the target (so user free-look keeps the plane in view).
            Vector3 lookDir = (target.position - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(lookDir, Vector3.Lerp(Vector3.up, target.up, rollFollow));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationLerp * Time.deltaTime);
            }
        }

        public void SetTarget(Transform t) => target = t;
    }
}
