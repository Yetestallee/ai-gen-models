using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeshyWorkspace
{
    public sealed class MeshyApiClient : IMeshyApi, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly MeshyApiConfig config;

        public MeshyApiClient(MeshyApiConfig config, HttpMessageHandler handler = null)
        {
            this.config = config ?? new MeshyApiConfig();

            if (handler != null)
            {
                httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(this.config.TimeoutSeconds)
                };
            }
            else
            {
                var httpHandler = new HttpClientHandler();
                if (!string.IsNullOrEmpty(this.config.ProxyUrl))
                {
                    httpHandler.Proxy = new WebProxy(this.config.ProxyUrl);
                    httpHandler.UseProxy = true;
                }

                httpClient = new HttpClient(httpHandler)
                {
                    Timeout = TimeSpan.FromSeconds(this.config.TimeoutSeconds)
                };
            }

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", this.config.ApiKey);
        }

        public async Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
        {
            return await SendAsync<BalanceResponse>(HttpMethod.Get, "/openapi/v1/balance", null, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v1/text-to-image", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v2/text-to-3d", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v1/image-to-3d", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v1/rigging", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v1/animations", request, ct).ConfigureAwait(false);
        }

        public async Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default)
        {
            return await SendAsync<CreateTaskResponse>(HttpMethod.Post, "/openapi/v1/retexture", request, ct).ConfigureAwait(false);
        }

        public async Task<T> GetTaskAsync<T>(string taskId, string taskType, CancellationToken ct = default) where T : MeshyTaskBase
        {
            var path = string.Format("/openapi/{0}/{1}/{2}", TaskVersion(taskType), taskType, taskId);
            return await SendAsync<T>(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        }

        public async Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, CancellationToken ct = default) where T : MeshyTaskBase
        {
            var path = string.Format("/openapi/{0}/{1}?page_num={2}&page_size={3}", TaskVersion(taskType), taskType, pageNum, pageSize);
            var wrapper = await SendAsync<MeshyTaskList<T>>(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
            return wrapper == null ? new List<T>() : (wrapper.Data ?? new List<T>());
        }

        public async Task DeleteTaskAsync(string taskType, string taskId, CancellationToken ct = default)
        {
            var path = string.Format("/openapi/{0}/{1}/{2}", TaskVersion(taskType), taskType, taskId);
            await SendAsync<object>(HttpMethod.Delete, path, null, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
        {
            var request = new HttpRequestMessage(method, config.BaseUrl.TrimEnd('/') + path);
            if (body != null)
            {
                request.Content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json");
            }

            HttpResponseMessage response;
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
