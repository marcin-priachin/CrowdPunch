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
        private readonly HashSet<uint> updatedPunches = new();
        private readonly List<uint> updateOrder = new();
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

            PlayerBridgeRegistry.TryGetBridge(out var bridge);
            foreach (var defeat in defeats)
            {
                defeatsByPunch.TryGetValue(defeat.PunchSequence, out int count);
                defeatsByPunch[defeat.PunchSequence] = ++count;
                if (count >= 2 && updatedPunches.Add(defeat.PunchSequence))
                    updateOrder.Add(defeat.PunchSequence);
                if (bridge != null) bridge.ReceiveScoredDefeat(defeat.WorldPosition);
            }

            if (bridge != null)
            {
                foreach (uint punch in updateOrder)
                    bridge.ReceivePunchChainScore(punch, defeatsByPunch[punch]);
            }

            updatedPunches.Clear();
            updateOrder.Clear();
            defeats.Clear();
        }
    }
}
