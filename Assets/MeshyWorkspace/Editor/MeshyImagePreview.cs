using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MeshyWorkspace.Editor
{
    public static class MeshyImagePreview
    {
        public static void DownloadInto(Image target, string url, List<Texture2D> keepAlive)
        {
            if (string.IsNullOrEmpty(url) || target == null)
            {
                return;
            }

            if (url.StartsWith("https://mock.invalid", StringComparison.OrdinalIgnoreCase))
            {
                ApplyPlaceholder(target, keepAlive);
                return;
            }

            var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var texture = DownloadHandlerTexture.GetContent(request);
                        if (keepAlive != null)
                        {
                            keepAlive.Add(texture);
                        }
                        target.image = texture;
                    }
                    else
                    {
                        Debug.LogWarning("[Meshy] 图片下载失败: " + url + " -> " + request.error);
                        ApplyPlaceholder(target, keepAlive);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static void ApplyPlaceholder(Image target, List<Texture2D> keepAlive)
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color32[128 * 128];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(36, 60, 56, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            if (keepAlive != null)
            {
                keepAlive.Add(texture);
            }
            target.image = texture;
        }
    }
}
