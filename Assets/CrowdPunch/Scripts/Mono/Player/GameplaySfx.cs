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
        private const float CrowdImpactRange = 48f;
        private const float CrowdImpactFullVolumeRange = 8f;
        private readonly AudioSource[] worldVoices = new AudioSource[8];
        private readonly float[] worldPriorities = new float[8];
        private readonly AudioSource[] crowdVoices = new AudioSource[2];
        private AudioSource punchVoice, hurtVoice, dashVoice, clearVoice;
        private DasherRollAudioPool dasherRolls;
        private Transform audioCamera;
        private int nextCrowdVoice;
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
            var camera = UnityEngine.Camera.main;
            audioCamera = camera != null ? camera.transform : null;
            if (profile.DasherRollLoop != null)
                dasherRolls = new DasherRollAudioPool(transform, player.transform, audioCamera, profile.DasherRollLoop);
            for (int i = 0; i < crowdVoices.Length; i++)
            {
                crowdVoices[i] = MakeVoice("Crowd Impact SFX", false);
                crowdVoices[i].priority = 96;
            }
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
            if (dasherRolls != null)
            {
                bridge.DasherTelegraphsBegan += dasherRolls.Begin;
                bridge.DasherRollReceived += dasherRolls.Show;
                bridge.DasherTelegraphsEnded += dasherRolls.End;
            }
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
                if (dasherRolls != null)
                {
                    bridge.DasherTelegraphsBegan -= dasherRolls.Begin;
                    bridge.DasherRollReceived -= dasherRolls.Show;
                    bridge.DasherTelegraphsEnded -= dasherRolls.End;
                }
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
                    PlayResults(profile.LevelClear, .62f);
            }
            if (CanPlay) FlushImpacts();
            collisionScore = chainScore = 0f;
        }

        private void StopCombat()
        {
            punchVoice.Stop();
            hurtVoice.Stop();
            dashVoice.Stop();
            dasherRolls?.Clear();
            foreach (var voice in crowdVoices) voice.Stop();
            foreach (var voice in worldVoices) voice.Stop();
            nextCrowdVoice = 0;
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
            float score = Mathf.Lerp(.25f, 1f, message.Intensity) * CrowdImpactGain(message.Position);
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
            if (chainScore > 0f && now >= nextChain && PlayCrowdImpact(profile.ExceptionalChain, chain.Position, .85f))
            {
                nextChain = now + feedback.ExceptionalCooldown;
                nextImpact = now + .085;
            }
            if (collisionScore > 0f && now >= nextImpact
                && PlayCrowdImpact(profile.CrowdCollision, collision.Position, Mathf.Lerp(.6f, .85f, collision.Intensity)))
                nextImpact = now + .085;
        }

        private float CrowdImpactGain(Vector3 position)
        {
            Vector3 offset = position - bridge.transform.position;
            offset.y = 0f;
            return Mathf.InverseLerp(CrowdImpactRange, CrowdImpactFullVolumeRange, offset.magnitude);
        }

        private bool PlayCrowdImpact(AudioClip[] clips, Vector3 position, float volume)
        {
            float gain = CrowdImpactGain(position);
            if (gain <= 0f) return false;
            var voice = crowdVoices[nextCrowdVoice];
            // Pan relative to the view while applying distance falloff only from the player.
            voice.panStereo = PlayerRelativePan(position);
            if (!Play(voice, clips, volume * gain)) return false;
            nextCrowdVoice = (nextCrowdVoice + 1) % crowdVoices.Length;
            return true;
        }

        private float PlayerRelativePan(Vector3 position)
        {
            Vector3 offset = position - bridge.transform.position;
            offset.y = 0f;
            return audioCamera != null ? .45f * Vector3.Dot(audioCamera.right, offset.normalized) : 0f;
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
            if (cue == GameplaySfxCue.GatekeeperDefeat)
            {
                // The regular world voices stop when the results camera takes over.
                PlayResults(profile.GatekeeperDefeat, .9f);
                return;
            }
            var clips = cue switch
            {
                GameplaySfxCue.GoblinGrunt => profile.GoblinGrunt,
                GameplaySfxCue.LinkedPairAttack => profile.LinkedPairAttack,
                GameplaySfxCue.EliteLaunch => profile.EliteLaunch,
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
                GameplaySfxCue.GatekeeperWindup => profile.GatekeeperWindup,
                GameplaySfxCue.GatekeeperSlam => profile.GatekeeperSlam,
                GameplaySfxCue.GatekeeperLunge => profile.GatekeeperLunge,
                GameplaySfxCue.GatekeeperSweep => profile.GatekeeperSweep,
                GameplaySfxCue.GatekeeperStrikeImpact => profile.GatekeeperStrikeImpact,
                GameplaySfxCue.GatekeeperMove => profile.GatekeeperMove,
                GameplaySfxCue.GatekeeperHeadHit => profile.GatekeeperHeadHit,
                GameplaySfxCue.GatekeeperHandHit => profile.GatekeeperHandHit,
                _ => null
            };
            bool gatekeeper = cue is GameplaySfxCue.GatekeeperWindup or GameplaySfxCue.GatekeeperSlam
                or GameplaySfxCue.GatekeeperLunge or GameplaySfxCue.GatekeeperSweep or GameplaySfxCue.GatekeeperMove
                or GameplaySfxCue.GatekeeperHeadHit or GameplaySfxCue.GatekeeperHandHit or GameplaySfxCue.GatekeeperStrikeImpact;
            float volume = gatekeeper ? (cue == GameplaySfxCue.GatekeeperMove ? .58f : .9f) : .46f;
            bool played = PlayWorld(clips, position, volume, gatekeeper ? 2.5f : 1f, playerRelative: gatekeeper);
            switch (cue)
            {
                case GameplaySfxCue.GatekeeperWindup:
                    played |= PlayWorld(profile.GatekeeperWindupGrunt, position, .72f, 3f, playerRelative: true, pitch: .82f);
                    break;
                case GameplaySfxCue.GatekeeperHeadHit:
                    played |= PlayWorld(profile.GatekeeperHitGrunt, position, .65f, 3f, playerRelative: true, pitch: .74f);
                    break;
                case GameplaySfxCue.GatekeeperSlam:
                    played |= PlayWorld(profile.GatekeeperStrikeImpact, position, .85f, 3f, playerRelative: true);
                    break;
            }
            if (played) nextCue[(int)cue] = now + .17;
        }

        private float Attenuation(Vector3 position) =>
            Mathf.Clamp01(1f - Vector3.Distance(position, bridge.transform.position) / WorldRange);

        private bool PlayWorld(AudioClip[] clips, Vector3 position, float volume, float priority,
            bool playerRelative = false, float pitch = 1f)
        {
            float gain = playerRelative ? CrowdImpactGain(position) : Attenuation(position);
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
            source.spatialBlend = playerRelative ? 0f : .65f;
            source.panStereo = playerRelative ? PlayerRelativePan(position) : 0f;
            source.pitch = pitch;
            worldPriorities[slot] = priority;
            return Play(source, clips, volume * gain);
        }

        private static bool Play(AudioSource source, AudioClip[] clips, float volume)
        {
            var clip = SelectClip(clips);
            if (clip == null) return false;
            source.clip = clip;
            source.volume = volume;
            source.Play();
            return true;
        }

        private void PlayResults(AudioClip[] clips, float volume)
        {
            var clip = SelectClip(clips);
            if (clip != null) clearVoice.PlayOneShot(clip, volume);
        }

        private static AudioClip SelectClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            // Use audio's clock to vary takes without perturbing Unity's shared gameplay RNG.
            return clips[(int)(AudioSettings.dspTime * 1000.0 % clips.Length)];
        }
    }
}
