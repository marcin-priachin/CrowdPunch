using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.UI;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class GameplaySfx : MonoBehaviour
    {
        private const float WorldRange = 30f;
        private readonly AudioSource[] worldVoices = new AudioSource[8];
        private readonly float[] worldPriorities = new float[8];
        private AudioSource punchVoice, hurtVoice, dashVoice, clearVoice;
        private PlayerEcsBridge bridge;
        private PlayerController controller;
        private PlayerHealth health;
        private CombatFeedbackSettings feedback;
        private GameplaySfxProfile profile;
        private uint punchSequence, restart, completion;
        private double nextImpact, nextChain, nextHurt, nextLaunch;
        private CombatFeedbackMessage collision, chain;
        private float collisionScore, chainScore;
        private bool subscribed, suspended;
        private bool CanPlay => isActiveAndEnabled && bridge.isActiveAndEnabled && !FeedbackTimeController.IsSuspended;

        internal void Configure(PlayerEcsBridge player, CombatFeedbackSettings settings)
        {
            if (bridge != null || player == null) return;
            var profiles = Resources.Load<GameplaySfxProfiles>("GameplaySfxProfiles");
            if (profiles == null || profiles.Default == null) return;
            profile = profiles.Default;
            bridge = player;
            controller = player.GetComponent<PlayerController>();
            health = player.GetComponent<PlayerHealth>();
            feedback = settings;
            bridge.GameplayAudio = this;
            punchVoice = MakeVoice("Punch SFX", false);
            hurtVoice = MakeVoice("Hurt SFX", false);
            dashVoice = MakeVoice("Dash SFX", false);
            clearVoice = MakeVoice("Level clear SFX", false);
            for (int i = 0; i < worldVoices.Length; i++) worldVoices[i] = MakeVoice("World SFX", true);
            Subscribe();
        }

        private AudioSource MakeVoice(string label, bool spatial)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? .65f : 0f;
            source.dopplerLevel = 0f;
            source.minDistance = 6f;
            source.maxDistance = WorldRange;
            source.priority = spatial ? 160 : 64;
            return source;
        }

        private void OnEnable() => Subscribe();

        private void Subscribe()
        {
            if (subscribed || bridge == null || !isActiveAndEnabled) return;
            subscribed = true;
            restart = GameRestartRegistry.Sequence;
            completion = GauntletCompletionRegistry.Sequence;
            punchSequence = bridge.PunchSequence;
            bridge.PunchResolved += OnPunch;
            bridge.ImpactReceived += OnImpact;
            bridge.LaunchReceived += OnLaunch;
            bridge.PillarImpactReceived += OnPillar;
            bridge.ExplosionReceived += OnExplosion;
            bridge.ProjectileBurstReceived += OnProjectileImpact;
            bridge.GameplaySfxReceived += OnWorldCue;
            if (controller != null) controller.DashStarted += OnDash;
            if (health != null) health.DamageAccepted += OnHurt;
        }

        private void OnDisable()
        {
            if (!subscribed) return;
            subscribed = false;
            if (bridge != null)
            {
                bridge.PunchResolved -= OnPunch;
                bridge.ImpactReceived -= OnImpact;
                bridge.LaunchReceived -= OnLaunch;
                bridge.PillarImpactReceived -= OnPillar;
                bridge.ExplosionReceived -= OnExplosion;
                bridge.ProjectileBurstReceived -= OnProjectileImpact;
                bridge.GameplaySfxReceived -= OnWorldCue;
            }
            if (controller != null) controller.DashStarted -= OnDash;
            if (health != null) health.DamageAccepted -= OnHurt;
            StopCombat();
            clearVoice.Stop();
        }

        private void LateUpdate()
        {
            if (!subscribed || bridge == null) return;
            if (restart != GameRestartRegistry.Sequence)
            {
                restart = GameRestartRegistry.Sequence;
                StopCombat();
                clearVoice.Stop();
            }
            bool blocked = FeedbackTimeController.IsSuspended || !bridge.isActiveAndEnabled;
            if (blocked && !suspended) StopCombat();
            suspended = blocked;
            if (completion != GauntletCompletionRegistry.Sequence)
            {
                completion = GauntletCompletionRegistry.Sequence;
                if (!FeedbackTimeController.IsMenuPreview)
                    Play(clearVoice, profile.LevelClear, .62f);
            }
            if (CanPlay) FlushImpacts();
            collisionScore = chainScore = 0f;
        }

        private void StopCombat()
        {
            punchVoice.Stop();
            hurtVoice.Stop();
            dashVoice.Stop();
            foreach (var voice in worldVoices) voice.Stop();
            nextImpact = nextChain = nextHurt = nextLaunch = 0;
            System.Array.Clear(nextCue, 0, nextCue.Length);
            collisionScore = chainScore = 0f;
        }

        private void OnPunch(uint sequence, bool hit)
        {
            if (sequence != bridge.PunchSequence || sequence == punchSequence) return;
            punchSequence = sequence;
            if (CanPlay) Play(punchVoice, hit ? profile.PunchConfirmed : profile.PunchMiss, hit ? .8f : .43f);
        }

        private void OnDash()
        {
            if (CanPlay) Play(dashVoice, profile.PlayerDash, .45f);
        }

        private void OnHurt(Vector3 _)
        {
            double now = Time.unscaledTimeAsDouble;
            if (!CanPlay || now < nextHurt) return;
            nextHurt = now + .25;
            Play(hurtVoice, profile.PlayerHurt, .68f);
        }

        private void OnLaunch(Vector3 position, Vector3 direction, Vector3 size)
        {
            double now = Time.unscaledTimeAsDouble;
            if (!CanPlay || now < nextLaunch) return;
            if (PlayWorld(profile.LaunchDeparture, position, .32f, .5f)) nextLaunch = now + .085;
        }

        private void OnImpact(CombatFeedbackMessage message)
        {
            if (!CanPlay || message.Kind != CombatImpactKind.EnemyCollision) return;
            float score = message.Intensity * Attenuation(message.Position);
            if (score > collisionScore) { collisionScore = score; collision = message; }
            if (message.PlayerOwned && message.ChainDepth >= feedback.ExceptionalChainDepth
                && message.Intensity >= feedback.CollisionCameraThreshold && score > chainScore)
            {
                chainScore = score;
                chain = message;
            }
        }

        private void FlushImpacts()
        {
            double now = Time.unscaledTimeAsDouble;
            if (chainScore > 0f && now >= nextChain && PlayWorld(profile.ExceptionalChain, chain.Position, .6f, 2f))
            {
                nextChain = now + feedback.ExceptionalCooldown;
                nextImpact = now + .085;
            }
            if (collisionScore > .04f && now >= nextImpact
                && PlayWorld(profile.CrowdCollision, collision.Position, .35f + .22f * collision.Intensity, collisionScore))
                nextImpact = now + .085;
        }

        private void OnPillar(Vector3 position, Vector3 direction, float width, bool bossHit)
        {
            if (CanPlay) PlayWorld(bossHit ? profile.PillarBossHit : profile.PillarLanding, position, .7f, 2f);
        }

        private void OnExplosion(Vector3 position, float radius, float impulse, float intensity)
        {
            if (CanPlay) PlayWorld(profile.ExploderDetonation, position, .6f, 2f);
        }

        private void OnProjectileImpact(ProjectileVisualKind kind, Vector3 position, Vector3 direction, float scale)
        {
            if (kind == ProjectileVisualKind.Ranged) OnWorldCue(GameplaySfxCue.RangerImpact, position);
        }

        private readonly double[] nextCue = new double[(int)GameplaySfxCue.Count];

        private void OnWorldCue(GameplaySfxCue cue, Vector3 position)
        {
            double now = Time.unscaledTimeAsDouble;
            if (!CanPlay || now < nextCue[(int)cue]) return;
            var clips = cue switch
            {
                GameplaySfxCue.GoblinGrunt => profile.GoblinGrunt,
                GameplaySfxCue.LinkedPairAttack => profile.LinkedPairAttack,
                GameplaySfxCue.EliteLaunch => profile.EliteLaunch,
                GameplaySfxCue.DasherRoll => profile.DasherRoll,
                GameplaySfxCue.ArmorBlock => profile.ArmorBlock,
                GameplaySfxCue.ArmorBreak => profile.ArmorBreak,
                GameplaySfxCue.RangerCast => profile.RangerCast,
                GameplaySfxCue.RangerImpact => profile.RangerImpact,
                GameplaySfxCue.FlowerWindup => profile.FlowerWindup,
                GameplaySfxCue.FlowerSporeBurst => profile.FlowerSporeBurst,
                GameplaySfxCue.SquidInk => profile.SquidInk,
                GameplaySfxCue.BlobDetach => profile.BlobDetach,
                GameplaySfxCue.BlobBounce => profile.BlobBounce,
                GameplaySfxCue.ExploderWarning => profile.ExploderWarning,
                GameplaySfxCue.GoblinWizardCharge => profile.GoblinWizardCharge,
                GameplaySfxCue.GoblinWizardCast => profile.GoblinWizardCast,
                GameplaySfxCue.GoblinWizardTeleport => profile.GoblinWizardTeleport,
                GameplaySfxCue.ArmoredBossMovement => profile.ArmoredBossMovement,
                GameplaySfxCue.SpikedTurtleRoll => profile.SpikedTurtleRoll,
                GameplaySfxCue.SpikedTurtleWallHit => profile.SpikedTurtleWallHit,
                GameplaySfxCue.BarricadeHit => profile.BarricadeHit,
                GameplaySfxCue.BarricadeBreak => profile.BarricadeBreak,
                _ => null
            };
            if (PlayWorld(clips, position, .46f, 1f)) nextCue[(int)cue] = now + .17;
        }

        private float Attenuation(Vector3 position) =>
            Mathf.Clamp01(1f - Vector3.Distance(position, bridge.transform.position) / WorldRange);

        private bool PlayWorld(AudioClip[] clips, Vector3 position, float volume, float priority)
        {
            float gain = Attenuation(position);
            if (gain <= 0f || clips == null || clips.Length == 0) return false;
            priority *= gain;
            int slot = -1;
            for (int i = 0; i < worldVoices.Length; i++)
            {
                if (!worldVoices[i].isPlaying) { slot = i; break; }
                if (worldPriorities[i] < priority && (slot < 0 || worldPriorities[i] < worldPriorities[slot])) slot = i;
            }
            if (slot < 0) return false;
            var source = worldVoices[slot];
            source.transform.position = position;
            worldPriorities[slot] = priority;
            return Play(source, clips, volume * gain);
        }

        private static bool Play(AudioSource source, AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return false;
            // Use audio's clock to vary takes without perturbing Unity's shared gameplay RNG.
            int index = (int)(AudioSettings.dspTime * 1000.0 % clips.Length);
            var clip = clips[index];
            if (clip == null) return false;
            source.clip = clip;
            source.volume = volume;
            source.Play();
            return true;
        }
    }
}
