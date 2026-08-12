using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshyWorkspace.Editor
{
    public static class MeshyModelDownloader
    {
        public static void DownloadFile(string url, string destinationPath, Action<bool> onDone)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var request = UnityWebRequest.Get(url);
            request.timeout = 120;
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        File.WriteAllBytes(destinationPath, request.downloadHandler.data);
                        onDone?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogWarning("[Meshy] 下载失败: " + url + " -> " + request.error);
                        onDone?.Invoke(false);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
        }
    }
}
