using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PvpLab
{
    public static class PunBuild
    {
        [MenuItem("Tools/PvP/Build Windows Online Demo")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath("Builds/HuntingPvP/HuntingPvP.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/menu.unity", "Assets/Scenes/hunting.unity" },
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("PvP build failed: " + report.summary.result + ". See the Unity log.");
            UnityEngine.Debug.Log("Online PvP build ready: " + output);
        }
    }
    public sealed class PunSceneValidation : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (scene.name != PunLobby.GameScene) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                HuntingCombatDemo demo = root.GetComponent<HuntingCombatDemo>();
                if (demo == null) continue;
                var serialized = new SerializedObject(demo);
                Transform player = serialized.FindProperty("playerRoot").objectReferenceValue as Transform;
                if (player == null || player.GetComponentInChildren<Animator>() == null)
                    throw new BuildFailedException("Hunting PvP needs a valid playerRoot with an active Animator.");
                return;
            }
            throw new BuildFailedException("HuntingCombatDemo is missing from hunting.");
        }
    }
}
