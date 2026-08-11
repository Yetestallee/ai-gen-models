using UnityEditor;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    /// <summary>
    /// Editor-only configuration backed by EditorPrefs. The API key is stored
    /// outside the project so it never lands in scenes, logs, or source control.
    /// </summary>
    public static class MeshySettings
    {
        private const string ApiKeyPref = "MeshyWorkspace.ApiKey";
        private const string ProxyUrlPref = "MeshyWorkspace.ProxyUrl";
        private const string TimeoutPref = "MeshyWorkspace.TimeoutSeconds";

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPref, string.Empty);
            set => EditorPrefs.SetString(ApiKeyPref, value?.Trim() ?? string.Empty);
        }

        public static string ProxyUrl
        {
            get => EditorPrefs.GetString(ProxyUrlPref, string.Empty);
            set => EditorPrefs.SetString(ProxyUrlPref, value?.Trim() ?? string.Empty);
        }

        public static int TimeoutSeconds
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(TimeoutPref, 30), 5, 120);
            set => EditorPrefs.SetInt(TimeoutPref, Mathf.Clamp(value, 5, 120));
        }

        public static bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public static void ClearApiKey() => EditorPrefs.DeleteKey(ApiKeyPref);

        [MenuItem("Meshy Workspace/Set API Key From Clipboard")]
        public static void SetApiKeyFromClipboard()
        {
            var text = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError("[Meshy] 剪贴板为空，无法设置 API Key。");
                return;
            }

            ApiKey = text.Trim();
            Debug.Log("[Meshy] API Key 已从剪贴板保存到 EditorPrefs。");
        }
    }
}
