using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PvpLab
{
    public static class CombatLabMenu
    {
        [MenuItem("Tools/PvP/Open Combat Lab")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before opening Combat Lab."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Combat Lab").AddComponent<CombatLab>();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.05f, .07f, .1f);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Combat Lab ready. Press Play, then choose a scenario. Save this scene if you want to build it.");
        }
    }
}
