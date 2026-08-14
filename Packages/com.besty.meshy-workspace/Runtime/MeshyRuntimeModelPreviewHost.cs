using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using GLTFast;
using UnityEngine;

namespace MeshyWorkspace
{
    public sealed class MeshyRuntimeModelPreviewHost
    {
        private readonly Transform root;
        private readonly Camera camera;
        private readonly RenderTexture renderTexture;
        private GameObject gridRoot;
        private GameObject modelRoot;
        private Animator animator;
        private Animation legacyAnimation;
        private AnimationClip[] playbackClips;
        private bool useLegacyPlayback;
        private int playbackClipIndex;
        private bool playing;
        private float yaw = 30f;
        private float pitch = 18f;
        private float distance = 2.8f;
        private Vector3 pivot;

        public MeshyRuntimeModelPreviewHost(Transform root, Camera camera, RenderTexture renderTexture)
        {
            this.root = root;
            this.camera = camera;
            this.renderTexture = renderTexture;
            if (this.camera != null)
            {
                this.camera.targetTexture = renderTexture;
                this.camera.clearFlags = CameraClearFlags.SolidColor;
                this.camera.backgroundColor = new Color(0.32f, 0.34f, 0.36f, 1f);
            }
            EnsureGrid();
        }

        private void EnsureGrid()
        {
            if (gridRoot != null || root == null)
            {
                return;
            }

            gridRoot = new GameObject("MeshyRuntimePreviewGrid");
            gridRoot.transform.SetParent(root, false);
            var material = new Material(Shader.Find("Sprites/Default"));
            var gridColor = new Color(0.55f, 0.57f, 0.60f, 1f);
            var axisX = new Color(0.80f, 0.28f, 0.28f, 1f);
            var axisY = new Color(0.30f, 0.70f, 0.35f, 1f);
            var axisZ = new Color(0.30f, 0.50f, 0.90f, 1f);
            const float half = 5f;

            for (var i = -5; i <= 5; i++)
            {
                AddGridLine(new[] { new Vector3(-half, 0f, i), new Vector3(half, 0f, i) }, gridColor, material);
                AddGridLine(new[] { new Vector3(i, 0f, -half), new Vector3(i, 0f, half) }, gridColor, material);
            }
            AddGridLine(new[] { new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f) }, axisX, material);
            AddGridLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, half, 0f) }, axisY, material);
            AddGridLine(new[] { new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half) }, axisZ, material);
        }

        private void AddGridLine(Vector3[] points, Color color, Material material)
        {
            var go = new GameObject("GridLine");
            go.transform.SetParent(gridRoot.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = material;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = 0.02f;
            line.endWidth = 0.02f;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.useWorldSpace = false;
        }

        public Texture Texture
        {
            get { return renderTexture; }
        }

        public int CurrentClipIndex
        {
            get { return playbackClipIndex; }
        }

        public string ClipName(int index)
        {
            if (playbackClips == null || index < 0 || index >= playbackClips.Length)
            {
                return string.Empty;
            }
            return playbackClips[index].name;
        }

        public async Task<bool> LoadAsync(string glbPath)
        {
            ClearModel();
            if (string.IsNullOrEmpty(glbPath) || !System.IO.File.Exists(glbPath))
            {
                LoadPlaceholder();
                return false;
            }

            modelRoot = new GameObject("MeshyRuntimePreviewModel");
            modelRoot.transform.SetParent(root, false);

            var import = new GltfImport();
            var loaded = await import.LoadFile(glbPath);
            if (!loaded || !await import.InstantiateMainSceneAsync(modelRoot.transform))
            {
                LoadPlaceholder();
                return false;
            }

            ApplyLocalTextures(glbPath);
            Frame();
            PreparePlayback();
            Play(0);
            return true;
        }

        public void LoadPlaceholder()
        {
            ClearModel();
            modelRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelRoot.name = "MeshyRuntimePreviewPlaceholder";
            modelRoot.transform.SetParent(root, false);
            var renderer = modelRoot.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.25f, 0.8f, 0.68f, 1f);
            }
            Frame();
            CreateDemoClip();
        }

        public void Tick(float deltaTime)
        {
            if (!playing || modelRoot == null)
            {
                Render();
                return;
            }

            if (legacyAnimation != null)
            {
                // Legacy Animation advances itself in Play mode.
            }
            else if (animator != null)
            {
                animator.Update(deltaTime);
            }
            else
            {
                modelRoot.transform.Rotate(Vector3.up, deltaTime * 60f, Space.World);
            }
            Render();
        }

        public void Play(int clipIndex)
        {
            playbackClipIndex = playbackClips == null ? 0 : Mathf.Clamp(clipIndex, 0, playbackClips.Length - 1);
            playing = true;
            if (useLegacyPlayback && legacyAnimation != null && playbackClips != null && playbackClips.Length > 0)
            {
                legacyAnimation.clip = playbackClips[playbackClipIndex];
                legacyAnimation.Play();
            }
            else if (animator != null && playbackClips != null && playbackClips.Length > 0)
            {
                animator.Play(playbackClips[playbackClipIndex].name, 0, 0f);
            }
        }

        public void Pause()
        {
            playing = false;
            if (legacyAnimation != null)
            {
                legacyAnimation.Stop();
            }
        }

        public void ResetPlayback()
        {
            playing = false;
            if (useLegacyPlayback && legacyAnimation != null && playbackClips != null && playbackClips.Length > 0)
            {
                legacyAnimation.Stop();
                playbackClips[playbackClipIndex].SampleAnimation(modelRoot, 0f);
            }
            else if (animator != null && playbackClips != null && playbackClips.Length > 0)
            {
                animator.Play(playbackClips[playbackClipIndex].name, 0, 0f);
                animator.Update(0f);
            }
            Render();
        }

        public void Drag(float x, float y)
        {
            yaw += x * 0.35f;
            pitch = Mathf.Clamp(pitch + y * 0.25f, -60f, 70f);
            Render();
        }

        public void Pan(float x, float y)
        {
            pivot += new Vector3(-x, y, 0f) * 0.003f;
            Render();
        }

        public void Zoom(float delta)
        {
            distance = Mathf.Clamp(distance + delta, 0.7f, 8f);
            Render();
        }

        private void PreparePlayback()
        {
            animator = modelRoot == null ? null : modelRoot.GetComponentInChildren<Animator>(true);
            legacyAnimation = modelRoot == null ? null : modelRoot.GetComponentInChildren<Animation>(true);
            playbackClips = null;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                playbackClips = animator.runtimeAnimatorController.animationClips;
            }
            if ((playbackClips == null || playbackClips.Length == 0) && legacyAnimation != null)
            {
                var clips = new List<AnimationClip>();
                foreach (AnimationState state in legacyAnimation)
                {
                    if (state != null && state.clip != null)
                    {
                        clips.Add(state.clip);
                    }
                }
                if (clips.Count > 0)
                {
                    playbackClips = clips.ToArray();
                }
            }
            if (playbackClips == null || playbackClips.Length == 0)
            {
                CreateDemoClip();
            }
            else
            {
                playbackClipIndex = 0;
                useLegacyPlayback = legacyAnimation != null && (animator == null || animator.runtimeAnimatorController == null);
            }
        }

        private void CreateDemoClip()
        {
            if (modelRoot == null)
            {
                return;
            }

            var clip = new AnimationClip { name = "Preview Motion", wrapMode = WrapMode.Loop, legacy = true };
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Linear(0f, 0f, 1.5f, 360f));
            legacyAnimation = modelRoot.GetComponent<Animation>();
            if (legacyAnimation == null)
            {
                legacyAnimation = modelRoot.AddComponent<Animation>();
            }
            legacyAnimation.AddClip(clip, clip.name);
            playbackClips = new[] { clip };
            playbackClipIndex = 0;
            Play(0);
        }

        private void Frame()
        {
            if (modelRoot != null)
            {
                var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    pivot = root == null ? bounds.center : root.InverseTransformPoint(bounds.center);
                    var radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
                    distance = Mathf.Clamp(radius * 2.6f, 0.7f, 12f);
                    if (gridRoot != null)
                    {
                        gridRoot.transform.localPosition = new Vector3(0f, bounds.min.y, 0f);
                    }
                    Render();
                    return;
                }
            }
            if (gridRoot != null)
            {
                gridRoot.transform.localPosition = Vector3.zero;
            }
            pivot = Vector3.zero;
            distance = 2.8f;
            Render();
        }

        private void Render()
        {
            if (camera == null || root == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var target = root.TransformPoint(pivot);
            camera.transform.position = target + rotation * new Vector3(0f, 0f, -distance);
            camera.transform.rotation = rotation;
            camera.transform.LookAt(target);
            camera.Render();
        }

        private void ClearModel()
        {
            if (modelRoot != null)
            {
                Object.Destroy(modelRoot);
                modelRoot = null;
            }
            animator = null;
            legacyAnimation = null;
            playbackClips = null;
            useLegacyPlayback = false;
            playing = false;
        }

        private void ApplyLocalTextures(string glbPath)
        {
            if (string.IsNullOrEmpty(glbPath) || modelRoot == null)
            {
                return;
            }
            var folder = Path.GetDirectoryName(glbPath);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var baseColor = LoadTexture(Path.Combine(folder, "texture_0.png"));
            var metallicRoughness = LoadTexture(Path.Combine(folder, "texture_0_metallic.png"));
            if (metallicRoughness == null)
            {
                metallicRoughness = LoadTexture(Path.Combine(folder, "texture_1.png"));
            }
            var normal = LoadTexture(Path.Combine(folder, "texture_0_normal.png"));
            if (normal == null)
            {
                normal = LoadTexture(Path.Combine(folder, "texture_2.png"));
            }
            var emissive = LoadTexture(Path.Combine(folder, "texture_0_emission.png"));

            foreach (var renderer in renderers)
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    if (baseColor != null)
                    {
                        material.mainTexture = baseColor;
                        material.SetTexture("_BaseMap", baseColor);
                        material.SetTexture("baseColorTexture", baseColor);
                    }
                    if (metallicRoughness != null)
                    {
                        material.SetTexture("_MetallicGlossMap", metallicRoughness);
                        material.SetTexture("metallicRoughnessTexture", metallicRoughness);
                        material.EnableKeyword("_METALLICGLOSSMAP");
                    }
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.SetTexture("normalTexture", normal);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    if (emissive != null)
                    {
                        material.SetTexture("_EmissionMap", emissive);
                        material.SetTexture("emissiveTexture", emissive);
                        material.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                Object.Destroy(texture);
                return null;
            }
            return texture;
        }
    }
}
