using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Running score and chain callouts on the campaign canvas (SCORE-001).</summary>
    [DefaultExecutionOrder(100)] // Project after the camera has followed the player.
    public sealed class LevelScoreFeedback : MonoBehaviour
    {
        private GauntletSequence sequence;
        private CampaignMenu menu;
        private PlayerEcsBridge bridge;
        private LevelScoreTally tally;
        private LevelChainCallouts callouts;
        private uint restartSequence;

        public void Configure(GauntletSequence gauntletSequence, CampaignMenu campaignMenu)
        {
            sequence = gauntletSequence;
            menu = campaignMenu;
            tally = new LevelScoreTally(transform);
            callouts = new LevelChainCallouts(transform);
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
                    bridge.PunchChainScored += QueueCallout;
                }
            }

            SyncRestart();
            bool visible = CanShowScore();
            tally.Tick(visible, Mathf.Min(Time.unscaledDeltaTime, .05f));
            if (visible) callouts.Tick(Time.unscaledTimeAsDouble);
            else callouts.Reset();
        }

        private bool CanShowScore() => sequence.Screen == CampaignScreen.Playing &&
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
            callouts.Reset();
        }

        private void QueueCallout(uint punchSequence, int defeats)
        {
            if (!CanShowScore()) return;
            SyncRestart();
            callouts.Queue(punchSequence, defeats, Time.unscaledTimeAsDouble);
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (bridge == null) return;
            bridge.ScoredDefeatReceived -= QueueDefeat;
            bridge.PunchChainScored -= QueueCallout;
        }
    }
}
