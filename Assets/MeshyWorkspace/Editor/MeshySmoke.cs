using System;
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
            await RunAsync("text-to-image", async client =>
            {
                var request = new TextToImageRequest
                {
                    AiModel = "nano-banana",
                    Prompt = "a red apple on a wooden table, studio lighting",
                    AspectRatio = "1:1"
                };

                var created = await client.CreateTextToImageAsync(request);
                var poller = new MeshyTaskPoller(client, TimeSpan.FromSeconds(2), 60);
                var task = await poller.WaitForTaskAsync<TextToImageTask>(
                    created.Result,
                    "text-to-image",
                    t => Debug.Log("[Meshy P1] progress=" + t.Progress + " status=" + t.StatusRaw));

                return string.Format(
                    "task={0} status={1} progress={2} credits={3} images={4}",
                    task.Id,
                    task.StatusRaw,
                    task.Progress,
                    task.ConsumedCredits,
                    task.ImageUrls == null ? 0 : task.ImageUrls.Count);
            });
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
