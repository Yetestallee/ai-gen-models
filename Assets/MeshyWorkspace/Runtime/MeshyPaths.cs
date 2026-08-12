using System.IO;
using UnityEngine;

namespace MeshyWorkspace
{
    /// <summary>
    /// Centralized layout for generated assets under Assets/MeshyGenerated.
    /// Task outputs are grouped by type: Images / Models / Animations, with
    /// a ReferenceModels folder for manually placed rigged assets.
    /// </summary>
    public static class MeshyPaths
    {
        public const string ImagesDir = "Images";
        public const string ModelsDir = "Models";
        public const string AnimationsDir = "Animations";
        public const string ReferenceModelsDir = "ReferenceModels";

        public static string Root
        {
            get { return Path.Combine(Application.dataPath, "MeshyGenerated"); }
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
            var canonical = TaskFolder(taskType, taskId);
            if (Directory.Exists(canonical))
            {
                return canonical;
            }
            if (taskType == "text-to-3d" || taskType == "image-to-3d" || taskType == "retexture")
            {
                var reference = Path.Combine(ReferenceModels, taskId);
                if (Directory.Exists(reference))
                {
                    return reference;
                }
            }
            var legacy = Path.Combine(Root, taskId);
            return Directory.Exists(legacy) ? legacy : canonical;
        }

        public static string FindModelFile(string taskId)
        {
            foreach (var folder in new[]
            {
                Path.Combine(Models, taskId),
                Path.Combine(ReferenceModels, taskId),
                Path.Combine(Root, taskId)
            })
            {
                var file = Path.Combine(folder, "model.glb");
                if (File.Exists(file))
                {
                    return file;
                }
            }
            return null;
        }

        public static string FindImageFile(string taskId, int index)
        {
            foreach (var folder in new[] { Path.Combine(Images, taskId), Path.Combine(Root, taskId) })
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                var files = Directory.GetFiles(folder, "image_" + index + ".*");
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            return null;
        }

        public static string Relative(string taskType, string taskId, string fileName)
        {
            return "Assets/MeshyGenerated/" + TypeFolder(taskType) + "/" + taskId + "/" + fileName;
        }
    }
}
