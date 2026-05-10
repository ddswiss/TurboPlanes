using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Core;

namespace SkyBrawl.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [Tooltip("Map id to load when Play is pressed. Must match a MapDefinition.id in the MapCatalog.")]
        [SerializeField] private string defaultMapId = "turbo_canyon";

        private void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[MainMenu] GameManager.Instance is null. Did Persistent scene load?");
                return;
            }
            GameManager.Instance.LoadMap(defaultMapId);
        }
    }
}
