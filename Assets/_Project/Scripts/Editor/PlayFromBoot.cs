using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkyBrawl.EditorTools
{
    /// <summary>
    /// Routes the Play button through Boot.unity for production scenes (Persistent, MainMenu, Map_*),
    /// so the Bootstrap flow runs even if you press Play while editing a different scene.
    /// FlightSandbox and Boot are excluded — they play directly so flight tuning iteration stays fast.
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
            bool needsBoot =
                current == "Persistent" ||
                current == "MainMenu"   ||
                current.StartsWith("Map_");

            if (needsBoot)
            {
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
            else
            {
                // Direct play (Boot itself, FlightSandbox, or any other scene)
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
