using UnityEngine;

namespace SkyBrawl.Tuning
{
    /// <summary>
    /// All values that define how a plane FEELS to fly.
    /// Create assets via: Assets > Create > SkyBrawl > Plane Tuning
    /// You will tweak these constantly during play mode. That is the point.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaneTuning_New", menuName = "SkyBrawl/Plane Tuning", order = 0)]
    public class PlaneTuningSO : ScriptableObject
    {
        [Header("Throttle / Speed")]
        [Tooltip("Cruise speed when throttle is at neutral.")]
        public float cruiseSpeed = 40f;
        [Tooltip("Max speed when throttle is held forward.")]
        public float maxSpeed = 80f;
        [Tooltip("Min speed when throttle is held back. Below this, plane stalls.")]
        public float minSpeed = 15f;
        [Tooltip("How fast the plane accelerates toward target speed (units/sec^2).")]
        public float throttleAccel = 25f;

        [Header("Pitch (nose up/down)")]
        [Tooltip("Max degrees per second the plane pitches.")]
        public float pitchRate = 60f;
        [Tooltip("How quickly pitch input ramps up (1 = instant, lower = floatier).")]
        [Range(0.1f, 1f)] public float pitchResponsiveness = 0.5f;

        [Header("Yaw (nose left/right)")]
        [Tooltip("Max degrees per second the plane yaws.")]
        public float yawRate = 25f;
        [Range(0.1f, 1f)] public float yawResponsiveness = 0.4f;

        [Header("Roll (banking)")]
        [Tooltip("Max degrees per second the plane rolls.")]
        public float rollRate = 120f;
        [Range(0.1f, 1f)] public float rollResponsiveness = 0.7f;

        [Header("Banking-into-turn")]
        [Tooltip("How much yaw is induced by roll. Higher = more arcade-y, planes turn by tilting.")]
        [Range(0f, 2f)] public float bankTurnFactor = 1f;
        [Tooltip("How much the plane auto-levels its roll when no input. 0 = none, 1 = strong.")]
        [Range(0f, 1f)] public float rollAutoLevel = 0.15f;

        [Header("Dive boost (nose-down speed gain)")]
        [Tooltip("Pitch below horizon (degrees) at which the dive boost starts contributing. Below this, no boost is applied.")]
        public float diveBoostMinAngle = 30f;
        [Tooltip("Pitch below horizon (degrees) below diveBoostMinAngle at which the deadzone ends and level decay starts. Between this and diveBoostMinAngle the bonus is preserved (no gain, no decay). Below this (toward level), level decay applies.")]
        public float diveBoostDeadzoneEnd = 15f;
        [Tooltip("Pitch below horizon (degrees) at which the dive boost reaches its maximum value. Linear interpolation between min and max angles.")]
        public float diveBoostMaxAngle = 90f;
        [Tooltip("Per-second speed gain (units/sec) added to currentSpeed at diveBoostMinAngle.")]
        public float diveBoostAtMin = 0.8f;
        [Tooltip("Per-second speed gain (units/sec) added to currentSpeed at diveBoostMaxAngle (straight down). Speed is clamped to maxSpeed.")]
        public float diveBoostAtMax = 15f;
        [Tooltip("Asymptote for accumulated diveBonusSpeed (units). Each frame the gain is scaled by (1 - bonus/asymptote), so the bonus approaches this value exponentially but never exceeds it. 50 = plane caps out at +50 units of dive speed.")]
        public float diveBoostAsymptote = 50f;
        [Tooltip("Flat decay rate (units/sec) of accumulated diveBonusSpeed when flying level or nose slightly down (not in the dive-boost range). 0 = bonus is permanent.")]
        public float diveBoostDecay = 3f;
        [Tooltip("Flat decay rate (units/sec) when the nose is pointed straight up (90 degrees above horizon). Decay interpolates linearly between diveBoostDecay (level) and this value (straight up).")]
        public float diveBoostDecayClimbMax = 15f;

        [Header("Lift / Gravity feel")]
        [Tooltip("Gravity strength. 0 = arcade floaty (no falling), 9.81 = realistic.")]
        public float gravity = 4f;
        [Tooltip("How much forward speed counteracts gravity. 1 = full lift at cruise speed.")]
        [Range(0f, 2f)] public float liftFromSpeed = 1f;

        [Header("Drag")]
        [Tooltip("How quickly sideways/vertical velocity bleeds off. Higher = tighter, less drift.")]
        [Range(0f, 10f)] public float lateralDrag = 4f;

        [Header("Hard Turn (Shift + A/D)")]
        [Tooltip("Multiplier applied to yaw input while HardTurnModifier (Shift) is held. Tighter turn radius.")]
        public float hardTurnYawMultiplier = 2.5f;
        [Tooltip("Visual bank angle in degrees while in hard-turn mode. Cosmetic only.")]
        public float hardTurnBankAngle = 90f;
        [Tooltip("How quickly the visual snaps to the hard-turn bank angle. Higher = snappier than normal banking.")]
        public float hardTurnBankSmoothing = 12f;

        [Header("Barrel Roll (double-tap Shift while turning)")]
        [Tooltip("Time in seconds for one full 360-degree barrel roll.")]
        public float barrelRollDuration = 0.8f;
        [Tooltip("Max time in seconds between Shift presses to count as a double-tap and trigger a barrel roll.")]
        public float doubleTapWindow = 0.3f;
    }
}
