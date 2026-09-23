using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Strands.Data;
using Strands.Game;

namespace Strands.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        static ProjectSetup() { EditorApplication.delayCall += EnsureScene; }

        [MenuItem("Strands/Prepare playground")]
        public static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            // Unity cannot create an additive scene while an Untitled scene exists.
            // Preserve its contents in a separate, uniquely named asset first.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var openScene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(openScene.path)) continue;
                string recoveryPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/RecoveredScene.unity");
                if (!EditorSceneManager.SaveScene(openScene, recoveryPath))
                    throw new IOException("Could not preserve the open scene at " + recoveryPath);
            }
            // Additive creation preserves any scene the user may already be editing.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save " + ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Strands Playground";
            PlayerSettings.productName = "Strands";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Strands/Validate rules")]
        public static void ValidateCore() { Debug.Log(GameChecks.Run(CardCatalog.Load())); }

        [MenuItem("Strands/Build Web")]
        public static void BuildWeb()
        {
            EnsureScene(); ValidateCore();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support through Unity Hub.");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Web",
                target = BuildTarget.WebGL, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Web build failed: " + report.summary.result);
            Debug.Log("Web build ready in Builds/Web. Run node Tools/serve.mjs.");
        }
    }
}
