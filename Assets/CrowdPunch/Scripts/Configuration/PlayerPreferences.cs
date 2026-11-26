using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CrowdPunch.Configuration
{
    internal enum PlayerPreference
    {
        MouseSensitivity, ControllerSensitivity, ReverseMouse, ReverseController, StickDeadZone,
        Master, Music, Effects, CrowdImpacts, MenuSounds,
        CameraMotion, ReducedFlashes,
        VSync, FrameLimit, WindowMode, Width, Height,
        RenderScale, AntiAliasing, ReducedEffects
    }

    internal static class PlayerPreferences
    {
        private const string Prefix = "CrowdPunch.Settings.";
        private static readonly int?[] Values = new int?[(int)PlayerPreference.ReducedEffects + 1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => System.Array.Clear(Values, 0, Values.Length);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStartup()
        {
            AudioListener.volume = Gain(PlayerPreference.Master);
            PlayerDisplayPreferences.ApplySaved();
        }

        internal static int Default(PlayerPreference option) => option switch
        {
            PlayerPreference.MouseSensitivity or PlayerPreference.ControllerSensitivity or PlayerPreference.Master
                or PlayerPreference.Music or PlayerPreference.Effects or PlayerPreference.CrowdImpacts
                or PlayerPreference.MenuSounds or PlayerPreference.RenderScale => 100,
            PlayerPreference.StickDeadZone => Mathf.RoundToInt(InputSystem.settings.defaultDeadzoneMin * 100),
            PlayerPreference.VSync or PlayerPreference.CameraMotion => 1,
            PlayerPreference.AntiAliasing => 2,
            PlayerPreference.FrameLimit or PlayerPreference.WindowMode => -1,
            _ => 0
        };

        internal static int Get(PlayerPreference option) => Values[(int)option] ??= Load(option);

        private static int Load(PlayerPreference option)
        {
            string key = Prefix + option;
            if (option == PlayerPreference.CameraMotion && !PlayerPrefs.HasKey(key)
                && (PlayerPrefs.GetInt(Prefix + "ReducedMotion") == 1 || PlayerPrefs.GetInt(Prefix + "ImpactMotion", 100) == 0))
            {
                PlayerPrefs.SetInt(key, 0);
                return 0;
            }
            int value = PlayerPrefs.GetInt(key, Default(option));
            // Old saves used -1 to inherit the quality preset. The shipped preset enabled VSync.
            if (option == PlayerPreference.VSync && value != 0 && value != 1)
            {
                value = Default(option);
                PlayerPrefs.SetInt(key, value);
            }
            return value;
        }

        internal static bool Enabled(PlayerPreference option) => Get(option) == 1;

        internal static float Gain(PlayerPreference option) => Get(option) / 100f;

        internal static Vector2 ReadStick(InputAction action, Vector2 value, out InputDevice device)
        {
            var control = action?.activeControl;
            device = control?.device;
            if (action == null || Get(PlayerPreference.StickDeadZone) == Default(PlayerPreference.StickDeadZone)) return value;
            if (control is StickControl stick && device is Gamepad) return ScaleStick(stick.ReadUnprocessedValue());
            if (control != null) return value;
            // The Input System can leave the action idle below its built-in dead zone.
            foreach (var bound in action.controls)
            {
                if (bound is not StickControl candidate || candidate.device is not Gamepad) continue;
                Vector2 raw = ScaleStick(candidate.ReadUnprocessedValue());
                if (raw.sqrMagnitude <= value.sqrMagnitude) continue;
                value = raw;
                device = candidate.device;
            }
            return value;
        }

        private static Vector2 ScaleStick(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= 0f) return Vector2.zero;
            return value / magnitude * Mathf.InverseLerp(Gain(PlayerPreference.StickDeadZone),
                InputSystem.settings.defaultDeadzoneMax, magnitude);
        }

        internal static void Set(PlayerPreference option, int value)
        {
            if (value == Get(option)) return;
            Values[(int)option] = value;
            PlayerPrefs.SetInt(Prefix + option, value);
            if (option == PlayerPreference.Master) AudioListener.volume = Gain(option);
        }

        internal static string LoadBindings(string name) => PlayerPrefs.GetString(Prefix + "Binding." + name);

        internal static void SaveBindings(string name, string json) => PlayerPrefs.SetString(Prefix + "Binding." + name, json);

        internal static void Save() => PlayerPrefs.Save();
    }
}
