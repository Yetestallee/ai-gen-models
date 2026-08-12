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

        public Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-rig") });
        }

        public Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-anim") });
        }

        public Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-retx") });
        }

        public Task<CreateTaskResponse> CreateRemeshAsync(RemeshRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new CreateTaskResponse { Result = NewId("mock-remesh") });
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
                        "https://mock.invalid/image-1.png"
                    };
                }
                var modelTask = task as TextTo3DTask;
                if (modelTask != null)
                {
                    modelTask.ModelUrls = new Dictionary<string, string>
                    {
                        { "glb", "https://mock.invalid/model.glb" }
                    };
                    modelTask.TextureUrls = new List<string>
                    {
                        "https://mock.invalid/base_color.png"
                    };
                }
                var image3dTask = task as ImageTo3DTask;
                if (image3dTask != null)
                {
                    image3dTask.ModelUrls = new Dictionary<string, string>
                    {
                        { "glb", "https://mock.invalid/model.glb" }
                    };
                    image3dTask.TextureUrls = new List<string>
                    {
                        "https://mock.invalid/base_color.png"
                    };
                }
                var rigTask = task as RigTask;
                if (rigTask != null)
                {
                    rigTask.RiggedCharacterGlbUrl = "https://mock.invalid/rigged.glb";
                }
                var animationTask = task as AnimationTask;
                if (animationTask != null)
                {
                    animationTask.AnimatedCharacterFbxUrl = "https://mock.invalid/animated.fbx";
                    animationTask.AnimatedCharacterGlbUrl = "https://mock.invalid/animated.glb";
                }
                var retextureTask = task as RetextureTask;
                if (retextureTask != null)
                {
                    retextureTask.ModelUrls = new Dictionary<string, string>
                    {
                        { "glb", "https://mock.invalid/model.glb" }
                    };
                    retextureTask.TextureUrls = new List<string>
                    {
                        "https://mock.invalid/base_color.png"
                    };
                }
                var remeshTask = task as RemeshTask;
                if (remeshTask != null)
                {
                    remeshTask.ModelUrls = new Dictionary<string, string>
                    {
                        { "glb", "https://mock.invalid/model.glb" }
                    };
                    remeshTask.TextureUrls = new List<string>
                    {
                        "https://mock.invalid/base_color.png"
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
