using UnityEngine;
using SkyBrawl.Tuning;

namespace SkyBrawl.Flight
{
    /// <summary>
    /// Pure flight simulation. NO MonoBehaviour, NO Unity scene dependencies.
    /// Takes input + state, produces next state. This is what makes the flight model
    /// (a) testable, (b) deterministic, (c) usable identically on client and server.
    ///
    /// Arcade control model:
    ///   - Yaw rotates around the WORLD up axis (so pressing turn-left always turns left
    ///     from the player's POV, regardless of pitch).
    ///   - Pitch rotates around the plane's LOCAL right axis.
    ///   - Roll is unused (purely cosmetic banking is handled in PlaneController).
    /// This avoids gimbal lock at steep dive/climb angles.
    /// </summary>
    public struct FlightInput
    {
        public float pitch;     // -1 (nose up) .. +1 (nose down)
        public float yaw;       // -1 (left) .. +1 (right)
        public float roll;      // unused in arcade mode, kept for future
        public float throttle;  // -1 (slow down) .. +1 (speed up)
    }

    public struct FlightState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float currentSpeed;

        // smoothed input (for responsiveness)
        public float smoothedPitch;
        public float smoothedYaw;
        public float smoothedRoll;
    }

    public static class FlightModel
    {
        public static FlightState Step(FlightState state, FlightInput input, PlaneTuningSO t, float dt)
        {
            // Initialize rotation if first step
            if (state.rotation.x == 0f && state.rotation.y == 0f && state.rotation.z == 0f && state.rotation.w == 0f)
                state.rotation = Quaternion.identity;

            // 1. Smooth input (responsiveness)
            state.smoothedPitch = Mathf.Lerp(state.smoothedPitch, input.pitch, t.pitchResponsiveness * dt * 10f);
            state.smoothedYaw   = Mathf.Lerp(state.smoothedYaw,   input.yaw,   t.yawResponsiveness   * dt * 10f);

            // 2. Throttle -> target speed
            float targetSpeed;
            if (input.throttle >= 0f)
                targetSpeed = Mathf.Lerp(t.cruiseSpeed, t.maxSpeed, input.throttle);
            else
                targetSpeed = Mathf.Lerp(t.cruiseSpeed, t.minSpeed, -input.throttle);

            state.currentSpeed = Mathf.MoveTowards(state.currentSpeed, targetSpeed, t.throttleAccel * dt);

            // 3. Apply rotations cleanly, one axis at a time, around the right reference frame.
            //    This is the key to avoiding gimbal lock during steep dives/climbs.
            float pitchDeg = state.smoothedPitch * t.pitchRate * dt;
            float yawDeg   = state.smoothedYaw   * t.yawRate   * dt;

            // YAW around world up. This keeps "left" and "right" intuitive for the player
            // even when pitched steeply, and prevents nose-down drift.
            Quaternion yawRot = Quaternion.AngleAxis(yawDeg, Vector3.up);

            // PITCH around the plane's current LOCAL right axis.
            Vector3 localRight = state.rotation * Vector3.right;
            Quaternion pitchRot = Quaternion.AngleAxis(pitchDeg, localRight);

            // Apply yaw first (world-space), then pitch (local-space).
            state.rotation = pitchRot * yawRot * state.rotation;

            // Re-normalize occasionally to prevent quaternion drift over time.
            state.rotation.Normalize();

            // 4. Velocity: forward thrust + gravity + lateral drag
            Vector3 forward = state.rotation * Vector3.forward;
            Vector3 thrust = forward * state.currentSpeed;

            // Lift counteracts gravity proportional to forward speed
            float speedRatio = state.currentSpeed / t.cruiseSpeed;
            float effectiveGravity = t.gravity * Mathf.Max(0f, 1f - speedRatio * t.liftFromSpeed);

            // Decompose existing velocity into forward + lateral; bleed lateral component
            Vector3 lateral = state.velocity - Vector3.Project(state.velocity, forward);
            lateral = Vector3.Lerp(lateral, Vector3.zero, t.lateralDrag * dt);

            state.velocity = thrust + lateral + Vector3.down * effectiveGravity * dt;

            // 5. Integrate position
            state.position += state.velocity * dt;

            return state;
        }
    }
}
