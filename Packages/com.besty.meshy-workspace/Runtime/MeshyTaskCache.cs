using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MeshyWorkspace
{
    public sealed class MeshyTaskCache
    {
        private readonly string filePath;
        private List<MeshyCachedTask> entries;

        public MeshyTaskCache(string filePath)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            entries = Load();
        }

        public IReadOnlyList<MeshyCachedTask> Entries
        {
            get { return entries.AsReadOnly(); }
        }

        public void AddOrUpdate(MeshyCachedTask entry)
        {
            if (entry == null)
            {
                return;
            }

            var index = entries.FindIndex(e => e.TaskId == entry.TaskId);
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }

            Save();
        }

        public void Remove(string taskId)
        {
            var index = entries.FindIndex(e => e.TaskId == taskId);
            if (index >= 0)
            {
                entries.RemoveAt(index);
                Save();
            }
        }

        private List<MeshyCachedTask> Load()
        {
#if UNITY_WEBGL
            var json = PlayerPrefs.GetString(CacheKey, "[]");
            try
            {
                var loaded = JsonConvert.DeserializeObject<List<MeshyCachedTask>>(json);
                return loaded ?? new List<MeshyCachedTask>();
            }
            catch (Exception)
            {
                return new List<MeshyCachedTask>();
            }
#else
            if (!File.Exists(filePath))
            {
                return new List<MeshyCachedTask>();
            }

            try
            {
                var loaded = JsonConvert.DeserializeObject<List<MeshyCachedTask>>(File.ReadAllText(filePath));
                return loaded ?? new List<MeshyCachedTask>();
            }
            catch (Exception)
            {
                return new List<MeshyCachedTask>();
            }
#endif
        }

        private void Save()
        {
#if UNITY_WEBGL
            PlayerPrefs.SetString(CacheKey, JsonConvert.SerializeObject(entries, Formatting.Indented));
            PlayerPrefs.Save();
#else
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, JsonConvert.SerializeObject(entries, Formatting.Indented));
#endif
        }

#if UNITY_WEBGL
        private string CacheKey
        {
            get { return "MeshyTaskCache_" + Math.Abs(filePath.GetHashCode()); }
        }
#endif
    }

    public class MeshyCachedTask
    {
        public string TaskId { get; set; }
        public string TaskType { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string FinishedAt { get; set; }
        public double ConsumedCredits { get; set; }
        public string ErrorMessage { get; set; }
        public string Prompt { get; set; }
        public string AiModel { get; set; }
        public string AspectRatio { get; set; }
        public List<string> ImageUrls { get; set; }
        public Dictionary<string, string> ModelUrls { get; set; }
        public List<string> TextureUrls { get; set; }
        public int Progress { get; set; }
        public string DownloadState { get; set; }
        public string ErrorReason { get; set; }
        public bool CreditsDeducted { get; set; }
        public bool Recoverable { get; set; }
    }
}