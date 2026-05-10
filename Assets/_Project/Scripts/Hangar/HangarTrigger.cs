using UnityEngine;
using SkyBrawl.Core;

namespace SkyBrawl.Hangar
{
    /// <summary>
    /// Place on a trigger collider on the airport runway. Opens the hangar UI when the
    /// player plane enters. Arcade-friendly: speed check is generous and there's a
    /// short cooldown after closing so the trigger doesn't re-fire instantly.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HangarTrigger : MonoBehaviour
    {
        [SerializeField] private float maxEntrySpeed = 200f;
        [SerializeField] private float reopenCooldown = 3f;

        private float _lastClosedTime = -999f;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryOpen(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Cover the case where the player enters at high speed and physics ticks
            // skip the enter callback, or where the cooldown was active on first enter.
            TryOpen(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) _lastClosedTime = Time.time;
        }

        private void TryOpen(Collider other)
        {
            if (Time.time - _lastClosedTime < reopenCooldown) return;
            if (!other.CompareTag("Player")) return;
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic && rb.linearVelocity.magnitude > maxEntrySpeed) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.IsHangarOpen) return;
            gm.OpenHangar();
        }
    }
}
