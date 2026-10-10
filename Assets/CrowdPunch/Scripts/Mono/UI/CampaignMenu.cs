using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Campaign menu presentation; progression and encounter loading remain sequence-owned.</summary>
    public sealed class CampaignMenu : MonoBehaviour
    {
        private enum Page { Main, Chapters, Levels, Pause, ConfirmNew, Milestone, Failure }
        private GauntletSequence sequence;
        private GameObject root;
        private Transform panel;
        private UnityEngine.UI.Text hint;
        private UnityEngine.UI.Button first;
        private Page page;
        private int chapter;
        private CampaignScreen observedScreen;
        private bool observedFailure;
        private uint entry;
        private float hintRemaining;
        public bool IsOpen => root != null && root.activeSelf;

        public void Configure(GauntletSequence value)
        {
            sequence = value;
            hint = PauseMenu.CreateLabel(transform, string.Empty, 22, 64);
            hint.name = "Campaign Opening Hint";
            hint.raycastTarget = false;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 1);
            hint.rectTransform.anchoredPosition = new Vector2(0, -48);
            hint.rectTransform.sizeDelta = new Vector2(1120, 64);
            root = PauseMenu.CreateUiObject("Campaign Menu", transform);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .04f, .045f, .96f);
            observedScreen = sequence.Screen;
            Show(Page.Main);
        }

        public void Tick(InputAction pause)
        {
            if (IsOpen) root.transform.SetAsLastSibling();
            if (sequence.TransitionInProgress) { Close(); return; }
            if (entry != sequence.LevelEntrySequence)
            {
                entry = sequence.LevelEntrySequence;
                hint.text = sequence.OpeningHint;
                hintRemaining = 10;
                observedScreen = sequence.Screen;
                observedFailure = false;
                Close();
            }
            if (observedScreen != sequence.Screen)
            {
                observedScreen = sequence.Screen;
                if (observedScreen == CampaignScreen.Playing) Close();
                else if (observedScreen == CampaignScreen.MainMenu) Show(Page.Main);
                else if (observedScreen == CampaignScreen.ReplayComplete)
                {
                    chapter = sequence.CurrentLevelIndex / 10;
                    Show(Page.Levels);
                }
                else Show(Page.Milestone);
            }
            if (sequence.RunFailed && !observedFailure) { observedFailure = true; Show(Page.Failure); }
            hintRemaining = Mathf.Max(0, hintRemaining - Time.deltaTime);
            hint.gameObject.SetActive(!IsOpen && hintRemaining > 0 && !string.IsNullOrEmpty(hint.text));
            if (pause != null && pause.WasPressedThisFrame())
            {
                if (!IsOpen) Show(Page.Pause);
                else if (page == Page.Pause) Close();
                else if (page == Page.Levels) Show(Page.Chapters);
                else if (page == Page.Chapters || page == Page.ConfirmNew) Show(Page.Main);
            }
        }

        private void Show(Page next)
        {
            page = next;
            root.SetActive(true);
            FeedbackTimeController.SetPaused(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            panel = PauseMenu.CreateUiObject("Campaign Panel", root.transform).transform;
            var rect = (RectTransform)panel;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(860, 940);
            var layout = panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            first = null;
            switch (page)
            {
                case Page.Main:
                    Title("CROWD PUNCH");
                    Label(sequence.EditorPreview ? "EDITOR PREVIEW - campaign saves are isolated" : "CAMPAIGN", 24);
                    bool finished = sequence.NextUnfinished >= sequence.LevelCount;
                    bool available = sequence.CanSelect(sequence.NextUnfinished);
                    Button(finished ? "Campaign Complete - Replay Levels" : available ? "Continue" : "Next Chapter Not Yet Available",
                        () => { if (finished) Show(Page.Chapters); else sequence.ContinueCampaign(); }, finished || available);
                    Button("Chapter / Level Selection", () => Show(Page.Chapters));
                    Button("New Campaign", () => Show(Page.ConfirmNew), !sequence.EditorPreview);
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.Chapters:
                    Title("CHAPTERS");
                    for (int i = 0; i < sequence.Campaign.chapterNames.Length; i++)
                    {
                        int selected = i;
                        bool unlocked = sequence.EditorPreview || sequence.Progress.IsUnlocked(sequence.Campaign, i * 10);
                        bool installed = sequence.Campaign.Get(i * 10)?.Available == true;
                        Button($"{i + 1}. {sequence.Campaign.chapterNames[i]}" + (!installed ? " - Not Yet Available" : !unlocked ? " - Locked" : ""),
                            () => { chapter = selected; Show(Page.Levels); }, unlocked && installed);
                    }
                    Button("Back", () => Show(Page.Main));
                    break;
                case Page.Levels:
                    Title(sequence.Campaign.chapterNames[chapter]);
                    if (sequence.Screen == CampaignScreen.ReplayComplete)
                    {
                        Label("REPLAY COMPLETE", 22);
                        Button("Retry", sequence.RestartCurrentLevel);
                        Button("Next Level", () => sequence.SelectLevel(sequence.CurrentLevelIndex + 1), sequence.CanSelect(sequence.CurrentLevelIndex + 1));
                    }
                    var grid = PauseMenu.CreateUiObject("Campaign Levels", panel);
                    var group = grid.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                    group.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                    group.constraintCount = 2; group.cellSize = new Vector2(388, 64); group.spacing = new Vector2(12, 10);
                    grid.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 360;
                    for (int i = chapter * 10; i < Mathf.Min(chapter * 10 + 10, sequence.LevelCount); i++)
                    {
                        int selected = i;
                        var level = sequence.Campaign.Get(i);
                        var button = PauseMenu.CreateButton(grid.transform, $"{i + 1:00} {level.title}" +
                            (sequence.Progress.IsComplete(level.id) ? "  [Clear]" : sequence.CanSelect(i) ? "" : "  [Locked]"),
                            () => sequence.SelectLevel(selected));
                        button.interactable = sequence.CanSelect(i);
                        if (first == null && button.interactable) first = button;
                    }
                    Button("Chapters", () => Show(Page.Chapters));
                    Button("Main Menu", () => { sequence.ReturnToMenu(); Show(Page.Main); });
                    break;
                case Page.Pause:
                    Title("PAUSED");
                    Button("Resume", Close);
                    Button("Retry Level", sequence.RestartCurrentLevel);
                    Button("Chapter / Level Selection", () => Show(Page.Chapters));
                    Button("Main Menu", () => { sequence.ReturnToMenu(); Show(Page.Main); });
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.ConfirmNew:
                    Title("NEW CAMPAIGN?");
                    Label("This clears saved level completion and chapter unlocks.", 24);
                    Button("Cancel", () => Show(Page.Main));
                    Button("Clear Progress and Start", sequence.NewCampaign);
                    break;
                case Page.Milestone:
                    bool campaignComplete = sequence.Screen == CampaignScreen.CampaignComplete;
                    Title(campaignComplete ? "CAMPAIGN COMPLETE" : $"CHAPTER {sequence.CurrentLevelIndex / 10 + 1} COMPLETE");
                    bool nextAvailable = sequence.CanSelect(sequence.CurrentLevelIndex + 1);
                    if (!campaignComplete)
                        Button(nextAvailable ? "Continue" : "Next Chapter Not Yet Available",
                            () => sequence.SelectLevel(sequence.CurrentLevelIndex + 1), nextAvailable);
                    Button("Chapter / Level Selection", () => Show(Page.Chapters));
                    Button("Main Menu", () => { sequence.ReturnToMenu(); Show(Page.Main); });
                    break;
                case Page.Failure:
                    Title("LEVEL FAILED");
                    Label("Completed levels and chapter unlocks are saved.", 22);
                    Button("Retry Level", sequence.RestartCurrentLevel);
                    Button("Chapter / Level Selection", () => Show(Page.Chapters));
                    Button("Main Menu", () => { sequence.ReturnToMenu(); Show(Page.Main); });
                    break;
            }
            if (!string.IsNullOrEmpty(sequence.Progress.Error)) Label(sequence.Progress.Error, 18);
            if (!string.IsNullOrEmpty(sequence.LoadError)) Label(sequence.LoadError, 18);
            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(first == null ? null : first.gameObject);
        }

        private void Title(string text) => PauseMenu.CreateLabel(panel, text.ToUpperInvariant(), 36, 70);
        private void Label(string text, int size) => PauseMenu.CreateLabel(panel, text, size, 60);
        private void Button(string text, UnityEngine.Events.UnityAction action, bool enabled = true)
        {
            var button = PauseMenu.CreateButton(panel, text, action);
            button.interactable = enabled;
            if (first == null && enabled) first = button;
        }
        private void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            FeedbackTimeController.SetPaused(false);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        private void OnDestroy() => FeedbackTimeController.SetPaused(false);
    }
}
