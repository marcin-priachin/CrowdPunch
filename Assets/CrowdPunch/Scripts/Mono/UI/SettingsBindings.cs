using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CrowdPunch.Mono.UI
{
    using CrowdPunch.Configuration;

    internal sealed class SettingsBindings
    {
        internal static readonly string[] ActionLabels = { "Move", "Rotate Camera", "Punch", "Dash", "Pause" };
        private readonly InputAction[] actions;
        private InputActionRebindingExtensions.RebindingOperation capture;

        internal SettingsBindings(InputAction move, InputAction look, InputAction punch, InputAction dash, InputAction pause)
        {
            actions = new[] { move, look, punch, dash, pause };
            for (int i = 0; i < actions.Length; i++)
            {
                string json = PlayerPreferences.LoadBindings(ActionLabels[i]);
                if (actions[i] != null && !string.IsNullOrEmpty(json)) actions[i].LoadBindingOverridesFromJson(json);
            }
        }

        internal IEnumerable<int> Slots(int actionIndex, bool controller)
        {
            var action = actions[actionIndex];
            if (action == null) yield break;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite) continue;
                string path = binding.path ?? "";
                bool keyboard = path.StartsWith("<Keyboard>/", StringComparison.Ordinal) || path.StartsWith("<Mouse>/", StringComparison.Ordinal)
                    || string.IsNullOrEmpty(path) && binding.groups == "Keyboard&Mouse";
                bool gamepad = path.StartsWith("<Gamepad>/", StringComparison.Ordinal)
                    || string.IsNullOrEmpty(path) && binding.groups == "Gamepad";
                if (controller ? gamepad : keyboard) yield return i;
            }
        }

        internal string Label(int actionIndex, int slot, int ordinal)
        {
            var binding = actions[actionIndex].bindings[slot];
            if (!binding.isPartOfComposite) return ordinal == 0 ? "Primary" : $"Alternate {ordinal}";
            string name = binding.name;
            bool alternate = slot > 0 && actions[actionIndex].bindings[slot - 1].name == name;
            return char.ToUpperInvariant(name[0]) + name.Substring(1) + (alternate ? " (alternate)" : "");
        }

        internal string Display(int actionIndex, int slot)
        {
            if (IsFixed(actionIndex, slot)) return "Mouse movement";
            return string.IsNullOrEmpty(actions[actionIndex].bindings[slot].effectivePath) ? "Unassigned"
                : actions[actionIndex].GetBindingDisplayString(slot, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        internal bool IsFixed(int actionIndex, int slot) => actions[actionIndex].bindings[slot].path == "<Mouse>/delta";

        internal void Begin(int actionIndex, int slot, bool controller, Func<Vector2, bool> cancelTarget, Action<string> finished)
        {
            var action = actions[actionIndex];
            var binding = action.bindings[slot];
            bool vector = !binding.isPartOfComposite && action.expectedControlType == "Vector2";
            bool wasEnabled = action.enabled;
            action.Disable();
            string status = "Binding updated";
            void Finish(InputActionRebindingExtensions.RebindingOperation operation, string message)
            {
                capture = null;
                operation.Dispose();
                if (wasEnabled) action.Enable();
                finished(message);
            }

            capture = action.PerformInteractiveRebinding(slot)
                .WithExpectedControlType(vector ? "Vector2" : "Button")
                .WithControlsHavingToMatchPath(controller ? "<Gamepad>" : "<Keyboard>")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Gamepad>/buttonEast")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithMatchingEventsBeingSuppressed(false)
                .OnPotentialMatch(operation =>
                {
                    if (operation.selectedControl is ButtonControl button && button.device is Mouse mouse
                        && button == mouse.leftButton && cancelTarget(mouse.position.ReadValue())) operation.RemoveCandidate(button);
                })
                .OnMatchWaitForAnother(.08f)
                .WithTimeout(8f)
                .OnGeneratePath(control => (control.device is Gamepad ? "<Gamepad>" : control.device is Mouse ? "<Mouse>" : "<Keyboard>")
                    + control.path.Substring(control.device.path.Length))
                .OnApplyBinding((_, path) =>
                {
                    var conflict = FindConflict(actionIndex, slot, path);
                    if (conflict != null) { status = $"Already assigned to {conflict}"; return; }
                    action.ApplyBindingOverride(slot, path);
                    Save(actionIndex);
                })
                .OnCancel(operation => Finish(operation, "Binding canceled"))
                .OnComplete(operation => Finish(operation, status));

            if (!controller && !binding.isPartOfComposite && !vector)
                capture.WithControlsHavingToMatchPath("<Mouse>");
            if (controller && !vector)
                capture.WithControlsExcluding("<Gamepad>/leftStick/*").WithControlsExcluding("<Gamepad>/rightStick/*");
            if (actionIndex == 4)
                capture.WithControlsExcluding("<Gamepad>/buttonSouth").WithControlsExcluding("<Keyboard>/enter")
                    .WithControlsExcluding("<Keyboard>/numpadEnter").WithControlsExcluding("<Mouse>/leftButton");
            capture.Start();
        }

        private string FindConflict(int action, int slot, string path)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) continue;
                for (int j = 0; j < actions[i].bindings.Count; j++)
                {
                    if (i == action && j == slot) continue;
                    if (string.Equals(actions[i].bindings[j].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        return ActionLabels[i];
                }
            }
            return null;
        }

        internal void Cancel() => capture?.Cancel();

        internal void Reset()
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) continue;
                actions[i].RemoveAllBindingOverrides();
                Save(i);
            }
        }

        private void Save(int actionIndex)
        {
            PlayerPreferences.SaveBindings(ActionLabels[actionIndex], actions[actionIndex].SaveBindingOverridesAsJson());
            PlayerPreferences.Save();
        }
    }
}
