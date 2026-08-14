using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshyWorkspace
{
    public sealed class MeshyApiClient : IMeshyApi, IDisposable
    {
        private readonly MeshyApiConfig config;

#if !UNITY_WEBGL
        private readonly System.Net.Http.HttpClient httpClient;

        public MeshyApiClient(MeshyApiConfig config, System.Net.Http.HttpMessageHandler handler = null)
        {
            this.config = config ?? new MeshyApiConfig();

            if (handler != null)
            {
                httpClient = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(this.config.TimeoutSeconds)
                };
            }
            else
            {
                var httpHandler = new System.Net.Http.HttpClientHandler();
                if (!string.IsNullOrEmpty(this.config.ProxyUrl))
                {
                    httpHandler.Proxy = new WebProxy(this.config.ProxyUrl);
                    httpHandler.UseProxy = true;
                }

                httpClient = new System.Net.Http.HttpClient(httpHandler)
                {
                    Timeout = TimeSpan.FromSeconds(this.config.TimeoutSeconds)
                };
            }

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.config.ApiKey);
        }
#else
        public MeshyApiClient(MeshyApiConfig config, System.Net.Http.HttpMessageHandler handler = null)
        {
            this.config = config ?? new MeshyApiConfig();
            // WebGL: handler parameter is ignored — UnityWebRequest does not use HttpMessageHandler.
        }
#endif

        public async Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
        {
            return await SendAsync<BalanceResponse>("GET", "/openapi/v1/balance", null, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/text-to-image", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v2/text-to-3d", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/image-to-3d", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/rigging", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/animations", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/retexture", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateRemeshAsync(RemeshRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>("POST", "/openapi/v1/remesh", request, ct).ConfigureAwait(false);
        }

        public async Task<T> GetTaskAsync<T>(string taskId, string taskType, CancellationToken ct = default) where T : MeshyTaskBase
        {
            var path = string.Format("/openapi/{0}/{1}/{2}", TaskVersion(taskType), taskType, taskId);
            return await SendAsync<T>("GET", path, null, ct).ConfigureAwait(false);
        }

        public async Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, CancellationToken ct = default) where T : MeshyTaskBase
        {
            var path = string.Format("/openapi/{0}/{1}?page_num={2}&page_size={3}", TaskVersion(taskType), taskType, pageNum, pageSize);
            var token = await SendAsync<JToken>("GET", path, null, ct).ConfigureAwait(false);
            if (token == null)
            {
                return new List<T>();
            }
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>() ?? new List<T>();
            }
            var data = token["data"];
            return data == null ? new List<T>() : (data.ToObject<List<T>>() ?? new List<T>());
        }

        public async Task DeleteTaskAsync(string taskType, string taskId, CancellationToken ct = default)
        {
            await SendAsync<object>("DELETE", string.Format("/openapi/{0}/{1}/{2}", TaskVersion(taskType), taskType, taskId), null, ct).ConfigureAwait(false);
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

#if !UNITY_WEBGL
        private async Task<T> SendAsync<T>(string method, string path, object body, CancellationToken ct)
        {
            var httpMethod = method switch
            {
                "GET" => System.Net.Http.HttpMethod.Get,
                "POST" => System.Net.Http.HttpMethod.Post,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                _ => new System.Net.Http.HttpMethod(method)
            };

            var request = new System.Net.Http.HttpRequestMessage(httpMethod, config.BaseUrl.TrimEnd('/') + path);
            if (body != null)
            {
                request.Content = new System.Net.Http.StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json");
            }

            System.Net.Http.HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                throw new MeshyApiException("请求超时，请检查网络或增大超时时间。", HttpStatusCode.RequestTimeout, isRetryable: true);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogWarning("[Meshy] HTTP " + (int)response.StatusCode + " " + text);
                    throw new MeshyApiException(
                        MeshyErrorMapper.HttpStatusMessage(response.StatusCode, text),
                        response.StatusCode,
                        ReadJsonField(text, "type"),
                        ReadJsonField(text, "code"),
                        response.StatusCode == HttpStatusCode.TooManyRequests);
                }

                if (typeof(T) == typeof(object) || typeof(T) == typeof(string))
                {
                    return default(T);
                }

                return JsonConvert.DeserializeObject<T>(text);
            }
        }
#else
        private async Task<T> SendAsync<T>(string method, string path, object body, CancellationToken ct)
        {
            var url = config.BaseUrl.TrimEnd('/') + path;
            var jsonBody = body != null ? JsonConvert.SerializeObject(body) : null;

            using (var req = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                else
                {
                    req.downloadHandler = new DownloadHandlerBuffer();
                }

                req.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
                req.timeout = config.TimeoutSeconds;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        req.Abort();
                        ct.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                var statusCode = (int)req.responseCode;
                var text = req.downloadHandler?.text ?? string.Empty;

                if (req.result != UnityWebRequest.Result.Success && req.result != UnityWebRequest.Result.ProtocolError)
                {
                    // Network error (timeout, connection failure, etc.)
                    if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        throw new MeshyApiException(
                            req.error ?? "网络连接失败",
                            (HttpStatusCode)(statusCode > 0 ? statusCode : 408),
                            isRetryable: true);
                    }
                    throw new MeshyApiException(req.error ?? "请求失败", HttpStatusCode.InternalServerError);
                }

                if (statusCode < 200 || statusCode >= 300)
                {
                    Debug.LogWarning("[Meshy] HTTP " + statusCode + " " + text);
                    throw new MeshyApiException(
                        MeshyErrorMapper.HttpStatusMessage((HttpStatusCode)statusCode, text),
                        (HttpStatusCode)statusCode,
                        ReadJsonField(text, "type"),
                        ReadJsonField(text, "code"),
                        statusCode == 429);
                }

                if (typeof(T) == typeof(object) || typeof(T) == typeof(string))
                {
                    return default(T);
                }

                return JsonConvert.DeserializeObject<T>(text);
            }
        }
#endif

        private static string ReadJsonField(string body, string field)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            try
            {
                var obj = JObject.Parse(body);
                return obj[field] == null ? null : obj[field].ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string TaskVersion(string taskType)
        {
            return taskType == "text-to-3d" ? "v2" : "v1";
        }
    }
}