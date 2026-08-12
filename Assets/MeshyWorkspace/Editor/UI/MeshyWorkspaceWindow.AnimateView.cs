using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public sealed partial class MeshyWorkspaceWindow
    {
        private const string AnimationLibraryPath = "Assets/MeshyWorkspace/Runtime/MeshyAnimationLibrary.json";
        private const int AnimateActionRenderLimit = 200;

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
        private readonly List<AnimationAction> filteredActions = new List<AnimationAction>();
        private AnimationAction selectedAction;
        private MeshyCachedTask animateModelTask;
        private string animateModelGlbPath;
        private string animateRigTaskId;
        private bool animateGenerating;
        private bool animateRigging;
        private MeshyModelPreviewHost animatePreviewHost;
        private bool animatePreviewWired;
        private bool animateDragging;
        private int animatePointerMode;
        private Vector3 animateLastPointer;

        private void BindAnimatePage()
        {
            animateModelButton = rootVisualElement.Q<Button>("AnimateModelButton");
            animateModelLabel = rootVisualElement.Q<Label>("AnimateModelLabel");
            animateRigButton = rootVisualElement.Q<Button>("AnimateRigButton");
            animateRigLabel = rootVisualElement.Q<Label>("AnimateRigLabel");
            animateSearchField = rootVisualElement.Q<TextField>("AnimateSearchField");
            animateCategoryDropdown = rootVisualElement.Q<DropdownField>("AnimateCategoryDropdown");
            animateActionList = rootVisualElement.Q<ScrollView>("AnimateActionList");
            animateCostLabel = rootVisualElement.Q<Label>("AnimateCostLabel");
            animateStatusLabel = rootVisualElement.Q<Label>("AnimateStatusLabel");
            animateProgressBar = rootVisualElement.Q<ProgressBar>("AnimateProgressBar");
            animateGenerateButton = rootVisualElement.Q<Button>("AnimateGenerateButton");
            animatePlayButton = rootVisualElement.Q<Button>("AnimatePlayButton");
            animatePauseButton = rootVisualElement.Q<Button>("AnimatePauseButton");
            animateResetButton = rootVisualElement.Q<Button>("AnimateResetButton");
            animatePreviewImage = rootVisualElement.Q<Image>("AnimatePreviewImage");
            animateHistoryList = rootVisualElement.Q<ScrollView>("AnimateHistoryList");
            animateFpsToggle = rootVisualElement.Q<Toggle>("AnimateFpsToggle");
            animateFbxToggle = rootVisualElement.Q<Toggle>("AnimateFbxToggle");
            animateArmatureToggle = rootVisualElement.Q<Toggle>("AnimateArmatureToggle");

            LoadAnimationLibrary();

            if (animateCategoryDropdown != null)
            {
                var categories = animationActions
                    .Select(a => a.Category)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                categories.Insert(0, "全部");
                animateCategoryDropdown.choices = categories;
                animateCategoryDropdown.index = 0;
                animateCategoryDropdown.RegisterValueChangedCallback(_ => RefreshAnimateActions());
            }

            if (animateSearchField != null)
            {
                animateSearchField.RegisterValueChangedCallback(_ => RefreshAnimateActions());
            }

            if (animateModelButton != null)
            {
                animateModelButton.clicked += OnAnimateModelClicked;
            }
            if (animateRigButton != null)
            {
                animateRigButton.clicked += () => _ = RunRiggingAsync();
            }
            if (animateGenerateButton != null)
            {
                animateGenerateButton.clicked += () => _ = GenerateAnimationAsync();
            }
            if (animatePlayButton != null)
            {
                animatePlayButton.clicked += () =>
                {
                    if (animatePreviewHost != null)
                    {
                        animatePreviewHost.Play(animatePreviewHost.CurrentClipIndex);
                        SetAnimateStatus("播放中：" + animatePreviewHost.ClipName(animatePreviewHost.CurrentClipIndex), false);
                    }
                };
                animatePlayButton.SetEnabled(false);
            }
            if (animatePauseButton != null)
            {
                animatePauseButton.clicked += () =>
                {
                    if (animatePreviewHost != null)
                    {
                        animatePreviewHost.Pause();
                        SetAnimateStatus("已暂停", false);
                    }
                };
                animatePauseButton.SetEnabled(false);
            }
            if (animateResetButton != null)
            {
                animateResetButton.clicked += () =>
                {
                    if (animatePreviewHost != null)
                    {
                        animatePreviewHost.ResetPlayback();
                        SetAnimateStatus("已重置到起始帧", false);
                    }
                };
                animateResetButton.SetEnabled(false);
            }

            if (animatePreviewImage != null)
            {
                animatePreviewImage.RegisterCallback<PointerDownEvent>(evt =>
                {
                    animateDragging = true;
                    animatePointerMode = evt.button == 1 ? 1 : 0;
                    animateLastPointer = evt.localPosition;
                    animatePreviewImage.CapturePointer(evt.pointerId);
                });
                animatePreviewImage.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!animateDragging)
                    {
                        return;
                    }
                    var delta = evt.localPosition - animateLastPointer;
                    animateLastPointer = evt.localPosition;
                    if (delta.sqrMagnitude < 0.25f)
                    {
                        return;
                    }
                    if (animatePointerMode == 1)
                    {
                        animatePreviewHost?.Drag(delta.x, delta.y);
                    }
                    else
                    {
                        animatePreviewHost?.Pan(delta.x, delta.y);
                    }
                });
                animatePreviewImage.RegisterCallback<PointerUpEvent>(evt =>
                {
                    animateDragging = false;
                    animatePointerMode = 0;
                    animatePreviewImage.ReleasePointer(evt.pointerId);
                });
                animatePreviewImage.RegisterCallback<WheelEvent>(evt =>
                {
                    animatePreviewHost?.Zoom(evt.delta.y * 0.05f);
                });
            }

            if (animateCostLabel != null)
            {
                animateCostLabel.text = "绑定 5 + 动画 3 积分";
            }

            RefreshAnimateActions();
            RefreshAnimateHistory();
            UpdateAnimateModelLabel();
            UpdateAnimateRigLabel();
            SetAnimateStatus("就绪", false);
        }

        private void LoadAnimationLibrary()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AnimationLibraryPath);
            var actions = asset == null
                ? new List<AnimationAction>()
                : MeshyAnimationLibrary.Parse(asset.text);
            animationActions.Clear();
            animationActions.AddRange(actions);
        }

        private void RefreshAnimateActions()
        {
            if (animateActionList == null)
            {
                return;
            }

            var keyword = animateSearchField == null ? string.Empty : animateSearchField.value.Trim();
            var category = animateCategoryDropdown == null || animateCategoryDropdown.value == "全部"
                ? null
                : animateCategoryDropdown.value;

            filteredActions.Clear();
            foreach (var action in animationActions)
            {
                if (category != null && action.Category != category)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(keyword) &&
                    action.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                filteredActions.Add(action);
            }

            animateActionList.Clear();
            var count = Math.Min(filteredActions.Count, AnimateActionRenderLimit);
            for (var i = 0; i < count; i++)
            {
                var action = filteredActions[i];
                var captured = action;
                var button = new Button(() => SelectAnimateAction(captured))
                {
                    text = action.Name
                };
                button.AddToClassList("action-card");
                if (selectedAction != null && selectedAction.Id == action.Id)
                {
                    button.AddToClassList("active");
                }
                button.tooltip = action.Category + " / " + action.Subcategory + " · ID " + action.Id;
                animateActionList.Add(button);
            }

            SetAnimateStatus(
                "动作库 " + filteredActions.Count + " 条" +
                (filteredActions.Count > count ? "（显示前 " + count + " 条）" : string.Empty),
                false);
        }

        private void SelectAnimateAction(AnimationAction action)
        {
            selectedAction = action;
            RefreshAnimateActions();
            SetAnimateStatus("已选动作：" + action.Name + "（ID " + action.Id + "）", false);
        }

        private void OnAnimateModelClicked()
        {
            var menu = new GenericMenu();
            var entries = new List<MeshyCachedTask>(imageCache.Entries);
            entries.Reverse();
            var added = 0;
            foreach (var entry in entries)
            {
                if (!IsModelTaskType(entry.TaskType))
                {
                    continue;
                }
                var glb = MeshyPaths.FindModelFile(entry.TaskId);
                if (!File.Exists(glb) && (entry.ModelUrls == null || !entry.ModelUrls.ContainsKey("glb")))
                {
                    continue;
                }
                var captured = entry;
                var label = (string.IsNullOrEmpty(captured.Prompt) ? "模型" : captured.Prompt) +
                            " · " + ShortId(captured.TaskId);
                menu.AddItem(new GUIContent(label), false, () => SelectAnimateModel(captured));
                added++;
            }

            if (added == 0)
            {
                menu.AddDisabledItem(new GUIContent("暂无可用模型历史"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("选择本地 GLB 文件..."), false, () =>
            {
                var path = EditorUtility.OpenFilePanel("选择 GLB 模型", "", "glb");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                animateModelTask = null;
                animateModelGlbPath = path;
                animateRigTaskId = null;
                UpdateAnimateModelLabel();
                UpdateAnimateRigLabel();
                SetAnimateStatus("已选择本地模型：" + Path.GetFileName(path), false);
            });
            menu.ShowAsContext();
        }

        private void SelectAnimateModel(MeshyCachedTask entry)
        {
            animateModelTask = entry;
            var glb = MeshyPaths.FindModelFile(entry.TaskId);
            animateModelGlbPath = File.Exists(glb) ? glb : null;
            animateRigTaskId = FindRigTaskForModel(entry.TaskId);
            UpdateAnimateModelLabel();
            UpdateAnimateRigLabel();
            SetAnimateStatus("已选择模型：" + (string.IsNullOrEmpty(entry.Prompt) ? entry.TaskId : entry.Prompt), false);
        }

        private string FindRigTaskForModel(string modelTaskId)
        {
            if (string.IsNullOrEmpty(modelTaskId))
            {
                return null;
            }
            for (var i = imageCache.Entries.Count - 1; i >= 0; i--)
            {
                var entry = imageCache.Entries[i];
                if (entry.TaskType == "rigging" &&
                    !string.IsNullOrEmpty(entry.Prompt) &&
                    entry.Prompt.StartsWith("绑定:", StringComparison.Ordinal) &&
                    entry.Prompt.EndsWith(modelTaskId, StringComparison.Ordinal))
                {
                    return entry.TaskId;
                }
            }
            return null;
        }

        private void UpdateAnimateModelLabel()
        {
            if (animateModelLabel == null)
            {
                return;
            }
            if (animateModelTask != null)
            {
                animateModelLabel.text = "已选：" + (string.IsNullOrEmpty(animateModelTask.Prompt)
                    ? ShortId(animateModelTask.TaskId)
                    : animateModelTask.Prompt);
            }
            else if (!string.IsNullOrEmpty(animateModelGlbPath))
            {
                animateModelLabel.text = "已选：" + Path.GetFileName(animateModelGlbPath);
            }
            else
            {
                animateModelLabel.text = "从生成历史或 GLB 选择角色";
            }
            if (animateRigButton != null)
            {
                animateRigButton.SetEnabled(animateModelTask != null || !string.IsNullOrEmpty(animateModelGlbPath));
            }
        }

        private void UpdateAnimateRigLabel()
        {
            if (animateRigLabel == null)
            {
                return;
            }
            animateRigLabel.text = string.IsNullOrEmpty(animateRigTaskId)
                ? "未绑定"
                : "已绑定：" + ShortId(animateRigTaskId) +
                  (imageCache.Entries.Any(e => e.TaskId == animateRigTaskId && e.TaskType == "rigging")
                      ? "（历史）"
                      : string.Empty);
        }

        private async Task<bool> EnsureRiggedAsync(IMeshyApi api, bool useMock)
        {
            if (!string.IsNullOrEmpty(animateRigTaskId))
            {
                return true;
            }

            var rigUrl = animateModelTask != null &&
                         animateModelTask.ModelUrls != null &&
                         animateModelTask.ModelUrls.ContainsKey("glb")
                ? animateModelTask.ModelUrls["glb"]
                : null;
            if (!useMock && string.IsNullOrEmpty(rigUrl))
            {
                SetAnimateStatus("真实模式需要选择历史任务模型（本地 GLB 需先上传）", true);
                return false;
            }

            var rigRequest = new RiggingRequest
            {
                InputTaskId = animateModelTask != null ? animateModelTask.TaskId : null,
                ModelUrl = string.IsNullOrEmpty(rigUrl) ? null : rigUrl
            };
            var rigCreated = await api.CreateRiggingAsync(rigRequest);
            if (!useMock)
            {
                _ = WatchSseProgressAsync(
                    SseConfig(),
                    rigCreated.Result,
                    "rigging",
                    p => PostAnimateSseProgress(p));
            }
            var rigPoller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 120);
            var rigTask = await rigPoller.WaitForTaskAsync<RigTask>(
                rigCreated.Result,
                "rigging",
                t => PostAnimateProgress(t),
                System.Threading.CancellationToken.None);
            animateRigTaskId = rigTask.Id;
            imageCache.AddOrUpdate(new MeshyCachedTask
            {
                TaskId = rigTask.Id,
                TaskType = "rigging",
                Status = "SUCCEEDED",
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ConsumedCredits = rigTask.ConsumedCredits,
                Prompt = "绑定:" + (animateModelTask != null ? animateModelTask.TaskId : animateModelGlbPath ?? string.Empty),
                AiModel = "rigging"
            });
            return true;
        }

        private async Task RunRiggingAsync()
        {
            if (animateRigging)
            {
                return;
            }
            if (animateModelTask == null && string.IsNullOrEmpty(animateModelGlbPath))
            {
                SetAnimateStatus("请先选择模型", true);
                return;
            }

            var useMock = MeshySettings.UseMockMode;
            animateRigging = true;
            animateRigButton.SetEnabled(false);
            SetAnimateStatus(useMock ? "模拟模式：正在绑定骨骼..." : "正在绑定骨骼...", false);

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
                var ok = await EnsureRiggedAsync(api, useMock);
                if (ok)
                {
                    MeshyUiDispatcher.Post(() =>
                    {
                        UpdateAnimateRigLabel();
                        SetAnimateStatus("绑定完成：" + ShortId(animateRigTaskId), false);
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                MeshyUiDispatcher.Post(() => SetAnimateStatus("绑定失败：" + e.Message, true));
            }
            finally
            {
                MeshyUiDispatcher.Post(() =>
                {
                    animateRigging = false;
                    animateRigButton.SetEnabled(
                        animateModelTask != null || !string.IsNullOrEmpty(animateModelGlbPath));
                });
            }
        }

        private async Task GenerateAnimationAsync()
        {
            if (animateGenerating)
            {
                return;
            }
            if (selectedAction == null)
            {
                SetAnimateStatus("请先在动作库中选择一个动作", true);
                return;
            }
            if (animateModelTask == null && string.IsNullOrEmpty(animateModelGlbPath))
            {
                SetAnimateStatus("请先选择模型", true);
                return;
            }

            var useMock = MeshySettings.UseMockMode;
            animateGenerating = true;
            animateGenerateButton.SetEnabled(false);
            animateProgressBar.value = 0;
            SetAnimateStatus(useMock ? "模拟模式：绑定并生成动画..." : "正在绑定并生成动画...", false);

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

                if (string.IsNullOrEmpty(animateRigTaskId) && animateModelTask != null)
                {
                    animateRigTaskId = FindRigTaskForModel(animateModelTask.TaskId);
                }
                if (string.IsNullOrEmpty(animateRigTaskId))
                {
                    var continueAnyway = EditorUtility.DisplayDialog(
                        "未绑定骨骼",
                        "该模型没有绑定记录，可能无法正确生成动画。是否继续？",
                        "继续动画",
                        "取消");
                    if (!continueAnyway)
                    {
                        SetAnimateStatus("已取消：请先绑定骨骼", false);
                        return;
                    }
                }

                var rigOk = await EnsureRiggedAsync(api, useMock);
                if (!rigOk)
                {
                    return;
                }

                var postProcess = new List<string>();
                if (animateFpsToggle != null && animateFpsToggle.value)
                {
                    postProcess.Add("change_fps");
                }
                if (animateFbxToggle != null && animateFbxToggle.value)
                {
                    postProcess.Add("fbx2usdz");
                }
                if (animateArmatureToggle != null && animateArmatureToggle.value)
                {
                    postProcess.Add("extract_armature");
                }

                var animationRequest = new AnimationRequest
                {
                    RigTaskId = animateRigTaskId,
                    ActionId = selectedAction.Id,
                    PostProcess = postProcess.Count == 0 ? null : postProcess
                };
                var animCreated = await api.CreateAnimationAsync(animationRequest);
                if (!useMock)
                {
                    _ = WatchSseProgressAsync(
                        SseConfig(),
                        animCreated.Result,
                        "animations",
                        p => PostAnimateSseProgress(p));
                }
                var animPoller = new MeshyTaskPoller(api, TimeSpan.FromSeconds(2), 120);
                var animTask = await animPoller.WaitForTaskAsync<AnimationTask>(
                    animCreated.Result,
                    "animations",
                    t => PostAnimateProgress(t),
                    System.Threading.CancellationToken.None);

                var folder = MeshyPaths.TaskFolder("animation", animTask.Id);
                Directory.CreateDirectory(folder);
                string glbPath = null;
                if (!useMock && !string.IsNullOrEmpty(animTask.EffectiveGlbUrl))
                {
                    glbPath = Path.Combine(folder, "animated.glb");
                    var downloaded = await DownloadToFileAsync(animTask.EffectiveGlbUrl, glbPath);
                    if (!downloaded)
                    {
                        glbPath = null;
                    }
                    else
                    {
                        AssetDatabase.ImportAsset(MeshyPaths.Relative("animation", animTask.Id, "animated.glb"));
                    }
                }
                else if (useMock && !string.IsNullOrEmpty(animateModelGlbPath) && File.Exists(animateModelGlbPath))
                {
                    glbPath = animateModelGlbPath;
                }

                MeshyUiDispatcher.Post(() =>
                {
                    imageCache.AddOrUpdate(new MeshyCachedTask
                    {
                        TaskId = animTask.Id,
                        TaskType = "animation",
                        Status = "SUCCEEDED",
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ConsumedCredits = animTask.ConsumedCredits,
                        Prompt = selectedAction.Name + "（ID " + selectedAction.Id + "）",
                        AiModel = "animation"
                    });
                    RefreshAnimateHistory();
                });

                await LoadAnimatePreviewAsync(glbPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                MeshyUiDispatcher.Post(() => SetAnimateStatus("动画任务失败：" + e.Message, true));
            }
            finally
            {
                MeshyUiDispatcher.Post(() =>
                {
                    animateGenerating = false;
                    animateGenerateButton.SetEnabled(true);
                });
            }
        }

        private void PostAnimateProgress(MeshyTaskBase task)
        {
            MeshyUiDispatcher.Post(() =>
            {
                animateProgressBar.value = task.Progress;
                SetAnimateStatus("任务状态: " + task.StatusRaw + " " + task.Progress + "%", false);
            });
        }

        private void PostAnimateSseProgress(int progress)
        {
            MeshyUiDispatcher.Post(() =>
            {
                if (animateProgressBar != null)
                {
                    animateProgressBar.value = progress;
                }
            });
        }

        private async Task LoadAnimatePreviewAsync(string glbPath)
        {
            if (animatePreviewHost == null)
            {
                animatePreviewHost = new MeshyModelPreviewHost();
            }
            if (!animatePreviewWired)
            {
                animatePreviewWired = true;
                animatePreviewHost.TextureChanged += () =>
                {
                    if (animatePreviewImage != null)
                    {
                        animatePreviewImage.image = animatePreviewHost.Texture;
                        animatePreviewImage.MarkDirtyRepaint();
                    }
                };
                animatePreviewHost.Rendered += () =>
                {
                    if (animatePreviewImage != null)
                    {
                        animatePreviewImage.MarkDirtyRepaint();
                    }
                };
            }

            var loaded = string.IsNullOrEmpty(glbPath)
                ? await animatePreviewHost.LoadPlaceholderAsync()
                : await animatePreviewHost.LoadAsync(glbPath);

            MeshyUiDispatcher.Post(() =>
            {
                if (loaded && animatePreviewHost != null)
                {
                    animatePreviewHost.PreparePlayback();
                    animatePreviewHost.Render();
                    animatePreviewImage.image = animatePreviewHost.Texture;
                    animatePreviewImage.MarkDirtyRepaint();
                    animatePlayButton.SetEnabled(animatePreviewHost.ClipCount > 0);
                    animatePauseButton.SetEnabled(true);
                    animateResetButton.SetEnabled(true);
                    SetAnimateStatus("动画就绪：" + animatePreviewHost.ClipName(0), false);
                }
                else
                {
                    SetAnimateStatus("动画预览加载失败", true);
                }
            });
        }

        private void RefreshAnimateHistory()
        {
            if (animateHistoryList == null)
            {
                return;
            }

            animateHistoryList.Clear();
            var entries = new List<MeshyCachedTask>(imageCache.Entries);
            entries.Reverse();
            foreach (var entry in entries)
            {
                if (entry.TaskType != "animation" && entry.TaskType != "rigging")
                {
                    continue;
                }

                var isRig = entry.TaskType == "rigging";
                var tag = isRig ? "[绑定记录] " : "[动画记录] ";
                var card = new Button
                {
                    text = tag +
                           (isRig
                               ? ShortId(entry.TaskId)
                               : (string.IsNullOrEmpty(entry.Prompt) ? entry.TaskId : entry.Prompt)) +
                           " · " + entry.Status + " · " + entry.CreatedAt
                };
                card.AddToClassList("action-card");
                card.userData = entry.TaskId;
                card.clicked += () =>
                {
                    foreach (var other in animateHistoryList.Children())
                    {
                        other.RemoveFromClassList("active");
                    }
                    card.AddToClassList("active");
                    if (isRig)
                    {
                        SetAnimateStatus("绑定记录（无本地产物）：" + ShortId(entry.TaskId), false);
                    }
                    else
                    {
                        LoadAnimateFromHistory(entry);
                    }
                };
                card.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction(
                        "跳转到相关文件夹",
                        _ => OpenAnimateHistoryFolder(entry),
                        DropdownMenuAction.AlwaysEnabled);
                    if (!isRig)
                    {
                        evt.menu.AppendAction(
                            "在工程中定位动画",
                            _ => LocateAnimateInProject(entry),
                            DropdownMenuAction.AlwaysEnabled);
                    }
                    evt.menu.AppendAction(
                        "删除记录",
                        _ => DeleteAnimateHistory(entry),
                        DropdownMenuAction.AlwaysEnabled);
                }));
                animateHistoryList.Add(card);
            }
        }

        private void LoadAnimateFromHistory(MeshyCachedTask entry)
        {
            var glb = Path.Combine(MeshyPaths.FindTaskFolder("animation", entry.TaskId), "animated.glb");
            if (!File.Exists(glb))
            {
                SetAnimateStatus("本地无该动画文件", true);
                return;
            }
            _ = LoadAnimatePreviewAsync(glb);
            SetAnimateStatus("已载入动画历史", false);
        }

        private void OpenAnimateHistoryFolder(MeshyCachedTask entry)
        {
            var folder = MeshyPaths.FindTaskFolder("animation", entry.TaskId);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
                SetAnimateStatus("已打开文件夹: " + folder, false);
            }
            else
            {
                SetAnimateStatus("本地无该动画的生成文件夹", true);
            }
        }

        private void LocateAnimateInProject(MeshyCachedTask entry)
        {
            var glb = Path.Combine(MeshyPaths.FindTaskFolder("animation", entry.TaskId), "animated.glb");
            if (!File.Exists(glb))
            {
                SetAnimateStatus("本地无该动画文件", true);
                return;
            }
            var relative = "Assets" + glb.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
            var main = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relative);
            Selection.activeObject = main;
            EditorGUIUtility.PingObject(main);
            SetAnimateStatus("已在工程中定位: " + relative, false);
        }

        private void DeleteAnimateHistory(MeshyCachedTask entry)
        {
            var confirmed = EditorUtility.DisplayDialog(
                "删除记录",
                entry.TaskType == "animation"
                    ? "确定删除该动画记录及其本地文件？"
                    : "确定删除该绑定记录？",
                "删除",
                "取消");
            if (!confirmed)
            {
                return;
            }

            if (entry.TaskType == "animation")
            {
                var folder = MeshyPaths.FindTaskFolder("animation", entry.TaskId);
                if (Directory.Exists(folder))
                {
                    var relative = "Assets" + folder.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
                    AssetDatabase.DeleteAsset(relative);
                }
            }
            imageCache.Remove(entry.TaskId);
            RefreshAnimateHistory();
            SetAnimateStatus("已删除记录：" + ShortId(entry.TaskId), false);
        }

        private void SetAnimateStatus(string text, bool isError)
        {
            if (animateStatusLabel == null)
            {
                return;
            }
            animateStatusLabel.text = text;
            if (isError)
            {
                animateStatusLabel.AddToClassList("error-text");
            }
            else
            {
                animateStatusLabel.RemoveFromClassList("error-text");
            }
        }

        [MenuItem("Meshy Workspace/Smoke Test Rig (Mock)")]
        public static void SmokeTestRigMock()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("AnimateView", "SidebarAnimate");
            MeshySettings.UseMockMode = true;

            MeshyCachedTask model = null;
            for (var i = window.imageCache.Entries.Count - 1; i >= 0; i--)
            {
                var candidate = window.imageCache.Entries[i];
                if (candidate.TaskType == "text-to-3d" || candidate.TaskType == "image-to-3d")
                {
                    model = candidate;
                    break;
                }
            }
            if (model == null)
            {
                Debug.LogError("[Meshy P5] 没有可用模型历史，请先完成模型生成。");
                return;
            }
            window.SelectAnimateModel(model);
            _ = window.RunRiggingAsync();

            var started = EditorApplication.timeSinceStartup;
            void Poll()
            {
                if (!window.animateRigging &&
                    window.animateStatusLabel != null &&
                    window.animateStatusLabel.text.Contains("绑定完成"))
                {
                    EditorApplication.update -= Poll;
                    var lines = string.Join(
                        Environment.NewLine,
                        "mockRig=" + (string.IsNullOrEmpty(window.animateRigTaskId) ? "FAILED" : "OK"),
                        "rigTask=" + (window.animateRigTaskId ?? string.Empty),
                        "rigLabel=" + (window.animateRigLabel == null ? "missing" : window.animateRigLabel.text));
                    try
                    {
                        var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p5-rig-report.txt");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(
                            path,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Meshy P5] 写入绑定报告失败: " + e.Message);
                    }
                    return;
                }

                if (EditorApplication.timeSinceStartup - started > 120.0)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[Meshy P5] 绑定模拟超时");
                }
            }
            EditorApplication.update += Poll;
        }

        [MenuItem("Meshy Workspace/Smoke Test Animate UI (Real)")]
        public static void SmokeTestAnimateUiReal()
        {
            if (!MeshySettings.HasApiKey)
            {
                Debug.LogError("[Meshy P5] 未配置 API Key。");
                return;
            }

            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("AnimateView", "SidebarAnimate");
            MeshySettings.UseMockMode = false;

            const string targetTask = "019ff03b-9867-7f5e-b095-465d3710d3fd";
            var model = window.imageCache.Entries.FirstOrDefault(e =>
                e.TaskId == targetTask &&
                e.ModelUrls != null &&
                e.ModelUrls.ContainsKey("glb"));
            if (model == null)
            {
                Debug.LogError("[Meshy P5] 历史中找不到模型任务 " + targetTask);
                return;
            }

            var walk = window.animationActions.FirstOrDefault(a => a.Id == 1);
            if (walk == null)
            {
                Debug.LogError("[Meshy P5] 动作库中找不到 Walking_Woman（ID 1）。");
                return;
            }

            window.SelectAnimateModel(model);
            _ = window.RunRiggingAsync();

            var started = EditorApplication.timeSinceStartup;
            var stage = 0;
            void Poll()
            {
                if (stage == 0 &&
                    !window.animateRigging &&
                    !string.IsNullOrEmpty(window.animateRigTaskId))
                {
                    stage = 1;
                    window.SelectAnimateAction(walk);
                    _ = window.GenerateAnimationAsync();
                    return;
                }

                if (stage == 1 &&
                    !window.animateGenerating)
                {
                    var status = window.animateStatusLabel == null ? string.Empty : window.animateStatusLabel.text;
                    if (status.Contains("动画就绪") || status.Contains("失败") || status.Contains("超时"))
                    {
                        EditorApplication.update -= Poll;
                        FinishAnimateReal(window, status);
                        return;
                    }
                }

                if (EditorApplication.timeSinceStartup - started > 900.0)
                {
                    EditorApplication.update -= Poll;
                    FinishAnimateReal(window, "timeout");
                }
            }
            EditorApplication.update += Poll;
        }

        private static async void FinishAnimateReal(MeshyWorkspaceWindow window, string status)
        {
            var animEntry = window.imageCache.Entries
                .Where(e => e.TaskType == "animation")
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            var animTaskId = animEntry == null ? string.Empty : animEntry.TaskId;
            var folder = string.IsNullOrEmpty(animTaskId)
                ? string.Empty
                : MeshyPaths.TaskFolder("animation", animTaskId);
            var files = !string.IsNullOrEmpty(folder) && Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.glb").Length
                : 0;

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
                Debug.LogWarning("[Meshy P5] 获取冒烟后余额失败: " + e.Message);
            }

            var lines = string.Join(
                Environment.NewLine,
                "realAnimate=" + (status.Contains("动画就绪") ? "OK" : "FAILED"),
                "status=" + status,
                "action=Walking_Woman(1)",
                "rigTask=" + (window.animateRigTaskId ?? string.Empty),
                "animTask=" + animTaskId,
                "credits=" + (animEntry == null ? 0 : animEntry.ConsumedCredits),
                "glbFiles=" + files,
                "clipName=" + (window.animatePreviewHost == null ? "none" : window.animatePreviewHost.ClipName(0)),
                "balanceAfter=" + balance);

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p5-real-report.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P5] 写入真实动画报告失败: " + e.Message);
            }
            Debug.Log("[Meshy P5] 真实动画流程: " + (status.Contains("动画就绪") ? "OK" : "FAILED") + " " + status);
        }

        [MenuItem("Meshy Workspace/Smoke Test Animate UI (Mock)")]
        public static void SmokeTestAnimateUiMock()
        {
            var existing = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            if (existing != null)
            {
                existing.Close();
            }
            var window = GetWindow<MeshyWorkspaceWindow>(false, "Meshy Workspace");
            window.Show();
            window.ShowView("AnimateView", "SidebarAnimate");
            MeshySettings.UseMockMode = true;

            MeshyCachedTask model = null;
            for (var i = window.imageCache.Entries.Count - 1; i >= 0; i--)
            {
                var candidate = window.imageCache.Entries[i];
                if (candidate.TaskType == "text-to-3d" || candidate.TaskType == "image-to-3d")
                {
                    model = candidate;
                    break;
                }
            }
            if (model == null)
            {
                Debug.LogError("[Meshy P5] 没有可用模型历史，请先完成模型生成。");
                return;
            }
            window.SelectAnimateModel(model);
            _ = window.RunRiggingAsync();

            var started = EditorApplication.timeSinceStartup;
            var stage = 0;
            void Poll()
            {
                if (stage == 0 &&
                    !window.animateRigging &&
                    !string.IsNullOrEmpty(window.animateRigTaskId))
                {
                    stage = 1;
                    if (window.animationActions.Count > 0)
                    {
                        window.SelectAnimateAction(window.animationActions[0]);
                    }
                    _ = window.GenerateAnimationAsync();
                    return;
                }

                if (stage == 1 &&
                    !window.animateGenerating &&
                    window.animatePreviewHost != null &&
                    window.animatePreviewHost.Texture != null &&
                    window.animateStatusLabel != null &&
                    window.animateStatusLabel.text.Contains("动画就绪"))
                {
                    EditorApplication.update -= Poll;
                    window.animatePreviewHost.Play(0);
                    FinishAnimateMock(window, true, "rigAndAnimateDone", window.animatePreviewHost.ClipCount);
                    return;
                }

                if (!window.animateGenerating &&
                    stage > 0 &&
                    window.animateStatusLabel != null &&
                    window.animateStatusLabel.text.Contains("失败"))
                {
                    EditorApplication.update -= Poll;
                    FinishAnimateMock(window, false, "animateFailed", 0);
                    return;
                }

                if (EditorApplication.timeSinceStartup - started > 120.0)
                {
                    EditorApplication.update -= Poll;
                    FinishAnimateMock(window, false, "timeout", 0);
                }
            }
            EditorApplication.update += Poll;
        }

        private static void FinishAnimateMock(
            MeshyWorkspaceWindow window,
            bool ok,
            string note,
            int clipCount)
        {
            var lines = string.Join(
                Environment.NewLine,
                "mockAnimate=" + (ok ? "OK" : "FAILED"),
                "note=" + note,
                "clipCount=" + clipCount,
                "playing=" + (window.animatePreviewHost != null && window.animatePreviewHost.IsPlaying),
                "history=" + (window.imageCache == null ? 0 : window.imageCache.Entries.Count));

            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", "MeshyWorkspace", "p5-mock-report.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + lines + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Meshy P5] 写入模拟动画报告失败: " + e.Message);
            }
            Debug.Log("[Meshy P5] 模拟动画流程: " + (ok ? "OK" : "FAILED") + " " + note);
        }

    }
}
