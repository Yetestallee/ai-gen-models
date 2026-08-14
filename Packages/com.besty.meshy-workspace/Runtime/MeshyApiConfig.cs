using System;

namespace MeshyWorkspace
{
    [Serializable]
    public sealed class MeshyApiConfig
    {
        public string BaseUrl { get; set; } = "https://api.meshy.ai";
        public string ApiKey { get; set; } = string.Empty;
        public string ProxyUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;

        public MeshyApiConfig()
        {
        }

        public MeshyApiConfig(string apiKey)
        {
            ApiKey = apiKey ?? string.Empty;
        }
    }
}
