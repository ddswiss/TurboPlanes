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
    }
}
