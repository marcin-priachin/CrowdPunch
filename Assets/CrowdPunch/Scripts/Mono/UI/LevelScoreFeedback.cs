using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Running score, chain callouts and clear summaries on the campaign canvas (SCORE-001).</summary>
    [DefaultExecutionOrder(100)] // Project after the camera has followed the player.
    public sealed class LevelScoreFeedback : MonoBehaviour
    {
        private const float AwardDuration = .85f;
        private const float ClearDuration = 2.5f;
        private GauntletSequence sequence;
        private CampaignMenu menu;
        private PlayerEcsBridge bridge;
        private LevelScoreTally tally;
        private Text award, clear;
        private Color awardColor;
        private float awardAt, clearRemaining;
        private uint clearSequence, restartSequence;
        private bool finishingScoredLevel;

        public void Configure(GauntletSequence gauntletSequence, CampaignMenu campaignMenu)
        {
            sequence = gauntletSequence;
            menu = campaignMenu;
            tally = new LevelScoreTally(transform);
            award = CreateCallout("Defeat Score", 34, 1f);
            award.rectTransform.anchoredPosition = new Vector2(0, -122);
            CelComicUi.StyleText(award, heading: true);
            clear = CreateCallout("Level Clear Score", 24, .18f);
            clearSequence = sequence.ClearScoreSequence;
            restartSequence = GameRestartRegistry.Sequence;
        }

        private Text CreateCallout(string labelName, int size, float verticalAnchor)
        {
            var label = PauseMenu.CreateLabel(transform, string.Empty, size, 70);
            label.name = labelName;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, verticalAnchor);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(740, 76);
            RingsideUi.OutlineHud(label);
            label.gameObject.SetActive(false);
            return label;
        }

        private void LateUpdate()
        {
            if (sequence == null) return;
            PlayerBridgeRegistry.TryGetBridge(out var activeBridge);
            if (bridge != activeBridge)
            {
                Unsubscribe();
                bridge = activeBridge;
                if (bridge != null)
                {
                    bridge.ScoredDefeatReceived += QueueDefeat;
                    bridge.ScoreAwarded += ShowAward;
                }
            }

            SyncRestart();
            float now = Time.unscaledTime;
            if (clearSequence != sequence.ClearScoreSequence)
            {
                clearSequence = sequence.ClearScoreSequence;
                clear.text = (sequence.LastClearIsBest ? "NEW BEST  |  " : "CLEAR  |  ") + Format(sequence.LastClearScore);
                clearRemaining = ClearDuration;
                finishingScoredLevel = sequence.LastClearScore.IsScored && sequence.Screen == CampaignScreen.Playing;
                clear.gameObject.SetActive(finishingScoredLevel);
            }

            // Let the final award finish in the existing transition window without delaying loading.
            tally.Tick(CanShowAward() || finishingScoredLevel && sequence.TransitionInProgress && !menu.IsOpen,
                Mathf.Min(Time.unscaledDeltaTime, .05f));
            if (award.gameObject.activeSelf)
            {
                float age = now - awardAt;
                float settle = Mathf.Clamp01(age / .20f);
                float scale = 1f + .25f * (1f - settle) * (1f - settle);
                award.rectTransform.localScale = Vector3.one * scale;
                awardColor.a = Mathf.Clamp01((AwardDuration - age) / .22f);
                award.color = awardColor;
                award.gameObject.SetActive(age < AwardDuration && CanShowAward());
            }
            if (clear.gameObject.activeSelf)
            {
                // Give the summary its full reading time even when the next scene takes time to load.
                if (!sequence.TransitionInProgress)
                    clearRemaining -= Mathf.Min(Time.unscaledDeltaTime, .1f);
                var color = RingsideUi.Paper;
                color.a = Mathf.Clamp01(clearRemaining / .35f);
                clear.color = color;
                clear.gameObject.SetActive(clearRemaining > 0 && sequence.Screen == CampaignScreen.Playing && !menu.IsOpen);
            }
        }

        private bool CanShowAward() => sequence.Screen == CampaignScreen.Playing &&
            !menu.IsOpen && !sequence.TransitionInProgress && !sequence.RunFailed;

        private void QueueDefeat(Vector3 worldPosition)
        {
            if (sequence.Screen != CampaignScreen.Playing || sequence.TransitionInProgress || sequence.RunFailed) return;
            SyncRestart();
            tally.Queue(worldPosition);
        }

        private void SyncRestart()
        {
            if (restartSequence == GameRestartRegistry.Sequence) return;
            restartSequence = GameRestartRegistry.Sequence;
            finishingScoredLevel = false;
            tally.Reset();
            award.gameObject.SetActive(false);
        }

        private void ShowAward(int _, int chainDefeats)
        {
            if (chainDefeats < 2 || !CanShowAward()) return;
            award.text = $"{chainDefeats} IN ONE!";
            awardColor = RingsideUi.Gold;
            award.color = awardColor;
            awardAt = Time.unscaledTime;
            award.gameObject.SetActive(true);
        }

        public static string Format(LevelClearScore score) =>
            $"{score.Points:N0} pts   |   {score.Defeats} defeated   |   {score.Punches} {(score.Punches == 1 ? "punch" : "punches")}";

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (bridge == null) return;
            bridge.ScoredDefeatReceived -= QueueDefeat;
            bridge.ScoreAwarded -= ShowAward;
        }
    }
}
