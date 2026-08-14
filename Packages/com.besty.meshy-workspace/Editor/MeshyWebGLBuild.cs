using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MeshyWorkspace
{
    public static class MeshyWebGLBuild
    {
        private const string ScenePath = "Packages/com.besty.meshy-workspace/Samples~/MeshyGame/Scenes/MeshyGame.unity";
        private const string BuildFolder = "Builds/MeshyGameWebGL";

        [MenuItem("Meshy Workspace/Build WebGL")]
        public static void BuildWebGL()
        {
            var outputPath = BuildFolder;
            var report = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                outputPath,
                BuildTarget.WebGL,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Meshy] WebGL build failed: " + report.summary.result);
                return;
            }

            Debug.Log("[Meshy] WebGL build succeeded: " + System.IO.Path.GetFullPath(outputPath));
        }
    }
}