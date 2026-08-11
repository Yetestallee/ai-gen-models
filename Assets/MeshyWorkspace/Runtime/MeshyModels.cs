using System.Collections.Generic;
using Newtonsoft.Json;

namespace MeshyWorkspace
{
    public enum MeshyTaskStatus
    {
        Pending,
        InProgress,
        Succeeded,
        Failed,
        Canceled,
        Unknown
    }

    public static class MeshyTaskStatusExtensions
    {
        public static MeshyTaskStatus Parse(string value)
        {
            switch (value)
            {
                case "PENDING": return MeshyTaskStatus.Pending;
                case "IN_PROGRESS": return MeshyTaskStatus.InProgress;
                case "SUCCEEDED": return MeshyTaskStatus.Succeeded;
                case "FAILED": return MeshyTaskStatus.Failed;
                case "CANCELED": return MeshyTaskStatus.Canceled;
                default: return MeshyTaskStatus.Unknown;
            }
        }
    }

    public class MeshyTaskError
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("doc_url")]
        public string DocUrl { get; set; }
    }

    public class MeshyTaskBase
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string StatusRaw { get; set; }

        [JsonIgnore]
        public MeshyTaskStatus Status
        {
            get { return MeshyTaskStatusExtensions.Parse(StatusRaw); }
        }

        [JsonProperty("progress")]
        public int Progress { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("finished_at")]
        public string FinishedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }

        [JsonProperty("task_error")]
        public MeshyTaskError TaskError { get; set; }

        [JsonProperty("consumed_credits")]
        public double ConsumedCredits { get; set; }

        [JsonProperty("preceding_tasks")]
        public List<string> PrecedingTasks { get; set; }
    }

    public class TextToImageTask : MeshyTaskBase
    {
        [JsonProperty("image_urls")]
        public List<string> ImageUrls { get; set; }
    }

    public class TextTo3DTask : MeshyTaskBase
    {
        [JsonProperty("model_urls")]
        public Dictionary<string, string> ModelUrls { get; set; }

        [JsonProperty("texture_urls")]
        public Dictionary<string, string> TextureUrls { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }

    public class ImageTo3DTask : MeshyTaskBase
    {
        [JsonProperty("model_urls")]
        public Dictionary<string, string> ModelUrls { get; set; }

        [JsonProperty("texture_urls")]
        public Dictionary<string, string> TextureUrls { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }

    public class RigTask : MeshyTaskBase
    {
        [JsonProperty("rigged_character_glb_url")]
        public string RiggedCharacterGlbUrl { get; set; }
    }

    public class AnimationTask : MeshyTaskBase
    {
        [JsonProperty("animated_character_fbx_url")]
        public string AnimatedCharacterFbxUrl { get; set; }

        [JsonProperty("animated_character_glb_url")]
        public string AnimatedCharacterGlbUrl { get; set; }
    }

    public class BalanceResponse
    {
        [JsonProperty("balance")]
        public double Balance { get; set; }
    }

    public class CreateTaskResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }
    }

    public class MeshyTaskList<T> where T : MeshyTaskBase
    {
        [JsonProperty("data")]
        public List<T> Data { get; set; }
    }

    public class TextToImageRequest
    {
        [JsonProperty("ai_model")]
        public string AiModel { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("aspect_ratio")]
        public string AspectRatio { get; set; }

        [JsonProperty("generate_multi_view")]
        public bool GenerateMultiView { get; set; }

        [JsonProperty("pose_mode")]
        public string PoseMode { get; set; }
    }

    public class TextTo3DRequest
    {
        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("ai_model")]
        public string AiModel { get; set; }

        [JsonProperty("model_type")]
        public string ModelType { get; set; }

        [JsonProperty("preview_task_id")]
        public string PreviewTaskId { get; set; }

        [JsonProperty("enable_pbr")]
        public bool EnablePbr { get; set; }

        [JsonProperty("texture_resolution")]
        public string TextureResolution { get; set; }

        [JsonProperty("texture_prompt")]
        public string TexturePrompt { get; set; }

        [JsonProperty("pose_mode")]
        public string PoseMode { get; set; }

        [JsonProperty("target_formats")]
        public List<string> TargetFormats { get; set; }
    }

    public class ImageTo3DRequest
    {
        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("input_task_id")]
        public string InputTaskId { get; set; }

        [JsonProperty("should_texture")]
        public bool ShouldTexture { get; set; }

        [JsonProperty("enable_pbr")]
        public bool EnablePbr { get; set; }

        [JsonProperty("model_type")]
        public string ModelType { get; set; }

        [JsonProperty("ai_model")]
        public string AiModel { get; set; }

        [JsonProperty("pose_mode")]
        public string PoseMode { get; set; }
    }
}
