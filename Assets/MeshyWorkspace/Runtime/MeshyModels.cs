using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public int PrecedingTasks { get; set; }
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
        [JsonConverter(typeof(StringListOrDictionaryConverter))]
        public List<string> TextureUrls { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }

    public class ImageTo3DTask : MeshyTaskBase
    {
        [JsonProperty("model_urls")]
        public Dictionary<string, string> ModelUrls { get; set; }

        [JsonProperty("texture_urls")]
        [JsonConverter(typeof(StringListOrDictionaryConverter))]
        public List<string> TextureUrls { get; set; }

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

        [JsonProperty("result")]
        public AnimationTaskResult Result { get; set; }

        [JsonIgnore]
        public string EffectiveGlbUrl
        {
            get
            {
                return !string.IsNullOrEmpty(AnimatedCharacterGlbUrl)
                    ? AnimatedCharacterGlbUrl
                    : Result == null ? string.Empty : Result.AnimationGlbUrl;
            }
        }

        [JsonIgnore]
        public string EffectiveFbxUrl
        {
            get
            {
                return !string.IsNullOrEmpty(AnimatedCharacterFbxUrl)
                    ? AnimatedCharacterFbxUrl
                    : Result == null ? string.Empty : Result.AnimationFbxUrl;
            }
        }
    }

    public class AnimationTaskResult
    {
        [JsonProperty("animation_glb_url")]
        public string AnimationGlbUrl { get; set; }

        [JsonProperty("animation_fbx_url")]
        public string AnimationFbxUrl { get; set; }

        [JsonProperty("processed_usdz_url")]
        public string ProcessedUsdzUrl { get; set; }

        [JsonProperty("processed_armature_fbx_url")]
        public string ProcessedArmatureFbxUrl { get; set; }

        [JsonProperty("processed_animation_fps_fbx_url")]
        public string ProcessedAnimationFpsFbxUrl { get; set; }
    }

    public class RetextureTask : MeshyTaskBase
    {
        [JsonProperty("model_urls")]
        public Dictionary<string, string> ModelUrls { get; set; }

        [JsonProperty("texture_urls")]
        [JsonConverter(typeof(StringListOrDictionaryConverter))]
        public List<string> TextureUrls { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
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

        [JsonProperty("pose_mode", NullValueHandling = NullValueHandling.Ignore)]
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

        [JsonProperty("model_type", NullValueHandling = NullValueHandling.Ignore)]
        public string ModelType { get; set; }

        [JsonProperty("preview_task_id", NullValueHandling = NullValueHandling.Ignore)]
        public string PreviewTaskId { get; set; }

        [JsonProperty("enable_pbr")]
        public bool EnablePbr { get; set; }

        [JsonProperty("texture_resolution", NullValueHandling = NullValueHandling.Ignore)]
        public string TextureResolution { get; set; }

        [JsonProperty("texture_prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string TexturePrompt { get; set; }

        [JsonProperty("pose_mode", NullValueHandling = NullValueHandling.Ignore)]
        public string PoseMode { get; set; }

        [JsonProperty("target_formats", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> TargetFormats { get; set; }
    }

    public class ImageTo3DRequest
    {
        [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageUrl { get; set; }

        [JsonProperty("input_task_id", NullValueHandling = NullValueHandling.Ignore)]
        public string InputTaskId { get; set; }

        [JsonProperty("should_texture")]
        public bool ShouldTexture { get; set; }

        [JsonProperty("enable_pbr")]
        public bool EnablePbr { get; set; }

        [JsonProperty("model_type", NullValueHandling = NullValueHandling.Ignore)]
        public string ModelType { get; set; }

        [JsonProperty("ai_model", NullValueHandling = NullValueHandling.Ignore)]
        public string AiModel { get; set; }

        [JsonProperty("pose_mode", NullValueHandling = NullValueHandling.Ignore)]
        public string PoseMode { get; set; }
    }

    public class RiggingRequest
    {
        [JsonProperty("input_task_id", NullValueHandling = NullValueHandling.Ignore)]
        public string InputTaskId { get; set; }

        [JsonProperty("model_url", NullValueHandling = NullValueHandling.Ignore)]
        public string ModelUrl { get; set; }

        [JsonProperty("height_meters", NullValueHandling = NullValueHandling.Ignore)]
        public double? HeightMeters { get; set; }
    }

    public class AnimationRequest
    {
        [JsonProperty("rig_task_id")]
        public string RigTaskId { get; set; }

        [JsonProperty("action_id")]
        public int ActionId { get; set; }

        [JsonProperty("post_process", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> PostProcess { get; set; }
    }

    public class RetextureRequest
    {
        [JsonProperty("input_task_id", NullValueHandling = NullValueHandling.Ignore)]
        public string InputTaskId { get; set; }

        [JsonProperty("model_url", NullValueHandling = NullValueHandling.Ignore)]
        public string ModelUrl { get; set; }

        [JsonProperty("text_style_prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string TextStylePrompt { get; set; }

        [JsonProperty("image_style_url", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageStyleUrl { get; set; }
    }

    public sealed class StringListOrDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType)
        {
            return objectType == typeof(List<string>);
        }

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var result = new List<string>();
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                foreach (var item in token)
                {
                    if (item is JObject itemObject)
                    {
                        foreach (var property in itemObject.Properties())
                        {
                            result.Add(property.Value.ToString());
                        }
                    }
                    else
                    {
                        result.Add(item.ToString());
                    }
                }
            }
            else if (token.Type == JTokenType.Object)
            {
                foreach (var property in ((JObject)token).Properties())
                {
                    result.Add(property.Value.ToString());
                }
            }
            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }
    }
}
