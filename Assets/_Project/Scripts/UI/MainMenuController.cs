using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Core;
using SkyBrawl.Maps;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Drives the main menu. The PlayButton swaps the menu over to a Map Select
    /// panel, which lists every unlocked map in the MapCatalog as a button. Clicking
    /// a map button delegates to GameManager.LoadMap.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button playButton;

        [Header("Map Select panel")]
        [Tooltip("Root GameObject for the Map Select panel. Hidden until Play is clicked.")]
        [SerializeField] private GameObject mapSelectPanel;
        [Tooltip("Parent under which one button is instantiated per map (typically a VerticalLayoutGroup).")]
        [SerializeField] private RectTransform mapButtonContainer;
        [Tooltip("Inactive button template that is cloned for each map. Kept hidden in the hierarchy.")]
        [SerializeField] private Button mapButtonTemplate;

        private readonly List<Button> _spawnedButtons = new List<Button>();

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (mapSelectPanel != null) mapSelectPanel.SetActive(false);
            if (mapButtonTemplate != null) mapButtonTemplate.gameObject.SetActive(false);
        }

        private void OnPlayClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[MainMenu] GameManager.Instance is null. Did Persistent scene load?");
                return;
            }

            if (playButton != null) playButton.gameObject.SetActive(false);
            if (mapSelectPanel != null) mapSelectPanel.SetActive(true);
            PopulateMapButtons();
        }

        private void PopulateMapButtons()
        {
            // Clear any previous spawn (defensive — Awake hides the panel so this
            // normally only runs once per session).
            foreach (var b in _spawnedButtons) if (b != null) Destroy(b.gameObject);
            _spawnedButtons.Clear();

            if (mapButtonContainer == null || mapButtonTemplate == null) return;

            var catalog = GameManager.Instance.MapCatalog;
            if (catalog == null || catalog.maps == null) return;

            var profile = GameManager.Instance.Profile;
            foreach (var def in catalog.maps)
            {
                if (def == null) continue;
                if (profile != null && !profile.IsMapUnlocked(def.id)) continue;

                var btn = Instantiate(mapButtonTemplate, mapButtonContainer);
                btn.gameObject.SetActive(true);
                btn.name = "MapBtn_" + def.id;

                // Set the visible label. Search the cloned button for a Text or TMP_Text.
                var txt = btn.GetComponentInChildren<Text>(true);
                if (txt != null) txt.text = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
                var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmp != null) tmp.text = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;

                string capturedId = def.id;
                btn.onClick.AddListener(() => GameManager.Instance.LoadMap(capturedId));

                _spawnedButtons.Add(btn);
            }
        }
    }
}
