using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>Creates one enemy at this transform from the selected spawn-settings profile.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredEnemySpawnPointAuthoring : MonoBehaviour
    {
        [SerializeField, Tooltip("Creates one enemy at this transform using the selected existing spawn-settings profile.")]
        private EnemySpawnSettings settings;

        public EnemySpawnSettings Settings => settings;

        private void OnDrawGizmos()
        {
            Gizmos.color = GetGizmoColor();
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        }

        private Color GetGizmoColor()
        {
            if (settings == null)
            {
                return new Color(1f, 0.85f, 0.15f, 0.75f);
            }

            return settings.Archetype switch
            {
                EnemyArchetype.Ranged => new Color(0.15f, 0.65f, 1f, 0.75f),
                EnemyArchetype.Explosive => new Color(1f, 0.25f, 0.05f, 0.75f),
                EnemyArchetype.Dasher => new Color(0.55f, 0.55f, 0.62f, 0.75f),
                EnemyArchetype.Elite => new Color(0.8f, 0.2f, 0.85f, 0.85f),
                EnemyArchetype.Baseline => new Color(0.25f, 0.8f, 0.65f, 0.75f),
                _ => new Color(0.25f, 0.8f, 0.65f, 0.75f)
            };
        }
    }
}
