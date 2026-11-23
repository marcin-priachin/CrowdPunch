using System;
using UnityEngine;
using UnityEngine.Audio;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "CampaignMusicSettings", menuName = "Crowd Punch/Campaign Music Settings")]
    public sealed class CampaignMusicSettings : ScriptableObject
    {
        [Serializable]
        public struct Cue
        {
            [SerializeField] private AudioClip intro;
            [SerializeField] private AudioClip loop;

            public readonly AudioClip Intro => intro;
            public readonly AudioClip Loop => loop;

            internal Cue(AudioClip loop)
            {
                intro = null;
                this.loop = loop;
            }
        }

        [SerializeField] private Cue mainMenuCue;
        [Tooltip("Assign cues in campaign order. Element 0 is Chapter 1. An empty slot is silent.")]
        [SerializeField] private Cue[] chapterCues = Array.Empty<Cue>();
        [SerializeField] private AudioClip victorySting;
        [SerializeField] private AudioMixerGroup musicOutput;
        [SerializeField, Range(0f, 1f)] private float volume = .35f;

        [SerializeField, HideInInspector] private AudioClip mainMenuTrack;
        [SerializeField, HideInInspector] private AudioClip[] chapterTracks;

        public Cue MainMenuCue => mainMenuCue.Intro != null || mainMenuCue.Loop != null
            ? mainMenuCue : new Cue(mainMenuTrack);
        public AudioClip VictorySting => victorySting;
        public AudioMixerGroup MusicOutput => musicOutput;
        public float Volume => volume;

        public Cue GetChapterCue(int chapterIndex)
        {
            if (chapterIndex < 0) return default;
            if (chapterCues != null && chapterCues.Length > 0)
                return chapterIndex < chapterCues.Length ? chapterCues[chapterIndex] : default;
            return chapterTracks != null && chapterIndex < chapterTracks.Length
                ? new Cue(chapterTracks[chapterIndex]) : default;
        }
    }
}
