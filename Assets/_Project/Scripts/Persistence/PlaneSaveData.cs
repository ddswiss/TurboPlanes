using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyBrawl.Persistence
{
    [Serializable]
    public class PlaneSaveData
    {
        public string planeId;
        public List<string> upgradeIds = new();
        public List<int>    upgradeLevels = new();
        // White = no tint (MaterialPropertyBlock pass-through). Texture renders as-imported.
        public Color color = Color.white;

        // Per-plane upgrade stages. Each plane tracks its own progression — buying a Cruise Speed
        // upgrade on the Biplane does NOT apply to the Saucer. Skill points are spent from the
        // shared PlayerProfile.skillPoints pool when these are incremented.
        public int speedStage         = 0;  // 0..10
        public int boostStage         = 0;  // 0..10
        public int boostDurationStage = 0;  // 0..8
        public int boostRefillStage   = 0;  // 0..5

        public int GetUpgradeLevel(string upgradeId)
        {
            int i = upgradeIds.IndexOf(upgradeId);
            return i >= 0 ? upgradeLevels[i] : 0;
        }

        public void SetUpgradeLevel(string upgradeId, int level)
        {
            int i = upgradeIds.IndexOf(upgradeId);
            if (i >= 0) upgradeLevels[i] = level;
            else { upgradeIds.Add(upgradeId); upgradeLevels.Add(level); }
        }

        /// <summary>Sum of all skill-point-funded upgrade stages on this plane.</summary>
        public int TotalUpgradePoints =>
            speedStage + boostStage + boostDurationStage + boostRefillStage;
    }
}
