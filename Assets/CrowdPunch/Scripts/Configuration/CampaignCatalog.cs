using System;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Campaign Catalog")]
    public sealed class CampaignCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public string id;
            public string title;
            [Tooltip("Empty until this encounter is implemented.")]
            public string scenePath;
            public bool Available => !string.IsNullOrEmpty(scenePath);
        }

        public string[] chapterNames;
        public Level[] levels;
        public int Count => levels?.Length ?? 0;
        public Level Get(int index) => index >= 0 && index < Count ? levels[index] : null;
    }
}
