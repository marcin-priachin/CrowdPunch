using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Local completion records keyed by stable campaign IDs; replays never erase progress.</summary>
    public sealed class CampaignProgress
    {
        [Serializable]
        private sealed class Save
        {
            public int version = 1;
            public List<string> completed = new();
        }

        private readonly string path;
        private Save data = new();
        public string Error { get; private set; }
        public CampaignProgress(string savePath) { path = savePath; Load(); }
        public bool IsComplete(string id) => data.completed.Contains(id);
        public int NextUnfinished(CampaignCatalog catalog)
        {
            for (int i = 0; i < catalog.Count; i++)
                if (!IsComplete(catalog.Get(i).id)) return i;
            return catalog.Count;
        }
        public bool IsUnlocked(CampaignCatalog catalog, int index)
            => index >= 0 && index < catalog.Count && index <= NextUnfinished(catalog);
        public bool Complete(string id)
        {
            // A successful replay does not mutate progress or rotate the recovery backup.
            // Retry a previous failed write even when its completion is already in memory.
            if (data.completed.Contains(id) && Error == null) return true;
            if (!data.completed.Contains(id)) data.completed.Add(id);
            return Write();
        }
        public bool Reset()
        {
            Save previous = data;
            data = new Save();
            if (Write()) return true;
            data = previous;
            return false;
        }
        private void Load()
        {
            if (TryRead(path) || TryRead(path + ".bak")) return;
            if (File.Exists(path)) Error = "Campaign save could not be read. The original file has been retained.";
        }
        private bool TryRead(string source)
        {
            try
            {
                if (!File.Exists(source)) return false;
                var saved = JsonUtility.FromJson<Save>(File.ReadAllText(source));
                if (saved == null || saved.version != 1 || saved.completed == null) return false;
                saved.completed.RemoveAll(string.IsNullOrEmpty);
                data = saved;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { return false; }
        }
        private bool Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                string json = JsonUtility.ToJson(data, true);
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.WriteAllText(temporary, json);
                        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                        else File.Move(temporary, path);
                        break;
                    }
                    catch (IOException) when (attempt < 3)
                    {
                        // Windows file scanners can briefly lock the replacement or backup.
                        // Bound the total retry delay to 70ms; permanent failures still surface.
                        System.Threading.Thread.Sleep(10 << attempt);
                    }
                }
                Error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Error = "Progress could not be saved: " + exception.Message;
                Debug.LogError(Error);
                return false;
            }
        }
    }
}
