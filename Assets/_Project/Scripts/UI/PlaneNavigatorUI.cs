using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Core;
using SkyBrawl.Planes;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Left-panel "plane selector" with a preview image, a name label, and prev/next arrows.
    /// Walks the unlocked planes from the PlaneCatalog and tells GameManager when the user
    /// picks one (which respawns the plane and broadcasts HangarEvents.RaisePlaneChanged).
    ///
    /// Optional fields are tolerant of missing wiring — drop in only what your prefab has.
    /// </summary>
    public class PlaneNavigatorUI : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image    previewImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button   prevButton;
        [SerializeField] private Button   nextButton;

        [Header("Optional: index dots")]
        [Tooltip("Optional row of small Image elements (one per unlocked plane). Filled = current plane.")]
        [SerializeField] private Image[] indexDots;
        [SerializeField] private Color   dotActive   = new Color(0.95f, 0.85f, 0.20f, 1f);
        [SerializeField] private Color   dotInactive = new Color(1f,    1f,    1f,    0.35f);

        [Header("Fallback preview tint")]
        [Tooltip("If a plane has no preview Sprite, the previewImage's color is set to this so the player still sees something change.")]
        [SerializeField] private Color noPreviewTint = new Color(0.3f, 0.3f, 0.3f, 1f);

        private void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(+1));
        }

        private void OnEnable()
        {
            Refresh();
            HangarEvents.UpgradesOrPlaneChanged += Refresh;
        }

        private void OnDisable()
        {
            HangarEvents.UpgradesOrPlaneChanged -= Refresh;
        }

        private void Step(int delta)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PlaneCatalog == null || gm.Profile == null) return;

            // Build the visible (unlocked) list in catalog order.
            var unlocked = new System.Collections.Generic.List<PlaneDefinition>();
            foreach (var p in gm.PlaneCatalog.planes)
                if (p != null && gm.Profile.IsPlaneUnlocked(p.id))
                    unlocked.Add(p);
            if (unlocked.Count == 0) return;

            int currentIdx = -1;
            for (int i = 0; i < unlocked.Count; i++)
                if (unlocked[i].id == gm.Profile.selectedPlaneId) { currentIdx = i; break; }
            if (currentIdx < 0) currentIdx = 0;

            int next = (currentIdx + delta + unlocked.Count) % unlocked.Count;
            if (next == currentIdx) return;
            gm.SetSelectedPlane(unlocked[next].id);
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PlaneCatalog == null || gm.Profile == null) return;
            var def = gm.PlaneCatalog.GetById(gm.Profile.selectedPlaneId);

            if (nameLabel != null)
                nameLabel.text = def != null ? def.displayName.ToUpperInvariant() : "";

            if (previewImage != null)
            {
                if (def != null && def.preview != null)
                {
                    previewImage.sprite = def.preview;
                    previewImage.color  = Color.white;
                    previewImage.enabled = true;
                }
                else
                {
                    previewImage.sprite = null;
                    previewImage.color  = noPreviewTint;
                    previewImage.enabled = true;
                }
            }

            UpdateDots(gm);
        }

        private void UpdateDots(GameManager gm)
        {
            if (indexDots == null || indexDots.Length == 0) return;
            var planes = gm.PlaneCatalog.planes;
            int currentIdx = -1;
            int unlockedIdx = 0;
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i] == null || !gm.Profile.IsPlaneUnlocked(planes[i].id)) continue;
                if (planes[i].id == gm.Profile.selectedPlaneId) { currentIdx = unlockedIdx; }
                unlockedIdx++;
            }
            for (int i = 0; i < indexDots.Length; i++)
            {
                if (indexDots[i] == null) continue;
                bool show = i < unlockedIdx;
                indexDots[i].gameObject.SetActive(show);
                indexDots[i].color = (i == currentIdx) ? dotActive : dotInactive;
            }
        }
    }
}
