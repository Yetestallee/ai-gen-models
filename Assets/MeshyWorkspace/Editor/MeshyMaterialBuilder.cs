using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    public static class MeshyMaterialBuilder
    {
        public static Material CreatePbrMaterial(string folderPath, List<string> textureUrls, string materialName)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            TryAssignFirstTexture(material, "_MainTex", folderPath, "texture_0");
            TryAssignFirstTexture(material, "_BumpMap", folderPath, "texture_1");
            TryAssignFirstTexture(material, "_MetallicGlossMap", folderPath, "texture_2");
            TryAssignFirstTexture(material, "_OcclusionMap", folderPath, "texture_3");
            TryAssignFirstTexture(material, "_EmissionMap", folderPath, "texture_4");

            var relativeFolder = folderPath.Replace(Application.dataPath, "Assets");
            var assetPath = relativeFolder + "/" + materialName + ".mat";
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void TryAssignFirstTexture(Material material, string property, string folderPath, string prefix)
        {
            var matches = System.IO.Directory.GetFiles(folderPath, prefix + ".*");
            if (matches.Length == 0)
            {
                return;
            }

            var assetPath = matches[0].Replace(Application.dataPath, "Assets");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                material.SetTexture(property, texture);
            }
        }
    }
}
