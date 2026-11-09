using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.UI;
using UnityEngine;

namespace CrowdPunch.Mono.Camera
{
    /// <summary>Frames the finishing subject in the part of the arena exposed by the results rail.</summary>
    internal sealed class LevelClearCamera
    {
        private readonly UnityEngine.Camera view;
        private readonly GauntletSequence sequence;
        private bool framing;
        private Pose start;
        private Vector3 trackedFocus;
        private Quaternion trackedOrbit;
        private double startedAt;

        internal LevelClearCamera(UnityEngine.Camera view, GauntletSequence sequence)
        {
            this.view = view;
            this.sequence = sequence;
        }

        internal bool TryFrame(Vector3 orbitOffset, Vector3 player, out Vector3 subject)
        {
            subject = default;
            var settings = sequence != null ? sequence.RuntimeSettings : null;
            if (settings == null || !settings.LevelClearCameraEnabled || !sequence.ShowingClear
                || GauntletCompletionRegistry.Focus is not Vector3 focus || view == null || orbitOffset.sqrMagnitude < .01f)
            {
                framing = false;
                return false;
            }

            double now = Time.unscaledTimeAsDouble;
            Transform camera = view.transform;
            bool starting = !framing;
            if (starting)
            {
                framing = true;
                start = new Pose(camera.position, camera.rotation);
                startedAt = now;
                trackedFocus = focus;
                trackedOrbit = Quaternion.identity;
            }

            float tracking = settings.LevelClearCameraTrackingSeconds;
            float follow = tracking > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / tracking) : 1f;
            trackedFocus = Vector3.Lerp(trackedFocus, focus, follow);
            subject = trackedFocus + Vector3.up * settings.LevelClearCameraSubjectHeight;
            Vector2 viewport = settings.LevelClearCameraFraming;
            viewport.x = Mathf.Lerp(ArenaEdge(view.aspect, viewport.y, sequence.Screen), 1f, viewport.x);
            Vector3 offset = orbitOffset * settings.LevelClearCameraDistance;
            Pose shot = Compose(view, subject, offset, viewport);
            Quaternion orbit = settings.LevelClearCameraIncludePlayer
                ? FindPlayerOrbit(view, shot, subject, player, viewport, sequence.Screen)
                : Quaternion.identity;
            trackedOrbit = Quaternion.Slerp(trackedOrbit, orbit, follow);
            float duration = settings.LevelClearCameraBlendSeconds;
            float blend = duration > 0f ? Mathf.SmoothStep(0f, 1f, (float)(now - startedAt) / duration) : 1f;
            var focused = new Pose(Vector3.Lerp(start.position, shot.position, blend),
                Quaternion.Slerp(start.rotation, shot.rotation, blend));
            Pose framed = Orbit(focused, subject, Quaternion.Slerp(Quaternion.identity, trackedOrbit, blend));
            camera.SetPositionAndRotation(framed.position, framed.rotation);
            return true;
        }

        internal static Pose Compose(UnityEngine.Camera view, Vector3 subject, Vector3 offset, Vector2 viewport)
        {
            Quaternion rotation = Quaternion.LookRotation(-offset);
            float halfHeight = view.orthographic ? view.orthographicSize
                : offset.magnitude * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            Vector2 shift = (viewport * 2f - Vector2.one) * halfHeight;
            return new Pose(subject + offset - rotation * new Vector3(shift.x * view.aspect, shift.y, 0f), rotation);
        }

        internal static Quaternion FindPlayerOrbit(UnityEngine.Camera view, Pose shot, Vector3 subject,
            Vector3 player, Vector2 viewport, CampaignScreen screen)
        {
            Quaternion best = Quaternion.identity;
            if (Fits(best)) return best;
            bool hasVisiblePlayer = InArena(view, shot, player, screen, out _);
            Vector3 sightline = subject - shot.position;
            Vector3 towardPlayer = player - subject;
            Vector3 horizontal = Vector3.ProjectOnPlane(towardPlayer, Vector3.up);
            if (horizontal.sqrMagnitude < .0001f) return best;
            float bearing = Vector3.SignedAngle(Vector3.ProjectOnPlane(sightline, Vector3.up), horizontal, Vector3.up);
            float turn = bearing < 0f ? -1f : 1f;
            for (int step = 1; step <= 36; step++)
            {
                float yaw = step * 5f * turn;
                var first = Quaternion.AngleAxis(yaw, Vector3.up);
                if (Fits(first)) return first;
                var second = Quaternion.AngleAxis(-yaw, Vector3.up);
                if (Fits(second)) return second;
            }
            if (hasVisiblePlayer) return best;

            // For a distant player, the original downward angle can put the horizon offscreen.
            // Ease the orbit toward the player's sightline while keeping the camera elevated.
            float pitch = Mathf.Clamp(Mathf.Atan2(-towardPlayer.y, horizontal.magnitude) * Mathf.Rad2Deg, 5f, 80f);
            Vector3 direction = horizontal.normalized * Mathf.Cos(pitch * Mathf.Deg2Rad)
                - Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad);
            Quaternion aligned = Quaternion.LookRotation(direction) * Quaternion.Inverse(Quaternion.LookRotation(sightline));
            for (int step = 4; step >= 1; step--)
            {
                float yaw = step * 2.5f * turn;
                var first = Quaternion.AngleAxis(yaw, Vector3.up) * aligned;
                if (Fits(first)) return first;
                var second = Quaternion.AngleAxis(-yaw, Vector3.up) * aligned;
                if (Fits(second)) return second;
            }
            return InArena(view, Orbit(shot, subject, aligned), player, screen, out _) ? aligned : best;

            bool Fits(Quaternion candidate) => InArena(view, Orbit(shot, subject, candidate), player, screen, out var projected)
                && (projected - viewport).sqrMagnitude >= .035f * .035f;
        }

        internal static Pose Orbit(Pose shot, Vector3 subject, Quaternion orbit) =>
            new Pose(subject + orbit * (shot.position - subject), orbit * shot.rotation);

        private static bool InArena(UnityEngine.Camera view, Pose pose, Vector3 point, CampaignScreen screen, out Vector2 projected)
        {
            Vector3 local = Quaternion.Inverse(pose.rotation) * (point - pose.position);
            projected = default;
            if (local.z <= view.nearClipPlane || local.z >= view.farClipPlane) return false;
            float halfHeight = view.orthographic ? view.orthographicSize
                : local.z * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float y = .5f + local.y / (2f * halfHeight);
            float x = .5f + local.x / (2f * halfHeight * view.aspect);
            projected = new Vector2(x, y);
            float edge = ArenaEdge(view.aspect, y, screen);
            float margin = (1f - edge) * .12f;
            return y >= .12f && y <= .88f && x >= edge + margin && x <= 1f - margin;
        }

        private static float ArenaEdge(float aspect, float height, CampaignScreen screen) =>
            screen == CampaignScreen.ReplayComplete ? 0f
                : RingsideBackdrop.ArenaEdge(aspect, height, screen == CampaignScreen.LevelComplete);
    }
}
