using UnityEngine;

namespace SkyBrawl.CameraRig
{
    /// <summary>
    /// Simple chase camera. Use this until you set up Cinemachine properly.
    /// Once you're happy with the feel, port these values to a CinemachineFollow + CinemachineRotationComposer.
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

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPos = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPos, positionLerp * Time.deltaTime);

            // Build a target rotation that follows yaw/pitch fully but only partially follows roll
            Vector3 fwd = target.forward;
            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.Lerp(Vector3.up, target.up, rollFollow));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
        }

        public void SetTarget(Transform t) => target = t;
    }
}
