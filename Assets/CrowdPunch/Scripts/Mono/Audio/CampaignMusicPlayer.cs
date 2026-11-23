using System;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using UnityEngine;

namespace CrowdPunch.Mono.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GauntletSequence))]
    public sealed class CampaignMusicPlayer : MonoBehaviour
    {
        private const int NoCue = -2;
        private const int MenuCue = -1;
        private const double ScheduleLead = .12;
        private const float FadeOutSeconds = .15f;
        private const float MixSeconds = .2f;
        private const float PausedDb = -8f;
        private const float ClearDb = -7f;
        private const float FailureDb = -16f;
        private const float StingDuckDb = -15f;

        [SerializeField] private CampaignMusicSettings settings;

        private GauntletSequence sequence;
        private CampaignMenu menu;
        private AudioSource intro;
        private AudioSource loop;
        private AudioSource sting;
        private int activeCue = NoCue;
        private double cueStart;
        private double fadeStart;
        private float fadeDuration = MixSeconds;
        private float fadeFrom;
        private float targetVolume;
        private float volume;
        private float mixDb;
        private bool wasMilestone;

        private void Awake()
        {
            sequence = GetComponent<GauntletSequence>();
            intro = CreateSource();
            loop = CreateSource();
            sting = CreateSource();
            loop.loop = true;
        }

        private void Start() => menu = FindFirstObjectByType<CampaignMenu>(FindObjectsInactive.Include);

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.priority = 64;
            source.volume = 0f;
            source.outputAudioMixerGroup = settings != null ? settings.MusicOutput : null;
            return source;
        }

        private void LateUpdate()
        {
            if (settings == null || !sequence.HasCampaign)
            {
                if (activeCue != NoCue) StopPlayback();
                return;
            }

            double now = AudioSettings.dspTime;
            volume = Mathf.SmoothStep(fadeFrom, targetVolume, (float)((now - fadeStart) / fadeDuration));
            int requestedCue = GetRequestedCue();
            bool loading = sequence.TransitionInProgress && !sequence.IsRevealingLevel;
            bool milestone = !sequence.TransitionInProgress &&
                sequence.Screen is CampaignScreen.ChapterComplete or CampaignScreen.CampaignComplete;
            UpdateSting(milestone);

            if (requestedCue != activeCue)
            {
                FadeTo(0f, now, FadeOutSeconds);
                bool startMenuDuringBoot = activeCue == NoCue && requestedCue == MenuCue;
                if (volume == 0f && (!loading || startMenuDuringBoot))
                    StartCue(requestedCue, now);
            }
            if (requestedCue == activeCue && (intro.clip != null || loop.clip != null))
            {
                mixDb = GetMixDb(loading, milestone);
                FadeTo(settings.Volume * Mathf.Pow(10f, mixDb / 20f), Math.Max(now, cueStart), MixSeconds);
            }
            intro.volume = loop.volume = volume;
        }

        private int GetRequestedCue()
        {
            if (sequence.Screen == CampaignScreen.MainMenu) return MenuCue;
            int level = sequence.TransitionInProgress ? sequence.LoadingLevelIndex : sequence.CurrentLevelIndex;
            return level >= 0 ? sequence.Campaign.ChapterFor(level) : NoCue;
        }

        private float GetMixDb(bool loading, bool milestone)
        {
            if (loading && activeCue != MenuCue) return Mathf.Min(mixDb, PausedDb);
            if (sequence.ShowingFailure) return FailureDb;
            if (milestone && sting.isPlaying) return StingDuckDb;
            if (menu != null && menu.IsBrowsingLevels) return PausedDb;
            if (activeCue == MenuCue) return 0f;
            if (sequence.ShowingClear) return ClearDb;
            return FeedbackTimeController.IsPaused ? PausedDb : 0f;
        }

        private void UpdateSting(bool milestone)
        {
            if (milestone && !wasMilestone && settings.VictorySting != null)
            {
                sting.clip = settings.VictorySting;
                sting.volume = settings.Volume * .8f;
                sting.Play();
            }
            else if (!milestone && sting.isPlaying)
            {
                sting.volume = Mathf.MoveTowards(sting.volume, 0f, Time.unscaledDeltaTime * .8f * settings.Volume / FadeOutSeconds);
                if (sting.volume == 0f) sting.Stop();
            }
            wasMilestone = milestone;
        }

        private void FadeTo(float target, double start, float duration)
        {
            if (target == targetVolume) return;
            fadeFrom = volume;
            targetVolume = target;
            fadeStart = start;
            fadeDuration = duration;
        }

        private void StartCue(int cue, double now)
        {
            intro.Stop();
            loop.Stop();
            var clips = cue == MenuCue ? settings.MainMenuCue : settings.GetChapterCue(cue);
            activeCue = cue;
            intro.clip = clips.Intro;
            loop.clip = clips.Loop;
            cueStart = now + ScheduleLead;

            double loopStart = cueStart;
            if (intro.clip != null)
            {
                intro.PlayScheduled(cueStart);
                // The edit is baked into the intro. Both clips meet at adjacent PCM frames.
                loopStart += (double)intro.clip.samples / intro.clip.frequency;
            }
            if (loop.clip != null) loop.PlayScheduled(loopStart);
        }

        private void OnDisable()
        {
            if (intro != null) StopPlayback();
        }

        private void StopPlayback()
        {
            intro.Stop();
            loop.Stop();
            sting.Stop();
            intro.clip = loop.clip = null;
            intro.volume = loop.volume = 0f;
            volume = fadeFrom = targetVolume = mixDb = 0f;
            activeCue = NoCue;
            wasMilestone = false;
        }
    }
}
