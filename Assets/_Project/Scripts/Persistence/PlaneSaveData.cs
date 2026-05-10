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
        public Color color = new Color(0.85f, 0.20f, 0.20f, 1f);

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
