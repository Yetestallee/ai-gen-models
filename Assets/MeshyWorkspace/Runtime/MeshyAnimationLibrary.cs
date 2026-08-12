using System.Collections.Generic;
using Newtonsoft.Json;

namespace MeshyWorkspace
{
    public class AnimationAction
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("subcategory")]
        public string Subcategory { get; set; }
    }

    public static class MeshyAnimationLibrary
    {
        public static List<AnimationAction> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<AnimationAction>();
            }
            return JsonConvert.DeserializeObject<List<AnimationAction>>(json) ?? new List<AnimationAction>();
        }
    }
}
