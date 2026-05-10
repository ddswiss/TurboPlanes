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
