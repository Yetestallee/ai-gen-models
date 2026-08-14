using System;
using UnityEngine;

namespace MeshyWorkspace
{
    [Serializable]
    public sealed class MeshyRuntimeSettings
    {
        public string ApiKey = string.Empty;
        public string ProxyUrl = string.Empty;
        public int TimeoutSeconds = 30;
        public bool UseMockMode = true;

        public MeshyApiConfig ToApiConfig()
        {
            return new MeshyApiConfig
            {
                ApiKey = ApiKey ?? string.Empty,
                ProxyUrl = ProxyUrl ?? string.Empty,
                TimeoutSeconds = Mathf.Clamp(TimeoutSeconds, 5, 120)
            };
        }
    }

    public static class MeshyRuntimeSettingsStore
    {
        private const string SettingsKey = "MeshyWorkspace.RuntimeSettings";

        public static MeshyRuntimeSettings Load()
        {
            var json = PlayerPrefs.GetString(SettingsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new MeshyRuntimeSettings();
            }

            try
            {
                return JsonUtility.FromJson<MeshyRuntimeSettings>(json) ?? new MeshyRuntimeSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy] 运行时设置读取失败，已使用默认值: " + e.Message);
                return new MeshyRuntimeSettings();
            }
        }

        public static void Save(MeshyRuntimeSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.TimeoutSeconds = Mathf.Clamp(settings.TimeoutSeconds, 5, 120);
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }
    }
}
