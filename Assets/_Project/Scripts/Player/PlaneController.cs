using UnityEngine;
using UnityEngine.InputSystem;
using SkyBrawl.Flight;
using SkyBrawl.Tuning;

namespace SkyBrawl.Player
{
    /// <summary>
    /// Drives a plane using the FlightModel and Unity's new Input System.
    ///
    /// CONTROL SCHEME (arcade):
    ///   W/S          -> pitch
    ///   A/D          -> turn (yaw) + visual banking tilt to +/- maxBankAngle
    ///   Space/LCtrl  -> throttle (speed up / slow down). Space consumes boost fuel.
    ///   Shift+A/D    -> instant barrel roll left/right (heading locked, plane goes straight)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlaneController : MonoBehaviour
    {
        [SerializeField] private PlaneTuningSO tuning;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Visual Banking (cosmetic only)")]
        [Tooltip("The visual mesh of the plane. Will be tilted on the Z axis when turning. Assign a child GameObject here.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Maximum bank angle in degrees when turning at full input.")]
        [SerializeField] private float maxBankAngle = 45f;
        [Tooltip("How quickly the visual tilt eases in/out. Higher = snappier.")]
        [SerializeField] private float bankSmoothing = 6f;

        [Header("Bank Curve")]
        [Tooltip("Easing exponent for the bank curve. 1 = linear (classic Lerp). 2 = quadratic taper ('fast at start, slower and slower'). Higher = more pronounced asymptote. Applies symmetrically to entry and recovery.")]
        [SerializeField] private float _bankEasingPower = 2f;

        [Tooltip("Minimum angular bank speed (deg/sec). Floor that kicks in when the natural curve speed would otherwise crawl. Prevents the last few degrees from feeling like they never finish.")]
        [SerializeField] private float _bankMinSpeed = 50f;

        [Header("Boost Fuel (Space throttle)")]
        [Tooltip("Max boost fuel. UI bar shows this as 100%.")]
        [SerializeField] private float maxBoostFuel = 100f;
        [Tooltip("Boost fuel consumed per second while Space is held and throttle is positive.")]
        [SerializeField] private float boostConsumeRate = 40f;
        [Tooltip("Boost fuel regenerated per second when not boosting.")]
        [SerializeField] private float boostRegenRate = 18f;

        private enum FlightMode { Normal, BarrelRoll }

        private FlightState _state;
        private FlightInput _input;
        private Rigidbody _rb;
        private float _currentBankAngle;

        private InputAction _pitchAction;
        private InputAction _yawAction;
        private InputAction _throttleAction;
        private InputAction _rollTriggerAction; // Shift — triggers barrel roll when pressed with yaw

        private FlightMode _mode = FlightMode.Normal;
        private float _barrelRollElapsed;
        private float _barrelRollDirection; // +1 = roll left (A), -1 = roll right (D)

        // Boost fuel runtime state
        private float _currentBoostFuel;
        public float CurrentBoostFuel => _currentBoostFuel;
        public float MaxBoostFuel    => maxBoostFuel;
        public float BoostFuelRatio01 => maxBoostFuel > 0 ? Mathf.Clamp01(_currentBoostFuel / maxBoostFuel) : 0f;

        // External yaw bias (e.g. from MapBoundary auto-return). Added to player input.
        [HideInInspector] public float externalYawBias;

        public FlightState State => _state;
        public PlaneTuningSO Tuning => tuning;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _state.position = transform.position;
            _state.rotation = transform.rotation;
            _state.currentSpeed = tuning != null ? tuning.cruiseSpeed : 40f;

            _currentBoostFuel = maxBoostFuel;

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Flight", throwIfNotFound: true);
                _pitchAction    = map.FindAction("Pitch", true);
                _yawAction      = map.FindAction("Yaw", true);
                _throttleAction = map.FindAction("Throttle", true);
                _rollTriggerAction = map.FindAction("HardTurnModifier", false); // reused as barrel-roll trigger
                map.Enable();
            }
        }

        private void Update()
        {
            if (tuning == null) return;

            // Read raw inputs
            float rawPitch    = _pitchAction    != null ? _pitchAction.ReadValue<float>()    : 0f;
            float rawYaw      = _yawAction      != null ? _yawAction.ReadValue<float>()      : 0f;
            float rawThrottle = _throttleAction != null ? _throttleAction.ReadValue<float>() : 0f;
            bool shiftPressedThisFrame = _rollTriggerAction != null && _rollTriggerAction.WasPressedThisFrame();

            // --- Boost fuel: consume while positive throttle, regen otherwise ---
            //     If fuel runs out, the positive throttle is clamped to zero.
            float effectiveThrottle = rawThrottle;
            if (rawThrottle > 0f)
            {
                if (_currentBoostFuel > 0f)
                {
                    _currentBoostFuel = Mathf.Max(0f, _currentBoostFuel - boostConsumeRate * Time.deltaTime);
                }
                else
                {
                    effectiveThrottle = 0f; // no fuel -> no boost; falls back to cruise
                }
            }
            else
            {
                _currentBoostFuel = Mathf.Min(maxBoostFuel, _currentBoostFuel + boostRegenRate * Time.deltaTime);
            }

            // --- Barrel roll trigger: Shift pressed THIS FRAME while yaw is held ---
            if (_mode != FlightMode.BarrelRoll && shiftPressedThisFrame && Mathf.Abs(rawYaw) > 0.01f)
            {
                _mode = FlightMode.BarrelRoll;
                _barrelRollElapsed = 0f;
                // A => negative yaw => roll left  (+360 in Z)
                // D => positive yaw => roll right (-360 in Z)
                _barrelRollDirection = -Mathf.Sign(rawYaw);
            }

            // --- Apply inputs depending on mode ---
            _input.pitch    = rawPitch;
            _input.roll     = 0f; // physics roll disabled (visual only)
            _input.throttle = effectiveThrottle;

            if (_mode == FlightMode.BarrelRoll)
            {
                // Suppress yaw entirely so heading does not change while rolling.
                _input.yaw = 0f;

                _barrelRollElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_barrelRollElapsed / Mathf.Max(0.001f, tuning.barrelRollDuration));
                float rollAngle = Mathf.Lerp(0f, 360f * _barrelRollDirection, t);

                _currentBankAngle = rollAngle;
                if (visualRoot != null)
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);

                if (_barrelRollElapsed >= tuning.barrelRollDuration)
                {
                    _mode = FlightMode.Normal;
                    _currentBankAngle = 0f; // reset so the normal lerp resumes cleanly
                }
            }
            else
            {
                _input.yaw = rawYaw + externalYawBias;
                UpdateVisualBanking(rawYaw);
            }
        }

        private void FixedUpdate()
        {
            if (tuning == null) return;

            _state = FlightModel.Step(_state, _input, tuning, Time.fixedDeltaTime);

            _rb.MovePosition(_state.position);
            _rb.MoveRotation(_state.rotation);
        }

        /// <summary>
        /// Tilts the visual mesh when turning. Pure cosmetic — does not affect physics.
        /// Uses a power-curve approach so the bank lerps fast at the start and tapers
        /// near the target, with a minimum speed floor to avoid an infinite asymptote.
        /// </summary>
        private void UpdateVisualBanking(float rawYaw)
        {
            if (visualRoot == null) return;

            float targetBank = -rawYaw * maxBankAngle;
            float smoothing  = bankSmoothing;

            float remaining   = targetBank - _currentBankAngle;
            float distNorm    = Mathf.Clamp01(Mathf.Abs(remaining) / Mathf.Max(1f, maxBankAngle));
            float speedScale  = Mathf.Pow(distNorm, _bankEasingPower);
            float maxSpeedDeg = smoothing * maxBankAngle;
            float curveSpeed  = maxSpeedDeg * speedScale;
            float speedDeg    = Mathf.Max(curveSpeed, _bankMinSpeed);
            float frameDelta  = speedDeg * Time.deltaTime;
            _currentBankAngle = Mathf.MoveTowards(_currentBankAngle, targetBank, frameDelta);

            visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);
        }

        /// <summary>Refill boost fuel to max. Called on respawn / map enter.</summary>
        public void RefillBoostFuel() => _currentBoostFuel = maxBoostFuel;

        /// <summary>Sync the internal FlightState position to a new world position. Used by
        /// the crash handler when it nudges the plane out of a wall during a slide so the
        /// next FixedUpdate doesn't snap us back inside.</summary>
        public void SyncStatePosition(Vector3 worldPos) => _state.position = worldPos;

        /// <summary>Fully reset the flight state and place the plane at a new pose. Called
        /// by the crash handler after a crash to respawn the plane at the airport.</summary>
        public void RespawnAt(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;
            _state.position = pos;
            _state.rotation = rot;
            _state.velocity = Vector3.zero;
            _state.currentSpeed = tuning != null ? tuning.cruiseSpeed : 40f;
            _state.smoothedPitch = 0f;
            _state.smoothedYaw = 0f;
            _state.smoothedRoll = 0f;
            _currentBankAngle = 0f;
            _mode = FlightMode.Normal;
            _barrelRollElapsed = 0f;
            externalYawBias = 0f;
            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
            RefillBoostFuel();
        }
    }
}
