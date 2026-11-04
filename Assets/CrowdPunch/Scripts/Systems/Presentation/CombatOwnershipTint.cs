using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Systems.Presentation
{
    internal static class CombatOwnershipTint
    {
        private static readonly int PlayerAccentId = Shader.PropertyToID("_PlayerAccent");
        private static readonly int EnemyAccentId = Shader.PropertyToID("_EnemyAccent");

        internal static Color Accent(EnemyLaunchOwner owner, CombatVfxLibrary library)
        {
            Color color;
            switch (owner)
            {
                case EnemyLaunchOwner.Player:
                    color = library != null ? library.PlayerOwnershipColor : CombatVfxLibrary.DefaultPlayerOwnershipColor;
                    break;
                case EnemyLaunchOwner.Enemy:
                case EnemyLaunchOwner.Boss:
                    color = library != null ? library.EnemyOwnershipColor : CombatVfxLibrary.DefaultEnemyOwnershipColor;
                    break;
                default:
                    return Color.clear;
            }
            // The picker changes RGB only. Effects keep their authored opacity.
            color.a = 1f;
            return color;
        }

        internal static float ShaderIndex(EnemyLaunchOwner owner) => owner switch
        {
            EnemyLaunchOwner.Player => 1f,
            EnemyLaunchOwner.Enemy or EnemyLaunchOwner.Boss => 2f,
            _ => 0f
        };

        internal static void SetShaderPalette(MaterialPropertyBlock properties, CombatVfxLibrary library)
        {
            properties.SetColor(PlayerAccentId, Accent(EnemyLaunchOwner.Player, library));
            properties.SetColor(EnemyAccentId, Accent(EnemyLaunchOwner.Enemy, library));
        }

        internal static Color Blend(Color original, Color accent, float strength)
        {
            accent.a *= strength;
            var tinted = Color.Lerp(original, accent, accent.a);
            tinted.a = original.a;
            return tinted;
        }
    }
}
