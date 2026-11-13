using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using UnityEngine;

namespace CrowdPunch.Mono.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GauntletSequence))]
    public sealed class CampaignMusicPlayer : MonoBehaviour
    {
        [SerializeField] private CampaignMusicSettings settings;

        private GauntletSequence sequence;
        private AudioSource source;

        private void Awake()
        {
            sequence = GetComponent<GauntletSequence>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        private void LateUpdate()
        {
            if (settings == null || !sequence.HasCampaign)
            {
                PlayTrack(null);
                return;
            }

            source.volume = settings.Volume;
            if (sequence.Screen == CampaignScreen.MainMenu)
            {
                // The menu's attract scene loads Chapter 1 without entering the campaign.
                PlayTrack(settings.MainMenuTrack);
            }
            else if (!sequence.TransitionInProgress)
            {
                int levelIndex = sequence.CurrentLevelIndex;
                PlayTrack(levelIndex >= 0
                    ? settings.GetChapterTrack(sequence.Campaign.ChapterFor(levelIndex))
                    : null);
            }
        }

        private void OnDisable()
        {
            if (source != null) PlayTrack(null);
        }

        private void PlayTrack(AudioClip track)
        {
            if (source.clip == track) return;

            source.Stop();
            source.clip = track;
            if (track != null) source.Play();
        }
    }
}
