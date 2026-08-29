using System;
using System.Collections.Generic;
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
        public readonly struct TrajectoryPreviewSegment
        {
            public TrajectoryPreviewSegment(float3 start, float3 end)
            {
                Start = start;
                End = end;
            }

            public float3 Start { get; }
            public float3 End { get; }
        }

        private readonly List<TrajectoryPreviewSegment> trajectoryPreviewSegments = new List<TrajectoryPreviewSegment>();

        public event Action<float, float, Vector3> EnemyContactHitReceived;
        public event Action<Vector3, float, float, float> ExplosionReceived;

        /// <summary>Latest player position published by MonoBehaviour player code.</summary>
        public float3 Position { get; private set; }

        /// <summary>Latest collision-resolved player velocity.</summary>
        public float3 Velocity { get; private set; }

        /// <summary>Latest player forward direction published by MonoBehaviour player code.</summary>
        public float3 Forward { get; private set; }

        /// <summary>Approximate player radius for ECS distance checks.</summary>
        public float Radius { get; private set; }
        public uint CollisionLayer { get; private set; }
        public bool HasPendingMovement { get; private set; }
        public uint MovementSequence { get; private set; }
        public float3 MovementStart { get; private set; }
        public float3 MovementEnd { get; private set; }
        public float MovementRadius { get; private set; }
        public float MovementDeltaTime { get; private set; }
        public uint ResolvedMovementSequence { get; private set; }
        public float3 ResolvedMovementPosition { get; private set; }

        /// <summary>Latest player health value published by MonoBehaviour player code.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Latest player max health value published by MonoBehaviour player code.</summary>
        public float MaxHealth { get; private set; }

        /// <summary>Whether a punch request is waiting for ECS consumption.</summary>
        public bool HasPendingPunch { get; private set; }

        /// <summary>Whether ECS should calculate punch trajectory previews.</summary>
        public bool IsPunchPreviewAvailable { get; private set; }

        public float3 PunchPreviewOrigin { get; private set; }
        public float3 PunchPreviewDirection { get; private set; }
        public float PunchPreviewRadius { get; private set; }
        public float PunchPreviewRange { get; private set; }
        public float PunchPreviewLength { get; private set; }
        public float PunchPreviewPositionWeight { get; private set; }
        public float PunchPreviewAimAssistRange { get; private set; }
        public float PunchPreviewAimAssistMaximumAngleDegrees { get; private set; }
        public IReadOnlyList<TrajectoryPreviewSegment> TrajectoryPreviewSegments => trajectoryPreviewSegments;

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

        /// <summary>Multiplier applied only when a player punch hits an elite target.</summary>
        public float PunchEliteKnockbackMultiplier { get; private set; }

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
            CollisionLayer = 1u << gameObject.layer;
        }

        /// <summary>Submits transform-driven movement for resolution against the ECS collision world.</summary>
        public uint PublishMovement(Vector3 start, Vector3 end, float radius, float deltaTime)
        {
            MovementStart = start;
            MovementEnd = end;
            MovementRadius = Mathf.Max(0f, radius);
            MovementDeltaTime = Mathf.Max(0f, deltaTime);
            Velocity = MovementDeltaTime > 0f ? (MovementEnd - MovementStart) / MovementDeltaTime : float3.zero;
            MovementSequence++;
            HasPendingMovement = true;
            return MovementSequence;
        }

        public void ResolveMovement(uint sequence, float3 position)
        {
            if (sequence != MovementSequence)
            {
                return;
            }

            ResolvedMovementSequence = sequence;
            ResolvedMovementPosition = position;
            Position = position;
            Velocity = MovementDeltaTime > 0f ? (position - MovementStart) / MovementDeltaTime : float3.zero;
            HasPendingMovement = false;
        }

        public void ClearMovement()
        {
            HasPendingMovement = false;
            ResolvedMovementSequence = MovementSequence;
            ResolvedMovementPosition = Position;
            Velocity = float3.zero;
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
            float eliteKnockbackMultiplier,
            float damage,
            float pushDirectionPositionWeight)
        {
            PunchOrigin = new float3(origin.x, origin.y, origin.z);
            PunchDirection = new float3(direction.x, direction.y, direction.z);
            PunchRadius = radius;
            PunchRange = range;
            PunchStrength = strength;
            PunchEliteKnockbackMultiplier = Mathf.Max(0f, eliteKnockbackMultiplier);
            PunchDamage = Mathf.Max(0f, damage);
            PunchPushDirectionPositionWeight = Mathf.Clamp01(pushDirectionPositionWeight);
            PunchSequence++;
            HasPendingPunch = true;
        }

        public void PublishPunchPreview(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float range,
            float previewLength,
            float pushDirectionPositionWeight,
            float aimAssistRange,
            float aimAssistMaximumAngleDegrees)
        {
            PunchPreviewOrigin = new float3(origin.x, origin.y, origin.z);
            PunchPreviewDirection = new float3(direction.x, direction.y, direction.z);
            PunchPreviewRadius = Mathf.Max(0f, radius);
            PunchPreviewRange = Mathf.Max(0f, range);
            PunchPreviewLength = Mathf.Max(0f, previewLength);
            PunchPreviewPositionWeight = Mathf.Clamp01(pushDirectionPositionWeight);
            PunchPreviewAimAssistRange = Mathf.Max(0f, aimAssistRange);
            PunchPreviewAimAssistMaximumAngleDegrees = Mathf.Clamp(aimAssistMaximumAngleDegrees, 0f, 180f);
            IsPunchPreviewAvailable = isActiveAndEnabled;
        }

        public void ClearPunchPreview()
        {
            IsPunchPreviewAvailable = false;
            trajectoryPreviewSegments.Clear();
        }

        public void BeginTrajectoryPreview()
        {
            trajectoryPreviewSegments.Clear();
        }

        public void AddTrajectoryPreview(float3 start, float3 end)
        {
            trajectoryPreviewSegments.Add(new TrajectoryPreviewSegment(start, end));
        }

        /// <summary>
        /// Marks the current punch request as consumed by ECS bridge code.
        /// </summary>
        public void ClearPunch()
        {
            HasPendingPunch = false;
        }

        /// <summary>
        /// Receives an ECS enemy contact hit for MonoBehaviour-owned player systems.
        /// </summary>
        public void ReceiveEnemyContactHit(float damagePercent, float invincibilitySeconds, float3 pushImpulse)
        {
            EnemyContactHitReceived?.Invoke(
                Mathf.Clamp01(damagePercent),
                Mathf.Max(0f, invincibilitySeconds),
                new Vector3(pushImpulse.x, pushImpulse.y, pushImpulse.z));
        }

        /// <summary>Routes configured enemy damage through the existing player health and invulnerability path.</summary>
        public void ReceiveEnemyHit(float damageAmount, float invincibilitySeconds, float3 pushImpulse)
        {
            float damagePercent = MaxHealth <= 0f ? 0f : Mathf.Max(0f, damageAmount) / MaxHealth;
            ReceiveEnemyContactHit(damagePercent, invincibilitySeconds, pushImpulse);
        }

        /// <summary>Publishes a transient explosion presentation event without exposing enemy entities.</summary>
        public void ReceiveExplosion(float3 position, float radius, float duration, float sizeMultiplier)
        {
            ExplosionReceived?.Invoke(
                new Vector3(position.x, position.y, position.z),
                Mathf.Max(0f, radius),
                Mathf.Max(0.01f, duration),
                Mathf.Max(0f, sizeMultiplier));
        }
    }
}
