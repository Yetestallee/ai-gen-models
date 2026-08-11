using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeshyWorkspace
{
    /// <summary>
    /// Deterministic mock used by UI flows and tests. It does not call the
    /// Meshy API or consume credits.
    /// </summary>
    public sealed class MeshyMockApi : IMeshyApi
    {
        private readonly Dictionary<string, int> pollCounts = new Dictionary<string, int>();
        private int nextId;

        public Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new BalanceResponse { Balance = 1000 });
        }

        public Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-tti") });
        }

        public Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-tt3d") });
        }

        public Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-it3d") });
        }

        public Task<T> GetTaskAsync<T>(string taskId, string taskType, CancellationToken ct = default) where T : MeshyTaskBase
        {
            pollCounts.TryGetValue(taskId, out var count);
            count++;
            pollCounts[taskId] = count;

            var task = (T)Activator.CreateInstance(typeof(T));
            task.Id = taskId;
            task.Type = taskType;

            if (count < 2)
            {
                task.StatusRaw = "PENDING";
                task.Progress = 0;
            }
            else if (count < 4)
            {
                task.StatusRaw = "IN_PROGRESS";
                task.Progress = 50;
            }
            else if (count < 5)
            {
                task.StatusRaw = "IN_PROGRESS";
                task.Progress = 90;
            }
            else
            {
                task.StatusRaw = "SUCCEEDED";
                task.Progress = 100;
                task.ConsumedCredits = 3;
                var imageTask = task as TextToImageTask;
                if (imageTask != null)
                {
                    imageTask.ImageUrls = new List<string>
                    {
                        "https://mock.invalid/image-1.png",
                        "https://mock.invalid/image-2.png"
                    };
                }
            }

            return Task.FromResult(task);
        }

        public Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, CancellationToken ct = default) where T : MeshyTaskBase
        {
            return Task.FromResult(new List<T>());
        }

        public Task DeleteTaskAsync(string taskType, string taskId, CancellationToken ct = default)
        {
            pollCounts.Remove(taskId);
            return Task.CompletedTask;
        }

        private string NewId(string prefix)
        {
            nextId++;
            return prefix + "-" + nextId.ToString("000");
        }
    }
}
