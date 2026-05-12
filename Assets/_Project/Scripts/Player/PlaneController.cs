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
        [SerializeField] private float maxBankAngle = 90f;
        [Tooltip("How quickly the visual tilt eases in/out. Higher = snappier. Combined with the bank curve below.")]
        [SerializeField] private float bankSmoothing = 4f;

        [Header("Bank Curve")]
        [Tooltip("Easing exponent for the bank curve. 1 = linear (classic Lerp). 2 = quadratic taper ('fast at start, slower and slower'). Higher = more pronounced asymptote. Applies symmetrically to entry and recovery.")]
        [SerializeField] private float _bankEasingPower = 2f;

        [Tooltip("Minimum angular bank speed (deg/sec). Floor that kicks in when the natural curve speed would otherwise crawl. Prevents the last few degrees from feeling like they never finish.")]
        [SerializeField] private float _bankMinSpeed = 50f;

        [Header("Boost Fuel (Space throttle)")]
        [Tooltip("Max boost fuel. UI bar shows this as 100%.")]
        [SerializeField] private float maxBoostFuel = 100f;
        [Tooltip("Boost fuel consumed per second while Space is held and throttle is positive. With maxBoostFuel=100, default 50/s = 2s of full boost. GameManager overrides at runtime based on Boost Duration upgrade.")]
        [SerializeField] private float boostConsumeRate = 50f;
        [Tooltip("Boost fuel regenerated per second when not boosting. With maxBoostFuel=100, default 20/s = 5s to refill. GameManager overrides at runtime based on Boost Refill upgrade.")]
        [SerializeField] private float boostRegenRate = 20f;

        [Header("Steering lock (low-speed safety)")]
        [Tooltip("Below this speed (game units), pitch + yaw input are ignored and the plane auto-levels toward horizontal. Above this speed, full pilot control. Useful for landing/taxi where WASD shouldn't fight the runway.")]
        [SerializeField] private float steeringLockBelowSpeed = 15f;
        [Tooltip("How fast the plane levels itself toward horizontal while steering is locked. Higher = snappier.")]
        [SerializeField] private float autoLevelSpeed = 4f;

        [Header("Continuous Collision (anti-tunnel)")]
        [Tooltip("SphereCast radius used each FixedUpdate to detect terrain in front of the plane before MovePosition teleports through thin walls. Should roughly match the PlaneCrashHandler hull radius. 0 = disabled.")]
        [SerializeField] private float ccdRadius = 2.5f;
        [Tooltip("How far past first contact the plane advances so PlaneCrashHandler.OnTriggerStay can still detect the overlap. Must be < ccdRadius so the plane center stays outside the wall (otherwise OnTriggerStay's inside-collider early-out fires).")]
        [SerializeField] private float ccdOverlap = 0.3f;

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
        private float _barrelRollDirection;  // +1 = roll left (A), -1 = roll right (D)
        private float _barrelRollStartAngle; // bank angle when roll began — roll continues from here

        // Boost fuel runtime state
        private float _currentBoostFuel;
        public float CurrentBoostFuel => _currentBoostFuel;
        public float MaxBoostFuel    => maxBoostFuel;
        public float BoostFuelRatio01 => maxBoostFuel > 0 ? Mathf.Clamp01(_currentBoostFuel / maxBoostFuel) : 0f;

        // Public setter so GameManager can apply the hangar's boost-drain upgrade at spawn time.
        public float BoostConsumeRate { get => boostConsumeRate; set => boostConsumeRate = value; }
        public float BoostRegenRate   { get => boostRegenRate;   set => boostRegenRate = value; }

        // Base values captured at Awake time so GameManager can recompute current values
        // as (base * multiplier) every time the player tweaks a hangar slider — instead of
        // compounding multipliers onto already-modified state.
        private float _baseCruiseSpeed, _baseMaxSpeed, _baseMinSpeed, _baseBoostConsumeRate;
        public float BaseCruiseSpeed      => _baseCruiseSpeed;
        public float BaseMaxSpeed         => _baseMaxSpeed;
        public float BaseMinSpeed         => _baseMinSpeed;
        public float BaseBoostConsumeRate => _baseBoostConsumeRate;

        // External yaw bias (e.g. from MapBoundary auto-return). Added to player input.
        [HideInInspector] public float externalYawBias;

        /// <summary>True when pilot input (pitch + yaw) is being suppressed because the plane
        /// is below the steering-lock speed threshold. Throttle still responds.</summary>
        public bool IsSteeringLocked => _state.currentSpeed < steeringLockBelowSpeed;

        /// <summary>While true, PlaneCrashHandler skips terrain collisions for this plane.
        /// Used by LandingZone so its Y-snap doesn't fight with terrain push-out.</summary>
        [HideInInspector] public bool IgnoreTerrainCollision;

        public FlightState State => _state;
        public PlaneTuningSO Tuning => tuning;

        /// <summary>Visual bank/roll angle in degrees (cosmetic — the flight model itself
        /// doesn't roll, banking is shown by tilting the visual mesh). Positive = banked
        /// one direction, negative = the other. LandingZone reads this for its tilt crash.</summary>
        public float CurrentBankAngle => _currentBankAngle;

        /// <summary>While true, all flight physics & input are paused. Set by LandingZone
        /// when the plane has stopped on the runway. Cleared on takeoff.</summary>
        public bool IsLanded { get; set; }

        /// <summary>Directly overwrite currentSpeed (skips throttle ramp). Used by LandingZone
        /// to freeze the plane at 0 km/h on touchdown and reset to 0 before takeoff.</summary>
        public void SetCurrentSpeed(float v) { _state.currentSpeed = v; }

        /// <summary>Hard-snap the plane to a pose and zero its velocity. Used by LandingZone
        /// to glue the plane to the runway surface during taxi/takeoff.</summary>
        public void SnapTo(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;
            _state.position = pos;
            _state.rotation = rot;
            _state.velocity = Vector3.zero;
            if (_rb != null)
            {
                _rb.position = pos;
                _rb.rotation = rot;  // sync rb rotation too — otherwise interpolation lags from a stale rb.rotation when FixedUpdate is suppressed (e.g. IsLanded=true on first spawn)
            }
        }

        /// <summary>Constrain the plane's Y to a runway surface without touching XZ — preserves
        /// forward motion, just prevents the plane from sinking through the strip during taxi.
        /// Zeros the vertical velocity component so gravity doesn't accumulate against the snap.</summary>
        public void ConstrainGroundY(float y)
        {
            _state.position.y = y;
            _state.velocity.y = 0f;
            var tp = transform.position; tp.y = y; transform.position = tp;
            if (_rb != null)
            {
                var rp = _rb.position; rp.y = y; _rb.position = rp;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Clone the tuning SO so runtime tweaks (e.g. speed multiplier from the
            // main-menu slider) don't permanently mutate the project asset.
            if (tuning != null)
            {
                tuning = Instantiate(tuning);
                tuning.name = tuning.name + " (Runtime)";
            }

            // Capture base values so GameManager can recompute current = base * multiplier
            // every time the hangar sliders change.
            if (tuning != null)
            {
                _baseCruiseSpeed = tuning.cruiseSpeed;
                _baseMaxSpeed    = tuning.maxSpeed;
                _baseMinSpeed    = tuning.minSpeed;
            }
            _baseBoostConsumeRate = boostConsumeRate;

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
            if (IsLanded) return;  // Frozen on runway — input + physics suspended

            // Read raw inputs
            float rawPitch    = _pitchAction    != null ? _pitchAction.ReadValue<float>()    : 0f;
            float rawYaw      = _yawAction      != null ? _yawAction.ReadValue<float>()      : 0f;
            float rawThrottle = _throttleAction != null ? _throttleAction.ReadValue<float>() : 0f;
            bool shiftPressedThisFrame = _rollTriggerAction != null && _rollTriggerAction.WasPressedThisFrame();

            // Steering lock kicks in automatically below the speed threshold (e.g. on the
            // runway during taxi). Throttle still works so the player can take off / brake.
            if (IsSteeringLocked) { rawPitch = 0f; rawYaw = 0f; shiftPressedThisFrame = false; }

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
                // Capture current visual bank so the roll continues from where we are
                // (no snap to 0 when the player triggers a roll while already banking).
                _barrelRollStartAngle = _currentBankAngle;
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
                float rollOffset = Mathf.Lerp(0f, 360f * _barrelRollDirection, t);

                // Continue the rotation FROM whatever bank we were at when the roll started.
                _currentBankAngle = _barrelRollStartAngle + rollOffset;
                if (visualRoot != null)
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);

                if (_barrelRollElapsed >= tuning.barrelRollDuration)
                {
                    _mode = FlightMode.Normal;
                    // End at the start angle (a full 360 lands us visually back where we began).
                    // This avoids a visible jump if the player is still holding the same direction.
                    _currentBankAngle = _barrelRollStartAngle;
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
            if (IsLanded) return;  // Frozen on runway — input + physics suspended

            Vector3 prevPos = _state.position;
            _state = FlightModel.Step(_state, _input, tuning, Time.fixedDeltaTime);

            // --- Auto-level when steering is locked (low speed) ---
            // Slerp _state.rotation toward a horizontal pose: zero pitch + roll, preserve
            // current yaw. Above the lock threshold this branch is skipped so normal pilot
            // input wins.
            if (IsSteeringLocked)
            {
                Vector3 fwd = _state.rotation * Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f)
                {
                    Quaternion levelRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                    _state.rotation = Quaternion.Slerp(_state.rotation, levelRot, autoLevelSpeed * Time.fixedDeltaTime);
                }
            }

            // --- Continuous Collision Detection (anti-tunnel) ---
            // At high speeds the per-step movement can exceed the trigger sphere diameter,
            // causing the plane to teleport across thin colliders without OnTriggerStay firing.
            // Cast a sphere along the intended move; if anything blocks, clamp the new
            // position to just past the contact point so the hull trigger still overlaps
            // the static collider and PlaneCrashHandler can slide/crash from there.
            if (ccdRadius > 0f)
            {
                Vector3 delta = _state.position - prevPos;
                float dist = delta.magnitude;
                if (dist > 0.001f)
                {
                    Vector3 dir = delta / dist;
                    if (Physics.SphereCast(prevPos, ccdRadius, dir, out RaycastHit hit, dist,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        float clampedAdvance = Mathf.Min(hit.distance + ccdOverlap, dist);
                        clampedAdvance = Mathf.Max(0f, clampedAdvance);
                        _state.position = prevPos + dir * clampedAdvance;
                    }
                }
            }

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
