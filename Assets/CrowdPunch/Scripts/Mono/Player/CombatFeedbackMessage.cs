using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    public struct CombatFeedbackMessage
    {
        public CombatImpactKind Kind;
        public Vector3 Position, Direction;
        public float Intensity;
        public int ChainDepth;
        public bool PlayerOwned;
    }
}
