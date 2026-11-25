using System;
using System.Linq;
using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    internal sealed class RingsideSettings
    {
        private static readonly string[] Tabs = { "Controls", "Audio", "Accessibility", "Display" };
        private static readonly string[] DisplayModes = { "Windowed", "Borderless Fullscreen" };
        private static readonly int[] FrameRates = { -1, 30, 60, 90, 120, 144, 165, 240 };
        private static readonly PlayerPreference[] Audio = { PlayerPreference.Master, PlayerPreference.Music,
            PlayerPreference.Effects, PlayerPreference.CrowdImpacts, PlayerPreference.MenuSounds };
        private readonly RingsideSettingsLayout view;
        private readonly SettingsBindings bindings;
        private readonly Action close;
        private readonly Action<PlayerPreference> preview;
        private readonly string openingHint;
        private readonly bool authoredHintsDisabled;
        private int tab, bindingAction = -1, bindingOrigin;
        private bool controller, usingGamepad, capturing, navigationWasEnabled;
        private int capturedFrame = -1;
        private Button editing;
        private Action<bool> finishEdit;
        private Navigation editNavigation;
        private Action dismissModal;
        private PlayerDisplayPreferences.Mode pendingMode, previousMode;
        private double confirmUntil;
        private bool resetDisplayMode;

        internal Button First => view.First;
        private bool Large => PlayerPreferences.Enabled(PlayerPreference.LargeText);

        internal RingsideSettings(RectTransform viewport, SettingsBindings bindings, bool gamepad, string hint,
            bool authoredHintsDisabled, Action close, Action<PlayerPreference> preview)
        {
            this.bindings = bindings;
            this.close = close;
            this.preview = preview;
            openingHint = hint;
            this.authoredHintsDisabled = authoredHintsDisabled;
            usingGamepad = controller = gamepad;
            pendingMode = PlayerDisplayPreferences.Mode.Current;
            view = new RingsideSettingsLayout(viewport, Tabs, SelectTab);
            Build();
        }

        internal void Tick(bool gamepad)
        {
            usingGamepad = gamepad;
            if (capturedFrame >= 0 && capturedFrame < Time.frameCount)
            {
                if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = navigationWasEnabled;
                capturedFrame = -1;
            }
            view.Fit();
            if (editing != null && EventSystem.current?.currentSelectedGameObject != editing.gameObject) FinishEdit(true);
            if (gamepad && !view.HasModal && EventSystem.current?.currentSelectedGameObject == null) view.First.Select();
            if (confirmUntil > 0)
            {
                double remaining = confirmUntil - Time.unscaledTimeAsDouble;
                if (remaining <= 0) FinishDisplay(false);
                else view.ModalText($"KEEP CHANGES?\nReverting in {Mathf.CeilToInt((float)remaining)} seconds");
            }
            UpdatePrompts();
        }

        internal void Back()
        {
            if (capturedFrame == Time.frameCount) return;
            if (dismissModal != null) dismissModal();
            else if (editing != null) FinishEdit(false);
            else if (bindingAction >= 0) CloseBindings();
            else close();
        }

        internal void Dispose()
        {
            if (capturing) bindings.Cancel();
            if (capturedFrame >= 0 && EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = navigationWasEnabled;
            if (confirmUntil > 0) previousMode.Apply();
            FinishEdit(true);
            PlayerPreferences.Save();
        }

        private void SelectTab(int index, bool focusRows)
        {
            FinishEdit(true);
            tab = index;
            bindingAction = -1;
            Build(focusRows ? 0 : -1);
        }

        private void Build(int focusRow = -1)
        {
            view.BeginTab(tab);
            switch (tab)
            {
                case 0: BuildControls(); break;
                case 1: BuildAudio(); break;
                case 2: BuildAccessibility(); break;
                case 3: BuildDisplay(); break;
            }
            if (bindingAction < 0)
            {
                view.AddFooter("Reset " + Tabs[tab], 527, 219, ResetTab);
                if (tab == 3 && HasPendingDisplay()) view.AddFooter("Apply", 761, 207, ApplyDisplay, true);
            }
            view.AddFooter("Back", 1001, 184, Back);
            view.Link(focusRow >= 0 ? view.RowAt(focusRow) : null);
            UpdatePrompts();
        }

        private void BuildControls()
        {
            if (bindingAction >= 0)
            {
                view.AddNote(SettingsBindings.ActionLabels[bindingAction] + " / " + (controller ? "Controller" : "Keyboard & Mouse"), Large);
                int ordinal = 0;
                foreach (int slot in bindings.Slots(bindingAction, controller))
                {
                    if (bindings.IsFixed(bindingAction, slot))
                    {
                        view.AddNote("Mouse movement rotates the camera. You can also bind rotation keys below.", Large);
                        continue;
                    }
                    var row = view.AddRow(bindings.Label(bindingAction, slot, ordinal++), bindings.Display(bindingAction, slot),
                        () => Capture(slot), out _, Large);
                    row.gameObject.name = "Binding " + slot;
                }
                return;
            }

            view.AddRow("Input Device", controller ? "Controller" : "Keyboard & Mouse", () =>
            {
                controller = !controller;
                Build(0);
            }, out _, Large);
            if (bindings != null)
            {
                for (int i = 0; i < SettingsBindings.ActionLabels.Length; i++)
                {
                    int action = i;
                    string current = string.Join(" / ", bindings.Slots(action, controller).Take(action == 0 && !controller ? 8 : 1)
                        .Where((_, slot) => action != 0 || controller || slot % 2 == 0).Select(slot => bindings.Display(action, slot)));
                    if (current.Length == 0) continue;
                    view.AddRow(SettingsBindings.ActionLabels[action], current + "  >", () =>
                    {
                        bindingOrigin = view.RowIndex(EventSystem.current?.currentSelectedGameObject?.GetComponent<Button>());
                        bindingAction = action;
                        Build(0);
                    }, out _, Large);
                }
            }
            Range("Camera Sensitivity", controller ? PlayerPreference.ControllerSensitivity : PlayerPreference.MouseSensitivity, 25, 250);
            Toggle("Reverse Camera Rotation", controller ? PlayerPreference.ReverseController : PlayerPreference.ReverseMouse);
            if (controller) Range("Stick Dead Zone", PlayerPreference.StickDeadZone, 0, 40);
        }

        private void CloseBindings()
        {
            bindingAction = -1;
            Build(bindingOrigin);
        }

        private void Capture(int slot)
        {
            int row = view.RowIndex(EventSystem.current?.currentSelectedGameObject?.GetComponent<Button>());
            view.ShowModal($"{SettingsBindings.ActionLabels[bindingAction].ToUpperInvariant()}\n" +
                (controller ? "Move a stick or press a controller button" : "Press the new key or mouse button"), null, null, bindings.Cancel);
            dismissModal = bindings.Cancel;
            capturing = true;
            navigationWasEnabled = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            bindings.Begin(bindingAction, slot, controller, view.IsCancelTarget, message =>
            {
                capturing = false;
                capturedFrame = Time.frameCount;
                dismissModal = null;
                view.CloseModal();
                Build(row);
                view.Status(message);
            });
        }

        private void BuildAudio()
        {
            string[] names = { "Master", "Music", "Sound Effects", "Crowd Impacts", "Menu Sounds" };
            for (int i = 0; i < Audio.Length; i++) Range(names[i], Audio[i], 0, 100, true);
        }

        private void BuildAccessibility()
        {
            Range("Impact Camera Motion", PlayerPreference.ImpactMotion, 0, 100);
            Toggle("Reduced Camera Motion", PlayerPreference.ReducedMotion);
            Toggle("Reduced Flashes", PlayerPreference.ReducedFlashes);
            Toggle("Text Size", PlayerPreference.LargeText, "Large", "Standard", 3);
            bool show = HintsEnabled(authoredHintsDisabled);
            view.AddRow("Opening Hints", show ? "On" : "Off", () =>
            {
                Save(PlayerPreference.HideHints, show ? 1 : 0);
                Build(4);
            }, out _, Large);
            if (!string.IsNullOrEmpty(openingHint))
                view.AddRow("Read Opening Hint", ">", () =>
                {
                    void CloseHint() { dismissModal = null; view.CloseModal(); view.RowAt(5)?.Select(); }
                    dismissModal = CloseHint;
                    view.ShowModal(openingHint, null, null, CloseHint);
                }, out _, Large);
        }

        internal static bool HintsEnabled(bool authoredDisabled)
        {
            int preference = PlayerPreferences.Get(PlayerPreference.HideHints);
            return preference < 0 ? !authoredDisabled : preference == 0;
        }

        private void BuildDisplay()
        {
            int mode = pendingMode.Window == FullScreenMode.Windowed ? 0 : 1;
            Choice("Window Mode", DisplayModes, mode, index =>
            {
                if (index == mode) return;
                var resolution = index == 0 ? new Vector2Int(pendingMode.Width, pendingMode.Height)
                    : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
                pendingMode = new PlayerDisplayPreferences.Mode(index == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow,
                    resolution.x, resolution.y);
                resetDisplayMode = false;
                Build(0);
            });

            if (mode == 0)
            {
                var options = PlayerDisplayPreferences.Resolutions();
                int closest = Mathf.Max(0, Array.IndexOf(options, new Vector2Int(pendingMode.Width, pendingMode.Height)));
                if (options.Length > 0)
                    Choice("Resolution", options.Select(size => $"{size.x} x {size.y}").ToArray(), closest, index =>
                    {
                        if (index == closest) return;
                        pendingMode = new PlayerDisplayPreferences.Mode(FullScreenMode.Windowed, options[index].x, options[index].y);
                        resetDisplayMode = false;
                        Build(1);
                    });
            }
            else view.AddNote("Borderless uses the desktop resolution.", Large);

            int sync = PlayerPreferences.Get(PlayerPreference.VSync);
            Choice("VSync", new[] { "Default", "Off", "On" }, Mathf.Clamp(sync + 1, 0, 2), value =>
            {
                Save(PlayerPreference.VSync, value - 1);
                PlayerDisplayPreferences.ApplyTiming();
            });
            Choice("Frame Rate Limit", FrameRates.Select(rate => rate < 0 ? "Uncapped" : rate + " FPS").ToArray(),
                Mathf.Max(0, Array.IndexOf(FrameRates, PlayerPreferences.Get(PlayerPreference.FrameLimit))), value =>
                {
                    Save(PlayerPreference.FrameLimit, FrameRates[value]);
                    PlayerDisplayPreferences.ApplyTiming();
                });
            view.AddNote("VSync takes priority over the frame rate limit.", Large);
        }

        private bool HasPendingDisplay() => !pendingMode.Equals(displayApplied ?? PlayerDisplayPreferences.Mode.Current);

        private void ApplyDisplay()
        {
            previousMode = PlayerDisplayPreferences.Mode.Current;
            pendingMode.Apply();
            confirmUntil = Time.unscaledTimeAsDouble + 15;
            dismissModal = () => FinishDisplay(false);
            view.ShowModal("KEEP CHANGES?\nReverting in 15 seconds", "Keep", () => FinishDisplay(true), () => FinishDisplay(false));
        }

        private void FinishDisplay(bool keep)
        {
            confirmUntil = 0;
            dismissModal = null;
            view.CloseModal();
            if (keep && resetDisplayMode)
            {
                Save(PlayerPreference.WindowMode, -1);
                Save(PlayerPreference.Width, 0);
                Save(PlayerPreference.Height, 0);
            }
            else if (keep) pendingMode.Save();
            else { previousMode.Apply(); pendingMode = previousMode; }
            resetDisplayMode = false;
            // Screen.SetResolution takes effect at the end of the frame.
            displayApplied = pendingMode;
            Build(0);
            displayApplied = null;
        }

        private PlayerDisplayPreferences.Mode? displayApplied;

        private void ResetTab()
        {
            FinishEdit(true);
            switch (tab)
            {
                case 0:
                    Reset(PlayerPreference.MouseSensitivity, PlayerPreference.ControllerSensitivity, PlayerPreference.ReverseMouse,
                        PlayerPreference.ReverseController, PlayerPreference.StickDeadZone);
                    bindings?.Reset();
                    break;
                case 1: Reset(Audio); break;
                case 2:
                    Reset(PlayerPreference.ImpactMotion, PlayerPreference.ReducedMotion, PlayerPreference.ReducedFlashes,
                        PlayerPreference.LargeText, PlayerPreference.HideHints);
                    break;
                case 3:
                    Reset(PlayerPreference.VSync, PlayerPreference.FrameLimit);
                    PlayerDisplayPreferences.ApplyTiming();
                    pendingMode = PlayerDisplayPreferences.DefaultMode;
                    resetDisplayMode = true;
                    break;
            }
            Build();
            if (tab == 3 && HasPendingDisplay()) ApplyDisplay();
            else if (tab == 3) FinishDisplay(true);
            else view.Status(Tabs[tab] + " reset to defaults");
        }

        private void Toggle(string name, PlayerPreference option, string on = "On", string off = "Off", int rebuildRow = -1)
        {
            Text value = null;
            view.AddRow(name, PlayerPreferences.Enabled(option) ? on : off, () =>
            {
                bool enabled = !PlayerPreferences.Enabled(option);
                Save(option, enabled ? 1 : 0);
                if (rebuildRow >= 0) Build(rebuildRow);
                else value.text = enabled ? on : off;
            }, out value, Large);
        }

        private void Range(string name, PlayerPreference option, int min, int max, bool audio = false)
        {
            Text label = null;
            Slider slider = null;
            int original = 0;
            int lastSample = -1;
            double nextSample = 0;
            void Change(int value)
            {
                PlayerPreferences.Set(option, value);
                label.text = value + "%";
                slider.SetValueWithoutNotify(value);
                double now = Time.unscaledTimeAsDouble;
                if (audio && now >= nextSample && value != lastSample)
                {
                    preview(option);
                    nextSample = now + .25;
                    lastSample = value;
                }
            }
            Button row = null;
            row = view.AddRow(name, PlayerPreferences.Get(option) + "%", () =>
            {
                if (editing == row) { FinishEdit(true); return; }
                original = PlayerPreferences.Get(option);
                Edit(row, accept =>
                {
                    if (!accept) Change(original);
                    PlayerPreferences.Save();
                });
            }, out label, Large);
            slider = view.AddSlider(row, min, max, PlayerPreferences.Get(option), Large, value => Change((int)value));
            RingsideSettingsLayout.Hook(slider, EventTriggerType.PointerUp, _ => PlayerPreferences.Save());
            EditArrows(row, step => Change(Mathf.Clamp(PlayerPreferences.Get(option) + step * (option == PlayerPreference.StickDeadZone ? 1 : 5), min, max)));
        }

        private void Choice(string name, string[] options, int initial, Action<int> commit)
        {
            int current = initial;
            Text value = null;
            Button row = null;
            row = view.AddRow(name, Format(), () =>
            {
                if (editing == row) { FinishEdit(true); return; }
                int original = current;
                Edit(row, accept =>
                {
                    if (accept) commit(current);
                    else current = original;
                    value.text = Format();
                });
            }, out value, Large);
            view.AddArrows(row, step =>
            {
                if (editing != null) FinishEdit(true);
                current = (current + step + options.Length) % options.Length;
                value.text = Format();
                commit(current);
            });
            EditArrows(row, step =>
            {
                current = (current + step + options.Length) % options.Length;
                value.text = Format();
            });
            string Format() => options[current];
        }

        private void EditArrows(Button row, Action<int> change) => RingsideSettingsLayout.Hook(row, EventTriggerType.Move, data =>
        {
            if (editing != row || data is not AxisEventData axis) return;
            if (axis.moveDir is MoveDirection.Left or MoveDirection.Right) change(axis.moveDir == MoveDirection.Left ? -1 : 1);
        });

        private void Edit(Button row, Action<bool> finish)
        {
            FinishEdit(true);
            editing = row;
            editNavigation = row.navigation;
            row.navigation = new Navigation { mode = Navigation.Mode.None };
            finishEdit = finish;
            view.Status("Adjust the value, then confirm or cancel.");
        }

        private void FinishEdit(bool accept)
        {
            if (editing == null) return;
            editing.navigation = editNavigation;
            editing = null;
            view.Status("");
            finishEdit(accept);
        }

        private void UpdatePrompts() => view.Prompts(editing != null
            ? (usingGamepad ? "LEFT / RIGHT  Adjust     A  Confirm     B  Cancel" : "LEFT / RIGHT  Adjust     ENTER  Confirm     ESC  Cancel")
            : (usingGamepad ? "D-PAD  Move     A  Select     B  Back" : "ARROWS  Move     ENTER  Select     ESC  Back"));

        private static void Save(PlayerPreference option, int value)
        {
            PlayerPreferences.Set(option, value);
            PlayerPreferences.Save();
        }

        private static void Reset(params PlayerPreference[] options)
        {
            foreach (var option in options) PlayerPreferences.Set(option, PlayerPreferences.Default(option));
            PlayerPreferences.Save();
        }
    }
}
