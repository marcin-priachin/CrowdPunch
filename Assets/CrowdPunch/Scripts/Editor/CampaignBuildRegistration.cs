using System.Linq;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace CrowdPunch.Editor
{
    /// <summary>Keeps legacy authoring recipes and validation scenes out of campaign player builds.</summary>
    [InitializeOnLoad]
    public sealed class CampaignBuildRegistration : IPreprocessBuildWithReport
    {
        public const string CatalogPath = "Assets/CrowdPunch/Data/Campaign/Campaign.asset";
        public const string BootstrapPath = "Assets/CrowdPunch/Scenes/Bootstrap.unity";
        private static bool applying;
        static CampaignBuildRegistration() => EditorBuildSettings.sceneListChanged += Apply;
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Apply();
        public static void Apply()
        {
            if (applying) return;
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CatalogPath);
            if (catalog == null) return;
            var desired = new[] { new EditorBuildSettingsScene(BootstrapPath, true) }
                .Concat(catalog.levels.Where(level => level.Available)
                    .Select(level => new EditorBuildSettingsScene(level.scenePath, true))).ToArray();
            if (EditorBuildSettings.scenes.Select(s => s.path + s.enabled)
                .SequenceEqual(desired.Select(s => s.path + s.enabled))) return;
            applying = true;
            try { EditorBuildSettings.scenes = desired; }
            finally { applying = false; }
        }
    }
}
