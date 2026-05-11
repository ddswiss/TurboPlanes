using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkyBrawl.Persistence
{
    [Serializable]
    public class PlayerProfile
    {
        public List<string> unlockedPlanes = new();
        public List<string> unlockedMaps   = new();
        public List<PlaneSaveData> planes  = new();
        public string selectedPlaneId = "";
        public string selectedMapId   = "";
        public int currency = 0;

        // Speed upgrade points (0..10). Each point adds +10% to the base cruise speed.
        // 0 = no upgrade (cruise = base), 10 = +100% (cruise = 2x base).
        public int speedStage = 0;
        // Boost upgrade points (0..10). Each point adds +5 game-units to the cruise→max gap.
        // 0 = stock 30-unit gap, 10 = 80-unit gap (much higher top speed when boosting).
        public int boostStage = 0;
        // Boost duration upgrade points (0..8). Default 2s of boost; each point adds +1s.
        // Max stage 8 → 10s of boost from full bar.
        public int boostDurationStage = 0;
        // Boost refill upgrade points (0..5). Default 5s to refill from empty; each point
        // shaves -0.5s off. Max stage 5 → 2.5s to fully refill.
        public int boostRefillStage = 0;

        const string FileName = "profile.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public PlaneSaveData GetOrCreatePlaneData(string planeId)
        {
            foreach (var p in planes) if (p.planeId == planeId) return p;
            var fresh = new PlaneSaveData { planeId = planeId };
            planes.Add(fresh);
            return fresh;
        }

        public bool IsPlaneUnlocked(string id) => unlockedPlanes.Contains(id);
        public bool IsMapUnlocked(string id)   => unlockedMaps.Contains(id);

        public static PlayerProfile Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var json = File.ReadAllText(SavePath);
                    var profile = JsonUtility.FromJson<PlayerProfile>(json);
                    if (profile != null) return profile;
                }
            }
            catch (Exception e) { Debug.LogWarning($"PlayerProfile load failed: {e.Message}"); }
            return new PlayerProfile();
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, prettyPrint: true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e) { Debug.LogWarning($"PlayerProfile save failed: {e.Message}"); }
        }
    }
}
