using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeshyWorkspace.Tests
{
    public sealed class FakeMeshyApi : IMeshyApi
    {
        private readonly List<MeshyTaskBase> sequence;
        private int index;

        public FakeMeshyApi(params MeshyTaskBase[] sequence)
        {
            this.sequence = new List<MeshyTaskBase>(sequence);
        }

        public int GetTaskCalls { get; private set; }

        public Task<T> GetTaskAsync<T>(string taskId, string taskType, CancellationToken ct = default) where T : MeshyTaskBase
        {
            GetTaskCalls++;
            var current = index < sequence.Count ? sequence[index] : sequence[sequence.Count - 1];
            if (index < sequence.Count)
            {
                index++;
            }
            return Task.FromResult(current as T);
        }

        public Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTaskResponse> CreateRemeshAsync(RemeshRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, CancellationToken ct = default) where T : MeshyTaskBase
        {
            throw new NotImplementedException();
        }

        public Task DeleteTaskAsync(string taskType, string taskId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
