using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyBrawl.Core;
using SkyBrawl.Planes;

namespace SkyBrawl.Hangar
{
    /// <summary>
    /// Hangar UI controller. Three tabs: Planes (select), Color (paint), Upgrade (stub).
    /// All references are wired from the HangarCanvas prefab.
    /// </summary>
    public class HangarPanel : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button planeTabButton;
        [SerializeField] private Button colorTabButton;
        [SerializeField] private Button upgradeTabButton;

        [Header("Content panels")]
        [SerializeField] private GameObject planePanel;
        [SerializeField] private GameObject colorPanel;
        [SerializeField] private GameObject upgradePanel;

        [Header("Plane select")]
        [SerializeField] private Transform planeListContent;
        [SerializeField] private GameObject planeButtonTemplate;

        [Header("Color select")]
        [SerializeField] private Transform colorGridContent;
        [SerializeField] private GameObject colorButtonTemplate;

        [Header("Bottom buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            if (planeTabButton   != null) planeTabButton.onClick.AddListener(() => ShowTab(0));
            if (colorTabButton   != null) colorTabButton.onClick.AddListener(() => ShowTab(1));
            if (upgradeTabButton != null) upgradeTabButton.onClick.AddListener(() => ShowTab(2));
            if (closeButton      != null) closeButton.onClick.AddListener(() => GameManager.Instance?.CloseHangar());
            if (quitButton       != null) quitButton.onClick.AddListener(() => GameManager.Instance?.QuitToMenu());
            ShowTab(0);
        }

        private void OnEnable()
        {
            RebuildPlaneList();
            RebuildColorList();
        }

        private void ShowTab(int idx)
        {
            if (planePanel   != null) planePanel.SetActive(idx == 0);
            if (colorPanel   != null) colorPanel.SetActive(idx == 1);
            if (upgradePanel != null) upgradePanel.SetActive(idx == 2);
            if (idx == 1) RebuildColorList(); // refresh swatches if plane changed
        }

        private void RebuildPlaneList()
        {
            if (planeListContent == null || planeButtonTemplate == null) return;
            for (int i = planeListContent.childCount - 1; i >= 0; i--)
            {
                var ch = planeListContent.GetChild(i).gameObject;
                if (ch != planeButtonTemplate) Destroy(ch);
            }
            var gm = GameManager.Instance;
            if (gm == null || gm.PlaneCatalog == null) return;
            foreach (var def in gm.PlaneCatalog.planes)
            {
                if (def == null) continue;
                if (!gm.Profile.IsPlaneUnlocked(def.id)) continue;
                var item = Instantiate(planeButtonTemplate, planeListContent);
                item.SetActive(true);
                var label = item.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = def.displayName;
                var btn = item.GetComponent<Button>();
                if (btn != null)
                {
                    string id = def.id;
                    btn.onClick.AddListener(() => GameManager.Instance?.SetSelectedPlane(id));
                }
            }
        }

        private void RebuildColorList()
        {
            if (colorGridContent == null || colorButtonTemplate == null) return;
            for (int i = colorGridContent.childCount - 1; i >= 0; i--)
            {
                var ch = colorGridContent.GetChild(i).gameObject;
                if (ch != colorButtonTemplate) Destroy(ch);
            }
            var gm = GameManager.Instance;
            if (gm == null) return;
            var def = gm.PlaneCatalog?.GetById(gm.Profile.selectedPlaneId);
            if (def == null || def.availableColors == null) return;
            foreach (var c in def.availableColors)
            {
                var item = Instantiate(colorButtonTemplate, colorGridContent);
                item.SetActive(true);
                var img = item.GetComponent<Image>();
                if (img != null) img.color = c;
                var btn = item.GetComponent<Button>();
                if (btn != null)
                {
                    var captured = c;
                    btn.onClick.AddListener(() => GameManager.Instance?.SetSelectedColor(captured));
                }
            }
        }
    }
}
