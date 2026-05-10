using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyBrawl.Core
{
    /// <summary>
    /// Lives in Boot.unity. Loads Persistent additively (so GameManager survives), then
    /// loads MainMenu additively, then unloads Boot itself. Async sequencing avoids the
    /// race condition where a Single-mode load in the same frame unloads the scenes
    /// you just queued additively.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string persistentSceneName = "Persistent";
        [SerializeField] private string mainMenuSceneName   = "MainMenu";

        private void Start()
        {
            StartCoroutine(BootSequence());
        }

        private IEnumerator BootSequence()
        {
            // 1. Load Persistent additively (GameManager comes online)
            if (!IsLoaded(persistentSceneName))
            {
                var op = SceneManager.LoadSceneAsync(persistentSceneName, LoadSceneMode.Additive);
                while (op != null && !op.isDone) yield return null;
            }

            // 2. Load MainMenu additively (so Persistent stays alive)
            if (!IsLoaded(mainMenuSceneName))
            {
                var op = SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Additive);
                while (op != null && !op.isDone) yield return null;
            }

            // 3. Make MainMenu the active scene (so its lighting/skybox apply)
            var menu = SceneManager.GetSceneByName(mainMenuSceneName);
            if (menu.IsValid()) SceneManager.SetActiveScene(menu);

            // 4. Unload Boot — its job is done
            yield return SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private static bool IsLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == name) return true;
            return false;
        }
    }
}
