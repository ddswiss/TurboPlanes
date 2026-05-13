using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SkyBrawl.Core;
using SkyBrawl.Persistence;
using SkyBrawl.Planes;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Runtime controller for the UI Toolkit hangar (Hangar.uxml + Hangar.uss).
    /// Binds the static layout to the live PlayerProfile / PlaneSaveData and the
    /// HangarEvents bus. Designed to coexist with the legacy UGUI HangarCanvas
    /// — only one should be open at a time.
    ///
    /// Setup:
    ///  1. Attach this script to a GameObject that ALSO has a UIDocument component.
    ///  2. On the UIDocument: set Source Asset = Hangar.uxml, Panel Settings = a
    ///     PanelSettings asset (the generated one in Assets/_Project/UI works).
    ///  3. Activate this GameObject when the hangar should open (or call Show()).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HangarUIDocument : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private UIDocument document;

        [Header("Per-row max stages (mirror the UGUI inspector values)")]
        [SerializeField] private int speedMaxStage    = 10;
        [SerializeField] private int boostMaxStage    = 10;
        [SerializeField] private int durationMaxStage = 8;
        [SerializeField] private int refillMaxStage   = 5;

        [Header("Plane preview placeholder")]
        [Tooltip("Tint applied to the preview rect when the selected plane has no preview Sprite.")]
        [SerializeField] private Color noPreviewTint = new Color(0.18f, 0.20f, 0.26f, 1f);

        // Cached element refs
        private VisualElement _root;
        private Label  _planeName;
        private Label  _skillPoints;
        private VisualElement _preview;
        private Button _prevBtn, _nextBtn;
        private VisualElement _dots;
        private Button _closeBtn, _quitBtn;

        private UpgradeRow _rowSpeed, _rowBoost, _rowDuration, _rowRefill;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (document == null) return;
            _root = document.rootVisualElement;
            if (_root == null) return;

            BindElements();
            BuildUpgradeRows();
            HookButtons();

            HangarEvents.UpgradesOrPlaneChanged += RefreshAll;
            RefreshAll();
        }

        private void OnDisable()
        {
            HangarEvents.UpgradesOrPlaneChanged -= RefreshAll;
        }

        private void BindElements()
        {
            _planeName   = _root.Q<Label>("plane-name");
            _skillPoints = _root.Q<Label>("skill-points");
            _preview     = _root.Q<VisualElement>("plane-preview");
            _prevBtn     = _root.Q<Button>("prev-btn");
            _nextBtn     = _root.Q<Button>("next-btn");
            _dots        = _root.Q<VisualElement>("dots");
            _closeBtn    = _root.Q<Button>("close-btn");
            _quitBtn     = _root.Q<Button>("quit-btn");
        }

        private void BuildUpgradeRows()
        {
            _rowSpeed    = BuildRow("upgrade-speed",    UpgradeKind.Speed,    speedMaxStage,    s => "Cruise Speed: " + (100 + 10 * s) + " KM/H");
            _rowBoost    = BuildRow("upgrade-boost",    UpgradeKind.Boost,    boostMaxStage,    s => "Boost Gain: +" + (15 * s) + " km/h");
            _rowDuration = BuildRow("upgrade-duration", UpgradeKind.Duration, durationMaxStage, s => "Max Boost: " + (2 + s) + "s");
            _rowRefill   = BuildRow("upgrade-refill",   UpgradeKind.Refill,   refillMaxStage,   s => "Boost Refill: " + (5f - 0.5f * s).ToString("0.#") + "s");
        }

        private UpgradeRow BuildRow(string elementName, UpgradeKind kind, int maxStage, System.Func<int, string> labelFn)
        {
            var rowRoot = _root.Q<VisualElement>(elementName);
            var row = new UpgradeRow
            {
                Root      = rowRoot,
                Kind      = kind,
                MaxStage  = maxStage,
                LabelFn   = labelFn,
                Label     = rowRoot.Q<Label>(className: "upgrade-label"),
                MinusBtn  = rowRoot.Q<Button>(className: "minus-btn"),
                PlusBtn   = rowRoot.Q<Button>(className: "plus-btn"),
                Squares   = new List<VisualElement>(),
            };

            // Always build a full 10-slot row so every upgrade is visually aligned.
            // Slots beyond `maxStage` are permanently "locked" (black sprite).
            const int SlotCount = 10;
            var sqContainer = rowRoot.Q<VisualElement>(className: "squares");
            sqContainer.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                var sq = new VisualElement();
                sq.AddToClassList("square");
                var glyph = new Label("⚡");
                glyph.AddToClassList("square-glyph");
                glyph.pickingMode = PickingMode.Ignore;
                sq.Add(glyph);
                sqContainer.Add(sq);
                row.Squares.Add(sq);
            }

            row.MinusBtn.clicked += () => Step(row, -1);
            row.PlusBtn.clicked  += () => Step(row, +1);
            return row;
        }

        private void HookButtons()
        {
            if (_prevBtn != null)  _prevBtn.clicked  += () => StepPlane(-1);
            if (_nextBtn != null)  _nextBtn.clicked  += () => StepPlane(+1);
            if (_closeBtn != null) _closeBtn.clicked += () => GameManager.Instance?.CloseHangar();
            if (_quitBtn != null)  _quitBtn.clicked  += () => GameManager.Instance?.QuitToMenu();
        }

        // ============= Plane switching =============
        private void StepPlane(int delta)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PlaneCatalog == null || gm.Profile == null) return;
            var unlocked = GetUnlockedPlanes(gm);
            if (unlocked.Count == 0) return;
            int idx = -1;
            for (int i = 0; i < unlocked.Count; i++)
                if (unlocked[i].id == gm.Profile.selectedPlaneId) { idx = i; break; }
            if (idx < 0) idx = 0;
            int next = (idx + delta + unlocked.Count) % unlocked.Count;
            if (next == idx) return;
            gm.SetSelectedPlane(unlocked[next].id);
        }

        private static List<PlaneDefinition> GetUnlockedPlanes(GameManager gm)
        {
            var list = new List<PlaneDefinition>();
            foreach (var p in gm.PlaneCatalog.planes)
                if (p != null && gm.Profile.IsPlaneUnlocked(p.id)) list.Add(p);
            return list;
        }

        // ============= Upgrade row buy / refund =============
        private void Step(UpgradeRow row, int delta)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null) return;
            var pd = gm.Profile.GetOrCreatePlaneData(gm.Profile.selectedPlaneId);
            int current = GetStage(pd, row.Kind);
            int next    = Mathf.Clamp(current + delta, 0, row.MaxStage);
            if (next == current) return;

            if (delta > 0)
            {
                if (gm.Profile.skillPoints <= 0) return;
                gm.Profile.skillPoints--;
            }
            else
            {
                gm.Profile.skillPoints++;
            }
            SetStage(pd, row.Kind, next);
            gm.Profile.Save();
            gm.RefreshActivePlaneUpgrades();
            HangarEvents.RaiseUpgradesChanged();
        }

        private static int GetStage(PlaneSaveData pd, UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Boost:    return pd.boostStage;
                case UpgradeKind.Duration: return pd.boostDurationStage;
                case UpgradeKind.Refill:   return pd.boostRefillStage;
                case UpgradeKind.Speed:    default: return pd.speedStage;
            }
        }
        private static void SetStage(PlaneSaveData pd, UpgradeKind kind, int v)
        {
            switch (kind)
            {
                case UpgradeKind.Boost:    pd.boostStage         = v; break;
                case UpgradeKind.Duration: pd.boostDurationStage = v; break;
                case UpgradeKind.Refill:   pd.boostRefillStage   = v; break;
                case UpgradeKind.Speed:    pd.speedStage         = v; break;
            }
        }

        // ============= Refresh display =============
        private void RefreshAll()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Profile == null || _root == null) return;
            var def = gm.PlaneCatalog != null ? gm.PlaneCatalog.GetById(gm.Profile.selectedPlaneId) : null;
            var pd  = gm.Profile.GetOrCreatePlaneData(gm.Profile.selectedPlaneId);

            // Plane preview + name
            if (_planeName != null) _planeName.text = def != null ? def.displayName.ToUpperInvariant() : "";
            if (_preview != null)
            {
                if (def != null && def.preview != null)
                {
                    _preview.style.backgroundImage = new StyleBackground(def.preview);
                    _preview.style.unityBackgroundImageTintColor = Color.white;
                }
                else
                {
                    _preview.style.backgroundImage = null;
                    _preview.style.unityBackgroundImageTintColor = noPreviewTint;
                    _preview.style.backgroundColor = noPreviewTint;
                }
            }

            // Dots
            if (_dots != null)
            {
                var unlocked = GetUnlockedPlanes(gm);
                int curIdx = -1;
                for (int i = 0; i < unlocked.Count; i++)
                    if (unlocked[i].id == gm.Profile.selectedPlaneId) { curIdx = i; break; }
                _dots.Clear();
                for (int i = 0; i < unlocked.Count; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("dot");
                    if (i == curIdx) d.AddToClassList("active");
                    _dots.Add(d);
                }
            }

            // Skill points
            if (_skillPoints != null) _skillPoints.text = "Skill Points: " + gm.Profile.skillPoints;

            // Upgrade rows
            RefreshRow(_rowSpeed,    pd, gm.Profile.skillPoints);
            RefreshRow(_rowBoost,    pd, gm.Profile.skillPoints);
            RefreshRow(_rowDuration, pd, gm.Profile.skillPoints);
            RefreshRow(_rowRefill,   pd, gm.Profile.skillPoints);
        }

        private static void RefreshRow(UpgradeRow row, PlaneSaveData pd, int skillPoints)
        {
            if (row == null || row.Root == null) return;
            int stage = Mathf.Clamp(GetStage(pd, row.Kind), 0, row.MaxStage);

            // Label
            if (row.Label != null) row.Label.text = row.LabelFn(stage);

            // Squares: 10 total per row.
            //   i <  stage              -> owned (yellow)
            //   i >= row.MaxStage       -> permanently unavailable (black)
            //   otherwise               -> available to buy (grey)
            // No skill points + at the next-purchasable slot? Still show as grey/available;
            // the button just disables. (Was previously coloring it black, which conflated
            // "can't afford right now" with "this slot is permanently locked".)
            for (int i = 0; i < row.Squares.Count; i++)
            {
                var sq = row.Squares[i];
                sq.RemoveFromClassList("owned");
                sq.RemoveFromClassList("locked");
                if (i < stage)                  sq.AddToClassList("owned");
                else if (i >= row.MaxStage)     sq.AddToClassList("locked");
                // else: grey (no extra class)
            }

            // Buttons
            bool canBuy = skillPoints > 0;
            if (row.PlusBtn  != null) row.PlusBtn.SetEnabled(stage < row.MaxStage && canBuy);
            if (row.MinusBtn != null) row.MinusBtn.SetEnabled(stage > 0);
        }

        private enum UpgradeKind { Speed, Boost, Duration, Refill }

        private class UpgradeRow
        {
            public VisualElement Root;
            public UpgradeKind Kind;
            public int MaxStage;
            public System.Func<int, string> LabelFn;
            public Label  Label;
            public Button MinusBtn, PlusBtn;
            public List<VisualElement> Squares;
        }
    }
}
