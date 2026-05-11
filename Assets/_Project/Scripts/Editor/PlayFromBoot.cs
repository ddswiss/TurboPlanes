using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkyBrawl.EditorTools
{
    /// <summary>
    /// Routes the Play button through Boot.unity for every scene except Boot itself,
    /// so the Bootstrap flow (Persistent → MainMenu) always runs no matter which scene
    /// is open in the editor when you press Play.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        static PlayFromBoot()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            var current = EditorSceneManager.GetActiveScene().name;
            bool playDirectly = current == "Boot";

            if (playDirectly)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (boot != null)
            {
                EditorSceneManager.playModeStartScene = boot;
            }
            else
            {
                Debug.LogWarning($"[PlayFromBoot] Could not find {BootScenePath}. Playing directly.");
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
