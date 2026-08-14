using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GLTFast;
using UnityEditor;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    public sealed class MeshyModelPreviewHost
    {
        private const int IdleRenderSize = 512;

        private PreviewRenderUtility previewUtility;
        private GameObject modelRoot;
        private Camera camera;
        private RenderTexture renderTexture;
        private GltfImport gltfImport;
        private bool renderPending;
        private Vector3 pivot;
        private float yaw = 30f;
        private float pitch = 20f;
        private float distance = 2.5f;
        private Animator animator;
        private Animation legacyAnimation;
        private AnimationClip[] playbackClips;
        private int playbackClipIndex;
        private bool playing;
        private float playbackTime;
        private double lastTick;
        private bool playbackTickRegistered;
        private double lastPlaybackRenderTime;

        public event Action TextureChanged;
        public event Action Rendered;

        public Texture Texture
        {
            get { return renderTexture; }
        }

        public int ClipCount
        {
            get { return playbackClips == null ? 0 : playbackClips.Length; }
        }

        public int CurrentClipIndex
        {
            get { return playbackClipIndex; }
        }

        public bool IsPlaying
        {
            get { return playing; }
        }

        public int LastBoneMotionCount { get; private set; }

        public string ClipName(int index)
        {
            if (playbackClips == null || index < 0 || index >= playbackClips.Length)
            {
                return string.Empty;
            }
            return playbackClips[index].name;
        }

        public void PreparePlayback()
        {
            animator = modelRoot == null ? null : modelRoot.GetComponentInChildren<Animator>(true);
            legacyAnimation = modelRoot == null ? null : modelRoot.GetComponentInChildren<Animation>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
            if (playbackClips == null || playbackClips.Length == 0)
            {
                var editorClips = AnimationUtility.GetAnimationClips(modelRoot);
                if (editorClips != null && editorClips.Length > 0)
                {
                    playbackClips = editorClips;
                }
            }
            if (playbackClips == null || playbackClips.Length == 0)
            {
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    playbackClips = animator.runtimeAnimatorController.animationClips;
                }
            }
            if (playbackClips == null || playbackClips.Length == 0)
            {
                CreateDemoClip();
            }

            playbackClipIndex = 0;
            playing = false;
            playbackTime = 0f;
            Render();
        }

        public void Play(int clipIndex)
        {
            if (playbackClips == null || playbackClips.Length == 0)
            {
                return;
            }
            playbackClipIndex = Mathf.Clamp(clipIndex, 0, playbackClips.Length - 1);
            playing = true;
            playbackTime = 0f;
            lastTick = EditorApplication.timeSinceStartup;
            lastPlaybackRenderTime = 0;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(playbackClips[playbackClipIndex].name, 0, 0f);
            }
            if (legacyAnimation != null)
            {
                legacyAnimation.clip = playbackClips[playbackClipIndex];
                legacyAnimation.Play();
            }
            EnsurePlaybackTick();
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
            playbackTime = 0f;
            if (animator != null)
            {
                animator.Update(0f);
            }
            if (legacyAnimation != null && playbackClips != null && playbackClips.Length > 0)
            {
                legacyAnimation.Stop();
                playbackClips[playbackClipIndex].SampleAnimation(modelRoot, 0f);
            }
            Render();
        }

        public void ApplyActionPreviewMotion(string actionName, string category)
        {
            var clip = new AnimationClip
            {
                name = string.IsNullOrEmpty(actionName) ? "ActionPreview" : actionName,
                wrapMode = WrapMode.Loop
            };

            var yaw = CategoryYawCurve(category);
            var bob = CategoryBobCurve(category);
            var lunge = CategoryLungeCurve(category);
            if (yaw != null)
            {
                clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", yaw);
            }
            if (bob != null)
            {
                clip.SetCurve("", typeof(Transform), "localPosition.y", bob);
            }
            if (lunge != null)
            {
                clip.SetCurve("", typeof(Transform), "localPosition.z", lunge);
            }
            AddCategoryBoneMotion(clip, category);

            var anim = modelRoot.GetComponent<Animation>();
            if (anim == null)
            {
                anim = modelRoot.AddComponent<Animation>();
            }
            if (anim.GetClip(clip.name) != null)
            {
                anim.RemoveClip(clip.name);
            }
            anim.AddClip(clip, clip.name);
            legacyAnimation = anim;
            playbackClips = new[] { clip };
            playbackClipIndex = 0;
            playing = false;
            playbackTime = 0f;
            Render();
        }

        public async Task<bool> LoadAsync(string glbPath)
        {
            Clear();
            CreateUtility();
            gltfImport = new GltfImport(deferAgent: new EditorDeferAgent());
            var loaded = await gltfImport.LoadFile(glbPath);
            if (!loaded)
            {
                Clear();
                return false;
            }

            var instantiated = await gltfImport.InstantiateMainSceneAsync(modelRoot.transform);
            if (!instantiated)
            {
                Clear();
                return false;
            }

            Frame();
            CreateCamera();
            return true;
        }

        public async Task<bool> LoadPlaceholderAsync()
        {
            Clear();
            CreateUtility();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(modelRoot.transform, false);
            sphere.transform.localScale = Vector3.one * 0.9f;
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.18f, 0.55f, 0.48f, 1f)
                };
            }
            Frame();
            CreateCamera();
            return await Task.FromResult(true);
        }

        public async Task<bool> LoadActionPreviewAsync(string actionName)
        {
            Clear();
            CreateUtility();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(modelRoot.transform, false);
            body.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.16f, 0.52f, 0.45f, 1f)
            };

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(modelRoot.transform, false);
            head.transform.localScale = Vector3.one * 0.45f;
            head.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            head.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.16f, 0.52f, 0.45f, 1f)
            };

            Frame();
            CreateCamera();
            CreateActionPreviewClip(actionName);
            PreparePlayback();
            return await Task.FromResult(true);
        }

        public void Render()
        {
            if (camera == null || renderTexture == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.position = pivot + rotation * (Vector3.back * distance);
            camera.transform.LookAt(pivot);
            camera.Render();
            Rendered?.Invoke();
        }

        public void Drag(float deltaX, float deltaY)
        {
            yaw += deltaX;
            pitch = Mathf.Clamp(pitch + deltaY, -89f, 89f);
            RequestRender();
        }

        public void Zoom(float delta)
        {
            distance = Mathf.Clamp(distance + delta, 0.5f, 20f);
            RequestRender();
        }

        public void Recenter()
        {
            Frame();
            Render();
        }

        public void Pan(float deltaX, float deltaY)
        {
            if (camera == null)
            {
                return;
            }

            var scale = distance * 0.0015f;
            pivot += camera.transform.right * (-deltaX * scale) + camera.transform.up * (-deltaY * scale);
            RequestRender();
        }

        public void RequestRender()
        {
            if (renderPending)
            {
                return;
            }
            renderPending = true;
            EditorApplication.update += RenderWhenReady;
        }

        private void RenderWhenReady()
        {
            EditorApplication.update -= RenderWhenReady;
            renderPending = false;
            Render();
        }

        public void Clear()
        {
            if (playbackTickRegistered)
            {
                EditorApplication.update -= PlaybackTick;
                playbackTickRegistered = false;
            }
            if (gltfImport != null)
            {
                gltfImport.Dispose();
                gltfImport = null;
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
            modelRoot = null;
            camera = null;
            renderTexture = null;
            renderPending = false;
            animator = null;
            legacyAnimation = null;
            playbackClips = null;
            playbackClipIndex = 0;
            playing = false;
            playbackTime = 0f;
            lastPlaybackRenderTime = 0;
        }

        public string GetDebugInfo()
        {
            if (previewUtility == null || modelRoot == null)
            {
                return "preview=null";
            }

            var lines = new List<string>
            {
                "modelRootChildren=" + modelRoot.transform.childCount,
                "cameraScene=" + camera.gameObject.scene.name
            };

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            lines.Add("renderers=" + renderers.Length);
            for (var i = 0; i < Mathf.Min(renderers.Length, 5); i++)
            {
                var renderer = renderers[i];
                var meshRenderer = renderer as MeshRenderer;
                var mesh = meshRenderer != null && meshRenderer.GetComponent<MeshFilter>() != null
                    ? meshRenderer.GetComponent<MeshFilter>().sharedMesh
                    : null;
                lines.Add(
                    "renderer" + i + "=" + renderer.GetType().Name +
                    " bounds=" + renderer.bounds.center + " size=" + renderer.bounds.size +
                    " meshNull=" + (mesh == null) +
                    " scene=" + renderer.gameObject.scene.name);
            }

            lines.Add("camera=" + (camera == null ? "null" : "pos=" + camera.transform.position));
            lines.Add("orbit=pivot=" + pivot + " yaw=" + yaw + " pitch=" + pitch + " distance=" + distance);
            return string.Join(Environment.NewLine, lines);
        }

        private void CreateUtility()
        {
            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.enabled = false;
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewUtility.camera.backgroundColor = new Color(0.055f, 0.067f, 0.075f, 1f);
            previewUtility.camera.fieldOfView = 35f;
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 100f;

            if (previewUtility.lights != null && previewUtility.lights.Length > 0)
            {
                var light = previewUtility.lights[0];
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            modelRoot = new GameObject("ModelRoot");
            previewUtility.AddSingleGO(modelRoot);
        }

        private void Frame()
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            pivot = bounds.center;
            distance = Mathf.Max(bounds.extents.magnitude * 2.2f, 1f);
        }

        private void CreateCamera()
        {
            camera = previewUtility.camera;
            EnsureRenderTexture(IdleRenderSize);
            Render();
        }

        private void EnsureRenderTexture(int size)
        {
            if (renderTexture != null && renderTexture.width == size)
            {
                return;
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            if (camera != null)
            {
                camera.targetTexture = renderTexture;
            }
            TextureChanged?.Invoke();
            Render();
        }

        private void CreateDemoClip()
        {
            var clip = new AnimationClip
            {
                name = "DemoRotate",
                wrapMode = WrapMode.Loop
            };
            clip.SetCurve(
                "",
                typeof(Transform),
                "localEulerAnglesRaw.y",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 360f)));

            var anim = modelRoot.GetComponent<Animation>();
            if (anim == null)
            {
                anim = modelRoot.AddComponent<Animation>();
            }
            anim.AddClip(clip, clip.name);
            legacyAnimation = anim;
            playbackClips = new[] { clip };
        }

        private void CreateActionPreviewClip(string actionName)
        {
            var clip = new AnimationClip
            {
                name = string.IsNullOrEmpty(actionName) ? "ActionPreview" : actionName,
                wrapMode = WrapMode.Loop
            };
            clip.SetCurve(
                "",
                typeof(Transform),
                "localEulerAnglesRaw.y",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(2f, 360f)));
            clip.SetCurve(
                "Body",
                typeof(Transform),
                "localPosition.y",
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.5f, 1.35f),
                    new Keyframe(1f, 1f),
                    new Keyframe(1.5f, 0.75f),
                    new Keyframe(2f, 1f)));
            clip.SetCurve(
                "Body",
                typeof(Transform),
                "localEulerAnglesRaw.z",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, -12f), new Keyframe(2f, 0f)));

            var anim = modelRoot.GetComponent<Animation>();
            if (anim == null)
            {
                anim = modelRoot.AddComponent<Animation>();
            }
            anim.AddClip(clip, clip.name);
            legacyAnimation = anim;
            playbackClips = new[] { clip };
        }

        private static AnimationCurve CategoryYawCurve(string category)
        {
            switch (category)
            {
                case "WalkAndRun":
                    return new AnimationCurve(
                        new Keyframe(0f, -5f),
                        new Keyframe(1f, 5f),
                        new Keyframe(2f, -5f));
                case "Fighting":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.5f, 25f),
                        new Keyframe(1f, -25f),
                        new Keyframe(1.5f, 15f),
                        new Keyframe(2f, 0f));
                case "DailyActions":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(1f, 12f),
                        new Keyframe(2f, 0f));
                default:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(2f, 360f));
            }
        }

        private static AnimationCurve CategoryBobCurve(string category)
        {
            switch (category)
            {
                case "WalkAndRun":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.5f, 0.12f),
                        new Keyframe(1f, 0f),
                        new Keyframe(1.5f, 0.12f),
                        new Keyframe(2f, 0f));
                case "Fighting":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.25f, 0.15f),
                        new Keyframe(0.5f, 0f),
                        new Keyframe(1.25f, 0.15f),
                        new Keyframe(1.5f, 0f),
                        new Keyframe(2f, 0f));
                default:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.5f, 0.08f),
                        new Keyframe(1f, 0f),
                        new Keyframe(1.5f, 0.08f),
                        new Keyframe(2f, 0f));
            }
        }

        private static AnimationCurve CategoryLungeCurve(string category)
        {
            switch (category)
            {
                case "WalkAndRun":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.5f, 0.25f),
                        new Keyframe(1f, 0.5f),
                        new Keyframe(1.5f, 0.25f),
                        new Keyframe(2f, 0f));
                case "Fighting":
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.3f, 0.35f),
                        new Keyframe(0.6f, -0.15f),
                        new Keyframe(1.2f, 0.3f),
                        new Keyframe(1.6f, -0.1f),
                        new Keyframe(2f, 0f));
                default:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(1f, 0.1f),
                        new Keyframe(2f, 0f));
            }
        }

        private void AddCategoryBoneMotion(AnimationClip clip, string category)
        {
            LastBoneMotionCount = 0;
            switch (category)
            {
                case "WalkAndRun":
                    AddSwingBone(clip, "LeftArm", 45f, 0f, 0f);
                    AddSwingBone(clip, "RightArm", 45f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftForeArm", 18f, 0f, 0f);
                    AddSwingBone(clip, "RightForeArm", 18f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftUpLeg", 35f, 0f, Mathf.PI);
                    AddSwingBone(clip, "RightUpLeg", 35f, 0f, 0f);
                    AddSwingBone(clip, "LeftLeg", 22f, 0f, Mathf.PI);
                    AddSwingBone(clip, "RightLeg", 22f, 0f, 0f);
                    AddSwingBone(clip, "Spine01", 0f, 8f, 0f);
                    AddSwingBone(clip, "Spine02", 0f, 6f, 0f);
                    AddSwingBone(clip, "Head", 6f, 0f, 0f);
                    break;
                case "Dancing":
                    AddSwingBone(clip, "LeftArm", 75f, 0f, 0f);
                    AddSwingBone(clip, "RightArm", 75f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftForeArm", 25f, 0f, 0f);
                    AddSwingBone(clip, "RightForeArm", 25f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftUpLeg", 20f, 0f, Mathf.PI);
                    AddSwingBone(clip, "RightUpLeg", 20f, 0f, 0f);
                    AddSwingBone(clip, "Spine01", 0f, 22f, 0f);
                    AddSwingBone(clip, "Spine02", 0f, 16f, 0f);
                    AddSwingBone(clip, "Head", 10f, 0f, 0f);
                    break;
                case "Fighting":
                    AddSwingBone(clip, "RightArm", 70f, 0f, 0f);
                    AddSwingBone(clip, "RightForeArm", 30f, 0f, 0f);
                    AddSwingBone(clip, "LeftArm", 45f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftUpLeg", 25f, 0f, Mathf.PI);
                    AddSwingBone(clip, "RightUpLeg", 25f, 0f, 0f);
                    AddSwingBone(clip, "Spine01", 8f, 10f, 0f);
                    AddSwingBone(clip, "Head", 8f, 0f, 0f);
                    break;
                case "DailyActions":
                    AddSwingBone(clip, "LeftArm", 15f, 0f, 0f);
                    AddSwingBone(clip, "RightArm", 15f, 0f, Mathf.PI);
                    AddSwingBone(clip, "Spine01", 0f, 6f, 0f);
                    AddSwingBone(clip, "Head", 5f, 0f, 0f);
                    break;
                default:
                    AddSwingBone(clip, "LeftArm", 40f, 0f, 0f);
                    AddSwingBone(clip, "RightArm", 40f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftForeArm", 15f, 0f, 0f);
                    AddSwingBone(clip, "RightForeArm", 15f, 0f, Mathf.PI);
                    AddSwingBone(clip, "LeftUpLeg", 25f, 0f, Mathf.PI);
                    AddSwingBone(clip, "RightUpLeg", 25f, 0f, 0f);
                    AddSwingBone(clip, "Spine01", 0f, 10f, 0f);
                    AddSwingBone(clip, "Head", 6f, 0f, 0f);
                    break;
            }
        }

        private void AddSwingBone(
            AnimationClip clip,
            string boneName,
            float amplitudeX,
            float amplitudeY,
            float phase)
        {
            var bone = FindBone(boneName);
            if (bone == null)
            {
                return;
            }

            var path = AnimationUtility.CalculateTransformPath(bone, modelRoot.transform);
            var bind = bone.localRotation;
            LastBoneMotionCount++;
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();
            const float duration = 2f;
            const int samples = 24;
            for (var i = 0; i <= samples; i++)
            {
                var t = duration * i / samples;
                var angle = Mathf.Sin((t / duration) * Mathf.PI * 2f + phase);
                var rotation = Quaternion.Euler(amplitudeX * angle, amplitudeY * angle, 0f) * bind;
                curveX.AddKey(t, rotation.x);
                curveY.AddKey(t, rotation.y);
                curveZ.AddKey(t, rotation.z);
                curveW.AddKey(t, rotation.w);
            }

            clip.SetCurve(path, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", curveW);
        }

        private Transform FindBone(string name)
        {
            if (modelRoot == null)
            {
                return null;
            }
            foreach (var transform in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return transform;
                }
            }
            return null;
        }

        private void EnsurePlaybackTick()
        {
            if (playbackTickRegistered)
            {
                return;
            }
            playbackTickRegistered = true;
            EditorApplication.update += PlaybackTick;
        }

        private void PlaybackTick()
        {
            if (!playing)
            {
                EditorApplication.update -= PlaybackTick;
                playbackTickRegistered = false;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - lastTick);
            lastTick = now;
            if (delta > 0.1f)
            {
                delta = 0.1f;
            }

            playbackTime += delta;
            if (playbackClips != null && playbackClips.Length > 0)
            {
                var length = Mathf.Max(playbackClips[playbackClipIndex].length, 0.01f);
                playbackTime %= length;
                if (animator != null)
                {
                    animator.Update(delta);
                }
                else if (legacyAnimation != null)
                {
                    playbackClips[playbackClipIndex].SampleAnimation(modelRoot, playbackTime);
                }
            }
            if (now - lastPlaybackRenderTime >= 1.0 / 30.0)
            {
                lastPlaybackRenderTime = now;
                Render();
            }
        }
    }

    public sealed class EditorDeferAgent : IDeferAgent
    {
        public bool ShouldDefer()
        {
            return false;
        }

        public bool ShouldDefer(float duration)
        {
            return false;
        }

        public Task BreakPoint()
        {
            return Task.CompletedTask;
        }

        public Task BreakPoint(float duration)
        {
            return Task.CompletedTask;
        }
    }
}
