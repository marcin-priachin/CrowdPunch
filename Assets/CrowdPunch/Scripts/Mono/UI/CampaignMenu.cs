using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Campaign menu routing and pause ownership (LOOP-005/006/007).</summary>
    public sealed class CampaignMenu : MonoBehaviour
    {
        private enum Page { Main, Browser, Chapters, Pause, ConfirmNew, Milestone, Failure }

        private GauntletSequence sequence;
        private GameObject root;
        private Image shade;
        private CanvasGroup controls, healthHud;
        private Transform panel;
        private Text hint;
        private Button first;
        private ArenaBrowser browser;
        private Page page, browserOrigin;
        private int encounter;
        private CampaignScreen observedScreen;
        private bool observedFailure, gamepad;
        private uint entry;
        private float hintRemaining;
        private InputAction previous, next, jump, back;
        private AudioSource menuAudio;
        private GameObject lastSelection;
        public bool IsOpen => root != null && root.activeSelf;

        public void Configure(GauntletSequence value)
        {
            sequence = value;
            gameObject.AddComponent<LevelScoreFeedback>().Configure(sequence, this);
            healthHud = GetHudGroup(GetComponentInChildren<PlayerHealthBar>());
            var restart = GetComponentInChildren<RestartGameButton>();
            if (restart != null) restart.gameObject.SetActive(false);
            hint = PauseMenu.CreateLabel(transform, string.Empty, 25, 64);
            hint.name = "Campaign Opening Hint";
            CelComicUi.AddInkShadow(hint, 2);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 1);
            hint.rectTransform.anchoredPosition = new Vector2(0, -48);
            hint.rectTransform.sizeDelta = new Vector2(1120, 64);
            hint.gameObject.SetActive(false);

            root = PauseMenu.CreateUiObject("Campaign Menu", transform);
            CelComicUi.Stretch((RectTransform)root.transform);
            shade = root.AddComponent<Image>();
            controls = root.AddComponent<CanvasGroup>();
            menuAudio = gameObject.AddComponent<AudioSource>();
            menuAudio.playOnAwake = false;
            menuAudio.ignoreListenerPause = true;
            menuAudio.volume = .28f;

            previous = MenuAction("Previous Encounter", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            next = MenuAction("Next Encounter", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            jump = MenuAction("Jump Chapter", "<Keyboard>/y", "<Gamepad>/buttonNorth");
            back = MenuAction("Menu Back", "<Gamepad>/buttonEast");
            gamepad = Gamepad.current != null;
            InputSystem.onActionChange += TrackInputDevice;

            observedScreen = sequence.Screen;
            Show(Page.Main);
        }

        public void Tick(InputAction pause)
        {
            if (sequence.TransitionInProgress && !sequence.LoadingMenuPreview)
            {
                Close();
                return;
            }
            if (entry != sequence.LevelEntrySequence)
            {
                entry = sequence.LevelEntrySequence;
                hint.text = sequence.OpeningHint;
                hint.rectTransform.anchoredPosition = new Vector2(0, sequence.CurrentLevelIndex == 80 ? -105 : -48);
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
                else if (observedScreen == CampaignScreen.ReplayComplete) OpenBrowser(Page.Main);
                else Show(Page.Milestone);
            }
            if (sequence.RunFailed && !observedFailure)
            {
                observedFailure = true;
                Show(Page.Failure);
            }
            hintRemaining = Mathf.Max(0, hintRemaining - Time.deltaTime);
            hint.gameObject.SetActive(!sequence.HintsDisabled && !IsOpen && hintRemaining > 0 && !string.IsNullOrEmpty(hint.text));

            if (IsOpen)
            {
                root.transform.SetAsLastSibling();
                controls.interactable = !sequence.LoadingMenuPreview;
                if (!controls.interactable) return;
                var selection = EventSystem.current?.currentSelectedGameObject;
                if (selection != lastSelection && selection != null)
                    PlaySound(CelComicUi.Theme?.MoveSound);
                lastSelection = selection;
                if (page == Page.Browser)
                {
                    if (previous.WasPressedThisFrame()) Browse(-1);
                    else if (next.WasPressedThisFrame()) Browse(1);
                    else if (jump.WasPressedThisFrame()) OpenChapters();
                }
                if (pause?.WasPressedThisFrame() == true || back.WasPressedThisFrame()) GoBack();
            }
            else if (pause?.WasPressedThisFrame() == true)
            {
                Show(Page.Pause);
            }
        }

        private void Show(Page nextPage)
        {
            page = nextPage;
            root.SetActive(true);
            FeedbackTimeController.SetPaused(sequence.Screen != CampaignScreen.MainMenu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetHudVisible(false);
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                Destroy(panel.gameObject);
            }
            browser = null;
            first = null;

            bool showArena = page == Page.Browser;
            shade.color = showArena || page == Page.Main ? Color.clear : CelComicUi.Shade;
            if (showArena)
            {
                panel = PauseMenu.CreateUiObject("Arena Browser", root.transform).transform;
                CelComicUi.Stretch((RectTransform)panel);
                browser = new ArenaBrowser(panel, sequence, encounter, OpenChapters, GoBack, ConfirmSound);
                browser.SetPrompts(gamepad);
                first = browser.First;
                if (sequence.Screen == CampaignScreen.ReplayComplete) BuildReplayResult();
            }
            else
            {
                panel = CreatePagePanel();
                BuildPage();
                ShowErrors();
            }

            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(first == null ? null : first.gameObject);
            lastSelection = EventSystem.current?.currentSelectedGameObject;
        }

        private Transform CreatePagePanel()
        {
            bool main = page == Page.Main;
            bool pause = page == Page.Pause;
            Vector2 anchor = main ? new Vector2(0, .5f) : new Vector2(.5f, .5f);
            float width = main ? 600 : pause ? 540 : 680;
            var rect = ArenaBrowser.Anchor("Campaign Panel", root.transform, anchor,
                main ? new Vector2(64, 0) : Vector2.zero, new Vector2(width, main ? 750 : 920));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = main ? new Color32(23, 28, 42, 235) : CelComicUi.Ink;
            image.raycastTarget = false;
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(38, 38, 30, 30);
            layout.spacing = 10;
            layout.childAlignment = main ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            if (pause)
            {
                var fit = rect.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return rect;
        }

        private void BuildPage()
        {
            switch (page)
            {
                case Page.Main:
                    Title("CROWD", 112, 104);
                    Title("PUNCH", 112, 104).color = CelComicUi.Gold;
                    if (sequence.EditorPreview) Label("EDITOR PREVIEW  /  SAVE ISOLATED", 24);
                    bool finished = sequence.NextUnfinished >= sequence.LevelCount;
                    bool available = sequence.CanSelect(sequence.NextUnfinished);
                    Button(finished ? "Replay Campaign" : available ? "Continue" : "Next Chapter Not Yet Available",
                        () => { if (finished) OpenBrowser(Page.Main); else sequence.ContinueCampaign(); },
                        finished || available, primary: true);
                    Button("Arena Browser", () => OpenBrowser(Page.Main));
                    Button("New Campaign", () => Show(Page.ConfirmNew), !sequence.EditorPreview);
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.Chapters:
                    BuildChapterPicker();
                    break;
                case Page.Pause:
                    Title("PAUSED");
                    Button("Resume", Close, primary: true);
                    Button("Retry Level", sequence.RestartCurrentLevel);
                    Button("Arena Browser", () => OpenBrowser(Page.Pause));
                    Button("Main Menu", ReturnToMain);
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.ConfirmNew:
                    Title("NEW CAMPAIGN?");
                    Label("This clears saved level completion, best scores and chapter unlocks.", 28);
                    Button("Cancel", () => Show(Page.Main), primary: true);
                    Button("Clear Progress and Start", () =>
                    {
                        sequence.NewCampaign();
                        if (!sequence.TransitionInProgress) Show(Page.ConfirmNew);
                    });
                    break;
                case Page.Milestone:
                    bool campaignComplete = sequence.Screen == CampaignScreen.CampaignComplete;
                    Title(campaignComplete ? "CAMPAIGN COMPLETE" : $"CHAPTER {sequence.Campaign.ChapterFor(sequence.CurrentLevelIndex) + 1} COMPLETE");
                    if (sequence.LastClearScore.IsScored) Label(LevelScoreFeedback.Format(sequence.LastClearScore), 27);
                    bool nextAvailable = sequence.CanSelect(sequence.CurrentLevelIndex + 1);
                    if (!campaignComplete)
                        Button(nextAvailable ? "Continue" : "Next Chapter Not Yet Available",
                            () => sequence.SelectLevel(sequence.CurrentLevelIndex + 1), nextAvailable, primary: true);
                    Button("Arena Browser", () => OpenBrowser(Page.Milestone), primary: campaignComplete);
                    Button("Main Menu", ReturnToMain);
                    break;
                case Page.Failure:
                    Title("LEVEL FAILED");
                    Label("Completed levels and chapter unlocks are saved.", 26);
                    Button("Retry Level", sequence.RestartCurrentLevel, primary: true);
                    Button("Arena Browser", () => OpenBrowser(Page.Failure));
                    Button("Main Menu", ReturnToMain);
                    break;
            }
        }

        private void BuildChapterPicker()
        {
            Title("JUMP CHAPTER", 57, 67);
            var catalog = sequence.Campaign;
            int selectedChapter = catalog.ChapterFor(encounter);
            for (int i = 0; i < catalog.chapterNames.Length; i++)
            {
                int chapter = i;
                int start = catalog.ChapterStart(chapter);
                bool available = catalog.Get(start)?.Available == true;
                bool unlocked = sequence.CanSelect(start);
                string state = !available ? "  /  Unavailable" : !unlocked ? "  /  Locked" : string.Empty;
                var button = Button($"{i + 1:00}   {catalog.chapterNames[i]}{state}", () =>
                {
                    if (chapter != selectedChapter)
                    {
                        int unfinished = sequence.NextUnfinished;
                        encounter = unfinished >= start && unfinished < catalog.ChapterEnd(chapter) ? unfinished : start;
                    }
                    Show(Page.Browser);
                }, unlocked, primary: chapter == selectedChapter);
                if (chapter == selectedChapter && unlocked) first = button;
            }
            Button("Back", () => Show(Page.Browser));
        }

        private void BuildReplayResult()
        {
            var result = ArenaBrowser.Anchor("Replay Result", panel, new Vector2(0, 1),
                new Vector2(68, -48), new Vector2(600, 238));
            var layout = result.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4;
            panel = result;
            Title("REPLAY COMPLETE", 42, 50);
            if (sequence.LastClearScore.IsScored)
            {
                Label(LevelScoreFeedback.Format(sequence.LastClearScore), 23, 31);
                var best = sequence.Progress.GetBestClear(sequence.Campaign.Get(sequence.CurrentLevelIndex).id);
                if (best.IsScored) Label("BEST  /  " + LevelScoreFeedback.Format(best), 23, 31);
            }
            Button("Retry", sequence.RestartCurrentLevel);
            Button("Next Level", () => sequence.SelectLevel(sequence.CurrentLevelIndex + 1),
                sequence.CanSelect(sequence.CurrentLevelIndex + 1));
            panel = result.parent;
        }

        private void OpenBrowser(Page origin)
        {
            browserOrigin = origin;
            int preferred = origin == Page.Main && sequence.Screen != CampaignScreen.ReplayComplete
                ? sequence.NextUnfinished : sequence.CurrentLevelIndex;
            encounter = Mathf.Clamp(preferred, 0, sequence.LevelCount - 1);
            Show(Page.Browser);
        }

        private void Browse(int direction)
        {
            int before = browser.Index;
            browser.Step(direction);
            if (before != browser.Index) PlaySound(CelComicUi.Theme?.MoveSound);
        }

        private void OpenChapters()
        {
            encounter = browser.Index;
            Show(Page.Chapters);
        }

        private void GoBack()
        {
            switch (page)
            {
                case Page.Pause: Close(); break;
                case Page.Browser:
                    if (sequence.Screen == CampaignScreen.ReplayComplete) ReturnToMain();
                    else Show(browserOrigin);
                    break;
                case Page.Chapters: Show(Page.Browser); break;
                case Page.ConfirmNew: Show(Page.Main); break;
            }
        }

        private void ReturnToMain()
        {
            sequence.ReturnToMenu();
            observedScreen = sequence.Screen;
            Show(Page.Main);
        }

        private void ShowErrors()
        {
            if (!string.IsNullOrEmpty(sequence.Progress.Error)) Label(sequence.Progress.Error, 21);
            if (!string.IsNullOrEmpty(sequence.LoadError)) Label(sequence.LoadError, 21);
        }

        private Text Title(string text, int size = 60, float height = 74)
        {
            var label = PauseMenu.CreateLabel(panel, text, size, height);
            CelComicUi.StyleText(label, heading: true);
            CelComicUi.AddInkShadow(label, 4);
            if (page == Page.Main) label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private Text Label(string text, int size, float height = 64)
        {
            var label = PauseMenu.CreateLabel(panel, text, size, height);
            if (page == Page.Browser) CelComicUi.AddInkShadow(label);
            return label;
        }

        private Button Button(string text, UnityAction action, bool enabled = true, bool primary = false)
        {
            var button = PauseMenu.CreateButton(panel, text, () =>
            {
                ConfirmSound();
                action();
            }, primary);
            CelComicUi.SetButtonAvailable(button, enabled, primary);
            if (page == Page.Main)
            {
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(26, 0);
            }
            if (first == null && enabled) first = button;
            return button;
        }

        private void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            FeedbackTimeController.SetPaused(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EventSystem.current?.SetSelectedGameObject(null);
            SetHudVisible(true);
        }

        private void SetHudVisible(bool visible)
        {
            if (healthHud != null) healthHud.alpha = visible ? 1 : 0;
        }

        private static CanvasGroup GetHudGroup(Component component)
        {
            if (component == null) return null;
            var group = component.GetComponent<CanvasGroup>();
            return group != null ? group : component.gameObject.AddComponent<CanvasGroup>();
        }

        private void TrackInputDevice(object source, InputActionChange change)
        {
            if (!IsOpen || change != InputActionChange.ActionPerformed || source is not InputAction action) return;
            if (action.activeControl?.device is not InputDevice device) return;
            bool pad = device is Gamepad;
            if (gamepad == pad || !pad && device is not (Keyboard or Mouse)) return;
            gamepad = pad;
            browser?.SetPrompts(gamepad);
        }

        private static InputAction MenuAction(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            action.Enable();
            return action;
        }

        private void ConfirmSound() => PlaySound(CelComicUi.Theme?.ConfirmSound);

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && menuAudio.isActiveAndEnabled) menuAudio.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            InputSystem.onActionChange -= TrackInputDevice;
            previous?.Dispose();
            next?.Dispose();
            jump?.Dispose();
            back?.Dispose();
            if (menuAudio != null) Destroy(menuAudio);
            FeedbackTimeController.SetPaused(false);
        }
    }
}
