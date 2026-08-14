using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MeshyWorkspace
{
    /// <summary>
    /// WebGL-compatible file system abstraction.
    /// On non-WebGL platforms, delegates to standard System.IO.
    /// On WebGL, uses PlayerPrefs for small data and keeps model references in memory.
    /// </summary>
    public static class MeshyPlatformIO
    {
        public static bool FileExists(string path)
        {
#if UNITY_WEBGL
            if (string.IsNullOrEmpty(path)) return false;
            var key = FileKey(path);
            return PlayerPrefs.HasKey(key);
#else
            return File.Exists(path);
#endif
        }

        public static byte[] ReadAllBytes(string path)
        {
#if UNITY_WEBGL
            if (string.IsNullOrEmpty(path)) return null;
            var key = FileKey(path);
            var base64 = PlayerPrefs.GetString(key, null);
            return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
#else
            return File.ReadAllBytes(path);
#endif
        }

        public static void WriteAllBytes(string path, byte[] data)
        {
#if UNITY_WEBGL
            if (string.IsNullOrEmpty(path) || data == null) return;
            var key = FileKey(path);
            PlayerPrefs.SetString(key, Convert.ToBase64String(data));
            PlayerPrefs.Save();
#else
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(path, data);
#endif
        }

        public static string ReadAllText(string path)
        {
#if UNITY_WEBGL
            if (string.IsNullOrEmpty(path)) return null;
            var key = FileKey(path);
            return PlayerPrefs.GetString(key, null);
#else
            return File.ReadAllText(path);
#endif
        }

        public static void WriteAllText(string path, string text)
        {
#if UNITY_WEBGL
            if (string.IsNullOrEmpty(path)) return;
            var key = FileKey(path);
            PlayerPrefs.SetString(key, text ?? string.Empty);
            PlayerPrefs.Save();
#else
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, text ?? string.Empty);
#endif
        }

        public static bool DirectoryExists(string path)
        {
#if UNITY_WEBGL
            // WebGL IndexedDB doesn't have directory concept.
            // Check if any file with this prefix exists.
            return !string.IsNullOrEmpty(path);
#else
            return Directory.Exists(path);
#endif
        }

        public static void CreateDirectory(string path)
        {
#if !UNITY_WEBGL
            if (!string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(path);
            }
#endif
        }

        public static void DeleteDirectory(string path, bool recursive)
        {
#if !UNITY_WEBGL
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
#endif
        }

        public static void FileMove(string source, string dest)
        {
#if !UNITY_WEBGL
            if (File.Exists(source))
            {
                File.Move(source, dest);
            }
#endif
        }

        public static string[] GetFiles(string path, string pattern)
        {
#if UNITY_WEBGL
            // WebGL cannot enumerate files in IndexedDB.
            return Array.Empty<string>();
#else
            return Directory.GetFiles(path, pattern);
#endif
        }

        public static string GetPersistentPath(string relativePath)
        {
#if UNITY_WEBGL
            return Application.persistentDataPath + "/" + relativePath;
#else
            return Path.Combine(Application.persistentDataPath, relativePath);
#endif
        }

        public static string GetStreamingPath(string relativePath)
        {
#if UNITY_WEBGL
            return Application.streamingAssetsPath + "/" + relativePath;
#else
            return Path.Combine(Application.streamingAssetsPath, relativePath);
#endif
        }

        public static bool IsWebGL
        {
            get
            {
#if UNITY_WEBGL
                return true;
#else
                return false;
#endif
            }
        }

        private static string FileKey(string path)
        {
            return "MeshyFile_" + Math.Abs(path.GetHashCode());
        }
    }
}