using UnityEngine;
using UnityEngine.SceneManagement;
using SkyBrawl.Persistence;
using SkyBrawl.Maps;
using SkyBrawl.Planes;
using SkyBrawl.CameraRig;

namespace SkyBrawl.Core
{
    /// <summary>
    /// Lives in Persistent.unity (DontDestroyOnLoad-equivalent via additive scene).
    /// Singleton-style access through Instance. Handles map loading and plane spawning.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Catalogs")]
        [SerializeField] private MapCatalog   mapCatalog;
        [SerializeField] private PlaneCatalog planeCatalog;

        [Header("UI")]
        [Tooltip("Hangar canvas prefab instantiated as a child. Hidden by default.")]
        [SerializeField] private GameObject hangarCanvasPrefab;
        [Tooltip("In-game HUD canvas (speed, boost). Instantiated as a child of GameManager so it survives scene loads. Shown only while a map is active.")]
        [SerializeField] private GameObject hudCanvasPrefab;

        public PlayerProfile Profile { get; private set; }
        public MapCatalog   MapCatalog   => mapCatalog;
        public PlaneCatalog PlaneCatalog => planeCatalog;

        private GameObject _hangarCanvas;
        private GameObject _hudCanvas;
        private GameObject _activePlane;
        private MapDefinition _currentMap;

        public bool IsHangarOpen => _hangarCanvas != null && _hangarCanvas.activeSelf;

        /// <summary>Fires after CloseHangar() is invoked. LandingZone subscribes to this so
        /// the plane resumes flight (currentSpeed = 0, IsLanded = false) when the player
        /// finishes customizing in the hangar.</summary>
        public event System.Action HangarClosed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Belt-and-suspenders: even if a Single-mode scene load unloads Persistent,
            // GameManager and its child UI (HangarCanvas) survive.
            DontDestroyOnLoad(gameObject);

            Profile = PlayerProfile.Load();
            EnsureDefaults();

            if (hangarCanvasPrefab != null)
            {
                _hangarCanvas = Instantiate(hangarCanvasPrefab, transform);
                _hangarCanvas.SetActive(false);
            }

            if (hudCanvasPrefab != null)
            {
                _hudCanvas = Instantiate(hudCanvasPrefab, transform);
                _hudCanvas.SetActive(false);
            }
        }

        private void Start()
        {
            // Dev convenience: if the user hits Play directly on a map / sandbox
            // scene (instead of going through Boot → MainMenu → LoadMap), wire up
            // the plane + HUD here. PersistentBootstrap ensures Persistent (and
            // this GameManager) is alive before any scene's Awake runs.
            var scene = SceneManager.GetActiveScene();
            if (_activePlane == null && IsMapScene(scene.name))
            {
                // If the scene already contains a PlaneController (sandbox scenes
                // with an inline plane), adopt it instead of spawning a duplicate.
                var existing = FindFirstObjectByType<SkyBrawl.Player.PlaneController>();
                if (existing != null)
                {
                    _activePlane = existing.gameObject;
                    var mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        var rig = mainCam.GetComponent<ChaseCameraRig>();
                        if (rig == null) rig = mainCam.gameObject.AddComponent<ChaseCameraRig>();
                        rig.SetTarget(_activePlane.transform);
                    }
                }
                else
                {
                    if (_currentMap == null && mapCatalog != null && mapCatalog.maps != null)
                    {
                        foreach (var m in mapCatalog.maps)
                            if (m != null && m.sceneName == scene.name) { _currentMap = m; break; }
                    }
                    SpawnSelectedPlane();
                }
                if (_hudCanvas != null) _hudCanvas.SetActive(true);
            }
        }

        private static bool IsMapScene(string sceneName)
        {
            // Convention: anything under Scenes/Maps/ is named Map_*.
            // FlightSandbox is also a flyable scene; treat it as a map for HUD purposes.
            return sceneName.StartsWith("Map_") || sceneName == "FlightSandbox";
        }

        private void EnsureDefaults()
        {
            if (planeCatalog != null && planeCatalog.planes != null)
                foreach (var p in planeCatalog.planes)
                    if (p != null && p.unlockedByDefault && !Profile.IsPlaneUnlocked(p.id))
                        Profile.unlockedPlanes.Add(p.id);

            if (mapCatalog != null && mapCatalog.maps != null)
                foreach (var m in mapCatalog.maps)
                    if (m != null && m.unlockedByDefault && !Profile.IsMapUnlocked(m.id))
                        Profile.unlockedMaps.Add(m.id);

            if (string.IsNullOrEmpty(Profile.selectedPlaneId) && Profile.unlockedPlanes.Count > 0)
                Profile.selectedPlaneId = Profile.unlockedPlanes[0];
            if (string.IsNullOrEmpty(Profile.selectedMapId) && Profile.unlockedMaps.Count > 0)
                Profile.selectedMapId = Profile.unlockedMaps[0];

            Profile.Save();
        }

        public void LoadMap(string mapId)
        {
            var map = mapCatalog != null ? mapCatalog.GetById(mapId) : null;
            if (map == null) { Debug.LogError($"Map '{mapId}' not found in catalog."); return; }

            _currentMap = map;
            Profile.selectedMapId = mapId;
            Profile.Save();

            SceneManager.sceneLoaded += OnMapLoaded;
            SceneManager.LoadScene(map.sceneName, LoadSceneMode.Single);
        }

        private void OnMapLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_currentMap == null || scene.name != _currentMap.sceneName) return;
            SceneManager.sceneLoaded -= OnMapLoaded;
            SpawnSelectedPlane();
            if (_hudCanvas != null) _hudCanvas.SetActive(true);
        }

        public void SpawnSelectedPlane()
        {
            if (planeCatalog == null) return;
            var def = planeCatalog.GetById(Profile.selectedPlaneId);
            if (def == null || def.prefab == null) { Debug.LogWarning("Selected plane has no prefab."); return; }

            // Despawn current
            if (_activePlane != null) Destroy(_activePlane);

            // Find airport spawn (a GameObject tagged "AirportSpawn" or named so)
            var spawn = GameObject.Find("AirportSpawn");
            Vector3    pos = spawn != null ? spawn.transform.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            _activePlane = Instantiate(def.prefab, pos, rot);

            // Hook the scene's Main Camera up to follow the spawned plane.
            // Auto-attaches a ChaseCameraRig if one isn't already on the camera.
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var rig = mainCam.GetComponent<ChaseCameraRig>();
                if (rig == null) rig = mainCam.gameObject.AddComponent<ChaseCameraRig>();
                rig.SetTarget(_activePlane.transform);
            }
            else
            {
                Debug.LogWarning("[GameManager] No Camera.main in active scene; the plane spawned but will not be followed.");
            }

            // TODO: apply runtime tuning clone with upgrades.

            // Apply upgrades from the selected plane's PlaneSaveData (upgrades are per-plane now).
            var pd0 = Profile.GetOrCreatePlaneData(Profile.selectedPlaneId);
            ApplySpeedAndBoostUpgradesToActivePlane(pd0.speedStage, pd0.boostStage);
            ApplyBoostUpgradesToActivePlane(pd0.boostDurationStage, pd0.boostRefillStage);

            // Apply the saved color for this plane to the freshly spawned instance.
            var saveData = Profile.GetOrCreatePlaneData(Profile.selectedPlaneId);
            ApplyColorToActivePlane(saveData.color);

            // If the active map has a LandingZone, drop the plane into it landed and open
            // the hangar. The player begins each map session at the runway, ready to
            // customize/take off.
            var landing = FindFirstObjectByType<SkyBrawl.Maps.LandingZone>();
            if (landing != null)
            {
                var pc = _activePlane.GetComponent<SkyBrawl.Player.PlaneController>();
                if (pc != null) landing.ForceLand(pc);
            }
        }

        /// <summary>Apply the speed + boost upgrades to the plane's runtime tuning.
        /// Speed upgrade (0..10): cruise = base * (1 + 0.10 * stage).
        /// Boost upgrade (0..10): max = cruise + 30 + 5 * stage.
        /// minSpeed is fixed at the base value (independent of upgrades).</summary>
        private void ApplySpeedAndBoostUpgradesToActivePlane(int speedStage, int boostStage)
        {
            if (_activePlane == null) return;
            var pc = _activePlane.GetComponent<SkyBrawl.Player.PlaneController>();
            if (pc == null || pc.Tuning == null) return;
            int s = Mathf.Clamp(speedStage, 0, 10);
            int b = Mathf.Clamp(boostStage, 0, 10);
            pc.Tuning.cruiseSpeed = pc.BaseCruiseSpeed * (1f + 0.10f * s);
            // +6 game units per boost stage (= +15 km/h with displayMultiplier 2.5)
            pc.Tuning.maxSpeed    = pc.Tuning.cruiseSpeed + 30f + 6f * b;
            pc.Tuning.minSpeed    = pc.BaseMinSpeed;
        }

        /// <summary>Derive boost consume + regen rates from the duration / refill upgrades.
        /// Duration upgrade (0..8): boost duration = 2 + stage seconds. Stage 0 = 2s, stage 8 = 10s.
        /// Refill upgrade  (0..5): refill from empty = 5 - 0.5*stage seconds. Stage 0 = 5s, stage 5 = 2.5s.
        /// Internally the boost system is fuel-based; we compute rates as MaxFuel / seconds so
        /// the seconds are exact regardless of capacity.</summary>
        private void ApplyBoostUpgradesToActivePlane(int durationStage, int refillStage)
        {
            if (_activePlane == null) return;
            var pc = _activePlane.GetComponent<SkyBrawl.Player.PlaneController>();
            if (pc == null) return;
            int dur = Mathf.Clamp(durationStage, 0, 8);
            int re  = Mathf.Clamp(refillStage,  0, 5);
            float boostSeconds  = 2f + dur;          // 2..10
            float refillSeconds = 5f - 0.5f * re;     // 5..2.5
            pc.BoostConsumeRate = pc.MaxBoostFuel / boostSeconds;
            pc.BoostRegenRate   = pc.MaxBoostFuel / refillSeconds;
        }

        /// <summary>Re-apply all upgrade sliders (speed + boost) to the currently active plane.
        /// Called by UI sliders so changes take effect immediately, not just on next respawn.</summary>
        public void RefreshActivePlaneUpgrades()
        {
            if (Profile == null || string.IsNullOrEmpty(Profile.selectedPlaneId)) return;
            var pd = Profile.GetOrCreatePlaneData(Profile.selectedPlaneId);
            ApplySpeedAndBoostUpgradesToActivePlane(pd.speedStage, pd.boostStage);
            ApplyBoostUpgradesToActivePlane(pd.boostDurationStage, pd.boostRefillStage);
        }

        public void SetSelectedPlane(string planeId)
        {
            if (planeCatalog == null) return;
            var def = planeCatalog.GetById(planeId);
            if (def == null) return;
            Profile.selectedPlaneId = planeId;
            Profile.Save();
            // Re-spawn at the airport with the new prefab (re-applies color too).
            SpawnSelectedPlane();
            // Notify hangar UI (upgrades / preview / name / skill points display).
            SkyBrawl.UI.HangarEvents.RaisePlaneChanged();
        }

        public void SetSelectedColor(Color color)
        {
            if (string.IsNullOrEmpty(Profile.selectedPlaneId)) return;
            var saveData = Profile.GetOrCreatePlaneData(Profile.selectedPlaneId);
            saveData.color = color;
            Profile.Save();
            ApplyColorToActivePlane(color);
        }

        private void ApplyColorToActivePlane(Color color)
        {
            if (_activePlane == null) return;
            var painter = _activePlane.GetComponent<PlanePainter>();
            if (painter == null) painter = _activePlane.AddComponent<PlanePainter>();
            painter.Apply(color);
        }

        public void OpenHangar()
        {
            if (_hangarCanvas != null) _hangarCanvas.SetActive(true);
            // TODO: pause / slow time
        }

        public void CloseHangar()
        {
            Debug.Log("[GameManager] CloseHangar() called.");
            if (_hangarCanvas != null) _hangarCanvas.SetActive(false);
            HangarClosed?.Invoke();
        }

        public void QuitToMenu()
        {
            CloseHangar();
            if (_hudCanvas != null) _hudCanvas.SetActive(false);
            if (_activePlane != null) Destroy(_activePlane);
            _currentMap = null;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }
    }
}
