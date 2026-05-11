using UnityEngine;

namespace SkyBrawl.Tuning
{
    /// <summary>
    /// Tuning for the manga-style radial speed-lines screen overlay.
    /// All values that control how the effect FEELS — visual density, fade
    /// curves, response time. Create assets via:
    /// Assets > Create > SkyBrawl > Speed Lines Tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeedLinesTuning_New", menuName = "SkyBrawl/Speed Lines Tuning", order = 10)]
    public class SpeedLinesTuningSO : ScriptableObject
    {
        [Header("Intensity ramp")]
        [Tooltip("Peak alpha multiplier when speed reaches max. 1 = full white, 0.85 = slightly softened.")]
        [Range(0f, 1f)] public float maxIntensity = 0.85f;

        [Tooltip("Overspeed deadzone (fraction of cruise→max range). Below this, no lines show. Keeps cruising clean.")]
        [Range(0f, 0.5f)] public float threshold = 0.05f;

        [Tooltip("Time in seconds for displayed intensity to catch up to target. ~0.2 feels natural.")]
        [Range(0.01f, 1f)] public float smoothTime = 0.2f;

        [Tooltip("Power applied to the cruise→max ratio before driving intensity. >1 makes lines stay subtle until near top speed; <1 makes them ramp earlier.")]
        [Range(0.25f, 4f)] public float ramp = 1.0f;
    }
}
