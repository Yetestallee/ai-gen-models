using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using TextCoreFontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace MeshyWorkspace
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MeshyWorkspaceApp : MonoBehaviour
    {
        private static readonly Queue<Action> mainActions = new Queue<Action>();
        private const int HistoryPageSize = 8;
        private const int AnimateActionRenderLimit = 200;
        private const string AnimationLibraryResourcePath = "MeshyAnimationLibrary";

        [SerializeField] private VisualTreeAsset workspaceTree;
        [SerializeField] private StyleSheet workspaceStyle;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private RenderTexture previewTexture;
        [SerializeField] private TextCoreFontAsset runtimeFontAsset;

        private UIDocument document;
        private VisualElement root;
        private MeshyRuntimeSettings settings;
        private MeshyTaskCache cache;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private MeshyRuntimeModelPreviewHost modelPreviewHost;
        private MeshyRuntimeModelPreviewHost animatePreviewHost;

        private DropdownField imageModelDropdown;
        private TextField imagePromptField;
        private TextField imageSearchField;
        private Label imageCharCountLabel;
        private Toggle imageMultiViewToggle;
        private Label imageReferenceLabel;
        private Label imageCostLabel;
        private Button imageGenerateButton;
        private ProgressBar imageProgressBar;
        private Label imageStatusLabel;
        private VisualElement imageResultGrid;
        private Label imageEmptyLabel;
        private ScrollView imageHistoryList;
        private Button imageHistoryMoreButton;
        private int imageHistoryPage;
        private string imageAspect = "1:1";
        private string imagePose;
        private int imageCount = 1;
        private bool imageGenerating;

        private DropdownField modelAiDropdown;
        private DropdownField modelTopologyAiDropdown;
        private DropdownField modelLicenseDropdown;
        private IntegerField modelTopologyFacesField;
        private TextField modelPromptField;
        private TextField modelImageTaskField;
        private Label modelCharCountLabel;
        private Label modelCostLabel;
        private Label modelStatusLabel;
        private Label modelStatsLabel;
        private Toggle modelAutoSplitToggle;
        private Toggle modelUltraToggle;
        private Toggle modelEnhanceToggle;
        private Button modelPreviewButton;
        private Button modelRefineButton;
        private Button modelImportButton;
        private Button modelRetextureButton;
        private Button modelRemeshButton;
        private TextField modelRetexturePromptField;
        private TextField modelRetextureImageUrlField;
        private IntegerField modelRemeshFacesField;
        private Image modelPreviewImage;
        private ProgressBar modelProgressBar;
        private ScrollView modelHistoryList;
        private string modelMode = "standard";
        private string modelTopology = "triangle";
        private string modelRemeshTopology = "triangle";
        private int modelTopologyFaces = 30000;
        private int modelRemeshFaces = 30000;
        private string modelPose;
        private string modelPreviewTaskId;
        private string modelLastGlbPath;
        private MeshyCachedTask modelSelectedEntry;
        private bool modelGenerating;

        private Button animateModelButton;
        private Label animateModelLabel;
        private Button animateRigButton;
        private Label animateRigLabel;
        private TextField animateSearchField;
        private DropdownField animateCategoryDropdown;
        private ScrollView animateActionList;
        private Label animateCostLabel;
        private Label animateStatusLabel;
        private ProgressBar animateProgressBar;
        private Button animateGenerateButton;
        private Button animatePlayButton;
        private Button animatePauseButton;
        private Button animateResetButton;
        private Image animatePreviewImage;
        private ScrollView animateHistoryList;
        private Toggle animateFpsToggle;
        private Toggle animateFbxToggle;
        private Toggle animateArmatureToggle;
        private readonly List<AnimationAction> animationActions = new List<AnimationAction>();
        private AnimationAction selectedAction;
        private MeshyCachedTask animateModelTask;
        private string animateModelGlbPath;
        private string animateRigTaskId;
        private ScrollView tasksList;

        private void Awake()
        {
            MeshyPaths.RootOverride = MeshyPaths.PersistentRoot;
            Directory.CreateDirectory(MeshyPaths.Root);
            Directory.CreateDirectory(HistoryFolder);

            settings = MeshyRuntimeSettingsStore.Load();
            cache = new MeshyTaskCache(HistoryPath);
            MigrateLegacyHistory();
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BuildDocument();
            BindCommon();
            BindImagePage();
            BindModelPage();
            BindAnimatePage();
            BindTasksPage();
            ShowView("ImageView", "SidebarImage");
            RefreshBalance();
        }

        private void Update()
        {
            DrainMainActions();
            modelPreviewHost?.Tick(Time.deltaTime);
            animatePreviewHost?.Tick(Time.deltaTime);
        }

        private string HistoryFolder
        {
            get { return Path.Combine(Application.persistentDataPath, "MeshyWorkspace", "History"); }
        }

        private string HistoryPath
        {
            get { return Path.Combine(HistoryFolder, "tasks.json"); }
        }

        private void BuildDocument()
        {
            root = document.rootVisualElement;
            root.Clear();
            if (workspaceTree != null)
            {
                root.Add(workspaceTree.CloneTree());
            }
            if (workspaceStyle != null)
            {
                root.styleSheets.Add(workspaceStyle);
            }
            ApplyRuntimeFont();
        }

        private void ApplyRuntimeFont()
        {
            if (runtimeFontAsset == null)
            {
                return;
            }

            root.style.unityFont = new StyleFont(StyleKeyword.Null);
            root.style.unityFontDefinition = new StyleFontDefinition(runtimeFontAsset);
        }

        private void BindCommon()
        {
            BindSidebar("SidebarImage", "ImageView");
            BindSidebar("SidebarModel", "ModelView");
            BindSidebar("SidebarAnimate", "AnimateView");
            BindSidebar("SidebarTasks", "TasksView");

            var refresh = root.Q<Button>("RefreshBalanceButton");
            if (refresh != null)
            {
                refresh.clicked += RefreshBalance;
            }

            var settingsButton = root.Q<Button>("SettingsButton");
            if (settingsButton != null)
            {
                settingsButton.clicked += ToggleSettingsPanel;
            }

            var help = root.Q<Button>("HelpButton");
            if (help != null)
            {
                help.clicked += () => Application.OpenURL("https://docs.meshy.ai/zh");
            }

            var exit = root.Q<Button>("ExitButton");
            if (exit != null)
            {
                exit.clicked += ExitApp;
            }
        }

        private void BindSidebar(string buttonName, string viewName)
        {
            var button = root.Q<Button>(buttonName);
            if (button != null)
            {
                button.clicked += () => ShowView(viewName, buttonName);
            }
        }

        private void ExitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string PickReferenceImage()
        {
#if UNITY_STANDALONE_WIN
            return MeshyWindowsFileDialog.PickImageFile();
#else
            return MeshyRuntimeDownloads.TryReadClipboardPathOrUrl();
#endif
        }

        private static void PostMain(Action action)
        {
            if (action == null)
            {
                return;
            }
            lock (mainActions)
            {
                mainActions.Enqueue(action);
            }
        }

        private static void DrainMainActions()
        {
            while (true)
            {
                Action action;
                lock (mainActions)
                {
                    if (mainActions.Count == 0)
                    {
                        break;
                    }
                    action = mainActions.Dequeue();
                }
                action();
            }
        }

        private void ShowView(string viewName, string buttonName)
        {
            SetVisible("ImageView", viewName == "ImageView");
            SetVisible("ModelView", viewName == "ModelView");
            SetVisible("AnimateView", viewName == "AnimateView");
            SetVisible("TasksView", viewName == "TasksView");
            foreach (var name in new[] { "SidebarImage", "SidebarModel", "SidebarAnimate", "SidebarTasks" })
            {
                var button = root.Q<Button>(name);
                if (button != null)
                {
                    button.RemoveFromClassList("active");
                }
            }
            var active = root.Q<Button>(buttonName);
            if (active != null)
            {
                active.AddToClassList("active");
            }
        }

        private void SetVisible(string name, bool visible)
        {
            var element = root.Q<VisualElement>(name);
            if (element == null)
            {
                return;
            }
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                element.RemoveFromClassList("hidden");
            }
            else
            {
                element.AddToClassList("hidden");
            }
        }

        private void BindImagePage()
        {
            imageModelDropdown = root.Q<DropdownField>("ImageModelDropdown");
            imagePromptField = root.Q<TextField>("ImagePromptField");
            imageSearchField = root.Q<TextField>("ImageSearchField");
            imageCharCountLabel = root.Q<Label>("ImageCharCountLabel");
            imageMultiViewToggle = root.Q<Toggle>("ImageMultiViewToggle");
            imageReferenceLabel = root.Q<Label>("ImageReferenceLabel");
            imageCostLabel = root.Q<Label>("ImageCostLabel");
            imageGenerateButton = root.Q<Button>("ImageGenerateButton");
            imageProgressBar = root.Q<ProgressBar>("ImageProgressBar");
            imageStatusLabel = root.Q<Label>("ImageStatusLabel");
            imageResultGrid = root.Q<VisualElement>("ImageResultGrid");
            imageEmptyLabel = root.Q<Label>("ImageEmptyLabel");
            imageHistoryList = root.Q<ScrollView>("ImageHistoryList");
            imageHistoryMoreButton = root.Q<Button>("ImageHistoryMoreButton");

            if (imageModelDropdown != null)
            {
                imageModelDropdown.choices = new List<string> { "nano-banana", "nano-banana-2", "nano-banana-pro", "gpt-image-2" };
                imageModelDropdown.index = 0;
                imageModelDropdown.RegisterValueChangedCallback(_ => UpdateImageCost());
            }
            if (imagePromptField != null)
            {
                imagePromptField.maxLength = 800;
                imagePromptField.RegisterValueChangedCallback(evt => imageCharCountLabel.text = evt.newValue.Length + "/800");
            }
            BindSegmented("AspectSegments", new[] { "1:1", "16:9", "9:16", "4:3", "3:4" }, value => imageAspect = value);
            BindSegmented("CountSegments", new[] { "1", "2", "3", "4" }, value => imageCount = int.Parse(value));
            BindSegmented("PoseSegments", new[] { "无", "A 姿势", "T 姿势" }, value => imagePose = MapPose(value));
            if (imageSearchField != null)
            {
                imageSearchField.RegisterValueChangedCallback(_ => RefreshImageHistory());
            }
            if (imageGenerateButton != null)
            {
                imageGenerateButton.clicked += () => _ = GenerateImageAsync();
            }
            if (imageHistoryMoreButton != null)
            {
                imageHistoryMoreButton.clicked += () =>
                {
                    imageHistoryPage++;
                    RenderImageHistory();
                };
            }
            var upload = root.Q<Button>("ImageUploadButton");
            if (upload != null)
            {
                upload.text = "粘贴图片路径/URL 后点击";
                upload.clicked += () =>
                {
                    var text = PickReferenceImage();
                    if (string.IsNullOrEmpty(text))
                    {
                        text = MeshyRuntimeDownloads.TryReadClipboardPathOrUrl();
                    }
                    imageReferenceLabel.text = string.IsNullOrEmpty(text) ? "剪贴板没有可用图片路径或 URL" : "已读取参考：" + ShortName(text);
                };
            }
            UpdateImageCost();
            SetImageStatus("就绪", false);
            RefreshImageHistory();
        }

        private void BindModelPage()
        {
            modelAiDropdown = root.Q<DropdownField>("ModelAiDropdown");
            modelTopologyAiDropdown = root.Q<DropdownField>("ModelTopologyAiDropdown");
            modelLicenseDropdown = root.Q<DropdownField>("ModelLicenseDropdown");
            modelTopologyFacesField = root.Q<IntegerField>("ModelTopologyFacesField");
            modelPromptField = root.Q<TextField>("ModelPromptField");
            modelImageTaskField = root.Q<TextField>("ModelImageTaskField");
            modelCharCountLabel = root.Q<Label>("ModelCharCountLabel");
            modelCostLabel = root.Q<Label>("ModelCostLabel");
            modelStatusLabel = root.Q<Label>("ModelStatusLabel");
            modelStatsLabel = root.Q<Label>("ModelStatsLabel");
            modelAutoSplitToggle = root.Q<Toggle>("ModelAutoSplitToggle");
            modelUltraToggle = root.Q<Toggle>("ModelUltraToggle");
            modelEnhanceToggle = root.Q<Toggle>("ModelEnhanceToggle");
            modelPreviewButton = root.Q<Button>("ModelPreviewButton");
            modelRefineButton = root.Q<Button>("ModelRefineButton");
            modelImportButton = root.Q<Button>("ModelImportButton");
            modelRetextureButton = root.Q<Button>("ModelRetextureButton");
            modelRemeshButton = root.Q<Button>("ModelRemeshButton");
            modelRetexturePromptField = root.Q<TextField>("ModelRetexturePromptField");
            modelRetextureImageUrlField = root.Q<TextField>("ModelRetextureImageUrlField");
            modelRemeshFacesField = root.Q<IntegerField>("ModelRemeshFacesField");
            modelPreviewImage = root.Q<Image>("ModelPreviewImage");
            modelProgressBar = root.Q<ProgressBar>("ModelProgressBar");
            modelHistoryList = root.Q<ScrollView>("ModelHistoryList");

            if (modelAiDropdown != null)
            {
                modelAiDropdown.choices = new List<string> { "meshy-5", "meshy-6", "latest" };
                modelAiDropdown.index = 1;
                modelAiDropdown.RegisterValueChangedCallback(_ => UpdateModelCost());
            }
            if (modelTopologyAiDropdown != null)
            {
                modelTopologyAiDropdown.choices = new List<string> { "meshy T1", "meshy T2" };
                modelTopologyAiDropdown.index = 0;
            }
            if (modelLicenseDropdown != null)
            {
                modelLicenseDropdown.choices = new List<string> { "CC BY 4.0", "私有" };
                modelLicenseDropdown.index = 0;
            }
            if (modelPromptField != null)
            {
                modelPromptField.maxLength = 600;
                modelPromptField.RegisterValueChangedCallback(evt => modelCharCountLabel.text = evt.newValue.Length + "/600");
            }
            if (modelTopologyFacesField != null)
            {
                modelTopologyFacesField.value = modelTopologyFaces;
                modelTopologyFacesField.RegisterValueChangedCallback(evt => modelTopologyFaces = Mathf.Clamp(evt.newValue, 100, 300000));
            }
            if (modelRemeshFacesField != null)
            {
                modelRemeshFacesField.value = modelRemeshFaces;
                modelRemeshFacesField.RegisterValueChangedCallback(evt => modelRemeshFaces = Mathf.Clamp(evt.newValue, 100, 300000));
            }

            BindSegmented("ModelModeSegments", new[] { "标准", "智能拓扑" }, value => modelMode = value == "标准" ? "standard" : "lowpoly");
            BindSegmented("ModelTopologySegments", new[] { "四边面", "三角面" }, value => modelTopology = value == "四边面" ? "quad" : "triangle");
            BindSegmented("ModelPoseSegments", new[] { "无", "A 姿势", "T 姿势" }, value => modelPose = MapPose(value));
            BindSegmented("ModelRemeshTopologySegments", new[] { "四边面", "三角面" }, value => modelRemeshTopology = value == "四边面" ? "quad" : "triangle");

            root.Q<Button>("ModelLocalImageButton").clicked += () =>
            {
                var text = PickReferenceImage();
                if (string.IsNullOrEmpty(text))
                {
                    text = MeshyRuntimeDownloads.TryReadClipboardPathOrUrl();
                }
                if (modelImageTaskField != null && !string.IsNullOrEmpty(text))
                {
                    modelImageTaskField.value = text;
                }
            };
            modelPreviewButton.clicked += () => _ = GenerateModelAsync(false);
            modelRefineButton.clicked += () => _ = GenerateModelAsync(true);
            modelImportButton.clicked += () => _ = LoadSelectedModelPreviewAsync();
            modelRetextureButton.clicked += () => _ = RunRetextureAsync();
            modelRemeshButton.clicked += () => _ = RunRemeshAsync();
            root.Q<Button>("ModelRetextureImageButton").clicked += () =>
            {
                var text = PickReferenceImage();
                if (string.IsNullOrEmpty(text))
                {
                    text = MeshyRuntimeDownloads.TryReadClipboardPathOrUrl();
                }
                if (!string.IsNullOrEmpty(text) && File.Exists(text))
                {
                    text = MeshyRuntimeDownloads.FileToDataUri(text);
                }
                if (modelRetextureImageUrlField != null)
                {
                    modelRetextureImageUrlField.value = text;
                }
            };

            WirePreviewPointer(modelPreviewImage, () => modelPreviewHost);
            SetupModelPreview();
            UpdateModelCost();
            SetModelStatus("就绪", false);
            RefreshModelHistory();
        }

        private void BindAnimatePage()
        {
            animateModelButton = root.Q<Button>("AnimateModelButton");
            animateModelLabel = root.Q<Label>("AnimateModelLabel");
            animateRigButton = root.Q<Button>("AnimateRigButton");
            animateRigLabel = root.Q<Label>("AnimateRigLabel");
            animateSearchField = root.Q<TextField>("AnimateSearchField");
            animateCategoryDropdown = root.Q<DropdownField>("AnimateCategoryDropdown");
            animateActionList = root.Q<ScrollView>("AnimateActionList");
            animateCostLabel = root.Q<Label>("AnimateCostLabel");
            animateStatusLabel = root.Q<Label>("AnimateStatusLabel");
            animateProgressBar = root.Q<ProgressBar>("AnimateProgressBar");
            animateGenerateButton = root.Q<Button>("AnimateGenerateButton");
            animatePlayButton = root.Q<Button>("AnimatePlayButton");
            animatePauseButton = root.Q<Button>("AnimatePauseButton");
            animateResetButton = root.Q<Button>("AnimateResetButton");
            animatePreviewImage = root.Q<Image>("AnimatePreviewImage");
            animateHistoryList = root.Q<ScrollView>("AnimateHistoryList");
            animateFpsToggle = root.Q<Toggle>("AnimateFpsToggle");
            animateFbxToggle = root.Q<Toggle>("AnimateFbxToggle");
            animateArmatureToggle = root.Q<Toggle>("AnimateArmatureToggle");

            LoadAnimationLibrary();
            var categories = animationActions.Select(a => a.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList();
            categories.Insert(0, "全部");
            animateCategoryDropdown.choices = categories;
            animateCategoryDropdown.index = 0;
            animateCategoryDropdown.RegisterValueChangedCallback(_ => RefreshAnimateActions());
            animateSearchField.RegisterValueChangedCallback(_ => RefreshAnimateActions());
            animateModelButton.clicked += SelectLatestModelForAnimation;
            animateRigButton.clicked += () => _ = RunRiggingAsync();
            animateGenerateButton.clicked += () => _ = GenerateAnimationAsync();
            animatePlayButton.clicked += () =>
            {
                animatePreviewHost?.Play(animatePreviewHost.CurrentClipIndex);
                SetAnimateStatus("播放中", false);
            };
            animatePauseButton.clicked += () =>
            {
                animatePreviewHost?.Pause();
                SetAnimateStatus("已暂停", false);
            };
            animateResetButton.clicked += () =>
            {
                animatePreviewHost?.ResetPlayback();
                SetAnimateStatus("已重置到起始帧", false);
            };

            WirePreviewPointer(animatePreviewImage, () => animatePreviewHost);
            SetupAnimatePreview();
            RefreshAnimateActions();
            RefreshAnimateHistory();
            UpdateAnimateLabels();
            animateCostLabel.text = "绑定 5 积分 + 动画 3 积分";
            SetAnimateStatus("就绪", false);
        }

        private void BindTasksPage()
        {
            tasksList = root.Q<ScrollView>("TasksList");
            RefreshTasksView();
        }

        private void RefreshTasksView()
        {
            PostMain(RefreshTasksViewCore);
        }

        private void RefreshTasksViewCore()
        {
            if (tasksList == null)
            {
                return;
            }
            tasksList.Clear();
            var header = new VisualElement();
            header.AddToClassList("task-row");
            header.AddToClassList("task-header");
            header.Add(MakeTaskCell("任务 ID", "task-id"));
            header.Add(MakeTaskCell("创建时间", "task-time"));
            header.Add(MakeTaskCell("类型", "task-type"));
            header.Add(MakeTaskCell("状态", "task-state"));
            header.Add(MakeTaskCell("进度", "task-progress"));
            header.Add(MakeTaskCell("失败原因", "task-reason"));
            header.Add(MakeTaskCell("扣积分", "task-credits"));
            header.Add(MakeTaskCell("可找回", "task-recover"));
            tasksList.Add(header);
            var entries = cache.Entries
                .Where(e => string.IsNullOrEmpty(e.TaskId) || !e.TaskId.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.CreatedAt)
                .Take(200)
                .ToList();
            foreach (var entry in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("task-row");
                row.Add(MakeTaskCell(MiddleEllipsis(entry.TaskId), "task-id"));
                row.Add(MakeTaskCell(FormatTaskTime(entry.CreatedAt), "task-time"));
                row.Add(MakeTaskCell(entry.TaskType ?? "--", "task-type"));
                var state = ResolveTaskState(entry);
                row.Add(MakeTaskCell(TaskStateText(state), "task-cell state-" + state));
                row.Add(MakeTaskCell(entry.Progress + "%", "task-progress"));
                row.Add(MakeTaskCell(string.IsNullOrEmpty(entry.ErrorReason) ? "--" : entry.ErrorReason, "task-reason"));
                row.Add(MakeTaskCell(entry.CreditsDeducted ? "已扣" : "未扣", "task-credits"));
                var canRetry = entry.Recoverable ||
                    entry.DownloadState == "failed" ||
                    !string.IsNullOrEmpty(entry.ErrorReason);
                row.Add(MakeTaskCell(canRetry ? "可以" : "不可", "task-recover"));
                if (canRetry)
                {
                    var retry = new Button(() => _ = RestoreHistoryEntryAsync(entry)) { text = "重试" };
                    retry.AddToClassList("secondary-button");
                    retry.AddToClassList("task-retry");
                    row.Add(retry);
                }
                tasksList.Add(row);
            }
        }

        private static string ResolveTaskState(MeshyCachedTask entry)
        {
            if (!string.IsNullOrEmpty(entry.DownloadState))
            {
                return entry.DownloadState;
            }
            if (entry.Status == "SUCCEEDED")
            {
                return "succeeded";
            }
            if (entry.Status == "FAILED" || !string.IsNullOrEmpty(entry.ErrorMessage))
            {
                return "failed";
            }
            return "unknown";
        }

        private static string TaskStateText(string state)
        {
            switch (state)
            {
                case "downloading":
                    return "下载中";
                case "succeeded":
                    return "下载成功";
                case "failed":
                    return "下载失败";
                default:
                    return "未知";
            }
        }

        private static Label MakeTaskCell(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("task-cell");
            foreach (var name in className.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                label.AddToClassList(name);
            }
            return label;
        }

        private void RecordTaskStart(string taskId, string taskType, string prompt, bool creditsDeducted)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                return;
            }
            var entry = cache.Entries.FirstOrDefault(e => e.TaskId == taskId);
            if (entry == null)
            {
                entry = new MeshyCachedTask();
                entry.TaskId = taskId;
                entry.CreatedAt = DateTime.UtcNow.ToString("o");
            }
            entry.TaskType = taskType;
            entry.Prompt = prompt;
            entry.Status = "PENDING";
            entry.Progress = 0;
            entry.DownloadState = "downloading";
            entry.ErrorReason = string.Empty;
            entry.CreditsDeducted = creditsDeducted;
            entry.Recoverable = true;
            cache.AddOrUpdate(entry);
            RefreshTasksView();
        }

        private void RecordTaskProgress(string taskId, int progress)
        {
            var entry = cache.Entries.FirstOrDefault(e => e.TaskId == taskId);
            if (entry == null)
            {
                return;
            }
            entry.Progress = Mathf.Clamp(progress, 0, 100);
            entry.DownloadState = "downloading";
            cache.AddOrUpdate(entry);
            RefreshTasksView();
        }

        private void RecordTaskSuccess(string taskId, double credits)
        {
            var entry = cache.Entries.FirstOrDefault(e => e.TaskId == taskId);
            if (entry == null)
            {
                return;
            }
            entry.Status = "SUCCEEDED";
            entry.Progress = 100;
            entry.DownloadState = "succeeded";
            entry.ErrorReason = string.Empty;
            entry.ConsumedCredits = credits;
            entry.CreditsDeducted = credits > 0 || entry.CreditsDeducted;
            entry.Recoverable = false;
            cache.AddOrUpdate(entry);
            RefreshTasksView();
        }

        private void RecordTaskFailure(string taskId, string reason, bool recoverable)
        {
            var entry = cache.Entries.FirstOrDefault(e => e.TaskId == taskId);
            if (entry == null)
            {
                return;
            }
            entry.Status = "FAILED";
            entry.DownloadState = "failed";
            entry.ErrorReason = reason;
            entry.Recoverable = recoverable;
            cache.AddOrUpdate(entry);
            RefreshTasksView();
        }

        private async Task GenerateImageAsync()
        {
            if (imageGenerating)
            {
                return;
            }

            var prompt = imagePromptField == null ? string.Empty : imagePromptField.value.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                SetImageStatus("请输入提示词", true);
                return;
            }

            imageGenerating = true;
            imageGenerateButton.SetEnabled(false);
            SetImageStatus("正在创建图像任务...", false);
            imageProgressBar.value = 10;
            string taskId = null;

            try
            {
                using (var api = CreateApi())
                {
                    var request = new TextToImageRequest
                    {
                        AiModel = imageModelDropdown.value,
                        Prompt = prompt,
                        AspectRatio = imageAspect,
                        GenerateMultiView = imageMultiViewToggle != null && imageMultiViewToggle.value ? true : (bool?)null,
                        PoseMode = imagePose
                    };
                    CreateTaskResponse response;
                    try
                    {
                        response = await api.CreateTextToImageAsync(request);
                    }
                    catch (MeshyApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        request.AspectRatio = null;
                        response = await api.CreateTextToImageAsync(request);
                    }
                    taskId = response.Result;
                    RecordTaskStart(taskId, "text-to-image", prompt, true);
                    var task = await new MeshyTaskPoller(api).WaitForTaskAsync<TextToImageTask>(
                        response.Result,
                        "text-to-image",
                        progress =>
                        {
                            imageProgressBar.value = Mathf.Clamp(progress.Progress, 10, 95);
                            SetImageStatus("图像生成中 " + progress.Progress + "%", false);
                            RecordTaskProgress(response.Result, progress.Progress);
                        });
                    await SaveImageTaskAsync(task, prompt);
                    RecordTaskSuccess(response.Result, task.ConsumedCredits);
                }
            }
            catch (Exception e)
            {
                SetImageStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
            finally
            {
                imageGenerating = false;
                imageGenerateButton.SetEnabled(true);
                RefreshImageHistory();
            }
        }

        private async Task GenerateModelAsync(bool refine)
        {
            if (modelGenerating)
            {
                return;
            }

            var prompt = modelPromptField == null ? string.Empty : modelPromptField.value.Trim();
            var imageOrTask = modelImageTaskField == null ? string.Empty : modelImageTaskField.value.Trim();
            if (string.IsNullOrEmpty(prompt) && string.IsNullOrEmpty(imageOrTask))
            {
                SetModelStatus("请输入提示词或图片任务 ID/URL", true);
                return;
            }
            if (!string.IsNullOrEmpty(imageOrTask))
            {
                var isUrl = imageOrTask.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                var isFile = File.Exists(imageOrTask);
                if (!isUrl && !isFile && !IsUuid(imageOrTask))
                {
                    SetModelStatus("图片任务 ID 必须是 UUID，或选择本地图片/URL", true);
                    return;
                }
            }

            modelGenerating = true;
            modelPreviewButton.SetEnabled(false);
            modelRefineButton.SetEnabled(false);
            modelProgressBar.value = 10;
            string taskId = null;

            try
            {
                using (var api = CreateApi())
                {
                    CreateTaskResponse response;
                    string taskType;
                    if (!string.IsNullOrEmpty(imageOrTask))
                    {
                        taskType = "image-to-3d";
                        var isUrl = imageOrTask.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                        var isFile = File.Exists(imageOrTask);
                        response = await api.CreateImageTo3DAsync(new ImageTo3DRequest
                        {
                            InputTaskId = !isUrl && !isFile ? imageOrTask : null,
                            ImageUrl = isUrl ? imageOrTask : (isFile ? MeshyRuntimeDownloads.FileToDataUri(imageOrTask) : null),
                            ShouldTexture = true,
                            EnablePbr = true,
                            AiModel = modelAiDropdown.value,
                            ModelType = modelMode,
                            ShouldRemesh = modelMode == "lowpoly",
                            Topology = modelTopology,
                            TargetPolycount = modelTopologyFaces,
                            PoseMode = modelPose
                        });
                        taskId = response.Result;
                        RecordTaskStart(taskId, taskType, prompt, true);
                        var task = await new MeshyTaskPoller(api).WaitForTaskAsync<ImageTo3DTask>(
                            response.Result,
                            taskType,
                            progress => { UpdateModelProgress(progress); RecordTaskProgress(response.Result, progress.Progress); });
                        await SaveModelTaskAsync(task, prompt, taskType);
                        RecordTaskSuccess(response.Result, task.ConsumedCredits);
                    }
                    else
                    {
                        taskType = "text-to-3d";
                        response = await api.CreateTextTo3DAsync(new TextTo3DRequest
                        {
                            Mode = refine ? "refine" : "preview",
                            Prompt = prompt,
                            AiModel = modelAiDropdown.value,
                            ModelType = modelMode,
                            ShouldRemesh = modelMode == "lowpoly",
                            Topology = modelTopology,
                            TargetPolycount = modelTopologyFaces,
                            PreviewTaskId = refine ? modelPreviewTaskId : null,
                            EnablePbr = true,
                            TextureResolution = "1024",
                            PoseMode = modelPose,
                            TargetFormats = new List<string> { "glb" }
                        });
                        taskId = response.Result;
                        RecordTaskStart(taskId, taskType, prompt, true);
                        var task = await new MeshyTaskPoller(api).WaitForTaskAsync<TextTo3DTask>(
                            response.Result,
                            taskType,
                            progress => { UpdateModelProgress(progress); RecordTaskProgress(response.Result, progress.Progress); });
                        modelPreviewTaskId = task.Id;
                        await SaveModelTaskAsync(task, prompt, taskType);
                        RecordTaskSuccess(response.Result, task.ConsumedCredits);
                    }
                }
            }
            catch (Exception e)
            {
                SetModelStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
            finally
            {
                modelGenerating = false;
                modelPreviewButton.SetEnabled(true);
                modelRefineButton.SetEnabled(!string.IsNullOrEmpty(modelPreviewTaskId));
                RefreshModelHistory();
            }
        }

        private async Task RunRetextureAsync()
        {
            if (modelSelectedEntry == null)
            {
                SetModelStatus("请先在模型历史中选择模型", true);
                return;
            }

            string taskId = null;
            try
            {
                using (var api = CreateApi())
                {
                    var response = await api.CreateRetextureAsync(new RetextureRequest
                    {
                        InputTaskId = modelSelectedEntry.TaskId,
                        TextStylePrompt = modelRetexturePromptField.value,
                        ImageStyleUrl = modelRetextureImageUrlField.value
                    });
                    taskId = response.Result;
                    RecordTaskStart(taskId, "retexture", modelRetexturePromptField.value, true);
                    var task = await new MeshyTaskPoller(api).WaitForTaskAsync<RetextureTask>(
                        response.Result,
                        "retexture",
                        progress => { UpdateModelProgress(progress); RecordTaskProgress(response.Result, progress.Progress); });
                    await SaveModelTaskAsync(task, modelRetexturePromptField.value, "retexture");
                    RecordTaskSuccess(response.Result, task.ConsumedCredits);
                }
            }
            catch (Exception e)
            {
                SetModelStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
            RefreshModelHistory();
        }

        private async Task RunRemeshAsync()
        {
            if (modelSelectedEntry == null)
            {
                SetModelStatus("请先在模型历史中选择模型", true);
                return;
            }

            string taskId = null;
            try
            {
                using (var api = CreateApi())
                {
                    var response = await api.CreateRemeshAsync(new RemeshRequest
                    {
                        InputTaskId = modelSelectedEntry.TaskId,
                        TargetPolycount = modelRemeshFaces,
                        Topology = modelRemeshTopology
                    });
                    taskId = response.Result;
                    RecordTaskStart(taskId, "remesh", "remesh " + modelSelectedEntry.TaskId, true);
                    var task = await new MeshyTaskPoller(api).WaitForTaskAsync<RemeshTask>(
                        response.Result,
                        "remesh",
                        progress => { UpdateModelProgress(progress); RecordTaskProgress(response.Result, progress.Progress); });
                    await SaveModelTaskAsync(task, "remesh " + modelSelectedEntry.TaskId, "remesh");
                    RecordTaskSuccess(response.Result, task.ConsumedCredits);
                }
            }
            catch (Exception e)
            {
                SetModelStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
            RefreshModelHistory();
        }

        private async Task RunRiggingAsync()
        {
            SelectLatestModelForAnimation();
            if (animateModelTask == null)
            {
                SetAnimateStatus("请先生成或选择模型", true);
                return;
            }

            string taskId = null;
            try
            {
                using (var api = CreateApi())
                {
                    var response = await api.CreateRiggingAsync(new RiggingRequest { InputTaskId = animateModelTask.TaskId });
                    taskId = response.Result;
                    RecordTaskStart(taskId, "rigging", animateModelTask.TaskId, true);
                    var task = await new MeshyTaskPoller(api).WaitForTaskAsync<RigTask>(response.Result, "rigging", progress =>
                    {
                        animateProgressBar.value = progress.Progress;
                        SetAnimateStatus("绑定骨骼中 " + progress.Progress + "%", false);
                        RecordTaskProgress(response.Result, progress.Progress);
                    });
                    animateRigTaskId = task.Id;
                    animateRigLabel.text = "已绑定：" + task.Id;
                    await SaveRiggingTaskAsync(task, animateModelTask.TaskId);
                    RecordTaskSuccess(response.Result, task.ConsumedCredits);
                    RefreshModelHistory();
                    RefreshTasksView();
                }
            }
            catch (Exception e)
            {
                SetAnimateStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
        }

        private async Task GenerateAnimationAsync()
        {
            if (selectedAction == null)
            {
                SetAnimateStatus("请先选择动作", true);
                return;
            }
            if (string.IsNullOrEmpty(animateRigTaskId))
            {
                await RunRiggingAsync();
            }
            if (string.IsNullOrEmpty(animateRigTaskId))
            {
                return;
            }

            string taskId = null;
            try
            {
                using (var api = CreateApi())
                {
                    var post = new List<string>();
                    if (animateFpsToggle.value) post.Add("change_fps");
                    if (animateFbxToggle.value) post.Add("fbx2usdz");
                    if (animateArmatureToggle.value) post.Add("extract_armature");
                    var response = await api.CreateAnimationAsync(new AnimationRequest
                    {
                        RigTaskId = animateRigTaskId,
                        ActionId = selectedAction.Id,
                        PostProcess = post.Count == 0 ? null : post
                    });
                    taskId = response.Result;
                    RecordTaskStart(taskId, "animation", selectedAction.Name, true);
                    var task = await new MeshyTaskPoller(api).WaitForTaskAsync<AnimationTask>(response.Result, "animations", progress =>
                    {
                        animateProgressBar.value = progress.Progress;
                        SetAnimateStatus("动画生成中 " + progress.Progress + "%", false);
                        RecordTaskProgress(response.Result, progress.Progress);
                    });
                    await SaveAnimationTaskAsync(task, selectedAction.Name);
                    RecordTaskSuccess(response.Result, task.ConsumedCredits);
                }
            }
            catch (Exception e)
            {
                SetAnimateStatus(e.Message, true);
                if (!string.IsNullOrEmpty(taskId))
                {
                    RecordTaskFailure(taskId, e.Message, true);
                }
            }
            RefreshAnimateHistory();
        }

        private async Task SaveImageTaskAsync(TextToImageTask task, string prompt)
        {
            var folder = MeshyPaths.TaskFolder("text-to-image", task.Id);
            Directory.CreateDirectory(folder);
            var urls = task.ImageUrls ?? new List<string>();
            var downloaded = new List<Texture2D>();
            for (var i = 0; i < Math.Max(imageCount, urls.Count); i++)
            {
                var url = i < urls.Count ? urls[i] : "https://mock.invalid/image.png";
                downloaded.Add(await MeshyRuntimeDownloads.DownloadTextureAsync(url, textures));
                await MeshyRuntimeDownloads.DownloadFileAsync(url, Path.Combine(folder, "image_" + i + ".png"));
            }
            PostMain(() =>
            {
                imageResultGrid.Clear();
                foreach (var texture in downloaded)
                {
                    var image = new Image { image = texture };
                    image.AddToClassList("result-image");
                    imageResultGrid.Add(image);
                }
                imageEmptyLabel.style.display = DisplayStyle.None;
                cache.AddOrUpdate(new MeshyCachedTask
                {
                    TaskId = task.Id,
                    TaskType = "text-to-image",
                    Status = task.StatusRaw,
                    Prompt = prompt,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    FinishedAt = task.FinishedAt,
                    ConsumedCredits = task.ConsumedCredits,
                    ImageUrls = urls
                });
                SetImageStatus("图像生成完成：" + task.Id, false);
                imageProgressBar.value = 100;
            });
        }

        private async Task SaveModelTaskAsync(TextTo3DTask task, string prompt, string taskType)
        {
            await SaveModelFilesAsync(task.Id, taskType, prompt, task.StatusRaw, task.ConsumedCredits, task.ModelUrls, task.TextureUrls);
        }

        private async Task SaveModelTaskAsync(ImageTo3DTask task, string prompt, string taskType)
        {
            await SaveModelFilesAsync(task.Id, taskType, prompt, task.StatusRaw, task.ConsumedCredits, task.ModelUrls, task.TextureUrls);
        }

        private async Task SaveModelTaskAsync(RetextureTask task, string prompt, string taskType)
        {
            await SaveModelFilesAsync(task.Id, taskType, prompt, task.StatusRaw, task.ConsumedCredits, task.ModelUrls, task.TextureUrls);
        }

        private async Task SaveModelTaskAsync(RemeshTask task, string prompt, string taskType)
        {
            await SaveModelFilesAsync(task.Id, taskType, prompt, task.StatusRaw, task.ConsumedCredits, task.ModelUrls, task.TextureUrls);
        }

        private async Task SaveModelFilesAsync(string taskId, string taskType, string prompt, string status, double credits, Dictionary<string, string> modelUrls, List<string> textureUrls)
        {
            var folder = MeshyPaths.TaskFolder(taskType, taskId);
            Directory.CreateDirectory(folder);
            var glbUrl = PickGlb(modelUrls);
            var glbPath = Path.Combine(folder, "model.glb");
            var downloaded = await MeshyRuntimeDownloads.DownloadFileAsync(glbUrl, glbPath);
            if (downloaded)
            {
                await modelPreviewHost.LoadAsync(glbPath);
                modelLastGlbPath = glbPath;
            }
            PostMain(() =>
            {
                if (!downloaded)
                {
                    modelPreviewHost.LoadPlaceholder();
                }
                modelPreviewImage.image = modelPreviewHost.Texture;
                cache.AddOrUpdate(new MeshyCachedTask
                {
                    TaskId = taskId,
                    TaskType = taskType,
                    Status = status,
                    Prompt = prompt,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    ConsumedCredits = credits,
                    ModelUrls = modelUrls,
                    TextureUrls = textureUrls
                });
                SetModelStatus("模型生成完成：" + taskId, false);
                modelStatsLabel.text = "拓扑 · 面数 -- · 顶点数 -- · 本地 " + (downloaded ? "已保存" : "Mock 预览");
                modelProgressBar.value = 100;
            });
        }

        private async Task SaveAnimationTaskAsync(AnimationTask task, string prompt)
        {
            var folder = MeshyPaths.TaskFolder("animation", task.Id);
            Directory.CreateDirectory(folder);
            var glbPath = Path.Combine(folder, "animated.glb");
            var downloaded = await MeshyRuntimeDownloads.DownloadFileAsync(task.EffectiveGlbUrl, glbPath);
            if (downloaded)
            {
                await animatePreviewHost.LoadAsync(glbPath);
            }
            PostMain(() =>
            {
                if (!downloaded)
                {
                    animatePreviewHost.LoadPlaceholder();
                }
                animatePreviewImage.image = animatePreviewHost.Texture;
                var modelUrls = string.IsNullOrEmpty(task.EffectiveGlbUrl)
                    ? null
                    : new Dictionary<string, string> { ["glb"] = task.EffectiveGlbUrl };
                cache.AddOrUpdate(new MeshyCachedTask
                {
                    TaskId = task.Id,
                    TaskType = "animation",
                    Status = task.StatusRaw,
                    Prompt = prompt,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    ConsumedCredits = task.ConsumedCredits,
                    ModelUrls = modelUrls
                });
                SetAnimateStatus("动画生成完成：" + task.Id, false);
                animateProgressBar.value = 100;
            });
        }

        private async Task SaveRiggingTaskAsync(RigTask task, string prompt)
        {
            if (task == null)
            {
                return;
            }

            var entry = cache.Entries.FirstOrDefault(e => e.TaskId == task.Id);
            if (entry == null)
            {
                entry = new MeshyCachedTask
                {
                    TaskId = task.Id,
                    TaskType = "rigging",
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };
            }

            var url = task.EffectiveRiggedGlbUrl;
            if (!string.IsNullOrEmpty(url))
            {
                entry.ModelUrls = new Dictionary<string, string> { ["glb"] = url };
                var folder = MeshyPaths.TaskFolder("rigging", task.Id);
                var glbPath = Path.Combine(folder, "model.glb");
                Directory.CreateDirectory(folder);
                await MeshyRuntimeDownloads.DownloadFileAsync(url, glbPath);
            }

            entry.Status = task.StatusRaw;
            entry.Prompt = prompt;
            entry.ConsumedCredits = task.ConsumedCredits;
            entry.Progress = 100;
            entry.FinishedAt = task.FinishedAt;
            cache.AddOrUpdate(entry);
        }

        private void RefreshImageHistory()
        {
            PostMain(() =>
            {
                imageHistoryPage = 0;
                RenderImageHistory();
            });
        }

        private void RenderImageHistory()
        {
            RenderHistory(imageHistoryList, imageHistoryMoreButton, "text-to-image", imageSearchField == null ? string.Empty : imageSearchField.value, imageHistoryPage, entry =>
            {
                var file = MeshyPaths.FindImageFile(entry.TaskId, 0);
                if (File.Exists(file))
                {
                    ShowLocalImage(file);
                }
                else
                {
                    _ = RestoreHistoryEntryAsync(entry);
                }
            });
        }

        private void ShowLocalImage(string file)
        {
            PostMain(() =>
            {
                var bytes = File.ReadAllBytes(file);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                textures.Add(texture);
                imageResultGrid.Clear();
                var image = new Image { image = texture };
                image.AddToClassList("result-image");
                imageResultGrid.Add(image);
                imageEmptyLabel.style.display = DisplayStyle.None;
            });
        }

        private void RefreshModelHistory()
        {
            PostMain(() =>
            {
                RenderHistory(
                    modelHistoryList,
                    null,
                    null,
                    string.Empty,
                    0,
                    entry =>
                    {
                        modelSelectedEntry = entry;
                        RefreshModelHistory();
                        _ = LoadSelectedModelPreviewAsync();
                    },
                    include: e => e.TaskType != "animation" && e.TaskType != "text-to-image");
            });
        }

        private static bool CanPreviewModel(MeshyCachedTask entry)
        {
            if (entry == null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(MeshyPaths.FindModelFile(entry.TaskId)))
            {
                return true;
            }
            return entry.ModelUrls != null &&
                entry.ModelUrls.ContainsKey("glb") &&
                !string.IsNullOrEmpty(entry.ModelUrls["glb"]);
        }

        private void RefreshAnimateHistory()
        {
            PostMain(() =>
            {
                RenderHistory(animateHistoryList, null, "animation", string.Empty, 0, entry =>
                {
                    var glb = Path.Combine(MeshyPaths.FindTaskFolder("animation", entry.TaskId), "animated.glb");
                    if (File.Exists(glb))
                    {
                        _ = animatePreviewHost.LoadAsync(glb);
                    }
                    else
                    {
                        _ = RestoreHistoryEntryAsync(entry);
                    }
                    animatePreviewImage.image = animatePreviewHost.Texture;
                });
            });
        }

        private void RenderHistory(ScrollView list, Button more, string taskType, string keyword, int page, Action<MeshyCachedTask> onClick, Func<MeshyCachedTask, bool> include = null)
        {
            if (list == null)
            {
                return;
            }
            list.Clear();
            var filtered = cache.Entries
                .Where(e => taskType == null || e.TaskType == taskType)
                .Where(e => include == null || include(e))
                .Where(e => string.IsNullOrEmpty(e.TaskId) || !e.TaskId.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
                .Where(e => string.IsNullOrEmpty(keyword) || ((e.Prompt ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .Reverse()
                .ToList();
            var takeCount = more == null ? Math.Min(filtered.Count, 200) : (page + 1) * HistoryPageSize;
            var entries = filtered.Take(takeCount).ToList();
            foreach (var entry in entries)
            {
                var captured = entry;
                var card = new VisualElement();
                card.AddToClassList("history-card");
                if (modelSelectedEntry != null && entry.TaskId == modelSelectedEntry.TaskId)
                {
                    card.AddToClassList("active");
                }
                var main = new Button(() => onClick(captured))
                {
                    text = ShortName(entry.TaskId) + "\n" + (entry.Prompt ?? entry.TaskType)
                };
                main.AddToClassList("history-card-main");
                var time = new Label(FormatTaskTime(entry.CreatedAt));
                time.AddToClassList("history-time");
                var row = new VisualElement();
                row.AddToClassList("history-row");
                var open = new Button(() => OpenHistoryFolder(captured)) { text = "打开文件夹" };
                open.AddToClassList("secondary-button");
                var remove = new Button(() => DeleteHistoryEntry(captured)) { text = "删除" };
                remove.AddToClassList("secondary-button");
                row.Add(open);
                row.Add(remove);
                card.Add(main);
                card.Add(time);
                card.Add(row);
                card.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("加载/查看", _ => onClick(captured));
                    evt.menu.AppendAction("重新查询", action => { _ = RequeryTaskAsync(captured); });
                    evt.menu.AppendAction("打开所在文件夹", _ => OpenHistoryFolder(captured));
                    evt.menu.AppendAction("重新下载文件", action => { _ = RestoreHistoryEntryAsync(captured); });
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("删除记录", _ => DeleteHistoryEntry(captured));
                    evt.menu.AppendAction("删除记录和文件", action => DeleteHistoryEntryFiles(captured));
                }));
                list.Add(card);
            }
            if (more != null)
            {
                more.SetEnabled(entries.Count >= (page + 1) * HistoryPageSize);
            }
        }

        private void OpenHistoryFolder(MeshyCachedTask entry)
        {
            if (entry == null)
            {
                return;
            }
            var folder = MeshyPaths.FindTaskFolder(entry.TaskType, entry.TaskId);
            if (!Directory.Exists(folder))
            {
                folder = MeshyPaths.TaskFolder(entry.TaskType, entry.TaskId);
                Directory.CreateDirectory(folder);
            }
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }

        private void DeleteHistoryEntry(MeshyCachedTask entry)
        {
            if (entry == null)
            {
                return;
            }
            cache.Remove(entry.TaskId);
            RefreshImageHistory();
            RefreshModelHistory();
            RefreshAnimateHistory();
            RefreshTasksView();
        }

        private void DeleteHistoryEntryFiles(MeshyCachedTask entry)
        {
            if (entry == null)
            {
                return;
            }
            var folder = MeshyPaths.FindTaskFolder(entry.TaskType, entry.TaskId);
            if (Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Meshy] 删除文件失败: " + e.Message);
                }
            }
            DeleteHistoryEntry(entry);
        }

        private async Task RequeryTaskAsync(MeshyCachedTask entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.TaskId))
            {
                return;
            }
            try
            {
                var task = await FetchTaskDetailsAsync(entry);
                if (task == null)
                {
                    throw new Exception("任务不存在或已过期");
                }
                ApplyTaskDetails(entry, task);
                entry.Recoverable = true;
                cache.AddOrUpdate(entry);
                RefreshTasksView();
                SetHistoryStatus(entry, "已重新查询：" + entry.TaskId, false);
            }
            catch (Exception e)
            {
                SetHistoryStatus(entry, "重新查询失败：" + e.Message, true);
            }
        }

        private async Task RestoreHistoryEntryAsync(MeshyCachedTask entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.TaskId))
            {
                return;
            }
            var folder = MeshyPaths.TaskFolder(entry.TaskType, entry.TaskId);
            Directory.CreateDirectory(folder);
            entry.DownloadState = "downloading";
            entry.Recoverable = true;
            entry.ErrorReason = string.Empty;
            cache.AddOrUpdate(entry);
            RefreshTasksView();
            SetHistoryStatus(entry, "正在恢复下载：" + entry.TaskId, false);
            var restored = false;
            try
            {
                if (entry.TaskType == "text-to-image")
                {
                    restored = await RestoreImageHistoryEntryAsync(entry, folder);
                    if (restored)
                    {
                        var file = MeshyPaths.FindImageFile(entry.TaskId, 0);
                        if (!string.IsNullOrEmpty(file))
                        {
                            ShowLocalImage(file);
                        }
                    }
                }
                else if (entry.TaskType == "animation")
                {
                    restored = await RestoreAnimationHistoryEntryAsync(entry, folder);
                }
                else
                {
                    restored = await RestoreModelHistoryEntryAsync(entry);
                }
            }
            catch (Exception e)
            {
                RecordTaskFailure(entry.TaskId, "恢复失败：" + e.Message, true);
                SetHistoryStatus(entry, "恢复失败：" + e.Message, true);
                return;
            }

            if (restored)
            {
                RefreshImageHistory();
                RefreshModelHistory();
                RefreshAnimateHistory();
                RecordTaskSuccess(entry.TaskId, entry.ConsumedCredits);
                SetHistoryStatus(entry, "文件已恢复：" + entry.TaskId, false);
            }
            else
            {
                const string reason = "恢复失败：未找到已保存的下载地址，且任务无法重新获取";
                RecordTaskFailure(entry.TaskId, reason, true);
                SetHistoryStatus(entry, reason, true);
            }
        }

        private async Task<bool> RestoreImageHistoryEntryAsync(MeshyCachedTask entry, string folder)
        {
            if (!string.IsNullOrEmpty(MeshyPaths.FindImageFile(entry.TaskId, 0)))
            {
                return true;
            }

            if (entry.ImageUrls == null || entry.ImageUrls.Count == 0 || entry.ImageUrls.All(string.IsNullOrEmpty))
            {
                var task = await FetchTaskDetailsAsync(entry) as TextToImageTask;
                ApplyTaskDetails(entry, task);
                if (task == null || task.ImageUrls == null || task.ImageUrls.Count == 0)
                {
                    return false;
                }
            }

            var downloaded = false;
            for (var i = 0; i < entry.ImageUrls.Count; i++)
            {
                var destination = Path.Combine(folder, "image_" + i + ".png");
                if (File.Exists(destination))
                {
                    downloaded = true;
                    continue;
                }
                downloaded |= await MeshyRuntimeDownloads.DownloadFileAsync(entry.ImageUrls[i], destination);
            }
            return downloaded;
        }

        private async Task<bool> RestoreAnimationHistoryEntryAsync(MeshyCachedTask entry, string folder)
        {
            var existing = Path.Combine(MeshyPaths.FindTaskFolder("animation", entry.TaskId), "animated.glb");
            if (File.Exists(existing))
            {
                await animatePreviewHost.LoadAsync(existing);
                PostMain(() => animatePreviewImage.image = animatePreviewHost.Texture);
                return true;
            }

            var url = PickGlb(entry.ModelUrls);
            if (string.IsNullOrEmpty(url))
            {
                var task = await FetchTaskDetailsAsync(entry) as AnimationTask;
                ApplyTaskDetails(entry, task);
                url = PickGlb(entry.ModelUrls);
                if (string.IsNullOrEmpty(url))
                {
                    return false;
                }
            }

            var destination = Path.Combine(folder, "animated.glb");
            var downloaded = await MeshyRuntimeDownloads.DownloadFileAsync(url, destination);
            if (downloaded)
            {
                await animatePreviewHost.LoadAsync(destination);
                PostMain(() => animatePreviewImage.image = animatePreviewHost.Texture);
            }
            return downloaded;
        }

        private async Task<bool> RestoreModelHistoryEntryAsync(MeshyCachedTask entry)
        {
            if (!string.IsNullOrEmpty(MeshyPaths.FindModelFile(entry.TaskId)))
            {
                return true;
            }

            if (entry.ModelUrls == null || string.IsNullOrEmpty(PickGlb(entry.ModelUrls)))
            {
                var task = await FetchTaskDetailsAsync(entry);
                ApplyTaskDetails(entry, task);
            }

            return !string.IsNullOrEmpty(await RestoreModelFileAsync(entry));
        }

        private async Task<MeshyTaskBase> FetchTaskDetailsAsync(MeshyCachedTask entry)
        {
            var apiTaskType = entry.TaskType == "animation" ? "animations" : (entry.TaskType ?? "text-to-3d");
            using (var api = CreateApi())
            {
                switch (apiTaskType)
                {
                    case "text-to-image":
                        return await api.GetTaskAsync<TextToImageTask>(entry.TaskId, apiTaskType);
                    case "animations":
                        return await api.GetTaskAsync<AnimationTask>(entry.TaskId, apiTaskType);
                    case "rigging":
                        return await api.GetTaskAsync<RigTask>(entry.TaskId, apiTaskType);
                    case "text-to-3d":
                        return await api.GetTaskAsync<TextTo3DTask>(entry.TaskId, apiTaskType);
                    case "image-to-3d":
                        return await api.GetTaskAsync<ImageTo3DTask>(entry.TaskId, apiTaskType);
                    case "retexture":
                        return await api.GetTaskAsync<RetextureTask>(entry.TaskId, apiTaskType);
                    case "remesh":
                        return await api.GetTaskAsync<RemeshTask>(entry.TaskId, apiTaskType);
                    default:
                        return await api.GetTaskAsync<MeshyTaskBase>(entry.TaskId, apiTaskType);
                }
            }
        }

        private static void ApplyTaskDetails(MeshyCachedTask entry, MeshyTaskBase task)
        {
            if (entry == null || task == null)
            {
                return;
            }

            entry.Status = task.StatusRaw;
            entry.Progress = task.Progress;
            entry.ConsumedCredits = task.ConsumedCredits;
            entry.FinishedAt = task.FinishedAt;
            entry.ErrorReason = task.TaskError == null ? string.Empty : task.TaskError.Message;

            var imageTask = task as TextToImageTask;
            if (imageTask != null)
            {
                entry.ImageUrls = imageTask.ImageUrls ?? new List<string>();
                return;
            }

            var animationTask = task as AnimationTask;
            if (animationTask != null)
            {
                entry.ModelUrls = string.IsNullOrEmpty(animationTask.EffectiveGlbUrl)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { ["glb"] = animationTask.EffectiveGlbUrl };
                return;
            }

            var rigTask = task as RigTask;
            if (rigTask != null)
            {
                entry.ModelUrls = string.IsNullOrEmpty(rigTask.EffectiveRiggedGlbUrl)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { ["glb"] = rigTask.EffectiveRiggedGlbUrl };
                return;
            }

            var modelTask = task as TextTo3DTask;
            if (modelTask != null)
            {
                entry.ModelUrls = modelTask.ModelUrls;
                entry.TextureUrls = modelTask.TextureUrls;
                return;
            }

            var image3dTask = task as ImageTo3DTask;
            if (image3dTask != null)
            {
                entry.ModelUrls = image3dTask.ModelUrls;
                entry.TextureUrls = image3dTask.TextureUrls;
                return;
            }

            var retextureTask = task as RetextureTask;
            if (retextureTask != null)
            {
                entry.ModelUrls = retextureTask.ModelUrls;
                entry.TextureUrls = retextureTask.TextureUrls;
                return;
            }

            var remeshTask = task as RemeshTask;
            if (remeshTask != null)
            {
                entry.ModelUrls = remeshTask.ModelUrls;
                entry.TextureUrls = remeshTask.TextureUrls;
            }
        }

        private void SetHistoryStatus(MeshyCachedTask entry, string message, bool error)
        {
            if (entry == null)
            {
                return;
            }
            if (entry.TaskType == "text-to-image")
            {
                SetImageStatus(message, error);
            }
            else if (entry.TaskType == "animation")
            {
                SetAnimateStatus(message, error);
            }
            else
            {
                SetModelStatus(message, error);
            }
        }

        private async Task<string> RestoreModelFileAsync(MeshyCachedTask entry)
        {
            if (entry == null || entry.ModelUrls == null || !entry.ModelUrls.TryGetValue("glb", out var url) || string.IsNullOrEmpty(url))
            {
                return null;
            }
            var folder = MeshyPaths.TaskFolder(entry.TaskType, entry.TaskId);
            var glbPath = Path.Combine(folder, "model.glb");
            var ok = await MeshyRuntimeDownloads.DownloadFileAsync(url, glbPath);
            if (ok && entry.TextureUrls != null)
            {
                for (var i = 0; i < entry.TextureUrls.Count; i++)
                {
                    var textureUrl = entry.TextureUrls[i];
                    var fileName = MeshyRuntimeDownloads.UrlFileName(textureUrl);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = "texture_" + i + ".png";
                    }
                    var destination = Path.Combine(folder, fileName);
                    var renamed = Path.Combine(folder, "texture_" + i + ".png");
                    if (File.Exists(renamed) && !File.Exists(destination))
                    {
                        File.Move(renamed, destination);
                    }
                    if (!File.Exists(destination))
                    {
                        await MeshyRuntimeDownloads.DownloadFileAsync(textureUrl, destination);
                    }
                }
            }
            return ok ? glbPath : null;
        }

        private async Task RestoreModelForAnimationAsync(MeshyCachedTask entry)
        {
            var glb = MeshyPaths.FindModelFile(entry.TaskId);
            if (string.IsNullOrEmpty(glb))
            {
                await RestoreHistoryEntryAsync(entry);
                glb = MeshyPaths.FindModelFile(entry.TaskId);
            }
            if (!string.IsNullOrEmpty(glb))
            {
                animateModelGlbPath = glb;
                await animatePreviewHost.LoadAsync(glb);
                PostMain(() =>
                {
                    animatePreviewImage.image = animatePreviewHost.Texture;
                    SetAnimateStatus("模型已恢复：" + entry.TaskId, false);
                });
            }
            else
            {
                PostMain(() => SetAnimateStatus("模型恢复失败：" + entry.TaskId, true));
            }
        }

        private async Task LoadSelectedModelPreviewAsync()
        {
            if (modelSelectedEntry == null)
            {
                return;
            }
            var glb = MeshyPaths.FindModelFile(modelSelectedEntry.TaskId);
            if (string.IsNullOrEmpty(glb))
            {
                await RestoreHistoryEntryAsync(modelSelectedEntry);
                glb = MeshyPaths.FindModelFile(modelSelectedEntry.TaskId);
            }
            if (!string.IsNullOrEmpty(glb) && File.Exists(glb))
            {
                await modelPreviewHost.LoadAsync(glb);
                PostMain(() =>
                {
                    modelPreviewImage.image = modelPreviewHost.Texture;
                    modelLastGlbPath = glb;
                    SetModelStatus("已载入：" + modelSelectedEntry.TaskId, false);
                });
            }
            else
            {
                PostMain(() =>
                {
                    modelPreviewHost.LoadPlaceholder();
                    modelPreviewImage.image = modelPreviewHost.Texture;
                    var hasUrl = modelSelectedEntry.ModelUrls != null &&
                        modelSelectedEntry.ModelUrls.ContainsKey("glb") &&
                        !string.IsNullOrEmpty(modelSelectedEntry.ModelUrls["glb"]);
                    var message = !string.IsNullOrEmpty(modelSelectedEntry.ErrorReason)
                        ? modelSelectedEntry.ErrorReason
                        : hasUrl
                            ? "模型下载失败，地址可能已过期：" + modelSelectedEntry.TaskId
                            : "该模型没有保存文件或下载地址：" + modelSelectedEntry.TaskId;
                    SetModelStatus(
                        message,
                        true);
                });
            }
        }

        private void SelectLatestModelForAnimation()
        {
            var entry = cache.Entries.Reverse().FirstOrDefault(e => e.TaskType != "animation" && e.TaskType != "text-to-image");
            if (entry == null)
            {
                return;
            }
            animateModelTask = entry;
            animateModelGlbPath = MeshyPaths.FindModelFile(entry.TaskId);
            if (!string.IsNullOrEmpty(animateModelGlbPath))
            {
                _ = animatePreviewHost.LoadAsync(animateModelGlbPath);
                animatePreviewImage.image = animatePreviewHost.Texture;
            }
            else
            {
                _ = RestoreModelForAnimationAsync(entry);
            }
            UpdateAnimateLabels();
        }

        private void RefreshAnimateActions()
        {
            animateActionList.Clear();
            var keyword = animateSearchField == null ? string.Empty : animateSearchField.value.Trim();
            var category = animateCategoryDropdown == null || animateCategoryDropdown.value == "全部" ? null : animateCategoryDropdown.value;
            var filtered = animationActions
                .Where(a => category == null || a.Category == category)
                .Where(a => string.IsNullOrEmpty(keyword) || a.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(AnimateActionRenderLimit)
                .ToList();
            foreach (var action in filtered)
            {
                var captured = action;
                var button = new Button(() =>
                {
                    selectedAction = captured;
                    RefreshAnimateActions();
                    SetAnimateStatus("已选择：" + captured.Name, false);
                })
                {
                    text = action.Name
                };
                button.AddToClassList("action-card");
                if (selectedAction != null && selectedAction.Id == action.Id)
                {
                    button.AddToClassList("active");
                }
                animateActionList.Add(button);
            }
        }

        private void LoadAnimationLibrary()
        {
            animationActions.Clear();
            var textAsset = Resources.Load<TextAsset>(AnimationLibraryResourcePath);
            if (textAsset != null)
            {
                animationActions.AddRange(MeshyAnimationLibrary.Parse(textAsset.text));
            }
            if (animationActions.Count == 0)
            {
                animationActions.Add(new AnimationAction { Id = 1, Name = "Preview Spin", Category = "全部", Subcategory = "Mock" });
            }
        }

        private void SetupModelPreview()
        {
            if (previewRoot == null)
            {
                var go = new GameObject("MeshyRuntimePreviewRoot");
                go.transform.SetParent(transform, false);
                previewRoot = go.transform;
            }
            if (previewCamera == null)
            {
                var cam = new GameObject("MeshyRuntimePreviewCamera").AddComponent<Camera>();
                cam.transform.SetParent(transform, false);
                cam.clearFlags = CameraClearFlags.Color;
                cam.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
                cam.enabled = false;
                previewCamera = cam;
            }
            if (previewTexture == null)
            {
                previewTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            }
            modelPreviewHost = new MeshyRuntimeModelPreviewHost(previewRoot, previewCamera, previewTexture);
            modelPreviewHost.LoadPlaceholder();
            if (modelPreviewImage != null)
            {
                modelPreviewImage.image = modelPreviewHost.Texture;
            }
        }

        private void SetupAnimatePreview()
        {
            animatePreviewHost = modelPreviewHost;
            if (animatePreviewImage != null && animatePreviewHost != null)
            {
                animatePreviewImage.image = animatePreviewHost.Texture;
            }
        }

        private void WirePreviewPointer(Image image, Func<MeshyRuntimeModelPreviewHost> host)
        {
            if (image == null)
            {
                return;
            }
            var dragging = false;
            var pointerMode = 0;
            var lastPointer = Vector3.zero;
            image.RegisterCallback<PointerDownEvent>(evt =>
            {
                dragging = true;
                pointerMode = evt.button == 1 ? 1 : 0;
                lastPointer = evt.localPosition;
                image.CapturePointer(evt.pointerId);
            });
            image.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                var delta = evt.localPosition - lastPointer;
                lastPointer = evt.localPosition;
                if (pointerMode == 1)
                {
                    host()?.Pan(delta.x, delta.y);
                }
                else
                {
                    host()?.Drag(delta.x, delta.y);
                }
            });
            image.RegisterCallback<PointerUpEvent>(evt =>
            {
                dragging = false;
                image.ReleasePointer(evt.pointerId);
            });
            image.RegisterCallback<WheelEvent>(evt => host()?.Zoom(evt.delta.y * 0.05f));
        }

        private IMeshyApi CreateApi()
        {
            if (settings.UseMockMode)
            {
                return new DisposableApi(new MeshyMockApi());
            }
            return new MeshyApiClient(settings.ToApiConfig());
        }

        private void RefreshBalance()
        {
            _ = RefreshBalanceAsync();
        }

        private async Task RefreshBalanceAsync()
        {
            var label = root.Q<Label>("BalanceLabel");
            if (label == null)
            {
                return;
            }
            label.text = "余额刷新中...";
            try
            {
                using (var api = CreateApi())
                {
                    var balance = await api.GetBalanceAsync();
                    label.text = "余额 " + balance.Balance;
                }
            }
            catch (Exception e)
            {
                label.text = "余额获取失败";
                Debug.LogWarning("[Meshy] 余额刷新失败: " + e.Message);
            }
        }

        private void ToggleSettingsPanel()
        {
            var existing = root.Q<VisualElement>("RuntimeSettingsPanel");
            if (existing != null)
            {
                existing.RemoveFromHierarchy();
                return;
            }
            var panel = new VisualElement { name = "RuntimeSettingsPanel" };
            panel.AddToClassList("runtime-settings-panel");
            var title = new Label("设置");
            title.AddToClassList("panel-title");
            var apiKey = new TextField("API Key") { value = settings.ApiKey, isPasswordField = true };
            var proxy = new TextField("代理地址") { value = settings.ProxyUrl };
            var timeout = new IntegerField("超时秒数") { value = settings.TimeoutSeconds };
            var mock = new Toggle("Mock 模式") { value = settings.UseMockMode };
            var row = new VisualElement();
            row.AddToClassList("reference-row");
            var save = new Button(() =>
            {
                settings.ApiKey = apiKey.value.Trim();
                settings.ProxyUrl = proxy.value;
                settings.TimeoutSeconds = Mathf.Clamp(timeout.value, 5, 120);
                settings.UseMockMode = mock.value;
                MeshyRuntimeSettingsStore.Save(settings);
                ToggleSettingsPanel();
                RefreshBalance();
            })
            {
                text = "保存"
            };
            save.AddToClassList("primary-button");
            var cancel = new Button(ToggleSettingsPanel) { text = "取消" };
            cancel.AddToClassList("secondary-button");
            var diagnoseResult = new Label("点击测试连接查看 API 状态");
            diagnoseResult.AddToClassList("hint-text");
            var diagnose = new Button(() => _ = DiagnoseApiAsync(apiKey.value.Trim(), proxy.value, timeout.value, diagnoseResult)) { text = "测试连接" };
            diagnose.AddToClassList("secondary-button");
            row.Add(save);
            row.Add(cancel);
            row.Add(diagnose);
            panel.Add(title);
            panel.Add(apiKey);
            panel.Add(proxy);
            panel.Add(timeout);
            panel.Add(mock);
            panel.Add(row);
            panel.Add(diagnoseResult);
            root.Add(panel);
        }

        private async Task DiagnoseApiAsync(string apiKey, string proxy, int timeout, Label result)
        {
            if (result != null)
            {
                result.text = "正在测试连接...";
            }
            try
            {
                using (var api = new MeshyApiClient(new MeshyApiConfig
                {
                    ApiKey = apiKey,
                    ProxyUrl = proxy,
                    TimeoutSeconds = Mathf.Clamp(timeout, 5, 120)
                }))
                {
                    var balance = await api.GetBalanceAsync();
                    if (result != null)
                    {
                        result.text = "连接成功，余额：" + balance.Balance;
                    }
                }
            }
            catch (Exception e)
            {
                if (result != null)
                {
                    result.text = "连接失败：" + e.Message;
                }
                Debug.LogWarning("[Meshy] Diagnose failed: " + e);
            }
        }

        private void BindSegmented(string containerName, string[] options, Action<string> onSelect)
        {
            var container = root.Q<VisualElement>(containerName);
            if (container == null)
            {
                return;
            }
            container.Clear();
            foreach (var option in options)
            {
                var captured = option;
                var button = new Button
                {
                    text = option
                };
                button.clicked += () =>
                {
                    foreach (var child in container.Children())
                    {
                        child.RemoveFromClassList("active");
                    }
                    button.AddToClassList("active");
                    onSelect(captured);
                };
                button.AddToClassList("segment");
                if (container.childCount == 0)
                {
                    button.AddToClassList("active");
                    onSelect(option);
                }
                container.Add(button);
            }
        }

        private void SetImageStatus(string text, bool error)
        {
            SetStatus(imageStatusLabel, text, error);
        }

        private void SetModelStatus(string text, bool error)
        {
            SetStatus(modelStatusLabel, text, error);
        }

        private void SetAnimateStatus(string text, bool error)
        {
            SetStatus(animateStatusLabel, text, error);
        }

        private static void SetStatus(Label label, string text, bool error)
        {
            PostMain(() =>
            {
                if (label == null)
                {
                    return;
                }
                label.text = text;
                if (error)
                {
                    label.AddToClassList("error-text");
                }
                else
                {
                    label.RemoveFromClassList("error-text");
                }
            });
        }

        private void UpdateImageCost()
        {
            if (imageCostLabel != null)
            {
                imageCostLabel.text = "预计消耗 " + (imageCount * 3) + " 积分";
            }
        }

        private void UpdateModelCost()
        {
            if (modelCostLabel != null)
            {
                modelCostLabel.text = "预计消耗 30 积分（预览 5 + 精修 25）";
            }
        }

        private void UpdateModelProgress(MeshyTaskBase progress)
        {
            modelProgressBar.value = Mathf.Clamp(progress.Progress, 10, 95);
            SetModelStatus("模型处理中 " + progress.Progress + "%", false);
        }

        private void UpdateAnimateLabels()
        {
            animateModelLabel.text = animateModelTask == null ? "从生成历史选择最新模型" : "已选择：" + animateModelTask.TaskId;
            animateRigLabel.text = string.IsNullOrEmpty(animateRigTaskId) ? "未绑定" : "已绑定：" + animateRigTaskId;
        }

        private static string MapPose(string value)
        {
            if (value == "A 姿势") return "a-pose";
            if (value == "T 姿势") return "t-pose";
            return null;
        }

        private static string PickGlb(Dictionary<string, string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return string.Empty;
            }
            if (urls.TryGetValue("glb", out var glb))
            {
                return glb;
            }
            return urls.Values.FirstOrDefault(v => v != null && v.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) ?? urls.Values.FirstOrDefault();
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var name = Path.GetFileName(value);
            if (string.IsNullOrEmpty(name))
            {
                name = value;
            }
            return name.Length > 36 ? name.Substring(0, 36) + "..." : name;
        }

        private static string MiddleEllipsis(string value, int maxLength = 24)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }
            var head = Math.Max(1, (int)(maxLength * 0.6));
            var tail = Math.Max(1, maxLength - head - 3);
            return value.Substring(0, head) + "..." + value.Substring(value.Length - tail);
        }

        private static string FormatTaskTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "--";
            }
            if (DateTimeOffset.TryParse(value, out var time))
            {
                return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            return "--";
        }

        private static bool IsUuid(string value)
        {
            return !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);
        }

        private void MigrateLegacyHistory()
        {
            foreach (var path in new[]
            {
                Path.Combine(Application.dataPath, "MeshyWorkspace", "History", "tasks.json"),
                Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "tasks.json"),
                Path.Combine(Application.streamingAssetsPath, "MeshyWorkspace", "History", "tasks.json")
            })
            {
                if (!File.Exists(path))
                {
                    continue;
                }
                var oldCache = new MeshyTaskCache(path);
                foreach (var entry in oldCache.Entries)
                {
                    cache.AddOrUpdate(entry);
                }
            }
        }

        private sealed class DisposableApi : IMeshyApi, IDisposable
        {
            private readonly IMeshyApi inner;

            public DisposableApi(IMeshyApi inner)
            {
                this.inner = inner;
            }

            public Task<BalanceResponse> GetBalanceAsync(System.Threading.CancellationToken ct = default) => inner.GetBalanceAsync(ct);
            public Task<CreateTaskResponse> CreateTextToImageAsync(TextToImageRequest request, System.Threading.CancellationToken ct = default) => inner.CreateTextToImageAsync(request, ct);
            public Task<CreateTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, System.Threading.CancellationToken ct = default) => inner.CreateTextTo3DAsync(request, ct);
            public Task<CreateTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, System.Threading.CancellationToken ct = default) => inner.CreateImageTo3DAsync(request, ct);
            public Task<CreateTaskResponse> CreateRiggingAsync(RiggingRequest request, System.Threading.CancellationToken ct = default) => inner.CreateRiggingAsync(request, ct);
            public Task<CreateTaskResponse> CreateAnimationAsync(AnimationRequest request, System.Threading.CancellationToken ct = default) => inner.CreateAnimationAsync(request, ct);
            public Task<CreateTaskResponse> CreateRetextureAsync(RetextureRequest request, System.Threading.CancellationToken ct = default) => inner.CreateRetextureAsync(request, ct);
            public Task<CreateTaskResponse> CreateRemeshAsync(RemeshRequest request, System.Threading.CancellationToken ct = default) => inner.CreateRemeshAsync(request, ct);
            public Task<T> GetTaskAsync<T>(string taskId, string taskType, System.Threading.CancellationToken ct = default) where T : MeshyTaskBase => inner.GetTaskAsync<T>(taskId, taskType, ct);
            public Task<List<T>> ListTasksAsync<T>(string taskType, int pageNum = 1, int pageSize = 20, System.Threading.CancellationToken ct = default) where T : MeshyTaskBase => inner.ListTasksAsync<T>(taskType, pageNum, pageSize, ct);
            public Task DeleteTaskAsync(string taskType, string taskId, System.Threading.CancellationToken ct = default) => inner.DeleteTaskAsync(taskType, taskId, ct);
            public void Dispose() { }
        }
    }
}
