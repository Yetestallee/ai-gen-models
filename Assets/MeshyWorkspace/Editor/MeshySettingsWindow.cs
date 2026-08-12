using UnityEditor;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    public sealed class MeshySettingsWindow : EditorWindow
    {
        private string apiKey;
        private string proxyUrl;
        private int timeoutSeconds = 30;
        private bool useMockMode = true;

        [MenuItem("Meshy Workspace/Settings...")]
        public static void Open()
        {
            GetWindow<MeshySettingsWindow>(true, "Meshy 设置");
        }

        private void OnEnable()
        {
            apiKey = MeshySettings.ApiKey;
            proxyUrl = MeshySettings.ProxyUrl;
            timeoutSeconds = MeshySettings.TimeoutSeconds;
            useMockMode = MeshySettings.UseMockMode;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("API", EditorStyles.boldLabel);
            apiKey = EditorGUILayout.PasswordField("Meshy API Key", apiKey);
            EditorGUILayout.LabelField("网络", EditorStyles.boldLabel);
            proxyUrl = EditorGUILayout.TextField("代理地址（可选）", proxyUrl);
            timeoutSeconds = EditorGUILayout.IntSlider("超时（秒）", timeoutSeconds, 5, 120);
            useMockMode = EditorGUILayout.Toggle("模拟模式（不消耗积分）", useMockMode);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("保存设置"))
            {
                MeshySettings.ApiKey = apiKey;
                MeshySettings.ProxyUrl = proxyUrl;
                MeshySettings.TimeoutSeconds = timeoutSeconds;
                MeshySettings.UseMockMode = useMockMode;
                Close();
                Debug.Log("[Meshy] 设置已保存到 EditorPrefs。");
            }
        }
    }
}
