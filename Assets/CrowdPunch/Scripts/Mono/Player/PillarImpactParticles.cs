using CrowdPunch.Configuration;
using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Three simultaneous landings plus three boss hits fit without allocating during an impact.
    internal sealed class PillarImpactParticles : System.IDisposable
    {
        private readonly ParticleSystem[] dust = new ParticleSystem[18];
        private readonly Transform root;
        private readonly CombatFeedbackSettings impactSettings;
        private readonly ComicImpactPool impacts;

        public PillarImpactParticles(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            root = new GameObject("Pillar cartoon impacts").transform;
            root.SetParent(parent, false);
            impactSettings = Object.Instantiate(settings);
            impactSettings.ComicImpactScale *= 2.4f;
            impacts = new ComicImpactPool(root, impactSettings, material);
            var prefab = Resources.Load<ParticleSystem>("GatekeeperImpact");
            if (prefab == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                dust[i] = Object.Instantiate(prefab, root);
                dust[i].name = "Pillar cream dust " + i;
                dust[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Show(Vector3 position, Vector3 direction, float width, bool bossHit)
        {
            foreach (var effect in dust)
            {
                if (effect == null || effect.IsAlive(true)) continue;
                effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.up));
                effect.transform.localScale = Vector3.one * Mathf.Clamp(width * (bossHit ? .4f : .55f), .5f, 2.4f);
                effect.Play(true);
                break;
            }
            if (bossHit) impacts.Show(CombatImpactKind.Punch, position, direction, 1);
        }

        public void Tick(float deltaTime, UnityEngine.Camera camera) => impacts.Tick(deltaTime, camera);
        public void Clear()
        {
            foreach (var effect in dust) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impacts.Clear();
        }
        public void Dispose()
        {
            Clear(); impacts.Dispose();
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(impactSettings);
        }
    }
}
