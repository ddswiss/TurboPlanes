using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyBrawl.Core
{
    /// <summary>
    /// Guarantees Persistent.unity (and therefore GameManager) is alive no matter
    /// which scene the editor starts Play in. The proper game flow goes through
    /// Boot.unity, but for fast iteration we want devs to be able to hit Play on
    /// any map / sandbox scene and still get the persistent HUD, GameManager, etc.
    /// Skips the load if Persistent is already active (so going through Boot stays
    /// idempotent).
    /// </summary>
    public static class PersistentBootstrap
    {
        private const string PersistentSceneName = "Persistent";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsurePersistentLoaded()
        {
            if (IsLoaded(PersistentSceneName)) return;
            SceneManager.LoadScene(PersistentSceneName, LoadSceneMode.Additive);
        }

        private static bool IsLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == name) return true;
            return false;
        }
    }
}
