using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MeshyWorkspace
{
    /// <summary>
    /// Reads Meshy task /stream SSE progress without blocking the editor loop.
    /// Completion is still decided by the poller; this only makes progress smoother.
    /// On WebGL, SSE is not supported — this class is a no-op (polling handles progress).
    /// </summary>
    public sealed class MeshyTaskSse : IDisposable
    {
#if !UNITY_WEBGL
        private readonly System.Net.Http.HttpClient httpClient;
        private readonly string baseUrl;
#endif

        public MeshyTaskSse(MeshyApiConfig config, System.Net.Http.HttpMessageHandler handler = null)
        {
#if !UNITY_WEBGL
            baseUrl = config.BaseUrl.TrimEnd('/');
            if (handler != null)
            {
                httpClient = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan
                };
            }
            else
            {
                httpClient = new System.Net.Http.HttpClient
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan
                };
            }
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
#endif
        }

        public async Task WatchAsync(
            string taskId,
            string taskType,
            Action<int> onProgress,
            CancellationToken ct = default)
        {
#if !UNITY_WEBGL
            var version = taskType == "text-to-3d" ? "v2" : "v1";
            var url = baseUrl + "/openapi/" + version + "/" + taskType + "/" + taskId + "/stream";
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);

            System.Net.Http.HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }
                        if (!line.StartsWith("data:", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var payload = line.Substring(5).Trim();
                        if (string.IsNullOrEmpty(payload))
                        {
                            continue;
                        }
                        try
                        {
                            var token = Newtonsoft.Json.Linq.JObject.Parse(payload);
                            var progress = token["progress"];
                            if (progress != null && onProgress != null)
                            {
                                onProgress(progress.Value<int>());
                            }
                        }
                        catch (Exception)
                        {
                            // Non-JSON keep-alive comments are expected.
                        }
                    }
                }
            }
#else
            // WebGL: SSE streaming is not supported via HttpClient.
            // Task polling (MeshyTaskPoller) handles progress updates instead.
            await Task.Yield();
#endif
        }

        public void Dispose()
        {
#if !UNITY_WEBGL
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
#endif
        }
    }
}