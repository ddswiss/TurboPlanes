using UnityEngine;
using SkyBrawl.Core;

namespace SkyBrawl.Hangar
{
    /// <summary>
    /// Place on a trigger collider on the airport runway. Opens the hangar UI when the
    /// player plane enters at low speed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HangarTrigger : MonoBehaviour
    {
        [SerializeField] private float maxEntrySpeed = 30f;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var rb = other.attachedRigidbody;
            if (rb != null && rb.linearVelocity.magnitude > maxEntrySpeed) return;
            if (GameManager.Instance != null) GameManager.Instance.OpenHangar();
        }
    }
}
