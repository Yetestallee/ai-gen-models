using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MeshyWorkspace
{
    public sealed class MeshyTaskPoller
    {
        private readonly IMeshyApi api;
        private readonly TimeSpan interval;
        private readonly int maxAttempts;

        public MeshyTaskPoller(IMeshyApi api, TimeSpan? interval = null, int maxAttempts = 300)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.interval = interval ?? TimeSpan.FromSeconds(2);
            this.maxAttempts = maxAttempts > 0 ? maxAttempts : 1;
        }

        public async Task<T> WaitForTaskAsync<T>(
            string taskId,
            string taskType,
            Action<MeshyTaskBase> onProgress = null,
            CancellationToken ct = default) where T : MeshyTaskBase
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var task = await api.GetTaskAsync<T>(taskId, taskType, ct).ConfigureAwait(false);
                if (task != null)
                {
                    if (onProgress != null)
                    {
                        onProgress(task);
                    }

                    switch (task.Status)
                    {
                        case MeshyTaskStatus.Succeeded:
                            return task;
                        case MeshyTaskStatus.Failed:
                            throw new MeshyApiException(
                                MeshyErrorMapper.TaskErrorMessage(task.TaskError) ?? "任务执行失败。",
                                HttpStatusCode.OK,
                                task.TaskError == null ? null : task.TaskError.Type,
                                task.TaskError == null ? null : task.TaskError.Code);
                        case MeshyTaskStatus.Canceled:
                            throw new MeshyApiException("任务已取消。", HttpStatusCode.OK, errorCode: "canceled");
                    }
                }

                try
                {
                    await Task.Delay(interval, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
            }

            throw new MeshyApiException(
                "任务轮询超时（任务 ID: " + taskId + "），任务可能仍在服务端运行，积分以服务端结算为准。可在历史记录中右键恢复查询。",
                HttpStatusCode.RequestTimeout,
                isRetryable: true,
                taskId: taskId);
        }
    }
}
