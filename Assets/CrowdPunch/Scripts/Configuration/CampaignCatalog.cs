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
        public int[] chapterStarts;
        public Level[] levels;
        public int Count => levels?.Length ?? 0;
        public Level Get(int index) => index >= 0 && index < Count ? levels[index] : null;
        public int ChapterStart(int chapter) => chapterStarts != null && chapterStarts.Length == chapterNames.Length
            ? chapterStarts[chapter] : chapter * 10;
        public int ChapterEnd(int chapter) => chapter + 1 < chapterNames.Length ? ChapterStart(chapter + 1) : Count;
        public int ChapterFor(int level)
        {
            for (int i = chapterNames.Length - 1; i >= 0; i--) if (level >= ChapterStart(i)) return i;
            return 0;
        }
    }
}
