using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Brief score callouts and clear summaries on the existing campaign canvas.</summary>
    public sealed class LevelScoreFeedback : MonoBehaviour
    {
        private const float AwardDuration = .85f;
        private const float ClearDuration = 2.5f;
        private static readonly Color ChainColor = new(1f, .81f, .31f);
        private GauntletSequence sequence;
        private CampaignMenu menu;
        private PlayerEcsBridge bridge;
        private Text award, clear;
        private Color awardColor;
        private float awardAt, clearRemaining;
        private uint clearSequence, restartSequence;

        public void Configure(GauntletSequence gauntletSequence, CampaignMenu campaignMenu)
        {
            sequence = gauntletSequence;
            menu = campaignMenu;
            award = CreateCallout("Defeat Score", 34, .68f);
            clear = CreateCallout("Level Clear Score", 24, .18f);
            clearSequence = sequence.ClearScoreSequence;
        }

        private Text CreateCallout(string labelName, int size, float verticalAnchor)
        {
            var label = PauseMenu.CreateLabel(transform, string.Empty, size, 70);
            label.name = labelName;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, verticalAnchor);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(740, 76);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.045f, .035f, .02f, .9f);
            outline.effectDistance = new Vector2(2f, -2f);
            label.gameObject.SetActive(false);
            return label;
        }

        private void LateUpdate()
        {
            if (sequence == null) return;
            PlayerBridgeRegistry.TryGetBridge(out var activeBridge);
            if (bridge != activeBridge)
            {
                if (bridge != null) bridge.ScoreAwarded -= ShowAward;
                bridge = activeBridge;
                if (bridge != null) bridge.ScoreAwarded += ShowAward;
            }

            float now = Time.unscaledTime;
            if (clearSequence != sequence.ClearScoreSequence)
            {
                clearSequence = sequence.ClearScoreSequence;
                clear.text = (sequence.LastClearIsBest ? "NEW BEST  |  " : "CLEAR  |  ") + Format(sequence.LastClearScore);
                clearRemaining = ClearDuration;
                clear.gameObject.SetActive(sequence.LastClearScore.IsScored && sequence.Screen == CampaignScreen.Playing);
            }

            if (restartSequence != GameRestartRegistry.Sequence)
            {
                restartSequence = GameRestartRegistry.Sequence;
                award.gameObject.SetActive(false);
            }
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
                clear.color = new Color(1f, 1f, 1f, Mathf.Clamp01(clearRemaining / .35f));
                clear.gameObject.SetActive(clearRemaining > 0 && sequence.Screen == CampaignScreen.Playing && !menu.IsOpen);
            }
        }

        private bool CanShowAward() => sequence.Screen == CampaignScreen.Playing &&
            !menu.IsOpen && !sequence.TransitionInProgress && !sequence.RunFailed;

        private void ShowAward(int defeats, int chainDefeats)
        {
            if (!CanShowAward()) return;
            int points = defeats * LevelClearScore.PointsPerDefeat;
            award.text = chainDefeats > 1 ? $"{chainDefeats} IN ONE!   |   +{points:N0} pts" : $"+{points:N0} pts";
            awardColor = chainDefeats > 1 ? ChainColor : Color.white;
            award.color = awardColor;
            awardAt = Time.unscaledTime;
            award.gameObject.SetActive(true);
        }

        public static string Format(LevelClearScore score) =>
            $"{score.Points:N0} pts   |   {score.Defeats} defeated   |   {score.Punches} {(score.Punches == 1 ? "punch" : "punches")}";

        private void OnDestroy()
        {
            if (bridge != null) bridge.ScoreAwarded -= ShowAward;
        }
    }
}
