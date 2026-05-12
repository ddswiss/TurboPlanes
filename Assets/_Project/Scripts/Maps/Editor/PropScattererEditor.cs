using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SkyBrawl.Maps;

namespace SkyBrawl.MapsEditor
{
    [CustomEditor(typeof(PropScatterer))]
    public class PropScattererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var s = (PropScatterer)target;
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = s.prefab != null && s.count > 0;
                if (GUILayout.Button("Scatter", GUILayout.Height(28))) Scatter(s);
                GUI.enabled = s.transform.childCount > 0;
                if (GUILayout.Button("Clear",   GUILayout.Height(28))) Clear(s);
                GUI.enabled = true;
            }

            EditorGUILayout.LabelField("Children:", s.transform.childCount.ToString(), EditorStyles.miniLabel);
        }

        private static void Scatter(PropScatterer s)
        {
            Clear(s);

            int seed = s.seed != 0 ? s.seed : Random.Range(1, int.MaxValue);
            var rng  = new System.Random(seed);

            for (int i = 0; i < s.count; i++)
            {
                // Uniform disc sampling: sqrt(u) gives uniform area distribution.
                float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                float dist  = Mathf.Sqrt((float)rng.NextDouble()) * s.radius;
                Vector3 pos = s.transform.position + new Vector3(
                    Mathf.Cos(angle) * dist,
                    500f,                       // start raycast well above terrain
                    Mathf.Sin(angle) * dist);

                Vector3 surfaceNormal = Vector3.up;
                if (s.snapToGround)
                {
                    if (Physics.Raycast(pos, Vector3.down, out var hit, 5000f, s.groundMask))
                    {
                        pos = hit.point;
                        surfaceNormal = hit.normal;
                    }
                    else
                        pos.y = s.transform.position.y;
                }
                else
                {
                    pos.y = s.transform.position.y;
                }
                pos.y += s.yOffset;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab, s.transform);
                inst.transform.position = pos;

                Quaternion rot = Quaternion.identity;
                if (s.randomYaw) rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                if (s.maxTilt > 0f)
                {
                    float tx = (float)(rng.NextDouble() * 2.0 - 1.0) * s.maxTilt;
                    float tz = (float)(rng.NextDouble() * 2.0 - 1.0) * s.maxTilt;
                    rot = Quaternion.Euler(tx, 0f, tz) * rot;
                }
                // Compose with prefab's authored rotation so FBX axis-conversions (e.g. 270° X on Meshy assets) survive.
                // If alignToSurface, tilt the local-up onto the terrain normal first, then apply random yaw around that.
                if (s.alignToSurface && s.snapToGround)
                {
                    Quaternion align = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                    inst.transform.rotation = align * rot * inst.transform.rotation;
                }
                else
                {
                    inst.transform.rotation = rot * inst.transform.rotation;
                }

                float sc = Mathf.Lerp(s.scaleRange.x, s.scaleRange.y, (float)rng.NextDouble());
                inst.transform.localScale = inst.transform.localScale * sc;

                Undo.RegisterCreatedObjectUndo(inst, "Scatter prop");
            }

            EditorUtility.SetDirty(s.gameObject);
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        }

        private static void Clear(PropScatterer s)
        {
            for (int i = s.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(s.transform.GetChild(i).gameObject);

            EditorUtility.SetDirty(s.gameObject);
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        }
    }
}
