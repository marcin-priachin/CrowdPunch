using System.Linq;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    internal static class PlayerDisplayPreferences
    {
        internal static Mode DefaultMode { get; private set; }
        internal readonly struct Mode
        {
            internal readonly FullScreenMode Window;
            internal readonly int Width, Height;

            internal Mode(FullScreenMode window, int width, int height)
            {
                Window = window;
                Width = width;
                Height = height;
            }

            internal static Mode Current => new Mode(Screen.fullScreenMode, Screen.width, Screen.height);

            internal void Apply() => Screen.SetResolution(Width, Height, Window);

            internal void Save()
            {
                PlayerPreferences.Set(PlayerPreference.WindowMode, (int)Window);
                PlayerPreferences.Set(PlayerPreference.Width, Width);
                PlayerPreferences.Set(PlayerPreference.Height, Height);
                PlayerPreferences.Save();
            }
        }

        internal static Vector2Int[] Resolutions()
        {
            var desktop = Screen.currentResolution;
            return Screen.resolutions.Select(r => new Vector2Int(r.width, r.height))
                .Append(new Vector2Int(Screen.width, Screen.height))
                .Append(new Vector2Int(desktop.width, desktop.height))
                .Where(r => r.x > 0 && r.y > 0 && r.x <= desktop.width && r.y <= desktop.height)
                .Distinct().OrderBy(r => r.x).ThenBy(r => r.y).ToArray();
        }

        internal static void ApplySaved()
        {
            DefaultMode = Mode.Current;
            ApplyTiming();
            int window = PlayerPreferences.Get(PlayerPreference.WindowMode);
            int width = PlayerPreferences.Get(PlayerPreference.Width);
            int height = PlayerPreferences.Get(PlayerPreference.Height);
            if (window == (int)FullScreenMode.FullScreenWindow)
                new Mode(FullScreenMode.FullScreenWindow, Screen.currentResolution.width, Screen.currentResolution.height).Apply();
            else if (window == (int)FullScreenMode.Windowed && Resolutions().Contains(new Vector2Int(width, height)))
                new Mode(FullScreenMode.Windowed, width, height).Apply();
        }

        internal static void ApplyTiming()
        {
            QualitySettings.vSyncCount = PlayerPreferences.Get(PlayerPreference.VSync);
            Application.targetFrameRate = PlayerPreferences.Get(PlayerPreference.FrameLimit);
        }
    }
}
