using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeshyWorkspace
{
    public interface IMeshyApi
    {
        Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default);

        Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, CancellationToken ct = default);

        Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default);

        Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default);

        Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default);

        Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default);

        Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default);

        Task<T> GetTaskAsync<T>(string taskId, string taskType, CancellationToken ct = default) where T : MeshyTaskBase;

        Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, CancellationToken ct = default) where T : MeshyTaskBase;

        Task DeleteTaskAsync(string taskType, string taskId, CancellationToken ct = default);
    }
}
