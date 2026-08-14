using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshyWorkspace
{
    public static class MeshyRuntimeDownloads
    {
        public static async Task<bool> DownloadFileAsync(string url, string destinationPath, int timeoutSeconds = 600)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(destinationPath))
            {
                return false;
            }

            if (url.StartsWith("https://mock.invalid", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            MeshyPlatformIO.CreateDirectory(Path.GetDirectoryName(destinationPath));

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.Max(1, timeoutSeconds);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Meshy] 下载失败: " + url + " -> " + request.error);
                    return false;
                }

                MeshyPlatformIO.WriteAllBytes(destinationPath, request.downloadHandler.data);
                return true;
            }
        }

        public static async Task<Texture2D> DownloadTextureAsync(string url, List<Texture2D> keepAlive = null)
        {
            if (string.IsNullOrEmpty(url) ||
                url.StartsWith("https://mock.invalid", StringComparison.OrdinalIgnoreCase))
            {
                return CreatePlaceholderTexture(128, 128, new Color32(36, 60, 56, 255), keepAlive);
            }

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Meshy] 图片下载失败: " + url + " -> " + request.error);
                    return CreatePlaceholderTexture(128, 128, new Color32(36, 60, 56, 255), keepAlive);
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (keepAlive != null)
                {
                    keepAlive.Add(texture);
                }
                return texture;
            }
        }

        public static Texture2D CreatePlaceholderTexture(int width, int height, Color32 color, List<Texture2D> keepAlive = null)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            if (keepAlive != null)
            {
                keepAlive.Add(texture);
            }
            return texture;
        }

        public static string TryReadClipboardPathOrUrl()
        {
            var text = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text.Trim().Trim('"');
            if (Uri.TryCreate(text, UriKind.Absolute, out _) || MeshyPlatformIO.FileExists(text))
            {
                return text;
            }
            return string.Empty;
        }

        public static string UrlFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.AbsolutePath);
                return string.IsNullOrEmpty(name) ? string.Empty : name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string FileToDataUri(string path)
        {
            if (string.IsNullOrEmpty(path) || !MeshyPlatformIO.FileExists(path))
            {
                return string.Empty;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var mime = extension == ".png" ? "image/png" : "image/jpeg";
            return "data:" + mime + ";base64," + Convert.ToBase64String(MeshyPlatformIO.ReadAllBytes(path));
        }
    }
}