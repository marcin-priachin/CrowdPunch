using System.Collections.Generic;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Authored poses only. The combat clock drives preparation and contact, never the collider.
    internal sealed class FinaleWraithAnimation
    {
        private readonly Animator animator;
        private readonly Dictionary<string, float> durations = new Dictionary<string, float>();
        private string state, recovery;
        private float recoveryUntil;
        private int previousPhase = -1;
        private uint previousAttack;
        public bool AttackPoseActive { get; private set; }

        public FinaleWraithAnimation(Animator animator)
        {
            this.animator = animator;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips) durations[clip.name] = clip.length;
        }

        public void Present(int phase, int attack, uint sequence, float progress, bool resolved, int resolvedAttack, bool damaged, bool stunned = false)
        {
            if (animator == null) return;
            bool changed = phase != previousPhase;
            if (changed) recoveryUntil = 0;
            bool preparing = attack >= 1 && attack <= 4 && phase != 4;
            if (resolved && resolvedAttack >= 1 && resolvedAttack <= 4)
            {
                recovery = Clip(resolvedAttack);
                recoveryUntil = Time.time + Duration(recovery) * (1 - Contact(resolvedAttack));
            }
            else if (phase == 2 && (changed || damaged) && !preparing && Time.time >= recoveryUntil)
            {
                recovery = "Take Damage";
                recoveryUntil = Time.time + Duration(recovery);
            }
            string clip = phase == 4 ? "Die" : phase == 1 || stunned ? "Take Damage" : preparing ? Clip(attack)
                : Time.time < recoveryUntil ? recovery : phase == 2 ? "Idle" : "Fly Forward In Place";
            AttackPoseActive = preparing || Time.time < recoveryUntil && recovery != "Take Damage";
            animator.speed = preparing || stunned || phase == 1 ? 0 : 1;
            if (phase == 1 || stunned)
                animator.Play("Take Damage", 0, phase == 1 ? Mathf.Lerp(.18f, .5f, Mathf.Clamp01(progress)) : .5f);
            else if (preparing)
            {
                // Readable early gathering pose, held anticipation, then a sharp final commitment.
                // Each clip has its own contact frame rather than sharing an arbitrary 65% sample.
                float p = Mathf.Clamp01(progress);
                float windup = p < .42f ? Mathf.SmoothStep(0, .32f, p / .42f)
                    : p < .7f ? Mathf.Lerp(.32f, .4f, (p - .42f) / .28f)
                    : Mathf.Lerp(.4f, 1, Mathf.Pow((p - .7f) / .3f, 2));
                animator.Play(clip, 0, windup * Contact(attack));
            }
            else if (resolved && resolvedAttack >= 1 && resolvedAttack <= 4)
                animator.Play(clip, 0, Contact(resolvedAttack));
            else if (clip != state || changed)
                animator.CrossFadeInFixedTime(clip, .14f, 0, 0);
            else if (damaged && clip == "Take Damage") animator.Play(clip, 0, 0);
            // Re-entering the same attack must start a fresh preparation after an interruption.
            if (preparing && sequence != previousAttack) animator.Update(0);
            state = clip; previousPhase = phase; previousAttack = sequence;
        }

        private float Duration(string clip) => durations.TryGetValue(clip, out float duration) ? duration : .8f;
        private static string Clip(int attack) => attack == 2 || attack == 3 ? "Spin Wing Slash Attack"
            : attack == 4 ? "Cast Spell" : "Projectile Attack";
        private static float Contact(int attack) => attack == 2 || attack == 3 ? .54f : attack == 4 ? .62f : .55f;
    }
}
