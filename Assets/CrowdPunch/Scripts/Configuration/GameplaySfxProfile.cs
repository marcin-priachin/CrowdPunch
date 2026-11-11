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
        public AudioClip[] LinkedPairShock;
        public AudioClip[] EliteLaunch;
        [Tooltip("Seamless rolling loop for the Dasher's charge, committed dash and launched roll.")]
        public AudioClip DasherRollLoop;
        public AudioClip[] ArmorBlock;
        public AudioClip[] ArmorBreak;

        [Header("Spells and creature effects")]
        public AudioClip[] RangerCast;
        public AudioClip[] RangerImpact;
        public AudioClip[] FlowerWindup;
        public AudioClip[] FlowerSporeBurst;
        [Tooltip("Loop for the flower's continuous spore emission.")]
        public AudioClip FlowerSporeLoop;
        public AudioClip[] SquidInk;
        public AudioClip[] DragonFlameBurst;
        [Tooltip("Quiet loop under the Dragon's active breath and launched flame.")]
        public AudioClip DragonFlameLoop;
        public AudioClip[] DragonHit;
        public AudioClip[] NinjaTeleport;
        public AudioClip[] NinjaDash;
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
        [Tooltip("Deep grunt shared by the Elite, giant goblin wizard, and armored giant goblin.")]
        public AudioClip[] GoblinWizardHit;
        public AudioClip[] GoblinWizardTeleport;
        public AudioClip[] GoblinWizardTeleportArrival;
        public AudioClip GoblinWizardProjectileLoop;
        public AudioClip[] GoblinWizardProjectileRedirect;
        public AudioClip[] GoblinWizardProjectileImpact;
        public AudioClip[] GoblinWizardProjectileDischarge;
        public AudioClip[] ArmoredBossMovement;
        public AudioClip[] ArmoredBossClang;
        public AudioClip[] PillarLanding;
        public AudioClip[] PillarBossHit;
        [Tooltip("Seamless rumble for the spiked turtle's spinning attack.")]
        public AudioClip SpikedTurtleRollLoop;
        public AudioClip[] SpikedTurtleWallHit;

        [Header("Final encounter")]
        public AudioClip FinaleFlightLoop;
        public AudioClip FinaleWarningLoop;
        public AudioClip[] FinaleLightningStrike;
        public AudioClip[] FinaleAirSweep;
        public AudioClip[] FinaleWingSweep;
        public AudioClip[] FinaleShockwave;
        public AudioClip[] FinaleAnchorOverload;
        public AudioClip[] FinaleLanding;
        public AudioClip[] FinaleTakeoff;
        public AudioClip[] FinaleHit;
        public AudioClip[] FinaleDefeat;

        [Header("Objectives and results")]
        public AudioClip[] BarricadeHit;
        public AudioClip[] BarricadeBreak;
        public AudioClip[] LevelClear;
    }
}
