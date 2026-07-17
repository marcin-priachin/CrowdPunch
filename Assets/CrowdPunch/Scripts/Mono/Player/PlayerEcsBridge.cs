using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Process-local registry for the single hybrid player bridge used by ECS bridge systems.
    /// </summary>
    public static class PlayerBridgeRegistry
    {
        private static PlayerEcsBridge activeBridge;

        /// <summary>
        /// Registers the active player bridge for ECS input bridge systems.
        /// </summary>
        public static void Register(PlayerEcsBridge bridge)
        {
            activeBridge = bridge;
        }

        /// <summary>
        /// Clears the active player bridge when its scene object is destroyed.
        /// </summary>
        public static void Unregister(PlayerEcsBridge bridge)
        {
            if (activeBridge == bridge)
            {
                activeBridge = null;
            }
        }

        /// <summary>
        /// Attempts to read the currently registered player bridge.
        /// </summary>
        public static bool TryGetBridge(out PlayerEcsBridge bridge)
        {
            bridge = activeBridge;
            return bridge != null;
        }
    }

    /// <summary>
    /// Dedicated MonoBehaviour-to-ECS bridge for player state and punch requests.
    /// </summary>
    public sealed class PlayerEcsBridge : MonoBehaviour
    {
        /// <summary>Latest player position published by MonoBehaviour player code.</summary>
        public float3 Position { get; private set; }

        /// <summary>Latest player forward direction published by MonoBehaviour player code.</summary>
        public float3 Forward { get; private set; }

        /// <summary>Approximate player radius for ECS distance checks.</summary>
        public float Radius { get; private set; }

        /// <summary>Latest player health value published by MonoBehaviour player code.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Latest player max health value published by MonoBehaviour player code.</summary>
        public float MaxHealth { get; private set; }

        /// <summary>Whether a punch request is waiting for ECS consumption.</summary>
        public bool HasPendingPunch { get; private set; }

        /// <summary>Monotonic request id used to distinguish repeated punches.</summary>
        public uint PunchSequence { get; private set; }

        /// <summary>Latest punch origin.</summary>
        public float3 PunchOrigin { get; private set; }

        /// <summary>Latest punch direction.</summary>
        public float3 PunchDirection { get; private set; }

        /// <summary>Latest punch radius.</summary>
        public float PunchRadius { get; private set; }

        /// <summary>Latest punch range.</summary>
        public float PunchRange { get; private set; }

        /// <summary>Latest punch strength.</summary>
        public float PunchStrength { get; private set; }

        /// <summary>Latest punch damage.</summary>
        public float PunchDamage { get; private set; }

        /// <summary>How much punch impulse direction comes from enemy position relative to the punch origin.</summary>
        public float PunchPushDirectionPositionWeight { get; private set; }

        /// <summary>
        /// Publishes current player transform data for ECS systems.
        /// </summary>
        public void PublishPlayerSnapshot(Vector3 position, Vector3 forward, float radius)
        {
            Position = new float3(position.x, position.y, position.z);
            Forward = new float3(forward.x, forward.y, forward.z);
            Radius = radius;
        }

        /// <summary>
        /// Publishes current player health for ECS presentation or gameplay systems.
        /// </summary>
        public void PublishPlayerHealth(float currentHealth, float maxHealth)
        {
            MaxHealth = Mathf.Max(0f, maxHealth);
            CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        }

        /// <summary>
        /// Publishes a punch request for ECS systems to consume once.
        /// </summary>
        public void PublishPunch(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float range,
            float strength,
            float damage,
            float pushDirectionPositionWeight)
        {
            PunchOrigin = new float3(origin.x, origin.y, origin.z);
            PunchDirection = new float3(direction.x, direction.y, direction.z);
            PunchRadius = radius;
            PunchRange = range;
            PunchStrength = strength;
            PunchDamage = Mathf.Max(0f, damage);
            PunchPushDirectionPositionWeight = Mathf.Clamp01(pushDirectionPositionWeight);
            PunchSequence++;
            HasPendingPunch = true;
        }

        /// <summary>
        /// Marks the current punch request as consumed by ECS bridge code.
        /// </summary>
        public void ClearPunch()
        {
            HasPendingPunch = false;
        }
    }
}
