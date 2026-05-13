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

        // Shared skill points pool. Each upgrade purchase (across any plane) costs 1.
        // Removing an upgrade refunds 1. Defaults to 20 on a fresh profile.
        public int skillPoints = 20;

        // === DEPRECATED global stages — kept for backward-compat migration only. ===
        // Old saves stored upgrades globally; on Load we move them onto the selected plane.
        // After migration these are zeroed and ignored. Do NOT read these at runtime; use
        // the per-plane fields on PlaneSaveData instead.
        public int speedStage         = 0;
        public int boostStage         = 0;
        public int boostDurationStage = 0;
        public int boostRefillStage   = 0;

        const string FileName = "profile.json";
        const int DefaultSkillPoints = 20;

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
                    if (profile != null)
                    {
                        profile.MigrateLegacyGlobalStages();
                        return profile;
                    }
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

        /// <summary>One-shot migration: if a legacy save has non-zero global stage fields,
        /// move them onto the currently-selected plane's save data and zero them out so
        /// the migration is idempotent. Skill points get a one-time default of 20 too.</summary>
        private void MigrateLegacyGlobalStages()
        {
            bool hasLegacyData = (speedStage + boostStage + boostDurationStage + boostRefillStage) > 0;
            if (hasLegacyData && !string.IsNullOrEmpty(selectedPlaneId))
            {
                var pd = GetOrCreatePlaneData(selectedPlaneId);
                pd.speedStage         = speedStage;
                pd.boostStage         = boostStage;
                pd.boostDurationStage = boostDurationStage;
                pd.boostRefillStage   = boostRefillStage;
                speedStage = boostStage = boostDurationStage = boostRefillStage = 0;
            }
            // Old saves predate skillPoints (would deserialize as 0). Seed the default once
            // so existing players get the pool. If a user has actually spent down to 0 in a
            // newer save, this would re-grant them — acceptable for a small game in early dev.
            if (skillPoints <= 0) skillPoints = DefaultSkillPoints;
        }
    }
}
