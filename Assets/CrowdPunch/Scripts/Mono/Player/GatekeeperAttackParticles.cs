using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Bounded cartoon dust pool. All clocks and hand snapshots are presentation-only.
    internal sealed class GatekeeperAttackParticles
    {
        private readonly ParticleSystem[] dust = new ParticleSystem[6];
        private readonly BossHandPhase[] phases = new BossHandPhase[2];
        private readonly uint[] sequences = new uint[2];
        private readonly float[] nextDust = new float[2];
        private readonly bool[] tracked = new bool[2];
        private readonly Transform root;
        private readonly ComicImpactPool impacts;
        private readonly CombatFeedbackSettings settings;
        private readonly bool ownsSettings;

        public GatekeeperAttackParticles(Transform parent, Material material)
        {
            root = new GameObject("Gatekeeper cartoon impacts").transform;
            root.SetParent(parent, false);
            settings = Resources.Load<CombatFeedbackSettings>("CombatFeedbackSettings");
            if (settings == null) { settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>(); ownsSettings = true; }
            impacts = new ComicImpactPool(root, settings, material);
            var prefab = Resources.Load<ParticleSystem>("GatekeeperImpact");
            if (prefab != null)
                for (int i = 0; i < dust.Length; i++)
                {
                    dust[i] = Object.Instantiate(prefab, root);
                    dust[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
        }
        public void ShowHand(int slot, Vector3 target, Vector3 position, Vector3 direction,
            BossAttack attack, BossHandPhase phase, uint sequence, float groundHeight)
        {
            bool sameAttack = tracked[slot] && sequences[slot] == sequence;
            if (sameAttack && phases[slot] == BossHandPhase.Active && phase == BossHandPhase.Recovery)
            {
                position.y = groundHeight + .12f;
                if (attack != BossAttack.Sweep) { position.x = target.x; position.z = target.z; }
                Puff(position, Vector3.up, attack == BossAttack.Slam ? 2.2f : 1.25f);
                impacts.Show(CombatImpactKind.Punch, position + Vector3.up * .35f, direction, .65f);
            }
            if (phase == BossHandPhase.Active && attack != BossAttack.Slam && Time.time >= nextDust[slot])
            {
                position.y = groundHeight + .12f;
                Puff(position, Vector3.up, .65f);
                nextDust[slot] = Time.time + .12f;
            }
            tracked[slot] = true; sequences[slot] = sequence; phases[slot] = phase;
        }
        public void Impact(Vector3 position, Vector3 direction, float intensity)
        {
            Puff(position, direction, Mathf.Lerp(.55f, 1.1f, intensity));
            impacts.Show(CombatImpactKind.EnemyCollision, position, direction, intensity);
        }
        private void Puff(Vector3 position, Vector3 direction, float scale)
        {
            foreach (var effect in dust)
            {
                if (effect == null || effect.IsAlive(true)) continue;
                effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                    direction.sqrMagnitude > .001f ? direction.normalized : Vector3.up));
                effect.transform.localScale = Vector3.one * scale;
                effect.Play(true);
                return;
            }
        }
        public void Cancel(int slot) { tracked[slot] = false; nextDust[slot] = 0; }
        public void Tick(float deltaTime) => impacts.Tick(deltaTime, UnityEngine.Camera.main);
        public void Clear()
        {
            for (int i = 0; i < 2; i++) Cancel(i);
            foreach (var effect in dust) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impacts.Clear();
        }
        public void Dispose()
        {
            impacts.Dispose();
            if (root != null) Object.Destroy(root.gameObject);
            if (ownsSettings) Object.Destroy(settings);
        }
    }
}
