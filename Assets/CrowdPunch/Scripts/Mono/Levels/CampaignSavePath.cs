using System.IO;
using UnityEngine;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Selects the campaign save location, including an optional local Editor override.</summary>
    public static class CampaignSavePath
    {
        public static string Resolve(bool editorPreview = false)
        {
            string directory = Application.persistentDataPath;
#if UNITY_EDITOR
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string configuration = Path.Combine(projectRoot, "UserSettings", "CampaignSaveDirectory.txt");
            if (File.Exists(configuration))
            {
                string configuredDirectory = File.ReadAllText(configuration).Trim();
                // A broken override must never silently write into the personal campaign save.
                if (string.IsNullOrWhiteSpace(configuredDirectory))
                    throw new InvalidDataException("CampaignSaveDirectory.txt must contain a save directory.");
                directory = Path.GetFullPath(Path.Combine(projectRoot, configuredDirectory));
            }
#endif
            return Path.Combine(directory, editorPreview ? "campaign-editor-preview.json" : "campaign-v1.json");
        }
    }
}
