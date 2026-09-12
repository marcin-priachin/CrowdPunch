using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Sidekick has no whole-body tint property. Append one shared transparent overlay
    // only during accepted damage; never instantiate or edit the authored materials.
    internal sealed class PlayerDamageFlash
    {
        private readonly Renderer[] renderers;
        private readonly Material[][] original, overlay;
        private readonly Material material;
        private bool showing;
        public PlayerDamageFlash(Transform player, Material material)
        {
            this.material = material;
            renderers = player.GetComponentsInChildren<Renderer>(true);
            original = new Material[renderers.Length][];
            overlay = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                original[i] = renderers[i].sharedMaterials;
                overlay[i] = new Material[original[i].Length + 1];
                System.Array.Copy(original[i], overlay[i], original[i].Length);
                overlay[i][original[i].Length] = material;
            }
        }
        public void Show(Color color, float amount)
        {
            color.a = Mathf.Clamp01(amount) * .65f;
            material.SetColor("_BaseColor", color);
            if (showing) return;
            showing = true;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = overlay[i];
        }
        public void Clear()
        {
            if (!showing) return;
            showing = false;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = original[i];
        }
    }
}
