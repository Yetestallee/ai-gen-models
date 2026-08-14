using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MeshyWorkspace
{
    /// <summary>
    /// Reads Meshy task /stream SSE progress without blocking the editor loop.
    /// Completion is still decided by the poller; this only makes progress smoother.
    /// </summary>
    public sealed class MeshyTaskSse : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string baseUrl;

        public MeshyTaskSse(MeshyApiConfig config, HttpMessageHandler handler = null)
        {
            baseUrl = config.BaseUrl.TrimEnd('/');
            if (handler != null)
            {
                httpClient = new HttpClient(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };
            }
            else
            {
                httpClient = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };
            }
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        public async Task WatchAsync(
            string taskId,
            string taskType,
            Action<int> onProgress,
            CancellationToken ct = default)
        {
            var version = taskType == "text-to-3d" ? "v2" : "v1";
            var url = baseUrl + "/openapi/" + version + "/" + taskType + "/" + taskId + "/stream";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
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
                using (var reader = new StreamReader(stream))
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
                            var token = JObject.Parse(payload);
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
        }

        public void Dispose()
        {
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
        }
    }
}
