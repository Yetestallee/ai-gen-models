using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MeshyWorkspace
{
    /// <summary>
    /// Centralized layout for generated assets. Editor tooling defaults to
    /// Assets/MeshyGenerated so AssetDatabase import continues to work, while
    /// runtime Game mode can override the root to Application.persistentDataPath.
    /// </summary>
    public static class MeshyPaths
    {
        public const string ImagesDir = "Images";
        public const string ModelsDir = "Models";
        public const string AnimationsDir = "Animations";
        public const string ReferenceModelsDir = "ReferenceModels";

        public static string RootOverride { get; set; }

        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(RootOverride))
                {
                    return RootOverride;
                }
#if UNITY_EDITOR
                return ProjectAssetRoot;
#else
                return PersistentRoot;
#endif
            }
        }

        public static string ProjectAssetRoot
        {
            get { return Path.Combine(Application.dataPath, "MeshyGenerated"); }
        }

        public static string PersistentRoot
        {
            get { return Path.Combine(Application.persistentDataPath, "MeshyWorkspace", "Generated"); }
        }

        public static string BundledRoot
        {
            get { return Path.Combine(Application.streamingAssetsPath, "MeshyGenerated"); }
        }

        public static string Images
        {
            get { return Path.Combine(Root, ImagesDir); }
        }

        public static string Models
        {
            get { return Path.Combine(Root, ModelsDir); }
        }

        public static string Animations
        {
            get { return Path.Combine(Root, AnimationsDir); }
        }

        public static string ReferenceModels
        {
            get { return Path.Combine(Root, ReferenceModelsDir); }
        }

        public static string TypeFolder(string taskType)
        {
            switch (taskType)
            {
                case "text-to-image":
                    return ImagesDir;
                case "animation":
                    return AnimationsDir;
                case "retexture":
                case "text-to-3d":
                case "image-to-3d":
                default:
                    return ModelsDir;
            }
        }

        public static string TaskFolder(string taskType, string taskId)
        {
            return Path.Combine(Root, TypeFolder(taskType), taskId);
        }

        /// <summary>
        /// Returns an existing task folder, checking the canonical type folder,
        /// ReferenceModels (for model tasks), and the legacy root layout.
        /// </summary>
        public static string FindTaskFolder(string taskType, string taskId)
        {
            foreach (var folder in TaskFolderCandidates(taskType, taskId))
            {
                if (MeshyPlatformIO.DirectoryExists(folder))
                {
                    return folder;
                }
            }
            return TaskFolder(taskType, taskId);
        }

        public static string FindModelFile(string taskId)
        {
            foreach (var folder in TaskFolderCandidates("text-to-3d", taskId))
            {
                var file = Path.Combine(folder, "model.glb");
                if (MeshyPlatformIO.FileExists(file))
                {
                    return file;
                }
            }
            return null;
        }

        public static string FindImageFile(string taskId, int index)
        {
            foreach (var folder in TaskFolderCandidates("text-to-image", taskId))
            {
                if (!MeshyPlatformIO.DirectoryExists(folder))
                {
                    continue;
                }
                var files = MeshyPlatformIO.GetFiles(folder, "image_" + index + ".*");
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            return null;
        }

        private static IEnumerable<string> TaskFolderCandidates(string taskType, string taskId)
        {
            yield return TaskFolder(taskType, taskId);
            yield return Path.Combine(BundledRoot, TypeFolder(taskType), taskId);
            yield return Path.Combine(ProjectAssetRoot, TypeFolder(taskType), taskId);

            if (taskType == "text-to-3d" || taskType == "image-to-3d" || taskType == "retexture" || taskType == "remesh" || taskType == "rigging")
            {
                yield return Path.Combine(ReferenceModels, taskId);
                yield return Path.Combine(BundledRoot, ReferenceModelsDir, taskId);
                yield return Path.Combine(ProjectAssetRoot, ReferenceModelsDir, taskId);

                foreach (var modelType in new[] { "text-to-3d", "image-to-3d", "retexture", "remesh", "rigging" })
                {
                    yield return Path.Combine(Root, ModelsDir, modelType, taskId);
                    yield return Path.Combine(BundledRoot, ModelsDir, modelType, taskId);
                    yield return Path.Combine(ProjectAssetRoot, ModelsDir, modelType, taskId);
                }
            }

            yield return Path.Combine(Root, taskId);
            yield return Path.Combine(BundledRoot, taskId);
            yield return Path.Combine(ProjectAssetRoot, taskId);
        }

        public static string Relative(string taskType, string taskId, string fileName)
        {
            return "Assets/MeshyGenerated/" + TypeFolder(taskType) + "/" + taskId + "/" + fileName;
        }
    }
}