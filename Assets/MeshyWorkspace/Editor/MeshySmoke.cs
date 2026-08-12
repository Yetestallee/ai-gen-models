using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    /// <summary>
    /// P1 smoke harness. Invoked from the menu after the API key is saved in
    /// Meshy Settings. Results are written under Library so no key or URLs
    /// leak into source control.
    /// </summary>
    public static class MeshySmoke
    {
        private const string ReportPath = "Library/MeshyWorkspace/p1-smoke-report.txt";

        [MenuItem("Meshy Workspace/Smoke Test Balance")]
        public static async void RunBalance()
        {
            await RunAsync("balance", async client =>
            {
                var balance = await client.GetBalanceAsync();
                return "balance=" + balance.Balance;
            });
        }

        [MenuItem("Meshy Workspace/Smoke Test Text To Image")]
        public static async void RunTextToImage()
        {
            await RunAsync("text-to-image", client => RunTextToImageCore(client, 1));
        }

        [MenuItem("Meshy Workspace/Smoke Test Text To Image x4")]
        public static async void RunTextToImage4()
        {
            await RunAsync("text-to-image-x4", client => RunTextToImageCore(client, 4));
        }

        private static async Task<string> RunTextToImageCore(MeshyApiClient client, int count)
        {
            var request = new TextToImageRequest
            {
                AiModel = "nano-banana",
                Prompt = "a red apple on a wooden table, studio lighting",
                AspectRatio = "1:1"
            };

            var urls = new List<string>();
            string lastTaskId = null;
            double credits = 0;
            for (var i = 0; i < count; i++)
            {
                var created = await client.CreateTextToImageAsync(request);
                var poller = new MeshyTaskPoller(client, TimeSpan.FromSeconds(2), 60);
                var task = await poller.WaitForTaskAsync<TextToImageTask>(
                    created.Result,
                    "text-to-image",
                    t => Debug.Log("[Meshy P1] progress=" + t.Progress + " status=" + t.StatusRaw));
                lastTaskId = task.Id;
                credits = task.ConsumedCredits;
                if (task.ImageUrls != null)
                {
                    urls.AddRange(task.ImageUrls);
                }
            }

            return string.Format(
                "task={0} status={1} progress={2} credits={3} images={4}",
                lastTaskId,
                "SUCCEEDED",
                100,
                credits,
                urls.Count);
        }

        [MenuItem("Meshy Workspace/Smoke Test Text To 3D")]
        public static async void RunTextTo3D()
        {
            await RunAsync("text-to-3d", async client =>
            {
                var prompt = "a low poly stone golem, game asset";
                var previewRequest = new TextTo3DRequest
                {
                    Mode = "preview",
                    Prompt = prompt,
                    AiModel = "meshy-6",
                    ModelType = "standard",
                    TargetFormats = new List<string> { "glb" }
                };

                var previewCreated = await client.CreateTextTo3DAsync(previewRequest);
                var poller = new MeshyTaskPoller(client, TimeSpan.FromSeconds(2), 120);
                var previewTask = await poller.WaitForTaskAsync<TextTo3DTask>(
                    previewCreated.Result,
                    "text-to-3d",
                    t => Debug.Log("[Meshy P4] preview progress=" + t.Progress + " status=" + t.StatusRaw));

                var refineRequest = new TextTo3DRequest
                {
                    Mode = "refine",
                    Prompt = prompt,
                    PreviewTaskId = previewTask.Id,
                    AiModel = "meshy-6",
                    EnablePbr = true,
                    TextureResolution = "2k",
                    TargetFormats = new List<string> { "glb" }
                };
                var refineCreated = await client.CreateTextTo3DAsync(refineRequest);
                var refineTask = await poller.WaitForTaskAsync<TextTo3DTask>(
                    refineCreated.Result,
                    "text-to-3d",
                    t => Debug.Log("[Meshy P4] refine progress=" + t.Progress + " status=" + t.StatusRaw));

                var glbInfo = "none";
                if (refineTask.ModelUrls != null && refineTask.ModelUrls.ContainsKey("glb"))
                {
                    var destination = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p4-refine.glb");
                    var tcs = new TaskCompletionSource<bool>();
                    MeshyModelDownloader.DownloadFile(refineTask.ModelUrls["glb"], destination, ok => tcs.TrySetResult(ok));
                    var downloaded = await tcs.Task;
                    glbInfo = downloaded ? new FileInfo(destination).Length.ToString() + " bytes" : "failed";
                }

                return string.Format(
                    "preview={0} refine={1} status={2} credits={3} formats={4} glb={5}",
                    previewTask.Id,
                    refineTask.Id,
                    refineTask.StatusRaw,
                    refineTask.ConsumedCredits,
                    refineTask.ModelUrls == null ? 0 : refineTask.ModelUrls.Count,
                    glbInfo);
            });
        }

        [MenuItem("Meshy Workspace/Smoke Test Model From Existing Task")]
        public static async void RunModelFromExistingTask()
        {
            const string previewTaskId = "019fef87-62e7-7e25-97df-757a03066ca4";
            const string refineTaskId = "019fef88-ca9d-721b-957b-e611d5a83822";
            await RunAsync("model-existing", async client =>
            {
                var previewTask = await client.GetTaskAsync<TextTo3DTask>(previewTaskId, "text-to-3d");
                var refineTask = await client.GetTaskAsync<TextTo3DTask>(refineTaskId, "text-to-3d");
                if (previewTask == null || previewTask.ModelUrls == null || !previewTask.ModelUrls.ContainsKey("glb"))
                {
                    return "no glb url";
                }

                var folder = MeshyPaths.FindTaskFolder("text-to-3d", previewTask.Id);
                Directory.CreateDirectory(folder);
                var glbPath = Path.Combine(folder, "model.glb");
                var downloaded = await DownloadToFileAsyncLocal(previewTask.ModelUrls["glb"], glbPath);
                if (!downloaded)
                {
                    return "glb download failed";
                }

                var textureOk = false;
                if (refineTask != null && refineTask.TextureUrls != null && refineTask.TextureUrls.Count > 0)
                {
                    textureOk = await DownloadToFileAsyncLocal(
                        refineTask.TextureUrls[0],
                        Path.Combine(folder, "texture_0.png"));
                }

                AssetDatabase.ImportAsset("Assets" + folder.Replace(Application.dataPath, string.Empty).Replace('\\', '/') + "/model.glb");
                var preview = new MeshyModelPreviewHost();
                var loaded = await preview.LoadAsync(glbPath);
                var previewOk = loaded && preview.Texture != null;
                preview.Clear();

                return string.Format(
                    "task={0} glb={1} bytes preview={2} textureDownload={3}",
                    previewTask.Id,
                    new FileInfo(glbPath).Length,
                    previewOk,
                    textureOk);
            });
        }

        private static async Task<bool> DownloadToFileAsyncLocal(string url, string path)
        {
            var tcs = new TaskCompletionSource<bool>();
            MeshyModelDownloader.DownloadFile(url, path, ok => tcs.TrySetResult(ok));
            return await tcs.Task;
        }

        private static async Task RunAsync(string name, Func<MeshyApiClient, Task<string>> action)
        {
            if (!MeshySettings.HasApiKey)
            {
                Debug.LogError("[Meshy P1] 尚未配置 API Key，请先打开 Meshy Workspace > Settings。");
                return;
            }

            try
            {
                var config = new MeshyApiConfig
                {
                    ApiKey = MeshySettings.ApiKey,
                    ProxyUrl = MeshySettings.ProxyUrl,
                    TimeoutSeconds = MeshySettings.TimeoutSeconds
                };

                using (var client = new MeshyApiClient(config))
                {
                    var startedAt = DateTime.Now;
                    var line = await action(client);
                    var elapsed = (DateTime.Now - startedAt).TotalSeconds;
                    WriteReport(string.Format("[{0}] result=ok elapsed={1:0.0}s {2}", name, elapsed, line));
                    Debug.Log("[Meshy P1] 冒烟完成: " + line);
                }
            }
            catch (Exception e)
            {
                WriteReport(string.Format("[{0}] result=failed error={1}", name, e.Message));
                Debug.LogException(e);
            }
        }

        private static void WriteReport(string line)
        {
            try
            {
                var path = Path.Combine(Application.dataPath, "..", ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P1] 写入冒烟报告失败: " + e.Message);
            }
        }
    }
}
