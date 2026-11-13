using System;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "CampaignMusicSettings", menuName = "Crowd Punch/Campaign Music Settings")]
    public sealed class CampaignMusicSettings : ScriptableObject
    {
        [SerializeField] private AudioClip mainMenuTrack;
        [Tooltip("Assign one track per chapter in campaign order. Element 0 is Chapter 1. An empty slot is silent.")]
        [SerializeField] private AudioClip[] chapterTracks = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volume = .35f;

        public AudioClip MainMenuTrack => mainMenuTrack;
        public float Volume => volume;

        public AudioClip GetChapterTrack(int chapterIndex) =>
            chapterTracks != null && chapterIndex >= 0 && chapterIndex < chapterTracks.Length
                ? chapterTracks[chapterIndex]
                : null;
    }
}
