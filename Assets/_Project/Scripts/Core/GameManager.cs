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

        public PlayerProfile Profile { get; private set; }
        public MapCatalog   MapCatalog   => mapCatalog;
        public PlaneCatalog PlaneCatalog => planeCatalog;

        private GameObject _hangarCanvas;
        private GameObject _activePlane;
        private MapDefinition _currentMap;

        public bool IsHangarOpen => _hangarCanvas != null && _hangarCanvas.activeSelf;

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

            // Apply the main-menu speed-slider multiplier to the freshly cloned tuning SO
            // (PlaneController.Awake instantiates its own copy so this doesn't mutate the asset).
            ApplySpeedMultiplierToActivePlane(Profile.speedStage);
            // Apply the hangar boost-drain-slider multiplier.
            ApplyBoostDrainMultiplierToActivePlane(Profile.boostDrainStage);

            // Apply the saved color for this plane to the freshly spawned instance.
            var saveData = Profile.GetOrCreatePlaneData(Profile.selectedPlaneId);
            ApplyColorToActivePlane(saveData.color);
        }

        /// <summary>Apply the menu speed-slider stage (1..10, default 5) to the plane's runtime tuning.
        /// Computes current = base * multiplier, so re-applying with a different stage doesn't compound.</summary>
        private void ApplySpeedMultiplierToActivePlane(int stage)
        {
            if (_activePlane == null) return;
            var pc = _activePlane.GetComponent<SkyBrawl.Player.PlaneController>();
            if (pc == null || pc.Tuning == null) return;
            float mult = 1f + (Mathf.Clamp(stage, 1, 10) - 5) * 0.2f;
            pc.Tuning.cruiseSpeed = pc.BaseCruiseSpeed * mult;
            pc.Tuning.maxSpeed    = pc.BaseMaxSpeed    * mult;
            pc.Tuning.minSpeed    = pc.BaseMinSpeed    * mult;
        }

        /// <summary>Apply the hangar boost-drain-slider stage (1..10, default 5) to the plane.
        /// Computes current = base * multiplier.</summary>
        private void ApplyBoostDrainMultiplierToActivePlane(int stage)
        {
            if (_activePlane == null) return;
            var pc = _activePlane.GetComponent<SkyBrawl.Player.PlaneController>();
            if (pc == null) return;
            float mult = 1f + (Mathf.Clamp(stage, 1, 10) - 5) * 0.2f;
            pc.BoostConsumeRate = pc.BaseBoostConsumeRate * mult;
        }

        /// <summary>Re-apply all upgrade sliders (speed + boost drain) to the currently active plane.
        /// Called by UI sliders so changes take effect immediately, not just on next respawn.</summary>
        public void RefreshActivePlaneUpgrades()
        {
            ApplySpeedMultiplierToActivePlane(Profile.speedStage);
            ApplyBoostDrainMultiplierToActivePlane(Profile.boostDrainStage);
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
            if (_hangarCanvas != null) _hangarCanvas.SetActive(false);
        }

        public void QuitToMenu()
        {
            CloseHangar();
            if (_activePlane != null) Destroy(_activePlane);
            _currentMap = null;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }
    }
}
