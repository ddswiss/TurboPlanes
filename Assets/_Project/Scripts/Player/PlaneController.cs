using UnityEngine;
using UnityEngine.InputSystem;
using SkyBrawl.Flight;
using SkyBrawl.Tuning;

namespace SkyBrawl.Player
{
    /// <summary>
    /// Drives a plane using the FlightModel and Unity's new Input System.
    /// Attach to a plane GameObject. Assign a PlaneTuningSO in the inspector.
    ///
    /// CONTROL SCHEME (arcade):
    ///   W/S          -> pitch
    ///   A/D          -> turn (yaw) + visual banking tilt
    ///   Space/LCtrl  -> throttle (speed up / slow down)
    ///   LShift+A/D   -> hard turn (tighter turn radius, ~90 deg visual bank)
    ///   double-tap LShift while holding A/D -> barrel roll
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

        [Header("TEMP TESTING")]
        [Tooltip("If true, normal A/D behaves like Shift+A/D (90 deg bank, dynamic yaw multiplier). Shift+A/D and the barrel roll are disabled while this is on. Toggle off in the Inspector to restore the original behavior.")]
        [SerializeField] private bool _testHardTurnAsDefault = true;

        [Tooltip("Easing exponent for the bank curve. 1 = linear (classic Lerp). 2 = quadratic taper ('fast at start, slower and slower'). Higher = more pronounced asymptote. Applies symmetrically to entry and recovery.")]
        [SerializeField] private float _bankEasingPower = 2f;

        [Tooltip("Minimum angular bank speed (deg/sec). Floor that kicks in when the natural curve speed would otherwise crawl. Prevents the last few degrees from feeling like they never finish.")]
        [SerializeField] private float _bankMinSpeed = 50f;

        private enum FlightMode { Normal, HardTurn, BarrelRoll }

        private FlightState _state;
        private FlightInput _input;
        private Rigidbody _rb;
        private float _currentBankAngle;

        private InputAction _pitchAction;
        private InputAction _yawAction;
        private InputAction _throttleAction;
        private InputAction _hardTurnAction;

        // Mode / barrel-roll state
        private FlightMode _mode = FlightMode.Normal;
        private float _lastShiftPressTime = -999f;
        private float _barrelRollElapsed;
        private float _barrelRollDirection; // +1 = roll left (A), -1 = roll right (D)

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

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Flight", throwIfNotFound: true);
                _pitchAction    = map.FindAction("Pitch", true);
                _yawAction      = map.FindAction("Yaw", true);
                _throttleAction = map.FindAction("Throttle", true);
                _hardTurnAction = map.FindAction("HardTurnModifier", false);
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
            bool hardTurnHeld = _hardTurnAction != null && _hardTurnAction.IsPressed();
            bool shiftPressedThisFrame = _hardTurnAction != null && _hardTurnAction.WasPressedThisFrame();

            // --- Barrel roll detection (double-tap Shift while yaw input is non-zero) ---
            // TEMP: disabled while _testHardTurnAsDefault is true (Shift is unbound during the test).
            if (!_testHardTurnAsDefault && _mode != FlightMode.BarrelRoll && shiftPressedThisFrame)
            {
                float dt = Time.time - _lastShiftPressTime;
                if (dt <= tuning.doubleTapWindow && Mathf.Abs(rawYaw) > 0.01f)
                {
                    // Trigger barrel roll
                    _mode = FlightMode.BarrelRoll;
                    _barrelRollElapsed = 0f;
                    // A => negative yaw => roll left => +360 in Z
                    // D => positive yaw => roll right => -360 in Z
                    _barrelRollDirection = -Mathf.Sign(rawYaw);
                    // Reset so a third tap doesn't immediately re-trigger after this roll
                    _lastShiftPressTime = -999f;
                }
                else
                {
                    _lastShiftPressTime = Time.time;
                }
            }

            // --- Apply inputs depending on mode ---
            _input.pitch    = rawPitch;
            _input.roll     = 0f; // physics roll disabled
            _input.throttle = rawThrottle;

            if (_mode == FlightMode.BarrelRoll)
            {
                // Suppress yaw entirely so heading does not change while rolling.
                _input.yaw = 0f;

                _barrelRollElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_barrelRollElapsed / Mathf.Max(0.001f, tuning.barrelRollDuration));
                float rollAngle = Mathf.Lerp(0f, 360f * _barrelRollDirection, t);

                // The roll animator owns visualRoot during the barrel roll.
                _currentBankAngle = rollAngle;
                if (visualRoot != null)
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);

                if (_barrelRollElapsed >= tuning.barrelRollDuration)
                {
                    _mode = FlightMode.Normal;
                    // Reset to 0 so the normal banking lerp resumes from a sane angle
                    // (avoids easing back from a 360-equivalent). Mathf.Repeat(360, 360) = 0.
                    _currentBankAngle = 0f;
                }
            }
            else
            {
                // Hard turn: Shift held AND yaw input present.
                // TEMP: when _testHardTurnAsDefault is true, any yaw input counts as hard turn (Shift unbound).
                bool inHardTurn = (_testHardTurnAsDefault || hardTurnHeld) && Mathf.Abs(rawYaw) > 0.01f;
                _mode = inHardTurn ? FlightMode.HardTurn : FlightMode.Normal;

                if (inHardTurn)
                {
                    // Tie yaw amplification to current visual bank progress so the plane
                    // must physically lerp through neutral before it can hard-bank the
                    // other direction. At |bank| == hardTurnBankAngle the multiplier is
                    // full strength; at bank == 0 it's 1x (no amplification).
                    float bankProgress = Mathf.Clamp01(
                        Mathf.Abs(_currentBankAngle) / Mathf.Max(0.001f, tuning.hardTurnBankAngle));
                    float dynamicMultiplier = Mathf.Lerp(1f, tuning.hardTurnYawMultiplier, bankProgress);
                    _input.yaw = rawYaw * dynamicMultiplier;
                }
                else
                {
                    _input.yaw = rawYaw;
                }

                UpdateVisualBanking(rawYaw, inHardTurn);
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
        /// In hard-turn mode the target bank is +/- hardTurnBankAngle and the lerp is faster.
        /// </summary>
        private void UpdateVisualBanking(float rawYaw, bool inHardTurn)
        {
            if (visualRoot == null) return;

            float targetBank;
            float smoothing;
            if (_testHardTurnAsDefault)
            {
                // TEMP test mode: always use hard-turn params so entry AND recovery share the same curve.
                targetBank = -rawYaw * tuning.hardTurnBankAngle;
                smoothing  = tuning.hardTurnBankSmoothing;
            }
            else if (inHardTurn)
            {
                // Sign of yaw drives direction. Negative so right turn (D, +yaw) tilts right (-Z).
                targetBank = -Mathf.Sign(rawYaw) * tuning.hardTurnBankAngle;
                smoothing  = tuning.hardTurnBankSmoothing;
            }
            else
            {
                targetBank = -rawYaw * maxBankAngle;
                smoothing  = bankSmoothing;
            }

            // Power-curve approach: angularSpeed = maxSpeed * (distance/maxAngle)^easing.
            // easing = 1 mimics Lerp; easing > 1 amplifies the "fast at start, slower and slower
            // toward target" feel. Same curve applies symmetrically to entry (-> +/-bankAngle)
            // and recovery (-> 0).
            float remaining   = targetBank - _currentBankAngle;
            float distNorm    = Mathf.Clamp01(Mathf.Abs(remaining) / Mathf.Max(1f, tuning.hardTurnBankAngle));
            float speedScale  = Mathf.Pow(distNorm, _bankEasingPower);
            float maxSpeedDeg = smoothing * tuning.hardTurnBankAngle;
            float curveSpeed  = maxSpeedDeg * speedScale;
            float speedDeg    = Mathf.Max(curveSpeed, _bankMinSpeed);
            float frameDelta  = speedDeg * Time.deltaTime;
            _currentBankAngle = Mathf.MoveTowards(_currentBankAngle, targetBank, frameDelta);

            visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);
        }
    }
}
