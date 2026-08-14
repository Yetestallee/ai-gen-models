using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;

namespace MeshyWorkspace
{
    public static class MeshyWindowsBuild
    {
        private const string ScenePath = "Assets/MeshyGame/Scenes/MeshyGame.unity";
        private const string BuildFolder = "Builds/MeshyGame";
        private const string ExeName = "MeshyGame.exe";
        private const string SourceGeneratedFolder = "Assets/MeshyGenerated";
        private const string SourceHistoryFile = "Assets/MeshyWorkspace/History/tasks.json";

        [MenuItem("Meshy Workspace/Build Windows Exe")]
        public static void BuildWindowsExe()
        {
            Directory.CreateDirectory(BuildFolder);
            var outputPath = Path.Combine(BuildFolder, ExeName);
            var report = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                outputPath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Meshy] Windows build failed: " + report.summary.result);
                return;
            }

            CopyBundledContentToData(Path.Combine(BuildFolder, "MeshyGame_Data"));
            Debug.Log("[Meshy] Windows build succeeded: " + Path.GetFullPath(outputPath));
        }

        [MenuItem("Meshy Workspace/Download Missing Models")]
        public static void DownloadMissingModels()
        {
            var historyPath = Path.Combine(Application.dataPath, "MeshyWorkspace", "History", "tasks.json");
            if (!File.Exists(historyPath))
            {
                Debug.LogWarning("[Meshy] History file not found: " + historyPath);
                return;
            }

            var entries = JsonConvert.DeserializeObject<List<MeshyCachedTask>>(File.ReadAllText(historyPath))
                ?? new List<MeshyCachedTask>();
            var candidates = entries
                .Where(e => e.ModelUrls != null && e.ModelUrls.ContainsKey("glb"))
                .ToList();
            var restored = 0;
            var failed = 0;

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                foreach (var entry in candidates)
                {
                    var folder = Path.Combine(
                        Application.dataPath,
                        "MeshyGenerated",
                        MeshyPaths.TypeFolder(entry.TaskType),
                        entry.TaskId);
                    var glbPath = Path.Combine(folder, "model.glb");
                    Directory.CreateDirectory(folder);
                    if (!File.Exists(glbPath))
                    {
                        Debug.Log("[Meshy] Downloading " + entry.TaskId + " -> " + entry.ModelUrls["glb"]);
                        try
                        {
                            using (var response = client.GetAsync(entry.ModelUrls["glb"]).GetAwaiter().GetResult())
                            {
                                response.EnsureSuccessStatusCode();
                                File.WriteAllBytes(glbPath, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                            }
                            restored++;
                            Debug.Log("[Meshy] Restored " + entry.TaskId + " -> " + glbPath);
                        }
                        catch (Exception e)
                        {
                            failed++;
                            Debug.LogWarning("[Meshy] Restore failed " + entry.TaskId + ": " + e.Message);
                            continue;
                        }
                    }

                    RepairOrDownloadTextures(entry, folder, client);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[Meshy] Missing models restored=" + restored + " failed=" + failed);
        }

        private static void RepairOrDownloadTextures(MeshyCachedTask entry, string folder, HttpClient client)
        {
            if (entry == null || entry.TextureUrls == null)
            {
                return;
            }
            for (var i = 0; i < entry.TextureUrls.Count; i++)
            {
                var textureUrl = entry.TextureUrls[i];
                var fileName = UrlFileName(textureUrl);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "texture_" + i + ".png";
                }
                var destination = Path.Combine(folder, fileName);
                var renamed = Path.Combine(folder, "texture_" + i + ".png");
                if (File.Exists(renamed) && !File.Exists(destination))
                {
                    File.Move(renamed, destination);
                }
                if (File.Exists(destination))
                {
                    continue;
                }
                try
                {
                    using (var response = client.GetAsync(textureUrl).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        File.WriteAllBytes(destination, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Meshy] Texture repair failed " + entry.TaskId + " " + fileName + ": " + e.Message);
                }
            }
        }

        private static string UrlFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.AbsolutePath);
                return string.IsNullOrEmpty(name) ? string.Empty : name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        [MenuItem("Meshy Workspace/Test Load First Model")]
        public static void TestLoadFirstModel()
        {
            var app = UnityEngine.Object.FindObjectOfType<MeshyWorkspaceApp>();
            if (app == null)
            {
                Debug.LogError("[Meshy] No MeshyWorkspaceApp in scene");
                return;
            }

            var hostField = typeof(MeshyWorkspaceApp).GetField("modelPreviewHost", BindingFlags.Instance | BindingFlags.NonPublic);
            var host = hostField == null ? null : hostField.GetValue(app) as MeshyRuntimeModelPreviewHost;
            if (host == null)
            {
                Debug.LogError("[Meshy] No model preview host");
                return;
            }

            var modelRoot = Path.Combine(Application.dataPath, "MeshyGenerated", "Models");
            if (!Directory.Exists(modelRoot))
            {
                Debug.LogError("[Meshy] No model folder: " + modelRoot);
                return;
            }

            var texturedId = "019ff03b-9867-7f5e-b095-465d3710d3fd";
            var glb = Path.Combine(modelRoot, texturedId, "model.glb");
            if (!File.Exists(glb))
            {
                var folders = Directory.GetDirectories(modelRoot);
                if (folders.Length == 0)
                {
                    Debug.LogError("[Meshy] No model folders");
                    return;
                }
                glb = Path.Combine(folders[0], "model.glb");
            }

            Debug.Log("[Meshy] Test loading " + glb);
            _ = host.LoadAsync(glb);
        }

        [MenuItem("Meshy Workspace/Diagnose Image Request")]
        public static async void DiagnoseImageRequest()
        {
            var settings = MeshyRuntimeSettingsStore.Load();
            Debug.Log("[Meshy] mock=" + settings.UseMockMode +
                " proxy=" + settings.ProxyUrl +
                " timeout=" + settings.TimeoutSeconds +
                " keyLen=" + (settings.ApiKey ?? string.Empty).Length);
            if (settings.UseMockMode)
            {
                Debug.Log("[Meshy] Mock mode on, no real request");
                return;
            }

            using (var api = new MeshyApiClient(settings.ToApiConfig()))
            {
                try
                {
                    var response = await api.CreateTextToImageAsync(new TextToImageRequest
                    {
                        AiModel = "nano-banana",
                        Prompt = "a red apple on a wooden table",
                        AspectRatio = "1:1",
                        GenerateMultiView = null
                    });
                    Debug.Log("[Meshy] Diagnose created task " + response.Result);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Meshy] Diagnose failed: " + e);
                }
            }
        }

        public static void CopyBundledContentToData(string dataDirectory)
        {
            var streaming = Path.Combine(dataDirectory, "StreamingAssets");
            CopyDirectory(SourceGeneratedFolder, Path.Combine(streaming, "MeshyGenerated"));

            var sourceHistory = Path.Combine(Directory.GetCurrentDirectory(), SourceHistoryFile);
            if (!File.Exists(sourceHistory))
            {
                return;
            }

            var targetHistory = Path.Combine(streaming, "MeshyWorkspace", "History");
            Directory.CreateDirectory(targetHistory);
            File.Copy(sourceHistory, Path.Combine(targetHistory, "tasks.json"), true);
        }

        private static void CopyDirectory(string sourceRelative, string target)
        {
            var source = Path.Combine(Directory.GetCurrentDirectory(), sourceRelative);
            if (!Directory.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = Path.Combine(target, relative);
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(file, destination, true);
            }
        }
    }

    public sealed class MeshyBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            var output = report.summary.outputPath;
            var dataDirectory = output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? output.Substring(0, output.Length - 4) + "_Data"
                : Path.Combine(
                    Path.GetDirectoryName(output) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(output) + "_Data");
            MeshyWindowsBuild.CopyBundledContentToData(dataDirectory);
        }
    }
}
