using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class LevelScoreBridgeSystem : SystemBase
    {
        private readonly Dictionary<uint, int> defeatsByPunch = new();
        private uint restartSequence;

        protected override void OnCreate() => RequireForUpdate<LevelClearScore>();

        protected override void OnUpdate()
        {
            if (restartSequence != GameRestartRegistry.Sequence)
            {
                restartSequence = GameRestartRegistry.Sequence;
                defeatsByPunch.Clear();
            }

            var defeats = SystemAPI.GetSingletonBuffer<ScoredDefeat>();
            if (defeats.Length == 0) return;

            int bestChain = 0;
            foreach (var defeat in defeats)
            {
                defeatsByPunch.TryGetValue(defeat.PunchSequence, out int count);
                defeatsByPunch[defeat.PunchSequence] = ++count;
                if (count > bestChain) bestChain = count;
            }

            if (PlayerBridgeRegistry.TryGetBridge(out var bridge))
                bridge.ReceiveScoreAward(defeats.Length, bestChain);
            defeats.Clear();
        }
    }
}
