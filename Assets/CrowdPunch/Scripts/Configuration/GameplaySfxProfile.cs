using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "GameplaySfxProfile", menuName = "Crowd Punch/Gameplay SFX Profile")]
    public sealed class GameplaySfxProfile : ScriptableObject
    {
        [Header("Player and crowd")]
        public AudioClip[] PunchConfirmed;
        public AudioClip[] PunchMiss;
        public AudioClip[] PlayerDash;
        public AudioClip[] LaunchDeparture;
        public AudioClip[] CrowdCollision;
        public AudioClip[] ExceptionalChain;
        public AudioClip[] PlayerHurt;

        [Header("Goblins")]
        [Tooltip("Hit reaction shared by Baseline, Dasher, Ranged and Linked Pair.")]
        public AudioClip[] GoblinGrunt;
        public AudioClip[] LinkedPairAttack;
        public AudioClip[] EliteLaunch;
        public AudioClip[] DasherRoll;
        public AudioClip[] ArmorBlock;
        public AudioClip[] ArmorBreak;

        [Header("Spells and creature effects")]
        public AudioClip[] RangerCast;
        public AudioClip[] RangerImpact;
        public AudioClip[] FlowerWindup;
        public AudioClip[] FlowerSporeBurst;
        public AudioClip[] SquidInk;
        public AudioClip[] BlobDetach;
        public AudioClip[] BlobBounce;
        public AudioClip[] ExploderWarning;
        public AudioClip[] ExploderDetonation;

        [Header("Bosses")]
        public AudioClip[] GatekeeperWindup;
        public AudioClip[] GatekeeperWindupGrunt;
        public AudioClip[] GatekeeperSlam;
        public AudioClip[] GatekeeperLunge;
        public AudioClip[] GatekeeperSweep;
        public AudioClip[] GatekeeperStrikeImpact;
        public AudioClip[] GatekeeperMove;
        public AudioClip[] GatekeeperHeadHit;
        public AudioClip[] GatekeeperHitGrunt;
        public AudioClip[] GatekeeperHandHit;
        public AudioClip[] GatekeeperDefeat;
        public AudioClip[] GoblinWizardCharge;
        public AudioClip[] GoblinWizardCast;
        public AudioClip[] GoblinWizardTeleport;
        public AudioClip[] ArmoredBossMovement;
        public AudioClip[] PillarLanding;
        public AudioClip[] PillarBossHit;
        public AudioClip[] SpikedTurtleRoll;
        public AudioClip[] SpikedTurtleWallHit;

        [Header("Objectives and results")]
        public AudioClip[] BarricadeHit;
        public AudioClip[] BarricadeBreak;
        public AudioClip[] LevelClear;
    }
}
