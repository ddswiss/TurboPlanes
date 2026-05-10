using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Core;

namespace SkyBrawl.Hangar
{
    /// <summary>
    /// Stub. Wire up real UI tabs (Plane Select / Upgrade / Color / Quit) later.
    /// </summary>
    public class HangarPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => GameManager.Instance?.CloseHangar());
            if (quitButton  != null) quitButton.onClick.AddListener (() => GameManager.Instance?.QuitToMenu());
        }
    }
}
