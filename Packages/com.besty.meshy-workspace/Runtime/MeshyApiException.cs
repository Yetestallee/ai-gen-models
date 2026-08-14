using System;
using System.Net;
using Newtonsoft.Json.Linq;

namespace MeshyWorkspace
{
    public sealed class MeshyApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorType { get; }
        public string ErrorCode { get; }
        public bool IsRetryable { get; }
        public int RetryAfterSeconds { get; }
        public string TaskId { get; }

        public MeshyApiException(
            string message,
            HttpStatusCode statusCode,
            string errorType = null,
            string errorCode = null,
            bool isRetryable = false,
            int retryAfterSeconds = 0,
            string taskId = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
            ErrorCode = errorCode;
            IsRetryable = isRetryable;
            RetryAfterSeconds = retryAfterSeconds;
            TaskId = taskId;
        }
    }

    public static class MeshyErrorMapper
    {
        public static string HttpStatusMessage(HttpStatusCode status, string body)
        {
            switch (status)
            {
                case HttpStatusCode.Unauthorized:
                    return "API Key 无效或已过期，请在 Meshy 设置中检查。";
                case (HttpStatusCode)402:
                    return "积分不足，请先充值后再试。";
                case HttpStatusCode.Forbidden:
                    return "没有权限访问该资源，请检查密钥权限。";
                case HttpStatusCode.NotFound:
                    return "任务不存在或已过期。";
                case HttpStatusCode.TooManyRequests:
                    return IsConcurrentLimit(body)
                        ? "并发任务已达上限，请等待当前任务结束后再试。"
                        : "请求过于频繁，请稍后重试。";
                default:
                    if ((int)status >= 500)
                    {
                        return "Meshy 服务暂时不可用，请稍后重试。";
                    }
                    var message = ServerMessage(body);
                    return string.IsNullOrEmpty(message)
                        ? "请求失败（HTTP " + (int)status + "），请检查参数后重试。"
                        : "请求失败（HTTP " + (int)status + "）：" + message;
            }
        }

        public static string ServerMessage(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }
            try
            {
                var obj = JObject.Parse(body);
                var message = obj["message"];
                return message == null ? null : message.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool IsConcurrentLimit(string body)
        {
            return !string.IsNullOrEmpty(body) && body.Contains("NoMoreConcurrentTasks");
        }

        public static string TaskErrorMessage(MeshyTaskError error)
        {
            if (error == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(error.Message))
            {
                return error.Message;
            }

            switch (error.Code)
            {
                case "image_too_complex":
                    return "图片过于复杂，请更换更简单的参考图。";
                case "moderation_blocked":
                    return "内容未通过安全审核，请修改提示词或图片。";
                case "model_missing_uv":
                    return "模型缺少 UV，无法生成贴图。";
                case "model_insufficient_uv":
                    return "模型 UV 不足，请先修复模型。";
                case "format_conversion_failed":
                    return "模型格式转换失败。";
                default:
                    return error.Type == "timeout" ? "任务超时，请稍后重试。" : "任务执行失败。";
            }
        }
    }
}
