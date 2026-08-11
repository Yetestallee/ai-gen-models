using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public sealed class MeshyWorkspaceWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/MeshyWorkspace/Editor/UI/MeshyWorkspaceWindow.uxml";
        private const string UssPath = "Assets/MeshyWorkspace/Editor/UI/MeshyWorkspaceWindow.uss";

        private Label balanceLabel;

        [MenuItem("Window/Meshy Workspace")]
        public static void Open()
        {
            var window = GetWindow<MeshyWorkspaceWindow>("Meshy Workspace");
            window.minSize = new Vector2(960, 600);
            window.Show();
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (tree == null || sheet == null)
            {
                Debug.LogError("[Meshy P2] 未找到工作区 UXML/USS 资源。");
                return;
            }

            rootVisualElement.Add(tree.CloneTree());
            rootVisualElement.styleSheets.Add(sheet);
            Bind();
        }

        private void Bind()
        {
            balanceLabel = rootVisualElement.Q<Label>("BalanceLabel");

            BindSidebar("SidebarImage", "ImageView");
            BindSidebar("SidebarModel", "ModelView");
            BindSidebar("SidebarAnimate", "AnimateView");

            BindPlaceholder("SidebarAssets", "资产区将在 P6 开放");
            BindPlaceholder("SidebarAgent", "智能体区将在 P6 开放");
            BindPlaceholder("SidebarPrint", "打印区将在 P6 开放");

            var refresh = rootVisualElement.Q<Button>("RefreshBalanceButton");
            if (refresh != null)
            {
                refresh.clicked += RefreshBalance;
            }

            var settings = rootVisualElement.Q<Button>("SettingsButton");
            if (settings != null)
            {
                settings.clicked += MeshySettingsWindow.Open;
            }

            var help = rootVisualElement.Q<Button>("HelpButton");
            if (help != null)
            {
                help.clicked += () => Application.OpenURL("https://docs.meshy.ai/zh");
            }

            ShowView("ImageView", "SidebarImage");
        }

        private void BindSidebar(string buttonName, string viewName)
        {
            var button = rootVisualElement.Q<Button>(buttonName);
            if (button != null)
            {
                button.clicked += () => ShowView(viewName, buttonName);
            }
        }

        private void BindPlaceholder(string buttonName, string message)
        {
            var button = rootVisualElement.Q<Button>(buttonName);
            if (button != null)
            {
                button.clicked += () => Debug.Log("[Meshy P2] " + message);
            }
        }

        private void ShowView(string viewName, string buttonName)
        {
            SetVisible("ImageView", viewName == "ImageView");
            SetVisible("ModelView", viewName == "ModelView");
            SetVisible("AnimateView", viewName == "AnimateView");
            UpdateSidebarActive(buttonName);
        }

        private void SetVisible(string name, bool visible)
        {
            var element = rootVisualElement.Q<VisualElement>(name);
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateSidebarActive(string activeName)
        {
            var names = new[] { "SidebarImage", "SidebarModel", "SidebarAnimate" };
            foreach (var name in names)
            {
                var button = rootVisualElement.Q<Button>(name);
                if (button != null)
                {
                    button.RemoveFromClassList("active");
                }
            }

            var active = rootVisualElement.Q<Button>(activeName);
            if (active != null)
            {
                active.AddToClassList("active");
            }
        }

        private async void RefreshBalance()
        {
            if (balanceLabel == null)
            {
                return;
            }

            if (!MeshySettings.HasApiKey)
            {
                balanceLabel.text = "余额 --（未配置 Key）";
                return;
            }

            balanceLabel.text = "余额刷新中...";
            try
            {
                var config = new MeshyApiConfig
                {
                    ApiKey = MeshySettings.ApiKey,
                    ProxyUrl = MeshySettings.ProxyUrl,
                    TimeoutSeconds = MeshySettings.TimeoutSeconds
                };

                using (var client = new MeshyApiClient(config))
                {
                    var balance = await client.GetBalanceAsync();
                    balanceLabel.text = "余额 " + balance.Balance;
                }
            }
            catch (Exception e)
            {
                balanceLabel.text = "余额获取失败";
                Debug.LogWarning("[Meshy P2] 余额刷新失败: " + e.Message);
            }
        }

        [MenuItem("Meshy Workspace/Validate Layout")]
        public static void ValidateLayout()
        {
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            var root = window.rootVisualElement;
            var required = new[]
            {
                "BalanceLabel",
                "RefreshBalanceButton",
                "SettingsButton",
                "HelpButton",
                "SidebarAssets",
                "SidebarAgent",
                "SidebarImage",
                "SidebarModel",
                "SidebarPrint",
                "SidebarAnimate",
                "ImageView",
                "ModelView",
                "AnimateView",
                "RightPanel"
            };

            var missing = new List<string>();
            foreach (var name in required)
            {
                if (root.Q<VisualElement>(name) == null)
                {
                    missing.Add(name);
                }
            }

            var views = new[] { "ImageView", "ModelView", "AnimateView" };
            var buttons = new[] { "SidebarImage", "SidebarModel", "SidebarAnimate" };
            var results = new List<string>();
            ValidateViewSequence(window, root, views, buttons, 0, missing, results);
        }

        private static void ValidateViewSequence(
            MeshyWorkspaceWindow window,
            VisualElement root,
            string[] views,
            string[] buttons,
            int index,
            List<string> missing,
            List<string> results)
        {
            if (index >= views.Length)
            {
                FinishValidation(root, missing, results);
                return;
            }

            window.ShowView(views[index], buttons[index]);
            EditorApplication.delayCall += () =>
            {
                results.Add(
                    views[index] + "=" + IsVisible(root, views[index]) +
                    " sized=" + IsSized(root, views[index]));
                ValidateViewSequence(window, root, views, buttons, index + 1, missing, results);
            };
        }

        private static void FinishValidation(VisualElement root, List<string> missing, List<string> results)
        {
            var ok = missing.Count == 0 && results.TrueForAll(r => r.Contains("=True"));
            var lines = new List<string>
            {
                "layout=" + (ok ? "OK" : "FAILED"),
                "missing=" + (missing.Count == 0 ? "none" : string.Join(",", missing))
            };
            lines.AddRange(results);
            var report = string.Join(Environment.NewLine, lines);

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p2-validation.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + report + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P2] 写入布局验证报告失败: " + e.Message);
            }

            Debug.Log("[Meshy P2] 布局验证: " + (ok ? "OK" : "FAILED"));
        }

        private static bool IsVisible(VisualElement root, string name)
        {
            var element = root.Q<VisualElement>(name);
            return element != null && element.style.display.value == DisplayStyle.Flex;
        }

        private static bool IsSized(VisualElement root, string name)
        {
            var element = root.Q<VisualElement>(name);
            return element != null && element.worldBound.width > 0f && element.worldBound.height > 0f;
        }
    }
}
