using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum WizardCastPhase : byte { Cooldown, Checking, Telegraph, Active }
    public struct WizardCastState : IComponentData
    {
        public WizardCastPhase Phase;
        public float Remaining;
        public uint RandomState;
        public uint ImpactFlight;
        public Entity MovingZone;
        public bool IsCasting => Phase == WizardCastPhase.Telegraph || Phase == WizardCastPhase.Active;
        public bool StopsMovement(WizardSettings settings) => IsCasting &&
            (settings.MovementMode == WizardMovementMode.StopThroughout ||
             settings.MovementMode == WizardMovementMode.StopTelegraph && Phase == WizardCastPhase.Telegraph);
    }
}
