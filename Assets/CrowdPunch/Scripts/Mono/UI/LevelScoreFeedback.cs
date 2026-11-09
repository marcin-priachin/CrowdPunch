using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Running score and chain callouts on the campaign canvas (SCORE-001).</summary>
    [DefaultExecutionOrder(100)] // Project after the camera has followed the player.
    public sealed class LevelScoreFeedback : MonoBehaviour
    {
        private const float AwardDuration = .85f;
        private GauntletSequence sequence;
        private CampaignMenu menu;
        private PlayerEcsBridge bridge;
        private LevelScoreTally tally;
        private Text award;
        private Color awardColor;
        private float awardAt;
        private uint restartSequence;

        public void Configure(GauntletSequence gauntletSequence, CampaignMenu campaignMenu)
        {
            sequence = gauntletSequence;
            menu = campaignMenu;
            tally = new LevelScoreTally(transform);
            award = PauseMenu.CreateLabel(transform, string.Empty, 34, 70);
            award.name = "Defeat Score";
            award.raycastTarget = false;
            award.rectTransform.anchorMin = award.rectTransform.anchorMax = new Vector2(.5f, 1);
            award.rectTransform.anchoredPosition = new Vector2(0, -122);
            award.rectTransform.sizeDelta = new Vector2(740, 76);
            CelComicUi.StyleText(award, heading: true);
            RingsideUi.OutlineHud(award);
            award.gameObject.SetActive(false);
            restartSequence = GameRestartRegistry.Sequence;
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
            bool showAward = CanShowAward();
            tally.Tick(showAward, Mathf.Min(Time.unscaledDeltaTime, .05f));
            if (award.gameObject.activeSelf)
            {
                float age = Time.unscaledTime - awardAt;
                float settle = Mathf.Clamp01(age / .20f);
                float scale = 1f + .25f * (1f - settle) * (1f - settle);
                award.rectTransform.localScale = Vector3.one * scale;
                awardColor.a = Mathf.Clamp01((AwardDuration - age) / .22f);
                award.color = awardColor;
                award.gameObject.SetActive(age < AwardDuration && showAward);
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

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (bridge == null) return;
            bridge.ScoredDefeatReceived -= QueueDefeat;
            bridge.ScoreAwarded -= ShowAward;
        }
    }
}
