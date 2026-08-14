using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshyWorkspace.Editor
{
    /// <summary>
    /// P0 smoke harness: calls GET /openapi/v1/balance through UnityWebRequest
    /// and writes the result to Library/MeshyWorkspace/p0-balance-report.txt.
    /// P1 replaces this with the unified MeshyApiClient.
    /// </summary>
    public static class MeshyConnectivityProbe
    {
        private const string BaseUrl = "https://api.meshy.ai";
        private const string ReportPath = "Library/MeshyWorkspace/p0-balance-report.txt";

        private static UnityWebRequest request;
        private static DateTime startedAt;

        [MenuItem("Meshy Workspace/Test API Connection")]
        public static void Run()
        {
            if (!MeshySettings.HasApiKey)
            {
                Debug.LogError("[Meshy P0] 尚未配置 API Key，请先打开 Meshy Workspace > Settings。");
                return;
            }

            request = UnityWebRequest.Get(BaseUrl + "/openapi/v1/balance");
            request.SetRequestHeader("Authorization", "Bearer " + MeshySettings.ApiKey);
            request.timeout = MeshySettings.TimeoutSeconds;
            startedAt = DateTime.Now;
            EditorApplication.update += Tick;
            request.SendWebRequest();
            Debug.Log("[Meshy P0] 正在请求余额接口...");
        }

        private static void Tick()
        {
            if (request == null || !request.isDone)
            {
                return;
            }

            EditorApplication.update -= Tick;
            var elapsed = (DateTime.Now - startedAt).TotalSeconds;
            var ok = request.result == UnityWebRequest.Result.Success;
            var body = ok ? request.downloadHandler.text : request.error;
            var statusCode = ok ? (int)request.responseCode : -1;
            var line = string.Format(
                "[Meshy P0] result={0} status={1} elapsed={2:0.0}s body={3}",
                ok ? "ok" : "failed",
                statusCode,
                elapsed,
                ok ? body : body);

            Debug.Log(line);

            try
            {
                var dir = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace");
                Directory.CreateDirectory(dir);
                File.WriteAllText(
                    Path.Combine(Application.dataPath, "..", ReportPath),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P0] 写入连通性报告失败: " + e.Message);
            }

            request = null;
        }
    }
}
