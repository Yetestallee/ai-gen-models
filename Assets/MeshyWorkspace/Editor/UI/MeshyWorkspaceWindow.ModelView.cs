using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public sealed partial class MeshyWorkspaceWindow
    {
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
        private TextField modelRetexturePromptField;
        private Image modelPreviewImage;
        private ProgressBar modelProgressBar;
        private ScrollView modelHistoryList;

        private string modelMode = "standard";
        private string modelTopology = "triangle";
        private string modelTopologyAiModel = "meshy T1";
        private int modelTopologyFaces = 4000;
        private string modelPose;
        private string modelPreviewTaskId;
        private string modelLastGlbPath;
        private string modelLocalImageDataUri;
        private bool modelGenerating;
        private bool modelRetexturing;
        private bool modelDragging;
        private int modelPointerMode;
        private Vector3 modelLastPointer;
        private MeshyModelPreviewHost modelPreviewHost;

        private void BindModelPage()
        {
            modelAiDropdown = rootVisualElement.Q<DropdownField>("ModelAiDropdown");
            modelLicenseDropdown = rootVisualElement.Q<DropdownField>("ModelLicenseDropdown");
            modelPromptField = rootVisualElement.Q<TextField>("ModelPromptField");
            modelImageTaskField = rootVisualElement.Q<TextField>("ModelImageTaskField");
            modelCharCountLabel = rootVisualElement.Q<Label>("ModelCharCountLabel");
            modelCostLabel = rootVisualElement.Q<Label>("ModelCostLabel");
            modelStatusLabel = rootVisualElement.Q<Label>("ModelStatusLabel");
            modelStatsLabel = rootVisualElement.Q<Label>("ModelStatsLabel");
            modelAutoSplitToggle = rootVisualElement.Q<Toggle>("ModelAutoSplitToggle");
            modelUltraToggle = rootVisualElement.Q<Toggle>("ModelUltraToggle");
            modelEnhanceToggle = rootVisualElement.Q<Toggle>("ModelEnhanceToggle");
            modelPreviewButton = rootVisualElement.Q<Button>("ModelPreviewButton");
            modelRefineButton = rootVisualElement.Q<Button>("ModelRefineButton");
            modelImportButton = rootVisualElement.Q<Button>("ModelImportButton");
            modelRetextureButton = rootVisualElement.Q<Button>("ModelRetextureButton");
            modelRetexturePromptField = rootVisualElement.Q<TextField>("ModelRetexturePromptField");
            modelPreviewImage = rootVisualElement.Q<Image>("ModelPreviewImage");
            modelProgressBar = rootVisualElement.Q<ProgressBar>("ModelProgressBar");
            modelHistoryList = rootVisualElement.Q<ScrollView>("ModelHistoryList");

            if (modelAiDropdown != null)
            {
                modelAiDropdown.choices = new List<string> { "meshy-5", "meshy-6", "latest" };
                modelAiDropdown.index = 1;
                modelAiDropdown.RegisterValueChangedCallback(_ => UpdateModelCost());
            }

            if (modelLicenseDropdown != null)
            {
                modelLicenseDropdown.choices = new List<string> { "CC BY 4.0", "私有" };
                modelLicenseDropdown.index = 0;
            }

            if (modelPromptField != null)
            {
                modelPromptField.maxLength = 600;
                modelPromptField.RegisterValueChangedCallback(evt =>
                {
                    if (modelCharCountLabel != null)
                    {
                        modelCharCountLabel.text = evt.newValue.Length + "/600";
                    }
                });
            }

            BindSegmented("ModelModeSegments", new[] { "标准", "智能拓扑" }, option =>
            {
                modelMode = option == "标准" ? "standard" : "lowpoly";
                UpdateModelModeUi();
            });
            BindSegmented(
                "ModelTopologySegments",
                new[] { "四边面", "三角面" },
                option => modelTopology = option == "四边面" ? "quad" : "triangle");
            modelTopologyAiDropdown = rootVisualElement.Q<DropdownField>("ModelTopologyAiDropdown");
            if (modelTopologyAiDropdown != null)
            {
                modelTopologyAiDropdown.choices = new List<string> { "meshy T1", "meshy T2" };
                modelTopologyAiDropdown.index = 0;
                modelTopologyAiDropdown.RegisterValueChangedCallback(evt => modelTopologyAiModel = evt.newValue);
            }
            modelTopologyFacesField = rootVisualElement.Q<IntegerField>("ModelTopologyFacesField");
            if (modelTopologyFacesField != null)
            {
                modelTopologyFacesField.value = 4000;
                modelTopologyFacesField.RegisterValueChangedCallback(evt =>
                {
                    modelTopologyFaces = Mathf.Clamp(evt.newValue, 100, 15000);
                    modelTopologyFacesField.SetValueWithoutNotify(modelTopologyFaces);
                });
            }
            BindSegmented("ModelPoseSegments", new[] { "无", "A 姿势", "T 姿势" }, option => modelPose = MapImagePose(option));

            var localImage = rootVisualElement.Q<Button>("ModelLocalImageButton");
            if (localImage != null)
            {
                localImage.clicked += OnModelLocalImageClicked;
                RegisterModelDropZone(localImage);
            }
            RegisterModelCanvasDrop();
            RegisterModelPaste();

            if (modelPreviewButton != null)
            {
                modelPreviewButton.clicked += () => _ = GenerateModelAsync(false);
            }
            if (modelRefineButton != null)
            {
                modelRefineButton.clicked += () => _ = GenerateModelAsync(true);
                modelRefineButton.SetEnabled(false);
            }
            if (modelImportButton != null)
            {
                modelImportButton.clicked += OnModelImportClicked;
                modelImportButton.SetEnabled(false);
            }
            if (modelRetextureButton != null)
            {
                modelRetextureButton.clicked += () => _ = RunRetextureAsync();
            }

            if (modelPreviewImage != null)
            {
                modelPreviewImage.RegisterCallback<PointerDownEvent>(evt =>
                {
                    modelDragging = true;
                    modelPointerMode = evt.button == 1 ? 1 : 0;
                    modelLastPointer = evt.localPosition;
                    modelPreviewImage.CapturePointer(evt.pointerId);
                });
                modelPreviewImage.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!modelDragging)
                    {
                        return;
                    }
                    var delta = evt.localPosition - modelLastPointer;
                    modelLastPointer = evt.localPosition;
                    if (delta.sqrMagnitude < 0.25f)
                    {
                        return;
                    }
                    if (modelPointerMode == 1)
                    {
                        modelPreviewHost?.Drag(delta.x, delta.y);
                    }
                    else
                    {
                        modelPreviewHost?.Pan(delta.x, delta.y);
                    }
                });
                modelPreviewImage.RegisterCallback<PointerUpEvent>(evt =>
                {
                    modelDragging = false;
                    modelPointerMode = 0;
                    modelPreviewImage.ReleasePointer(evt.pointerId);
                });
                modelPreviewImage.RegisterCallback<WheelEvent>(evt =>
                {
                    modelPreviewHost?.Zoom(evt.delta.y * 0.05f);
                });
            }

            RefreshModelHistory();
            UpdateModelCost();
            UpdateModelModeUi();
            SetModelStatus("就绪", false);
        }

        private void OnDisable()
        {
            if (modelPreviewHost != null)
            {
                modelPreviewHost.Clear();
                modelPreviewHost = null;
            }
            if (animatePreviewHost != null)
            {
                animatePreviewHost.Clear();
                animatePreviewHost = null;
                animatePreviewWired = false;
            }
        }

        private void UpdateModelCost()
        {
            if (modelCostLabel == null)
            {
                return;
            }
            if (modelMode == "lowpoly")
            {
                modelCostLabel.text =
                    "智能拓扑 · " + modelTopologyAiModel + " · " + modelTopologyFaces + " 面 · 预计 20 积分";
                return;
            }
            var aiModel = modelAiDropdown == null ? "meshy-6" : modelAiDropdown.value;
            modelCostLabel.text = "预览 " + MeshyPricing.ModelPreviewCost(aiModel) + " + 精修 " + MeshyPricing.ModelRefineCost(aiModel) + " 积分";
        }

        private void UpdateModelModeUi()
        {
            var standard = modelMode != "lowpoly";
            SetModelElementDisplay("ModelStandardSection", standard);
            SetModelElementDisplay("ModelTopologySection", !standard);
            SetModelElementDisplay("ModelPromptSection", standard);
            UpdateModelCost();
        }

        private void SetModelElementDisplay(string name, bool visible)
        {
            var element = rootVisualElement.Q<VisualElement>(name);
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnModelLocalImageClicked()
        {
            var path = EditorUtility.OpenFilePanel("选择参考图", "", "png,jpg,jpeg,webp");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            TryAddModelLocalImage(path);
        }

        private void TryAddModelLocalImage(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > 20L * 1024 * 1024)
            {
                SetModelStatus("图片超过 20MB 限制", true);
                return;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var mime = extension == ".png" ? "image/png" : extension == ".webp" ? "image/webp" : "image/jpeg";
            var bytes = File.ReadAllBytes(path);
            modelLocalImageDataUri = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            SetModelStatus("已选择本地参考图（可用作 Image-to-3D）", false);
        }

        private void RegisterModelDropZone(VisualElement zone)
        {
            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var supported = HasSupportedImagePaths(DragAndDrop.paths);
                DragAndDrop.visualMode = supported ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.None;
                SetModelDropHighlight(supported);
                evt.StopPropagation();
            });
            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!HasSupportedImagePaths(DragAndDrop.paths))
                {
                    return;
                }
                DragAndDrop.AcceptDrag();
                AddModelDropPaths(DragAndDrop.paths);
                SetModelDropHighlight(false);
                evt.StopPropagation();
            });
            zone.RegisterCallback<DragExitedEvent>(_ => SetModelDropHighlight(false));
        }

        private void RegisterModelCanvasDrop()
        {
            foreach (var name in new[] { "ModelParams", "ModelCanvas" })
            {
                var element = rootVisualElement.Q<VisualElement>(name);
                if (element == null)
                {
                    continue;
                }
                element.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    var supported = HasSupportedImagePaths(DragAndDrop.paths);
                    DragAndDrop.visualMode = supported ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.None;
                    SetModelDropHighlight(supported);
                    evt.StopPropagation();
                });
                element.RegisterCallback<DragPerformEvent>(evt =>
                {
                    if (!HasSupportedImagePaths(DragAndDrop.paths))
                    {
                        return;
                    }
                    DragAndDrop.AcceptDrag();
                    AddModelDropPaths(DragAndDrop.paths);
                    SetModelDropHighlight(false);
                    evt.StopPropagation();
                });
                element.RegisterCallback<DragExitedEvent>(_ => SetModelDropHighlight(false));
            }
        }

        private void SetModelDropHighlight(bool active)
        {
            var zone = rootVisualElement.Q<Button>("ModelLocalImageButton");
            if (zone != null)
            {
                if (active)
                {
                    zone.AddToClassList("drag-over");
                }
                else
                {
                    zone.RemoveFromClassList("drag-over");
                }
            }
        }

        private void AddModelDropPaths(string[] paths)
        {
            var added = 0;
            var names = new List<string>();
            foreach (var path in paths)
            {
                if (!IsSupportedImagePath(path))
                {
                    continue;
                }
                TryAddModelLocalImage(path);
                added++;
                names.Add(Path.GetFileName(path));
            }
            if (added > 0)
            {
                SetModelStatus("已添加参考图：" + string.Join("、", names), false);
            }
        }

        private void RegisterModelPaste()
        {
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!evt.ctrlKey || evt.keyCode != KeyCode.V)
                {
                    return;
                }
                var buffer = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrWhiteSpace(buffer))
                {
                    return;
                }
                var path = buffer.Trim().Trim('"');
                if (File.Exists(path) && IsSupportedImagePath(path))
                {
                    TryAddModelLocalImage(path);
                    evt.StopPropagation();
                }
            });
        }

        private async Task GenerateModelAsync(bool refine)
        {
            if (modelGenerating)
            {
                return;
            }

            var prompt = modelPromptField == null ? string.Empty : modelPromptField.value.Trim();
            var imageTaskId = modelImageTaskField == null ? string.Empty : modelImageTaskField.value.Trim();
            if (!refine && string.IsNullOrEmpty(prompt) && string.IsNullOrEmpty(imageTaskId) && string.IsNullOrEmpty(modelLocalImageDataUri))
            {
                SetModelStatus(
                    modelMode == "lowpoly"
                        ? "智能拓扑请选择参考图或图片任务 ID"
                        : "请输入提示词，或选择参考图 / 图片任务 ID",
                    true);
                return;
            }
            if (prompt.Length > 600)
            {
                SetModelStatus("提示词超过 600 字符", true);
                return;
            }
            if (refine && string.IsNullOrEmpty(modelPreviewTaskId))
            {
                SetModelStatus("请先生成预览再精修", true);
                return;
            }

            var useMock = MeshySettings.UseMockMode;
            modelGenerating = true;
            modelPreviewButton.SetEnabled(false);
            modelRefineButton.SetEnabled(false);
            modelProgressBar.value = 0;
            SetModelStatus(useMock ? "模拟模式：正在处理..." : "正在处理...", false);

            IMeshyApi api = null;
            string lastCreatedTaskId = null;
            string lastCreatedTaskType = "text-to-3d";
            try
            {
                api = useMock
                    ? (IMeshyApi)new MeshyMockApi()
                    : new MeshyApiClient(new MeshyApiConfig
                    {
                        ApiKey = MeshySettings.ApiKey,
                        ProxyUrl = MeshySettings.ProxyUrl,
                        TimeoutSeconds = MeshySettings.TimeoutSeconds
                    });

                if (!string.IsNullOrEmpty(modelLocalImageDataUri) || !string.IsNullOrEmpty(imageTaskId))
                {
                    var imageRequest = new ImageTo3DRequest
                    {
                        ImageUrl = string.IsNullOrEmpty(modelLocalImageDataUri) ? null : modelLocalImageDataUri,
                        InputTaskId = string.IsNullOrEmpty(modelLocalImageDataUri) ? imageTaskId : null,
                        ShouldTexture = true,
                        EnablePbr = true,
                        AiModel = modelAiDropdown == null ? "meshy-6" : modelAiDropdown.value,
                        PoseMode = modelPose,
                        ModelType = modelMode
                    };
                    var created = await api.CreateImageTo3DAsync(imageRequest);
                    lastCreatedTaskId = created.Result;
                    lastCreatedTaskType = "image-to-3d";
                    if (!useMock)
                    {
                        _ = WatchSseProgressAsync(
                            SseConfig(),
                            created.Result,
                            "image-to-3d",
                            p => PostModelSseProgress(p));
                    }
                    var poller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 120);
                    var task = await poller.WaitForTaskAsync<ImageTo3DTask>(
                        created.Result,
                        "image-to-3d",
                        t => PostModelProgress(t),
                        CancellationToken.None);
                    await FinalizeModelAsync(task.Id, task.ModelUrls, task.TextureUrls, task.ConsumedCredits, prompt, useMock);
                }
                else
                {
                    var request = new TextTo3DRequest
                    {
                        Mode = refine ? "refine" : "preview",
                        Prompt = prompt,
                        AiModel = modelAiDropdown == null ? "meshy-6" : modelAiDropdown.value,
                        ModelType = modelMode,
                        PreviewTaskId = refine ? modelPreviewTaskId : null,
                        EnablePbr = refine,
                        TextureResolution = refine ? "2k" : null,
                        PoseMode = modelPose,
                        TargetFormats = new List<string> { "glb" }
                    };
                    var created = await api.CreateTextTo3DAsync(request);
                    lastCreatedTaskId = created.Result;
                    lastCreatedTaskType = "text-to-3d";
                    if (!useMock)
                    {
                        _ = WatchSseProgressAsync(
                            SseConfig(),
                            created.Result,
                            "text-to-3d",
                            p => PostModelSseProgress(p));
                    }
                    var poller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 120);
                    var task = await poller.WaitForTaskAsync<TextTo3DTask>(
                        created.Result,
                        "text-to-3d",
                        t => PostModelProgress(t),
                        CancellationToken.None);

                    if (refine)
                    {
                        modelPreviewTaskId = null;
                        await FinalizeModelAsync(task.Id, task.ModelUrls, task.TextureUrls, task.ConsumedCredits, prompt, useMock);
                    }
                    else
                    {
                        modelPreviewTaskId = task.Id;
                        MeshyUiDispatcher.Post(() =>
                        {
                            modelRefineButton.SetEnabled(true);
                            SetModelStatus("预览完成，点击精修模型生成贴图", false);
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var apiException = e as MeshyApiException;
                var isTimeout = apiException != null &&
                                apiException.StatusCode == HttpStatusCode.RequestTimeout;
                if (isTimeout && !useMock && !string.IsNullOrEmpty(lastCreatedTaskId))
                {
                    var timeoutEntry = new MeshyCachedTask
                    {
                        TaskId = lastCreatedTaskId,
                        TaskType = lastCreatedTaskType,
                        Status = "TIMEOUT",
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ConsumedCredits = 0,
                        ErrorMessage = e.Message,
                        Prompt = prompt
                    };
                    imageCache.AddOrUpdate(timeoutEntry);
                    MeshyUiDispatcher.Post(() =>
                    {
                        RefreshModelHistory();
                        SetModelStatus("轮询超时：任务已保存到历史，右键可恢复查询（积分以服务端结算为准）", true);
                    });
                }
                else
                {
                    MeshyUiDispatcher.Post(() => SetModelStatus("模型任务失败：" + e.Message, true));
                }
            }
            finally
            {
                MeshyUiDispatcher.Post(() =>
                {
                    modelGenerating = false;
                    modelPreviewButton.SetEnabled(true);
                    UpdateModelCost();
                });
            }
        }

        private void PostModelProgress(MeshyTaskBase task)
        {
            MeshyUiDispatcher.Post(() =>
            {
                modelProgressBar.value = task.Progress;
                SetModelStatus("任务状态: " + task.StatusRaw + " " + task.Progress + "%", false);
            });
        }

        private async Task FinalizeModelAsync(
            string taskId,
            Dictionary<string, string> modelUrls,
            List<string> textureUrls,
            double credits,
            string prompt,
            bool useMock)
        {
            var folder = MeshyPaths.TaskFolder("text-to-3d", taskId);
            Directory.CreateDirectory(folder);
            string glbPath = null;

            if (!useMock && modelUrls != null && modelUrls.ContainsKey("glb"))
            {
                glbPath = Path.Combine(folder, "model.glb");
                var glbOk = await DownloadToFileAsync(modelUrls["glb"], glbPath);
                if (!glbOk)
                {
                    glbPath = null;
                }
            }

            if (!useMock && textureUrls != null)
            {
                for (var i = 0; i < textureUrls.Count; i++)
                {
                    var extension = Path.GetExtension(new Uri(textureUrls[i]).AbsolutePath);
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = ".png";
                    }
                    await DownloadToFileAsync(textureUrls[i], Path.Combine(folder, "texture_" + i + extension));
                }
            }

            if (!useMock && glbPath != null && File.Exists(glbPath))
            {
                var relativeGlb = MeshyPaths.Relative("text-to-3d", taskId, "model.glb");
                AssetDatabase.ImportAsset(relativeGlb);
                MeshyMaterialBuilder.CreatePbrMaterial(folder, textureUrls, "PBR");
            }

            modelLastGlbPath = glbPath;
            MeshyUiDispatcher.Post(() =>
            {
                if (modelStatsLabel != null)
                {
                    modelStatsLabel.text = useMock ? "拓扑 · 占位预览" : "拓扑 · GLB 已下载";
                }
                imageCache.AddOrUpdate(new MeshyCachedTask
                {
                    TaskId = taskId,
                    TaskType = "text-to-3d",
                    Status = "SUCCEEDED",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ConsumedCredits = credits,
                    ModelUrls = modelUrls,
                    TextureUrls = textureUrls,
                    Prompt = prompt,
                    AiModel = modelAiDropdown == null ? "meshy-6" : modelAiDropdown.value
                });
                RefreshModelHistory();
            });

            await LoadModelPreviewAsync(glbPath);
        }

        private async Task ResumeModelTaskAsync(MeshyCachedTask entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.TaskId))
            {
                return;
            }

            SetModelStatus("正在恢复查询任务 " + ShortId(entry.TaskId) + " ...", false);
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
                    var taskType = entry.TaskType == "image-to-3d" ? "image-to-3d" : "text-to-3d";
                    var poller = new MeshyTaskPoller(client, TimeSpan.FromSeconds(3), 200);
                    if (taskType == "image-to-3d")
                    {
                        var task = await poller.WaitForTaskAsync<ImageTo3DTask>(
                            entry.TaskId,
                            taskType,
                            t => PostModelProgress(t),
                            CancellationToken.None);
                        await FinalizeModelAsync(task.Id, task.ModelUrls, task.TextureUrls, task.ConsumedCredits, entry.Prompt, false);
                    }
                    else
                    {
                        var task = await poller.WaitForTaskAsync<TextTo3DTask>(
                            entry.TaskId,
                            taskType,
                            t => PostModelProgress(t),
                            CancellationToken.None);
                        await FinalizeModelAsync(task.Id, task.ModelUrls, task.TextureUrls, task.ConsumedCredits, entry.Prompt, false);
                    }
                }

                MeshyUiDispatcher.Post(() => SetModelStatus("恢复查询完成：任务已成功并下载", false));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                MeshyUiDispatcher.Post(() => SetModelStatus("恢复查询失败：" + e.Message, true));
            }
            finally
            {
                MeshyUiDispatcher.Post(RefreshModelHistory);
            }
        }

        private async Task<bool> DownloadToFileAsync(string url, string path)
        {
            var tcs = new TaskCompletionSource<bool>();
            MeshyModelDownloader.DownloadFile(url, path, ok => tcs.TrySetResult(ok));
            return await tcs.Task;
        }

        private async Task RunRetextureAsync()
        {
            if (modelRetexturing)
            {
                return;
            }

            var entry = LatestModelEntry();
            if (entry == null)
            {
                SetModelStatus("请先生成或选择一个模型，再执行重新纹理", true);
                return;
            }
            var prompt = modelRetexturePromptField == null ? string.Empty : modelRetexturePromptField.value.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                SetModelStatus("请输入重新纹理的风格描述", true);
                return;
            }

            var useMock = MeshySettings.UseMockMode;
            modelRetexturing = true;
            modelRetextureButton.SetEnabled(false);
            SetModelStatus(useMock ? "模拟模式：正在重新纹理..." : "正在重新纹理...", false);

            IMeshyApi api = null;
            try
            {
                api = useMock
                    ? (IMeshyApi)new MeshyMockApi()
                    : new MeshyApiClient(new MeshyApiConfig
                    {
                        ApiKey = MeshySettings.ApiKey,
                        ProxyUrl = MeshySettings.ProxyUrl,
                        TimeoutSeconds = MeshySettings.TimeoutSeconds
                    });

                var request = new RetextureRequest
                {
                    InputTaskId = entry.TaskId,
                    TextStylePrompt = prompt
                };
                var created = await api.CreateRetextureAsync(request);
                var poller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 120);
                var task = await poller.WaitForTaskAsync<RetextureTask>(
                    created.Result,
                    "retexture",
                    t => PostModelProgress(t),
                    System.Threading.CancellationToken.None);

                var folder = MeshyPaths.TaskFolder("retexture", task.Id);
                Directory.CreateDirectory(folder);
                if (!useMock && task.TextureUrls != null)
                {
                    for (var i = 0; i < task.TextureUrls.Count; i++)
                    {
                        var extension = Path.GetExtension(new Uri(task.TextureUrls[i]).AbsolutePath);
                        if (string.IsNullOrEmpty(extension))
                        {
                            extension = ".png";
                        }
                        await DownloadToFileAsync(task.TextureUrls[i], Path.Combine(folder, "texture_" + i + extension));
                    }
                }

                MeshyUiDispatcher.Post(() =>
                {
                    imageCache.AddOrUpdate(new MeshyCachedTask
                    {
                        TaskId = task.Id,
                        TaskType = "retexture",
                        Status = "SUCCEEDED",
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ConsumedCredits = task.ConsumedCredits,
                        Prompt = prompt,
                        ModelUrls = task.ModelUrls,
                        TextureUrls = task.TextureUrls
                    });
                    SetModelStatus("重新纹理完成：" + task.Id, false);
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                MeshyUiDispatcher.Post(() => SetModelStatus("重新纹理失败：" + e.Message, true));
            }
            finally
            {
                MeshyUiDispatcher.Post(() =>
                {
                    modelRetexturing = false;
                    modelRetextureButton.SetEnabled(true);
                });
            }
        }

        private MeshyCachedTask LatestModelEntry()
        {
            for (var i = imageCache.Entries.Count - 1; i >= 0; i--)
            {
                var candidate = imageCache.Entries[i];
                if ((candidate.TaskType == "text-to-3d" || candidate.TaskType == "image-to-3d") &&
                    candidate.ModelUrls != null && candidate.ModelUrls.ContainsKey("glb"))
                {
                    return candidate;
                }
            }
            return null;
        }

        private void PostModelSseProgress(int progress)
        {
            MeshyUiDispatcher.Post(() =>
            {
                if (modelProgressBar != null)
                {
                    modelProgressBar.value = progress;
                }
            });
        }

        private static async Task WatchSseProgressAsync(
            MeshyApiConfig config,
            string taskId,
            string taskType,
            Action<int> onProgress)
        {
            var sse = new MeshyTaskSse(config);
            try
            {
                await sse.WatchAsync(taskId, taskType, onProgress, System.Threading.CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P6] SSE 进度读取失败，继续使用轮询: " + e.Message);
            }
            finally
            {
                sse.Dispose();
            }
        }

        private static MeshyApiConfig SseConfig()
        {
            return new MeshyApiConfig
            {
                ApiKey = MeshySettings.ApiKey,
                ProxyUrl = MeshySettings.ProxyUrl,
                TimeoutSeconds = MeshySettings.TimeoutSeconds
            };
        }

        private async Task LoadModelPreviewAsync(string glbPath)
        {
            if (modelPreviewHost == null)
            {
                modelPreviewHost = new MeshyModelPreviewHost();
                modelPreviewHost.TextureChanged += () =>
                {
                    if (modelPreviewImage != null)
                    {
                        modelPreviewImage.image = modelPreviewHost.Texture;
                        modelPreviewImage.MarkDirtyRepaint();
                    }
                };
                modelPreviewHost.Rendered += () =>
                {
                    if (modelPreviewImage != null)
                    {
                        modelPreviewImage.MarkDirtyRepaint();
                    }
                };
            }

            var loaded = string.IsNullOrEmpty(glbPath)
                ? await modelPreviewHost.LoadPlaceholderAsync()
                : await modelPreviewHost.LoadAsync(glbPath);

            MeshyUiDispatcher.Post(() =>
            {
                if (loaded && modelPreviewImage != null && modelPreviewHost != null)
                {
                    modelPreviewHost.Render();
                    modelPreviewImage.image = modelPreviewHost.Texture;
                    modelPreviewImage.MarkDirtyRepaint();
                    modelImportButton.SetEnabled(glbPath != null);
                    SetModelStatus("模型预览就绪", false);
                }
                else
                {
                    SetModelStatus("模型预览加载失败", true);
                }
            });
        }

        private void OnModelImportClicked()
        {
            if (string.IsNullOrEmpty(modelLastGlbPath) || !File.Exists(modelLastGlbPath))
            {
                SetModelStatus("没有可导入的模型文件", true);
                return;
            }

            var relative = "Assets" + modelLastGlbPath.Replace(Application.dataPath, "");
            AssetDatabase.ImportAsset(relative);
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(relative);
            Selection.activeObject = main;
            EditorGUIUtility.PingObject(main);
            SetModelStatus("已导入工程: " + relative, false);
        }

        private void RefreshModelHistory()
        {
            if (modelHistoryList == null)
            {
                return;
            }

            modelHistoryList.Clear();
            var entries = new List<MeshyCachedTask>(imageCache.Entries);
            entries.Reverse();
            foreach (var entry in entries)
            {
                if (entry.TaskType != "text-to-3d" && entry.TaskType != "image-to-3d")
                {
                    continue;
                }

                var title = string.IsNullOrEmpty(entry.Prompt) ? "模型生成" : entry.Prompt;
                var shortDate = entry.CreatedAt;
                if (entry.CreatedAt != null && entry.CreatedAt.Length >= 16)
                {
                    shortDate = entry.CreatedAt.Substring(5, 11);
                }
                var localGlb = MeshyPaths.FindModelFile(entry.TaskId);
                var formatText = entry.ModelUrls == null
                    ? (localGlb != null && File.Exists(localGlb) ? "本地 GLB" : "0 格式")
                    : entry.ModelUrls.Count + " 格式";
                var creditsText = entry.ConsumedCredits <= 0
                    ? string.Empty
                    : entry.ConsumedCredits.ToString("0.#") + " 积分 · ";
                var rigBadge = string.IsNullOrEmpty(FindRigTaskForModel(entry.TaskId))
                    ? string.Empty
                    : " · 已绑定";
                var card = new Button
                {
                    text = title + " · " + entry.Status + " · " + shortDate + " · " + creditsText + formatText + rigBadge
                };
                card.AddToClassList("action-card");
                card.AddToClassList("model-history-card");
                card.clicked += () =>
                {
                    foreach (var other in modelHistoryList.Children())
                    {
                        other.RemoveFromClassList("active");
                    }
                    card.AddToClassList("active");
                    SelectModelHistory(entry);
                };
                card.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction(
                        "跳转到相关文件夹",
                        _ => OpenModelHistoryFolder(entry),
                        DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendAction(
                        "在工程中定位模型",
                        _ => LocateModelInProject(entry),
                        DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendAction(
                        "删除记录",
                        _ => DeleteModelHistory(entry),
                        DropdownMenuAction.AlwaysEnabled);
                    if (entry.Status == "TIMEOUT")
                    {
                        evt.menu.AppendAction(
                            "恢复查询任务",
                            action => { _ = ResumeModelTaskAsync(entry); },
                            DropdownMenuAction.AlwaysEnabled);
                    }
                }));
                modelHistoryList.Add(card);
            }
        }

        private void SelectModelHistory(MeshyCachedTask entry)
        {
            var glbPath = MeshyPaths.FindModelFile(entry.TaskId) ?? string.Empty;
            if (File.Exists(glbPath))
            {
                _ = LoadModelPreviewAsync(glbPath);
                SetModelStatus("已载入历史模型", false);
            }
            else if (MeshySettings.UseMockMode)
            {
                _ = LoadModelPreviewAsync(null);
                SetModelStatus("模拟模式占位预览", false);
            }
            else
            {
                SetModelStatus("本地无该模型文件（仅文本记录）", true);
            }
        }

        private void OpenModelHistoryFolder(MeshyCachedTask entry)
        {
            var folder = MeshyPaths.FindTaskFolder("text-to-3d", entry.TaskId);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
                SetModelStatus("已打开文件夹: " + folder, false);
            }
            else
            {
                SetModelStatus("本地无该模型的生成文件夹", true);
            }
        }

        private void LocateModelInProject(MeshyCachedTask entry)
        {
            var glb = MeshyPaths.FindModelFile(entry.TaskId);
            if (!File.Exists(glb))
            {
                SetModelStatus("本地无该模型文件", true);
                return;
            }

            var relative = "Assets" + glb.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
            AssetDatabase.ImportAsset(relative);
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(relative);
            Selection.activeObject = main;
            EditorGUIUtility.PingObject(main);
            SetModelStatus("已在工程中定位: " + relative, false);
        }

        private void DeleteModelHistory(MeshyCachedTask entry)
        {
            var confirmed = EditorUtility.DisplayDialog(
                "删除记录",
                "确定删除该模型记录及其本地文件（包含关联绑定记录）？",
                "删除",
                "取消");
            if (!confirmed)
            {
                return;
            }

            var folder = MeshyPaths.FindTaskFolder(entry.TaskType, entry.TaskId);
            if (Directory.Exists(folder))
            {
                var relative = "Assets" + folder.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
                AssetDatabase.DeleteAsset(relative);
            }

            foreach (var rig in imageCache.Entries.ToList())
            {
                if (rig.TaskType == "rigging" &&
                    !string.IsNullOrEmpty(rig.Prompt) &&
                    rig.Prompt.StartsWith("绑定:", StringComparison.Ordinal) &&
                    rig.Prompt.EndsWith(entry.TaskId, StringComparison.Ordinal))
                {
                    imageCache.Remove(rig.TaskId);
                }
            }

            imageCache.Remove(entry.TaskId);
            RefreshModelHistory();
            SetModelStatus("已删除记录：" + ShortId(entry.TaskId), false);
        }

        [MenuItem("Meshy Workspace/Smoke Test Model UI (Mock)")]
        public static void SmokeTestModelUiMock()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("ModelView", "SidebarModel");
            MeshySettings.UseMockMode = true;
            if (window.modelPromptField != null)
            {
                window.modelPromptField.SetValueWithoutNotify("a fantasy knight in armor");
            }
            window.modelPreviewTaskId = null;
            _ = window.GenerateModelAsync(false);

            var stage = 0;
            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (EditorApplication.timeSinceStartup - started > 60.0)
                {
                    EditorApplication.update -= Poll;
                    FinishModelMock(window, false, "timeout stage " + stage);
                    return;
                }

                if (stage == 0 && !window.modelGenerating && !string.IsNullOrEmpty(window.modelPreviewTaskId))
                {
                    stage = 1;
                    _ = window.GenerateModelAsync(true);
                    return;
                }

                if (stage == 1 &&
                    !window.modelGenerating &&
                    window.modelPreviewHost != null &&
                    window.modelPreviewHost.Texture != null &&
                    window.modelStatusLabel != null &&
                    window.modelStatusLabel.text.Contains("就绪"))
                {
                    EditorApplication.update -= Poll;
                    FinishModelMock(window, true, "previewAndRefineDone");
                }
            }
            EditorApplication.update += Poll;
        }

        [MenuItem("Meshy Workspace/Smoke Test Model UI (Real)")]
        public static void SmokeTestModelUiReal()
        {
            if (!MeshySettings.HasApiKey)
            {
                Debug.LogError("[Meshy P4] 未配置 API Key。");
                return;
            }

            MeshySettings.UseMockMode = false;
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("ModelView", "SidebarModel");
            if (window.modelPromptField != null)
            {
                window.modelPromptField.SetValueWithoutNotify("苹果");
            }
            window.modelPreviewTaskId = null;
            _ = window.GenerateModelAsync(false);

            var stage = 0;
            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (EditorApplication.timeSinceStartup - started > 900.0)
                {
                    EditorApplication.update -= Poll;
                    VerifyModelReal(window, false, "timeout stage " + stage);
                    return;
                }

                if (stage == 0 && !window.modelGenerating && !string.IsNullOrEmpty(window.modelPreviewTaskId))
                {
                    stage = 1;
                    _ = window.GenerateModelAsync(true);
                    return;
                }

                if (stage == 1 &&
                    !window.modelGenerating &&
                    window.modelPreviewHost != null &&
                    window.modelPreviewHost.Texture != null &&
                    window.modelStatusLabel != null &&
                    window.modelStatusLabel.text.Contains("就绪"))
                {
                    EditorApplication.update -= Poll;
                    VerifyModelReal(window, true, "previewAndRefineDone");
                }
            }
            EditorApplication.update += Poll;
        }

        private static void VerifyModelReal(
            MeshyWorkspaceWindow window,
            bool generationOk,
            string note)
        {
            MeshyCachedTask entry = null;
            for (var i = window.imageCache.Entries.Count - 1; i >= 0; i--)
            {
                var candidate = window.imageCache.Entries[i];
                if (candidate.TaskType == "text-to-3d" || candidate.TaskType == "image-to-3d")
                {
                    entry = candidate;
                    break;
                }
            }

            var historyCards = window.modelHistoryList == null ? -1 : window.modelHistoryList.childCount;
            var folder = entry == null
                ? string.Empty
                : MeshyPaths.FindTaskFolder(entry.TaskType, entry.TaskId);
            var glbBytes = 0L;
            var textureFiles = 0;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                var glb = Path.Combine(folder, "model.glb");
                if (File.Exists(glb))
                {
                    glbBytes = new FileInfo(glb).Length;
                }
                textureFiles = Directory.GetFiles(folder, "texture_*").Length;
            }

            if (entry == null)
            {
                FinishModelReal(
                    window,
                    false,
                    note + " noHistoryEntry",
                    historyCards,
                    false,
                    string.Empty,
                    0,
                    0,
                    string.Empty);
                return;
            }

            window.SelectModelHistory(entry);
            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (window.modelPreviewHost != null &&
                    window.modelPreviewHost.Texture != null &&
                    window.modelStatusLabel != null &&
                    window.modelStatusLabel.text.Contains("就绪"))
                {
                    EditorApplication.update -= Poll;
                    FinishModelReal(
                        window,
                        generationOk,
                        note,
                        historyCards,
                        true,
                        folder,
                        glbBytes,
                        textureFiles,
                        entry.TaskId);
                    return;
                }

                if (EditorApplication.timeSinceStartup - started > 60.0)
                {
                    EditorApplication.update -= Poll;
                    FinishModelReal(
                        window,
                        false,
                        note + " historySelectFailed",
                        historyCards,
                        window.modelPreviewHost != null && window.modelPreviewHost.Texture != null,
                        folder,
                        glbBytes,
                        textureFiles,
                        entry.TaskId);
                }
            }
            EditorApplication.update += Poll;
        }

        private static async void FinishModelReal(
            MeshyWorkspaceWindow window,
            bool ok,
            string note,
            int historyCards,
            bool previewSelected,
            string folder,
            long glbBytes,
            int textureFiles,
            string taskId)
        {
            var balance = "unknown";
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
                    var result = await client.GetBalanceAsync();
                    balance = result.Balance.ToString("F0");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P4] 获取冒烟后余额失败: " + e.Message);
            }

            var lines = string.Join(
                Environment.NewLine,
                "realModel=" + (ok ? "OK" : "FAILED"),
                "note=" + note,
                "taskId=" + taskId,
                "historyCards=" + historyCards,
                "previewSelected=" + previewSelected,
                "glbBytes=" + glbBytes,
                "textureFiles=" + textureFiles,
                "folder=" + folder,
                "balanceAfter=" + balance);

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p4-real-report.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P4] 写入真实模型报告失败: " + e.Message);
            }
            Debug.Log("[Meshy P4] 真实模型流程: " + (ok ? "OK" : "FAILED") + " " + note);
        }

        private static void FinishModelMock(MeshyWorkspaceWindow window, bool ok, string note)
        {
            var lines = string.Join(
                Environment.NewLine,
                "mockModel=" + (ok ? "OK" : "FAILED"),
                "note=" + note,
                "previewTask=" + (window.modelPreviewTaskId ?? string.Empty),
                "texture=" + (window.modelPreviewHost != null && window.modelPreviewHost.Texture != null ? "yes" : "no"),
                "history=" + (window.imageCache == null ? 0 : window.imageCache.Entries.Count));

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p4-mock-report.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P4] 写入模拟模型报告失败: " + e.Message);
            }
            Debug.Log("[Meshy P4] 模拟模型流程: " + (ok ? "OK" : "FAILED") + " " + note);
        }

        private void SetModelStatus(string text, bool isError)
        {
            if (modelStatusLabel == null)
            {
                return;
            }
            modelStatusLabel.text = text;
            if (isError)
            {
                modelStatusLabel.AddToClassList("error-text");
            }
            else
            {
                modelStatusLabel.RemoveFromClassList("error-text");
            }
        }

        [MenuItem("Meshy Workspace/Smoke Test Retexture (Mock)")]
        public static void SmokeTestRetextureMock()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("ModelView", "SidebarModel");
            MeshySettings.UseMockMode = true;

            if (window.LatestModelEntry() == null)
            {
                Debug.LogError("[Meshy P6] 没有可用模型历史，请先完成模型生成。");
                return;
            }
            if (window.modelRetexturePromptField != null)
            {
                window.modelRetexturePromptField.SetValueWithoutNotify("红木材质");
            }

            _ = window.RunRetextureAsync();
            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (!window.modelRetexturing &&
                    window.modelStatusLabel != null &&
                    window.modelStatusLabel.text.Contains("重新纹理完成"))
                {
                    EditorApplication.update -= Poll;
                    FinishRetextureMock(window, true, "retextureDone");
                    return;
                }

                if (EditorApplication.timeSinceStartup - started > 120.0)
                {
                    EditorApplication.update -= Poll;
                    FinishRetextureMock(window, false, "timeout");
                }
            }
            EditorApplication.update += Poll;
        }

        private static void FinishRetextureMock(MeshyWorkspaceWindow window, bool ok, string note)
        {
            var lines = string.Join(
                Environment.NewLine,
                "mockRetexture=" + (ok ? "OK" : "FAILED"),
                "note=" + note,
                "history=" + (window.imageCache == null ? 0 : window.imageCache.Entries.Count));

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p6-mock-report.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P6] 写入模拟重纹理报告失败: " + e.Message);
            }
            Debug.Log("[Meshy P6] 模拟重纹理流程: " + (ok ? "OK" : "FAILED") + " " + note);
        }
    }
}
