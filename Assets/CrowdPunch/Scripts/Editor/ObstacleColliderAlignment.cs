using System;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>Keeps authored static perimeter collision on the rendered surface.</summary>
    public static class ObstacleColliderAlignment
    {
        public static void MatchRenderedMesh(GameObject obstacle)
        {
            var mesh = obstacle.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) throw new InvalidOperationException($"Obstacle '{obstacle.name}' has no rendered mesh.");
            var previous = obstacle.GetComponent<Collider>();
            var collider = previous as MeshCollider;
            if (collider == null)
            {
                collider = obstacle.AddComponent<MeshCollider>();
                if (previous != null)
                {
                    collider.sharedMaterial = previous.sharedMaterial;
                    collider.enabled = previous.enabled;
                    collider.providesContacts = previous.providesContacts;
                    collider.contactOffset = previous.contactOffset;
                    collider.includeLayers = previous.includeLayers;
                    collider.excludeLayers = previous.excludeLayers;
                    collider.layerOverridePriority = previous.layerOverridePriority;
                    UnityEngine.Object.DestroyImmediate(previous);
                }
            }
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
    }
}
