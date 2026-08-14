using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace MeshyWorkspace.Editor
{
    public static class MeshyGameSceneBuilder
    {
        private const string ScenePath = "Assets/MeshyGame/Scenes/MeshyGame.unity";
        private const string AssetFolder = "Assets/MeshyGame/UI";
        private const string PanelSettingsPath = AssetFolder + "/MeshyGamePanelSettings.asset";
        private const string TextSettingsPath = AssetFolder + "/MeshyGameTextSettings.asset";
        private const string PreviewTexturePath = AssetFolder + "/MeshyPreview.renderTexture";
        private const string UxmlPath = "Packages/com.besty.meshy-workspace/Runtime/UI/MeshyWorkspaceWindow.uxml";
        private const string UssPath = "Packages/com.besty.meshy-workspace/Runtime/UI/MeshyWorkspaceWindow.uss";
        private const string FontPath = "Packages/com.besty.meshy-workspace/Runtime/Fonts/UnitySkillsCN-Regular.ttf";
        private const string FontAssetPath = "Packages/com.besty.meshy-workspace/Runtime/Fonts/MeshyCJK-UI.asset";

        [MenuItem("Meshy Workspace/Create Game Mode Scene")]
        public static void CreateGameModeScene()
        {
            EnsureFolders();
            var fontAsset = EnsureRuntimeFontAsset();
            var textSettings = EnsureTextSettings(fontAsset);
            var panelSettings = EnsurePanelSettings(textSettings);
            var previewTexture = EnsurePreviewTexture();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MeshyGame";

            var appObject = new GameObject("Meshy Workspace App");
            var document = appObject.AddComponent<UIDocument>();
            var app = appObject.AddComponent<MeshyWorkspaceApp>();
            AssignDocumentReferences(document, panelSettings);

            var previewRoot = new GameObject("Runtime Preview Root");
            previewRoot.transform.SetParent(appObject.transform, false);

            var previewCamera = new GameObject("Runtime Preview Camera");
            previewCamera.transform.SetParent(appObject.transform, false);
            previewCamera.transform.position = new Vector3(0f, 0.6f, -3f);
            var camera = previewCamera.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
            camera.enabled = false;
            camera.targetTexture = previewTexture;

            var light = new GameObject("Preview Light");
            light.transform.SetParent(appObject.transform, false);
            light.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            var directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.2f;

            AssignAppReferences(app, previewRoot.transform, camera, previewTexture, fontAsset);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[Meshy] Game 模式场景已创建：" + ScenePath);
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/MeshyGame/Scenes");
            Directory.CreateDirectory(AssetFolder);
            Directory.CreateDirectory("Packages/com.besty.meshy-workspace/Runtime/Fonts");
        }

        private static PanelSettings EnsurePanelSettings(PanelTextSettings textSettings)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.textSettings = textSettings;
            EditorUtility.SetDirty(panelSettings);
            return panelSettings;
        }

        private static FontAsset EnsureRuntimeFontAsset()
        {
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sourceFont == null)
            {
                throw new FileNotFoundException("Meshy CJK font is missing.", FontPath);
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (!IsUsableFontAsset(fontAsset))
            {
                if (fontAsset != null)
                {
                    AssetDatabase.DeleteAsset(FontAssetPath);
                }

                fontAsset = CreateFontAsset(sourceFont);
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            PersistFontRenderResources(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static FontAsset CreateFontAsset(Font sourceFont)
        {
            var fontAsset = FontAsset.CreateFontAsset(
                sourceFont, 32, 3, GlyphRenderMode.SDFAA, 4096, 4096,
                AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "Meshy CJK UI";

            var characters = CollectBootstrapCharacters();
            if (!fontAsset.TryAddCharacters(characters, out var missingCharacters, false) &&
                !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning("[Meshy] CJK UI 字体缺少部分预烘字符：" + missingCharacters);
            }

            return fontAsset;
        }

        private static bool IsUsableFontAsset(FontAsset fontAsset)
        {
            return fontAsset != null &&
                   fontAsset.material != null &&
                   fontAsset.atlasTextures != null &&
                   fontAsset.atlasTextures.Any(texture => texture != null);
        }

        private static void PersistFontRenderResources(FontAsset fontAsset)
        {
            PersistFontSubAsset(fontAsset.material, "Meshy CJK UI Material");
            if (fontAsset.atlasTextures != null)
            {
                foreach (var texture in fontAsset.atlasTextures.Where(texture => texture != null))
                {
                    PersistFontSubAsset(texture, "Meshy CJK UI Atlas");
                }
            }

            var serialized = new SerializedObject(fontAsset);
            var clearDynamicDataOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicDataOnBuild != null)
            {
                clearDynamicDataOnBuild.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static string CollectBootstrapCharacters()
        {
            var characters = new HashSet<char>();
            for (var value = 32; value <= 126; value++)
            {
                characters.Add((char)value);
            }

            AddCharacters(characters, "中文字体测试图片模型动画资产设置历史生成预览导入下载刷新余额取消确认保存提示错误完成失败进行中请选择请输入智能体打印模式材质网格重拓扑绑定骨骼动作分类搜索");

            var paths = Directory.GetFiles("Packages/com.besty.meshy-workspace", "*.*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".cs") ||
                    path.EndsWith(".uxml") ||
                    path.EndsWith(".uss") ||
                    path.EndsWith(".json"));

            foreach (var path in paths)
            {
                AddCharacters(characters, File.ReadAllText(path));
            }

            return new string(characters.Where(value => !char.IsControl(value) && !char.IsSurrogate(value)).OrderBy(value => value).ToArray());
        }

        private static void AddCharacters(HashSet<char> characters, string text)
        {
            foreach (var value in text)
            {
                if (!char.IsControl(value) && !char.IsSurrogate(value))
                {
                    characters.Add(value);
                }
            }
        }

        private static void PersistFontSubAsset(Object asset, string assetName)
        {
            if (asset == null)
            {
                return;
            }

            asset.name = assetName;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            {
                AssetDatabase.AddObjectToAsset(asset, FontAssetPath);
            }
        }

        private static PanelTextSettings EnsureTextSettings(FontAsset fontAsset)
        {
            var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
                textSettings.name = "MeshyGameTextSettings";
                AssetDatabase.CreateAsset(textSettings, TextSettingsPath);
            }

            ApplyTextSettingsFont(textSettings, fontAsset);
            EditorUtility.SetDirty(textSettings);
            return textSettings;
        }

        private static void ApplyTextSettingsFont(PanelTextSettings textSettings, FontAsset fontAsset)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = typeof(PanelTextSettings).GetProperty("defaultFontAsset", flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(textSettings, fontAsset);
            }

            var serialized = new SerializedObject(textSettings);
            SetObjectReference(serialized, "m_DefaultFontAsset", fontAsset);
            SetObjectReference(serialized, "defaultFontAsset", fontAsset);
            SetFirstArrayReference(serialized, "m_FallbackFontAssets", fontAsset);
            SetFirstArrayReference(serialized, "m_FallbackFontAssetTable", fontAsset);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetFirstArrayReference(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }

        private static RenderTexture EnsurePreviewTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(PreviewTexturePath);
            if (texture != null)
            {
                return texture;
            }

            texture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32)
            {
                name = "MeshyPreview"
            };
            AssetDatabase.CreateAsset(texture, PreviewTexturePath);
            return texture;
        }

        private static void AssignAppReferences(
            MeshyWorkspaceApp app,
            Transform previewRoot,
            Camera previewCamera,
            RenderTexture previewTexture,
            FontAsset fontAsset)
        {
            var serialized = new SerializedObject(app);
            serialized.FindProperty("workspaceTree").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            serialized.FindProperty("workspaceStyle").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            serialized.FindProperty("previewRoot").objectReferenceValue = previewRoot;
            serialized.FindProperty("previewCamera").objectReferenceValue = previewCamera;
            serialized.FindProperty("previewTexture").objectReferenceValue = previewTexture;
            serialized.FindProperty("runtimeFontAsset").objectReferenceValue = fontAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDocumentReferences(UIDocument document, PanelSettings panelSettings)
        {
            var serialized = new SerializedObject(document);
            serialized.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
            serialized.FindProperty("sourceAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            serialized.FindProperty("m_SortingOrder").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            else
            {
                for (var i = 0; i < scenes.Count; i++)
                {
                    if (scenes[i].path == scenePath)
                    {
                        scenes[i].enabled = true;
                    }
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
