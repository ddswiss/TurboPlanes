using UnityEngine;
using SkyBrawl.Player;

namespace SkyBrawl.UI
{
    /// <summary>
    /// World-space speed-streak particles that fly past the plane, scaled with overspeed.
    /// Replaces the older screen-edge SpeedLinesHUD with something more kinetic — particles
    /// spawn in a ring ahead of the plane and stream backward (relative to world), so the
    /// plane appears to be ripping through them. Self-bootstraps the ParticleSystem on Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedParticlesHUD : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Plane whose speed drives the effect. Auto-found at runtime if empty.")]
        [SerializeField] private PlaneController plane;

        private SkyBrawl.Player.PlaneCrashHandler _crashHandler;
        private bool _wasCrashing;

        [Header("Density curve (absolute speed units)")]
        [Tooltip("Below this speed: no particles at all.")]
        [SerializeField] private float densityStartSpeed = 20f;
        [Tooltip("At and above this speed the density is at maximum (1.0). Density ramps linearly between densityStartSpeed and densityFullSpeed.")]
        [SerializeField] private float densityFullSpeed = 200f;

        [Header("Emission")]
        [Tooltip("Maximum particles emitted per second at densityFullSpeed and above.")]
        [SerializeField] private float maxEmissionRate = 600f;
        [Tooltip("Streak lifetime (seconds). Short = quick zips, long = persistent trails.")]
        [SerializeField] private float lifetime = 0.45f;

        [Header("Spawn shape (plane-relative)")]
        [Tooltip("How far ahead of the plane new particles appear. Smaller = particles spawn nearer the plane and never reach the screen-center area.")]
        [SerializeField] private float spawnDistance = 18f;
        [Tooltip("Inner radius of the ring around the plane forward axis where particles spawn. Larger = streaks stay in the screen periphery instead of crossing the middle.")]
        [SerializeField] private float spawnRadiusMin = 7f;
        [Tooltip("Outer radius of the spawn ring.")]
        [SerializeField] private float spawnRadiusMax = 18f;

        [Header("Particle velocity (world space, backward through plane)")]
        [Tooltip("Backward speed (m/s) of the particle at the threshold (just over cruise).")]
        [SerializeField] private float particleSpeedMin = 90f;
        [Tooltip("Backward speed (m/s) at max plane speed — should be well above plane top speed for an obvious streak.")]
        [SerializeField] private float particleSpeedMax = 280f;
        [Tooltip("Random speed multiplier per particle to break up uniformity.")]
        [SerializeField] private Vector2 speedJitter = new Vector2(0.85f, 1.2f);

        [Header("Visual")]
        [SerializeField] private float streakSize = 0.14f;
        [SerializeField] private Vector2 sizeJitter = new Vector2(0.6f, 1.6f);
        [Tooltip("Stretched-billboard length scale, multiplied by particle velocity. Higher = longer streaks.")]
        [SerializeField] private float streakLengthScale = 0.04f;
        [Tooltip("Particle color at or below the plane's cruise speed.")]
        [SerializeField] private Color colorCruise = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Particle color at or above the plane's max speed. Lerped between this and colorCruise based on currentSpeed.")]
        [SerializeField] private Color colorMax = new Color(1f, 0.25f, 0.2f, 1f);

        private ParticleSystem _ps;
        private ParticleSystemRenderer _psr;
        private Material _mat;
        private float _retryTimer;
        private float _emissionAccumulator;

        private void Awake()
        {
            _ps  = GetComponent<ParticleSystem>();
            if (_ps == null) _ps = gameObject.AddComponent<ParticleSystem>();
            _psr = GetComponent<ParticleSystemRenderer>();
            ConfigureParticleSystem();
        }

        private void ConfigureParticleSystem()
        {
            var main = _ps.main;
            main.startLifetime   = lifetime;
            main.startSpeed      = 0f;   // velocity is provided per-particle via EmitParams
            main.startSize       = streakSize;
            main.startColor      = Color.white;  // actual color is set per-particle in EmitParams
            main.simulationSpace = ParticleSystemSimulationSpace.World;  // particles stay put in world while plane flies through them
            main.maxParticles    = 4000;
            main.gravityModifier = 0f;
            main.playOnAwake     = true;

            var emission = _ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;  // script-driven via Emit()

            // No automatic spawn shape — positions are computed per Emit() call so they always sit ahead of the plane.
            var shape = _ps.shape;
            shape.enabled = false;

            // Color-over-lifetime fades alpha in/out, multiplied with per-particle startColor.
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            _psr.renderMode          = ParticleSystemRenderMode.Stretch;
            _psr.lengthScale         = 0f;
            _psr.velocityScale       = streakLengthScale;
            _psr.cameraVelocityScale = 0f;
            _psr.maxParticleSize     = 2f;

            if (_psr.sharedMaterial == null)
                _psr.sharedMaterial = CreateParticleMaterial();
        }

        private Material CreateParticleMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _mat = new Material(sh) { name = "M_SpeedParticle (runtime)" };
            _mat.color = Color.white;
            // URP Particles/Unlit additive transparent setup
            if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 1f);
            if (_mat.HasProperty("_Blend"))   _mat.SetFloat("_Blend", 1f);
            if (_mat.HasProperty("_ZWrite"))  _mat.SetFloat("_ZWrite", 0f);
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mat.EnableKeyword("_BLENDMODE_ADDITIVE");
            _mat.renderQueue = 3000;
            return _mat;
        }

        private void Update()
        {
            if (plane == null)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    plane = FindFirstObjectByType<PlaneController>();
                    _retryTimer = 0.5f;
                }
                if (plane == null) return;
                _crashHandler = plane.GetComponent<SkyBrawl.Player.PlaneCrashHandler>();
            }
            else if (_crashHandler == null)
            {
                _crashHandler = plane.GetComponent<SkyBrawl.Player.PlaneCrashHandler>();
            }

            // Vanish during a crash: clear live particles on the entry frame, stop emitting until the plane respawns.
            bool crashing = _crashHandler != null && _crashHandler.IsCrashing;
            if (crashing)
            {
                if (!_wasCrashing && _ps != null) _ps.Clear();
                _wasCrashing = true;
                _emissionAccumulator = 0f;
                return;
            }
            _wasCrashing = false;

            float speed  = plane.State.currentSpeed;
            float cruise = plane.Tuning != null ? plane.Tuning.cruiseSpeed : 1f;
            float top    = plane.Tuning != null ? plane.Tuning.maxSpeed    : cruise;

            // Linear density ramp on absolute speed: 0 at densityStartSpeed, 1 at densityFullSpeed.
            if (speed <= densityStartSpeed)
            {
                _emissionAccumulator = 0f;
                return;
            }
            float density = Mathf.InverseLerp(densityStartSpeed, densityFullSpeed, speed);

            float rate          = maxEmissionRate * density;
            float particleSpeed = Mathf.Lerp(particleSpeedMin, particleSpeedMax, density);

            // Color tint: white-ish at cruise, shifting to colorMax (red by default) as speed climbs to max.
            float tintT = (top > cruise) ? Mathf.Clamp01((speed - cruise) / (top - cruise)) : 0f;
            Color particleColor = Color.Lerp(colorCruise, colorMax, tintT);

            _emissionAccumulator += rate * Time.deltaTime;
            int count = Mathf.FloorToInt(_emissionAccumulator);
            _emissionAccumulator -= count;
            if (count <= 0) return;

            Vector3 fwd = plane.transform.forward;
            Vector3 pos = plane.transform.position;
            Vector3 axis = Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, axis).normalized;
            Vector3 up    = Vector3.Cross(right, fwd).normalized;

            var emitParams = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Mathf.Lerp(spawnRadiusMin, spawnRadiusMax, Mathf.Sqrt(Random.value));  // sqrt gives uniform area
                Vector3 offset = right * Mathf.Cos(ang) * rad + up * Mathf.Sin(ang) * rad;
                Vector3 spawn  = pos + fwd * spawnDistance + offset;

                float sm = Random.Range(speedJitter.x, speedJitter.y);
                emitParams.position      = spawn;
                emitParams.velocity      = -fwd * particleSpeed * sm;
                emitParams.startSize     = streakSize * Random.Range(sizeJitter.x, sizeJitter.y);
                emitParams.startLifetime = lifetime;
                emitParams.startColor    = particleColor;
                _ps.Emit(emitParams, 1);
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
