using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public sealed partial class MeshyWorkspaceWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/MeshyWorkspace/Editor/UI/MeshyWorkspaceWindow.uxml";
        private const string UssPath = "Assets/MeshyWorkspace/Editor/UI/MeshyWorkspaceWindow.uss";

        private Label balanceLabel;

        [MenuItem("Window/Meshy Workspace")]
        public static void Open()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>("Meshy Workspace");
            window.minSize = new Vector2(1280, 720);
            window.position = new Rect(40, 40, 1280, 720);
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

            MeshyUiDispatcher.Capture();
            BindImagePage();
            BindModelPage();
            BindAnimatePage();
            ConfigureParamScrollViews();
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

        private void ConfigureParamScrollViews()
        {
            foreach (var name in new[] { "ImageParams", "ModelParams", "AnimateParams" })
            {
                var scrollView = rootVisualElement.Q<ScrollView>(name);
                if (scrollView == null)
                {
                    continue;
                }

                scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
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
                    imageBalance = balance.Balance;
                    UpdateImageCost();
                }
            }
            catch (Exception e)
            {
                balanceLabel.text = "余额获取失败";
                imageBalance = -1;
                UpdateImageCost();
                Debug.LogWarning("[Meshy P2] 余额刷新失败: " + e.Message);
            }
        }

    }
}
