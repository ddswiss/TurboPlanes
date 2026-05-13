using System;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Lightweight static event bus for the hangar UI. Lets the plane navigator, skill-points
    /// display, and upgrade sliders refresh in lockstep without each holding direct refs to
    /// the others. Fires whenever the selected plane changes OR an upgrade stage changes
    /// (since both invalidate every visible widget).
    /// </summary>
    public static class HangarEvents
    {
        public static event Action UpgradesOrPlaneChanged;

        public static void RaisePlaneChanged()    => UpgradesOrPlaneChanged?.Invoke();
        public static void RaiseUpgradesChanged() => UpgradesOrPlaneChanged?.Invoke();
    }
}
