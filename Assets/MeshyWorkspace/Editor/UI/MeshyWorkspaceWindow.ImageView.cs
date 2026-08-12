using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public sealed partial class MeshyWorkspaceWindow
    {
        private const int MaxReferenceImages = 5;
        private const long MaxReferenceBytes = 20L * 1024 * 1024;
        private const int HistoryPageSize = 6;
        private const string HistoryRelativePath = "Assets/MeshyWorkspace/History/tasks.json";

        private static readonly string[] SupportedImageExtensions =
            { ".png", ".jpg", ".jpeg", ".webp" };

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

        private readonly List<string> imageReferencePaths = new List<string>();
        private readonly List<Texture2D> imageTextures = new List<Texture2D>();
        private MeshyTaskCache imageCache;
        private List<MeshyCachedTask> imageHistory = new List<MeshyCachedTask>();
        private int imageHistoryPage;
        private bool imageGenerating;
        private double imageBalance = -1;
        private string imageAspect = "1:1";
        private string imagePose;
        private int imageCount = 1;

        private void BindImagePage()
        {
            imageModelDropdown = rootVisualElement.Q<DropdownField>("ImageModelDropdown");
            imagePromptField = rootVisualElement.Q<TextField>("ImagePromptField");
            imageSearchField = rootVisualElement.Q<TextField>("ImageSearchField");
            imageCharCountLabel = rootVisualElement.Q<Label>("ImageCharCountLabel");
            imageMultiViewToggle = rootVisualElement.Q<Toggle>("ImageMultiViewToggle");
            imageReferenceLabel = rootVisualElement.Q<Label>("ImageReferenceLabel");
            imageCostLabel = rootVisualElement.Q<Label>("ImageCostLabel");
            imageGenerateButton = rootVisualElement.Q<Button>("ImageGenerateButton");
            imageProgressBar = rootVisualElement.Q<ProgressBar>("ImageProgressBar");
            imageStatusLabel = rootVisualElement.Q<Label>("ImageStatusLabel");
            imageResultGrid = rootVisualElement.Q<VisualElement>("ImageResultGrid");
            imageEmptyLabel = rootVisualElement.Q<Label>("ImageEmptyLabel");
            imageHistoryList = rootVisualElement.Q<ScrollView>("ImageHistoryList");
            imageHistoryMoreButton = rootVisualElement.Q<Button>("ImageHistoryMoreButton");

            if (imageModelDropdown != null)
            {
                imageModelDropdown.choices = new List<string>
                {
                    "nano-banana",
                    "nano-banana-2",
                    "nano-banana-pro",
                    "gpt-image-2"
                };
                imageModelDropdown.RegisterValueChangedCallback(_ => UpdateImageCost());
            }

            if (imagePromptField != null)
            {
                imagePromptField.maxLength = 800;
                imagePromptField.RegisterValueChangedCallback(evt =>
                {
                    if (imageCharCountLabel != null)
                    {
                        imageCharCountLabel.text = evt.newValue.Length + "/800";
                    }
                });
            }

            BindSegmented("AspectSegments", new[] { "1:1", "16:9", "9:16", "4:3", "3:4" }, option => imageAspect = option);
            BindSegmented("CountSegments", new[] { "1", "2", "3", "4" }, option => imageCount = int.Parse(option));
            BindSegmented("PoseSegments", new[] { "无", "A 姿势", "T 姿势" }, option => imagePose = MapImagePose(option));

            if (imageSearchField != null)
            {
                imageSearchField.RegisterValueChangedCallback(_ => RefreshImageHistory());
            }

            var upload = rootVisualElement.Q<Button>("ImageUploadButton");
            if (upload != null)
            {
                upload.clicked += OnAddReferenceClick;
                RegisterImageDropZone(upload);
            }
            RegisterImageCanvasDrop();
            RegisterImagePaste();

            var clearReference = rootVisualElement.Q<Button>("ImageClearReferenceButton");
            if (clearReference != null)
            {
                clearReference.clicked += ClearImageReferences;
            }

            if (imageGenerateButton != null)
            {
                imageGenerateButton.clicked += OnGenerateImageClicked;
            }

            if (imageHistoryMoreButton != null)
            {
                imageHistoryMoreButton.clicked += () =>
                {
                    imageHistoryPage++;
                    RenderImageHistoryPage();
                };
            }

            var cachePath = Path.Combine(Application.dataPath, "MeshyWorkspace", "History", "tasks.json");
            imageCache = new MeshyTaskCache(cachePath);
            MigrateGeneratedLayout();
            MigrateLegacyHistory(imageCache);
            RebuildHistoryFromDisk(imageCache);
            RefreshImageHistory();
            UpdateImageCost();
            SetImageStatus("就绪", false);
        }

        private static void MigrateLegacyHistory(MeshyTaskCache cache)
        {
            var legacy = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "tasks.json");
            if (!File.Exists(legacy))
            {
                return;
            }

            try
            {
                var entries = JsonConvert.DeserializeObject<List<MeshyCachedTask>>(File.ReadAllText(legacy));
                if (entries == null)
                {
                    return;
                }
                foreach (var entry in entries)
                {
                    cache.AddOrUpdate(entry);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy] 迁移旧历史失败: " + e.Message);
            }
        }

        private static void RebuildHistoryFromDisk(MeshyTaskCache cache)
        {
            var known = new HashSet<string>(cache.Entries.Select(e => e.TaskId));
            var roots = new List<string>();
            foreach (var sub in new[] { MeshyPaths.Images, MeshyPaths.Models, MeshyPaths.Animations, MeshyPaths.ReferenceModels })
            {
                if (Directory.Exists(sub))
                {
                    roots.Add(sub);
                }
            }
            if (Directory.Exists(MeshyPaths.Root))
            {
                roots.Add(MeshyPaths.Root);
            }

            foreach (var root in roots)
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var taskId = Path.GetFileName(dir);
                    if (known.Contains(taskId))
                    {
                        continue;
                    }
                    var entry = BuildEntryFromFolder(dir, taskId);
                    if (entry == null)
                    {
                        continue;
                    }
                    cache.AddOrUpdate(entry);
                    known.Add(taskId);
                }
            }
        }

        private static void MigrateGeneratedLayout()
        {
            if (!Directory.Exists(MeshyPaths.Root))
            {
                return;
            }

            foreach (var sub in new[] { MeshyPaths.ImagesDir, MeshyPaths.ModelsDir, MeshyPaths.AnimationsDir, MeshyPaths.ReferenceModelsDir })
            {
                Directory.CreateDirectory(Path.Combine(MeshyPaths.Root, sub));
            }
            AssetDatabase.Refresh();

            var legacyModels = Path.Combine(MeshyPaths.Root, "models");
            if (Directory.Exists(legacyModels))
            {
                var target = Path.Combine(MeshyPaths.Root, MeshyPaths.ReferenceModelsDir);
                if (!Directory.Exists(target) || Directory.GetFiles(target).Length == 0)
                {
                    if (Directory.Exists(target))
                    {
                        AssetDatabase.DeleteAsset("Assets/MeshyGenerated/" + MeshyPaths.ReferenceModelsDir);
                    }
                    AssetDatabase.MoveAsset(
                        "Assets/MeshyGenerated/models",
                        "Assets/MeshyGenerated/" + MeshyPaths.ReferenceModelsDir);
                }
                else
                {
                    foreach (var file in Directory.GetFiles(legacyModels))
                    {
                        var destination = Path.Combine(target, Path.GetFileName(file));
                        if (!File.Exists(destination))
                        {
                            File.Move(file, destination);
                        }
                    }
                    if (Directory.GetFiles(legacyModels).Length == 0)
                    {
                        AssetDatabase.DeleteAsset("Assets/MeshyGenerated/models");
                    }
                }
            }

            foreach (var dir in Directory.GetDirectories(MeshyPaths.Root))
            {
                var name = Path.GetFileName(dir);
                if (name == MeshyPaths.ImagesDir ||
                    name == MeshyPaths.ModelsDir ||
                    name == MeshyPaths.AnimationsDir ||
                    name == MeshyPaths.ReferenceModelsDir ||
                    name == "README.md")
                {
                    continue;
                }

                var taskType = InferTypeFromFolder(dir);
                if (taskType == null)
                {
                    if (Directory.GetFiles(dir).Length == 0)
                    {
                        AssetDatabase.DeleteAsset("Assets/MeshyGenerated/" + name);
                    }
                    continue;
                }

                var destination = Path.Combine(MeshyPaths.Root, MeshyPaths.TypeFolder(taskType), name);
                if (Directory.Exists(destination))
                {
                    continue;
                }
                var moveError = AssetDatabase.MoveAsset(
                    "Assets/MeshyGenerated/" + name,
                    "Assets/MeshyGenerated/" + MeshyPaths.TypeFolder(taskType) + "/" + name);
                if (!string.IsNullOrEmpty(moveError))
                {
                    try
                    {
                        Directory.Move(dir, destination);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Meshy] 迁移回退失败: " + e.Message);
                    }
                }
            }

            AssetDatabase.Refresh();
        }

        private static string InferTypeFromFolder(string folder)
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (name == "animated.glb")
                {
                    return "animation";
                }
                if (name.StartsWith("image_", StringComparison.Ordinal))
                {
                    return "text-to-image";
                }
                if (name == "model.glb")
                {
                    return "text-to-3d";
                }
            }
            return null;
        }

        private static MeshyCachedTask BuildEntryFromFolder(string folder, string taskId)
        {
            var files = Directory.GetFiles(folder)
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (files.Count == 0)
            {
                return null;
            }

            string taskType;
            if (files.Any(f => Path.GetFileName(f) == "animated.glb"))
            {
                taskType = "animation";
            }
            else if (files.Any(f => Path.GetFileName(f).StartsWith("image_")))
            {
                taskType = "text-to-image";
            }
            else if (files.Any(f => Path.GetFileName(f) == "model.glb"))
            {
                taskType = "text-to-3d";
            }
            else
            {
                return null;
            }

            var created = files
                .Select(f => new FileInfo(f).LastWriteTime)
                .DefaultIfEmpty(DateTime.Now)
                .Min();
            return new MeshyCachedTask
            {
                TaskId = taskId,
                TaskType = taskType,
                Status = "SUCCEEDED",
                CreatedAt = created.ToString("yyyy-MM-dd HH:mm:ss"),
                ConsumedCredits = 0,
                Prompt = string.Empty,
                ModelUrls = null,
                TextureUrls = null,
                ImageUrls = null
            };
        }

        private void BindSegmented(string containerName, string[] options, Action<string> onSelect)
        {
            var container = rootVisualElement.Q<VisualElement>(containerName);
            if (container == null)
            {
                return;
            }

            container.Clear();
            var buttons = new List<Button>();
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                var button = new Button
                {
                    text = options[index]
                };
                button.clicked += () =>
                {
                    foreach (var other in buttons)
                    {
                        other.RemoveFromClassList("active");
                    }
                    button.AddToClassList("active");
                    onSelect(options[index]);
                    UpdateImageCost();
                };
                button.AddToClassList("segment");
                if (index == 0)
                {
                    button.AddToClassList("active");
                }
                container.Add(button);
                buttons.Add(button);
            }
        }

        private void SetSegmentedValue(string containerName, string value)
        {
            var container = rootVisualElement.Q<VisualElement>(containerName);
            if (container == null)
            {
                return;
            }

            var matched = false;
            foreach (var child in container.Children())
            {
                var button = child as Button;
                if (button == null)
                {
                    continue;
                }

                var active = button.text == value;
                matched = matched || active;
                if (active)
                {
                    button.AddToClassList("active");
                }
                else
                {
                    button.RemoveFromClassList("active");
                }
            }

            if (!matched && container.childCount > 0 && container[0] is Button first)
            {
                first.AddToClassList("active");
            }
        }

        private void UpdateImageCost()
        {
            if (imageCostLabel == null || imageGenerateButton == null)
            {
                return;
            }

            var model = imageModelDropdown == null ? "nano-banana" : imageModelDropdown.value;
            var count = imageCount;
            var cost = MeshyPricing.ImageCost(model, count);
            var text = "预计消耗 " + cost + " 积分";
            if (imageBalance >= 0)
            {
                text += " / 余额 " + imageBalance;
            }
            imageCostLabel.text = text;

            if (imageGenerating)
            {
                imageGenerateButton.SetEnabled(false);
                return;
            }

            var blocked = !MeshySettings.UseMockMode && imageBalance >= 0 && cost > imageBalance;
            imageGenerateButton.SetEnabled(!blocked);
            if (blocked)
            {
                SetImageStatus("余额不足，请先充值", true);
            }
        }

        private void OnAddReferenceClick()
        {
            var path = EditorUtility.OpenFilePanel("选择参考图", "", "png,jpg,jpeg,webp");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TryAddImageReference(path);
        }

        private void TryAddImageReference(string path)
        {
            var extension = Path.GetExtension(path);
            if (Array.IndexOf(SupportedImageExtensions, extension) < 0)
            {
                SetImageStatus("不支持的图片格式，仅支持 png/jpg/jpeg/webp", true);
                return;
            }

            var info = new FileInfo(path);
            if (info.Length > MaxReferenceBytes)
            {
                SetImageStatus("图片超过 20MB 限制", true);
                return;
            }

            if (imageReferencePaths.Count >= MaxReferenceImages)
            {
                SetImageStatus("参考图最多 5 张", true);
                return;
            }

            imageReferencePaths.Add(path);
            UpdateImageReferenceLabel();
            SetImageStatus("参考图已添加 " + imageReferencePaths.Count + "/5", false);
        }

        private static bool IsSupportedImagePath(string path)
        {
            var extension = Path.GetExtension(path);
            return Array.IndexOf(SupportedImageExtensions, extension) >= 0;
        }

        private static bool HasSupportedImagePaths(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }
            foreach (var path in paths)
            {
                if (IsSupportedImagePath(path))
                {
                    return true;
                }
            }
            return false;
        }

        private void RegisterImageDropZone(VisualElement zone)
        {
            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var supported = HasSupportedImagePaths(DragAndDrop.paths);
                DragAndDrop.visualMode = supported ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.None;
                SetImageDropHighlight(supported);
                evt.StopPropagation();
            });
            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!HasSupportedImagePaths(DragAndDrop.paths))
                {
                    return;
                }
                DragAndDrop.AcceptDrag();
                AddImageDropPaths(DragAndDrop.paths);
                SetImageDropHighlight(false);
                evt.StopPropagation();
            });
            zone.RegisterCallback<DragExitedEvent>(_ => SetImageDropHighlight(false));
        }

        private void RegisterImageCanvasDrop()
        {
            foreach (var name in new[] { "ImageParams", "ImageCanvas" })
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
                    SetImageDropHighlight(supported);
                    evt.StopPropagation();
                });
                element.RegisterCallback<DragPerformEvent>(evt =>
                {
                    if (!HasSupportedImagePaths(DragAndDrop.paths))
                    {
                        return;
                    }
                    DragAndDrop.AcceptDrag();
                    AddImageDropPaths(DragAndDrop.paths);
                    SetImageDropHighlight(false);
                    evt.StopPropagation();
                });
                element.RegisterCallback<DragExitedEvent>(_ => SetImageDropHighlight(false));
            }
        }

        private void SetImageDropHighlight(bool active)
        {
            var upload = rootVisualElement.Q<Button>("ImageUploadButton");
            if (upload != null)
            {
                if (active)
                {
                    upload.AddToClassList("drag-over");
                }
                else
                {
                    upload.RemoveFromClassList("drag-over");
                }
            }
        }

        private void AddImageDropPaths(string[] paths)
        {
            var added = 0;
            var names = new List<string>();
            foreach (var path in paths)
            {
                if (!IsSupportedImagePath(path))
                {
                    continue;
                }
                TryAddImageReference(path);
                added++;
                names.Add(Path.GetFileName(path));
            }
            if (added > 0)
            {
                SetImageStatus(
                    "已添加参考图：" + string.Join("、", names) + "（" + imageReferencePaths.Count + "/5）",
                    false);
            }
        }

        private void RegisterImagePaste()
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
                    TryAddImageReference(path);
                    evt.StopPropagation();
                }
            });
        }

        private void ClearImageReferences()
        {
            imageReferencePaths.Clear();
            UpdateImageReferenceLabel();
            SetImageStatus("参考图已清除", false);
        }

        private void UpdateImageReferenceLabel()
        {
            if (imageReferenceLabel != null)
            {
                imageReferenceLabel.text = "已选 " + imageReferencePaths.Count + "/5";
            }
        }

        private async void OnGenerateImageClicked()
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

            if (prompt.Length > 800)
            {
                SetImageStatus("提示词超过 800 字符", true);
                return;
            }

            var model = imageModelDropdown == null ? "nano-banana" : imageModelDropdown.value;
            var count = imageCount;
            var cost = MeshyPricing.ImageCost(model, count);
            var useMock = MeshySettings.UseMockMode;
            if (!useMock && imageBalance >= 0 && cost > imageBalance)
            {
                SetImageStatus("余额不足：预计 " + cost + "，当前余额 " + imageBalance, true);
                return;
            }

            imageGenerating = true;
            imageGenerateButton.SetEnabled(false);
            imageProgressBar.value = 0;
            SetImageStatus(useMock ? "模拟模式：正在创建任务..." : "正在创建任务...", false);

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

                var request = new TextToImageRequest
                {
                    AiModel = model,
                    Prompt = prompt,
                    AspectRatio = imageAspect,
                    GenerateMultiView = imageMultiViewToggle != null && imageMultiViewToggle.value,
                    PoseMode = imagePose
                };

                var allUrls = new List<string>();
                string lastTaskId = null;
                double credits = 0;
                for (var i = 0; i < count; i++)
                {
                    MeshyUiDispatcher.Post(() => SetImageStatus("任务 " + (i + 1) + "/" + count, false));
                    var created = await api.CreateTextToImageAsync(request);
                    var poller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 60);
                    var task = await poller.WaitForTaskAsync<TextToImageTask>(
                        created.Result,
                        "text-to-image",
                        t => MeshyUiDispatcher.Post(() =>
                        {
                            imageProgressBar.value = t.Progress;
                            SetImageStatus("任务状态: " + t.StatusRaw + " " + t.Progress + "%", false);
                        }),
                        CancellationToken.None);
                    lastTaskId = task.Id;
                    credits = task.ConsumedCredits;
                    if (task.ImageUrls != null)
                    {
                        allUrls.AddRange(task.ImageUrls);
                    }
                }

                string downloadFolder = null;
                if (!useMock && !string.IsNullOrEmpty(lastTaskId))
                {
                    downloadFolder = MeshyPaths.TaskFolder("text-to-image", lastTaskId);
                    Directory.CreateDirectory(downloadFolder);
                    for (var i = 0; i < allUrls.Count; i++)
                    {
                        var extension = Path.GetExtension(new Uri(allUrls[i]).AbsolutePath);
                        if (string.IsNullOrEmpty(extension))
                        {
                            extension = ".png";
                        }
                        await DownloadToFileAsync(allUrls[i], Path.Combine(downloadFolder, "image_" + i + extension));
                    }
                }

                MeshyUiDispatcher.Post(() =>
                {
                    imageProgressBar.value = 100;
                    SetImageStatus("生成完成，共 " + allUrls.Count + " 张", false);

                    imageCache.AddOrUpdate(new MeshyCachedTask
                    {
                        TaskId = lastTaskId,
                        TaskType = "text-to-image",
                        Status = "SUCCEEDED",
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ConsumedCredits = credits,
                        ImageUrls = allUrls,
                        Prompt = prompt,
                        AiModel = model,
                        AspectRatio = imageAspect
                    });
                    RefreshImageHistory();
                    ShowImageResults(allUrls, downloadFolder);
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                MeshyUiDispatcher.Post(() => SetImageStatus("生成失败：" + e.Message, true));
            }
            finally
            {
                MeshyUiDispatcher.Post(() =>
                {
                    imageGenerating = false;
                    if (api is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    UpdateImageCost();
                });
            }
        }

        private static string MapImagePose(string label)
        {
            switch (label)
            {
                case "A 姿势":
                    return "a-pose";
                case "T 姿势":
                    return "t-pose";
                default:
                    return null;
            }
        }

        private void ShowImageResults(List<string> urls, string localFolder = null)
        {
            imageResultGrid.Clear();
            if (urls == null)
            {
                return;
            }

            for (var i = 0; i < urls.Count; i++)
            {
                var image = new Image
                {
                    name = "ResultImage",
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("result-image");
                imageResultGrid.Add(image);
                LoadLocalImageOrDownload(image, urls[i], imageTextures, FindLocalImage(localFolder, i));
            }
        }

        private static void LoadLocalImageOrDownload(
            Image target,
            string url,
            List<Texture2D> keepAlive,
            string localPath)
        {
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                var bytes = File.ReadAllBytes(localPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(bytes))
                {
                    if (keepAlive != null)
                    {
                        keepAlive.Add(texture);
                    }
                    target.image = texture;
                    return;
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
            MeshyImagePreview.DownloadInto(target, url, keepAlive);
        }

        private static string FindLocalImage(string folder, int index)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }
            var files = Directory.GetFiles(folder, "image_" + index + ".*");
            return files.Length == 0 ? null : files[0];
        }

        private void RefreshImageHistory()
        {
            if (imageCache == null)
            {
                return;
            }

            var all = new List<MeshyCachedTask>(
                imageCache.Entries.Where(e => e.TaskType == "text-to-image"));
            all.Reverse();
            imageHistory = all;
            imageHistoryPage = 0;
            RenderImageHistoryPage();
        }

        private void RenderImageHistoryPage()
        {
            if (imageHistoryList == null)
            {
                return;
            }

            imageHistoryList.Clear();
            var start = imageHistoryPage * HistoryPageSize;
            var end = Math.Min(start + HistoryPageSize, imageHistory.Count);
            for (var i = start; i < end; i++)
            {
                imageHistoryList.Add(CreateImageHistoryCard(imageHistory[i]));
            }

            if (imageEmptyLabel != null)
            {
                imageEmptyLabel.style.display = imageHistory.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (imageHistoryMoreButton != null)
            {
                imageHistoryMoreButton.style.display =
                    end < imageHistory.Count ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private VisualElement CreateImageHistoryCard(MeshyCachedTask entry)
        {
            var card = new VisualElement();
            card.AddToClassList("history-card");
            card.userData = entry.TaskId;
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button)
                {
                    return;
                }
                SelectImageHistory(entry);
            });
            card.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "跳转到相关文件夹",
                    _ => OpenImageHistoryFolder(entry),
                    DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction(
                    "在工程中定位",
                    _ => LocateImageInProject(entry),
                    DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction(
                    "删除记录",
                    _ => DeleteImageHistoryWithFolder(entry),
                    DropdownMenuAction.AlwaysEnabled);
            }));

            var row = new VisualElement();
            row.AddToClassList("history-row");
            var title = new Label(
                (string.IsNullOrEmpty(entry.Prompt) ? ShortId(entry.TaskId) : entry.Prompt) + " · " +
                entry.Status + " · " +
                (entry.ImageUrls == null ? 0 : entry.ImageUrls.Count) + "张");
            title.AddToClassList("history-title");
            row.Add(title);

            var regenerate = new Button(() => RegenerateFromHistory(entry)) { text = "重新生成" };
            regenerate.AddToClassList("secondary-button");
            var delete = new Button(() => DeleteHistoryEntry(entry.TaskId)) { text = "删除" };
            delete.AddToClassList("secondary-button");

            row.Add(regenerate);
            row.Add(delete);
            card.Add(row);

            if (entry.ImageUrls != null && entry.ImageUrls.Count > 0)
            {
                var thumbnail = new Image { scaleMode = ScaleMode.ScaleToFit };
                thumbnail.AddToClassList("history-thumb");
                card.Add(thumbnail);
                LoadLocalImageOrDownload(
                    thumbnail,
                    entry.ImageUrls[0],
                    imageTextures,
                    MeshyPaths.FindImageFile(entry.TaskId, 0));
            }

            return card;
        }

        private void SelectImageHistory(MeshyCachedTask entry)
        {
            if (imageHistoryList != null)
            {
                foreach (var child in imageHistoryList.Children())
                {
                    var active = child.userData as string == entry.TaskId;
                    if (active)
                    {
                        child.AddToClassList("active");
                    }
                    else
                    {
                        child.RemoveFromClassList("active");
                    }
                }
            }

            ShowImageResults(
                entry.ImageUrls,
                MeshyPaths.FindTaskFolder("text-to-image", entry.TaskId));
            SetImageStatus(
                "已载入历史：" + (string.IsNullOrEmpty(entry.Prompt) ? ShortId(entry.TaskId) : entry.Prompt),
                false);
        }

        private void OpenImageHistoryFolder(MeshyCachedTask entry)
        {
            var folder = MeshyPaths.FindTaskFolder("text-to-image", entry.TaskId);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
                SetImageStatus("已打开文件夹: " + folder, false);
            }
            else
            {
                SetImageStatus("本地无该图像的生成文件夹", true);
            }
        }

        private void LocateImageInProject(MeshyCachedTask entry)
        {
            var file = MeshyPaths.FindImageFile(entry.TaskId, 0);
            if (string.IsNullOrEmpty(file))
            {
                SetImageStatus("本地无该图像文件", true);
                return;
            }

            var relative = "Assets" + file.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
            var main = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relative);
            Selection.activeObject = main;
            EditorGUIUtility.PingObject(main);
            SetImageStatus("已在工程中定位: " + relative, false);
        }

        private void DeleteImageHistoryWithFolder(MeshyCachedTask entry)
        {
            var confirmed = EditorUtility.DisplayDialog(
                "删除记录",
                "确定删除该图像记录及其本地文件？",
                "删除",
                "取消");
            if (!confirmed)
            {
                return;
            }

            var folder = MeshyPaths.FindTaskFolder("text-to-image", entry.TaskId);
            if (Directory.Exists(folder))
            {
                var relative = "Assets" + folder.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
                AssetDatabase.DeleteAsset(relative);
            }
            imageCache.Remove(entry.TaskId);
            RefreshImageHistory();
            SetImageStatus("已删除记录：" + ShortId(entry.TaskId), false);
        }

        private void RegenerateFromHistory(MeshyCachedTask entry)
        {
            if (imageModelDropdown != null && !string.IsNullOrEmpty(entry.AiModel))
            {
                imageModelDropdown.SetValueWithoutNotify(entry.AiModel);
            }
            if (imagePromptField != null && entry.Prompt != null)
            {
                imagePromptField.SetValueWithoutNotify(entry.Prompt);
                if (imageCharCountLabel != null)
                {
                    imageCharCountLabel.text = entry.Prompt.Length + "/800";
                }
            }
            if (!string.IsNullOrEmpty(entry.AspectRatio))
            {
                imageAspect = entry.AspectRatio;
                SetSegmentedValue("AspectSegments", entry.AspectRatio);
            }
            UpdateImageCost();
            OnGenerateImageClicked();
        }

        private void DeleteHistoryEntry(string taskId)
        {
            imageCache.Remove(taskId);
            RefreshImageHistory();
        }

        [MenuItem("Meshy Workspace/Smoke Test Image UI (Mock)")]
        public static void SmokeTestImageUiMock()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("ImageView", "SidebarImage");
            MeshySettings.UseMockMode = true;

            if (window.imageModelDropdown != null)
            {
                window.imageModelDropdown.SetValueWithoutNotify("nano-banana");
            }
            if (window.imagePromptField != null)
            {
                window.imagePromptField.SetValueWithoutNotify("mock red apple");
            }
            window.imageCount = 4;
            window.SetSegmentedValue("CountSegments", "4");

            window.UpdateImageCost();
            window.OnGenerateImageClicked();

            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (window.imageGenerating && EditorApplication.timeSinceStartup - started < 30.0)
                {
                    return;
                }

                EditorApplication.update -= Poll;
                var historyOk = window.imageHistory != null && window.imageHistory.Count > 0;
                var status = window.imageStatusLabel == null ? string.Empty : window.imageStatusLabel.text;
                var ok = historyOk && status.Contains("生成完成");
                var report = string.Join(
                    Environment.NewLine,
                    "mockUi=" + (ok ? "OK" : "FAILED"),
                    "status=" + status,
                    "history=" + (window.imageHistory == null ? 0 : window.imageHistory.Count),
                    "grid=" + (window.imageResultGrid == null ? 0 : window.imageResultGrid.childCount));

                try
                {
                    var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p3-mock-report.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(
                        path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + report + Environment.NewLine);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Meshy P3] 写入模拟流程报告失败: " + e.Message);
                }

                Debug.Log("[Meshy P3] 模拟 UI 流程: " + (ok ? "OK" : "FAILED"));
            }

            EditorApplication.update += Poll;
        }

        [MenuItem("Meshy Workspace/Smoke Test Image UI (Real)")]
        public static void SmokeTestImageUiReal()
        {
            if (!MeshySettings.HasApiKey)
            {
                Debug.LogError("[Meshy P3] 未配置 API Key。");
                return;
            }

            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("ImageView", "SidebarImage");
            MeshySettings.UseMockMode = false;

            if (window.imageModelDropdown != null)
            {
                window.imageModelDropdown.SetValueWithoutNotify("nano-banana");
            }
            if (window.imagePromptField != null)
            {
                window.imagePromptField.SetValueWithoutNotify("一个普通的男人（全身照）");
            }
            window.imageCount = 3;
            window.SetSegmentedValue("CountSegments", "3");
            window.UpdateImageCost();
            window.OnGenerateImageClicked();

            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (window.imageGenerating && EditorApplication.timeSinceStartup - started < 600.0)
                {
                    return;
                }
                EditorApplication.update -= Poll;
                VerifyImageReal(window);
            }
            EditorApplication.update += Poll;
        }

        private static void VerifyImageReal(MeshyWorkspaceWindow window)
        {
            var status = window.imageStatusLabel == null ? string.Empty : window.imageStatusLabel.text;
            var generationOk = status.Contains("生成完成");
            MeshyCachedTask entry = null;
            for (var i = window.imageCache.Entries.Count - 1; i >= 0; i--)
            {
                if (window.imageCache.Entries[i].TaskType == "text-to-image")
                {
                    entry = window.imageCache.Entries[i];
                    break;
                }
            }

            var historyCards = window.imageHistoryList == null ? -1 : window.imageHistoryList.childCount;
            var gridBefore = window.imageResultGrid == null ? -1 : window.imageResultGrid.childCount;
            if (entry != null)
            {
                window.SelectImageHistory(entry);
            }

            var folder = entry == null
                ? string.Empty
                : MeshyPaths.FindTaskFolder("text-to-image", entry.TaskId);

            window.rootVisualElement.schedule.Execute(() =>
            {
                var gridAfter = window.imageResultGrid == null ? -1 : window.imageResultGrid.childCount;
                var loadedImages = 0;
                if (window.imageResultGrid != null)
                {
                    foreach (var child in window.imageResultGrid.Children())
                    {
                        var image = child as Image;
                        if (image != null && image.image != null)
                        {
                            loadedImages++;
                        }
                    }
                }

                var activeCards = 0;
                var thumbnailCards = 0;
                if (window.imageHistoryList != null)
                {
                    foreach (var child in window.imageHistoryList.Children())
                    {
                        if (child.ClassListContains("active"))
                        {
                            activeCards++;
                        }
                        if (child.childCount > 1)
                        {
                            thumbnailCards++;
                        }
                    }
                }

                var files = 0;
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    files = Directory.GetFiles(folder, "image_*").Length;
                }

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
                        balance = client.GetBalanceAsync().Result.Balance.ToString("F0");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Meshy P3] 获取冒烟后余额失败: " + e.Message);
                }

                var lines = string.Join(
                    Environment.NewLine,
                    "realImage=" + (generationOk ? "OK" : "FAILED"),
                    "status=" + status,
                    "taskId=" + (entry == null ? string.Empty : entry.TaskId),
                    "credits=" + (entry == null ? 0 : entry.ConsumedCredits),
                    "historyCards=" + historyCards,
                    "gridBefore=" + gridBefore,
                    "gridAfter=" + gridAfter,
                    "loadedImages=" + loadedImages,
                    "activeCards=" + activeCards,
                    "thumbnailCards=" + thumbnailCards,
                    "files=" + files,
                    "balanceAfter=" + balance);

                try
                {
                    var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p3-real-report.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(
                        path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Meshy P3] 写入真实图片报告失败: " + e.Message);
                }
                Debug.Log("[Meshy P3] 真实图片流程: " + (generationOk ? "OK" : "FAILED") + " " + status);
            }).ExecuteLater(4000);
        }

        private static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) ? "unknown" : id.Substring(0, Math.Min(8, id.Length));
        }

        private void SetImageStatus(string text, bool isError)
        {
            if (imageStatusLabel == null)
            {
                return;
            }

            imageStatusLabel.text = text;
            if (isError)
            {
                imageStatusLabel.AddToClassList("error-text");
            }
            else
            {
                imageStatusLabel.RemoveFromClassList("error-text");
            }
        }
    }
}
