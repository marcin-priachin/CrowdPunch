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
        private enum Page { Main, Browser, Pause, ConfirmNew, Milestone, Failure }

        private GauntletSequence sequence;
        private GameObject root;
        private Image shade;
        private CanvasGroup controls, healthHud;
        private Transform panel;
        private Text hint;
        private RectTransform hintPanel;
        private Button first;
        private ArenaBrowser browser;
        private RingsideMenuLayout ringside;
        private Page page, browserOrigin;
        private CampaignScreen observedScreen;
        private bool observedFailure, gamepad;
        private uint entry;
        private float hintRemaining;
        private InputAction previousChapter, nextChapter, jump, back;
        private AudioSource menuAudio;
        private GameObject lastSelection;
        public bool IsOpen => root != null && root.activeSelf;

        public void Configure(GauntletSequence value)
        {
            sequence = value;
            gameObject.AddComponent<LevelScoreFeedback>().Configure(sequence, this);
            var playerHealth = GetComponentInChildren<PlayerHealthBar>();
            playerHealth?.StyleRingside();
            healthHud = GetHudGroup(playerHealth);
            var restart = GetComponentInChildren<RestartGameButton>();
            if (restart != null) restart.gameObject.SetActive(false);
            hintPanel = RingsideUi.Image(transform, "Campaign Opening Hint", RingsideUi.Hint).rectTransform;
            hintPanel.anchorMin = new Vector2(.24f, 1);
            hintPanel.anchorMax = new Vector2(.76f, 1);
            hintPanel.pivot = new Vector2(.5f, 1);
            hintPanel.anchoredPosition = new Vector2(0, -56);
            hintPanel.sizeDelta = new Vector2(0, 76);
            hint = RingsideUi.Label(hintPanel, string.Empty, 24, 76, RingsideUi.Paper);
            hint.alignment = TextAnchor.MiddleCenter;
            CelComicUi.Stretch(hint.rectTransform, 8);
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 17;
            hint.resizeTextMaxSize = 24;
            hintPanel.gameObject.SetActive(false);

            root = PauseMenu.CreateUiObject("Campaign Menu", transform);
            CelComicUi.Stretch((RectTransform)root.transform);
            shade = root.AddComponent<Image>();
            controls = root.AddComponent<CanvasGroup>();
            menuAudio = gameObject.AddComponent<AudioSource>();
            menuAudio.playOnAwake = false;
            menuAudio.ignoreListenerPause = true;
            menuAudio.volume = .28f;

            previousChapter = MenuAction("Previous Chapter", "<Keyboard>/pageUp");
            nextChapter = MenuAction("Next Chapter", "<Keyboard>/pageDown");
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
                hintPanel.anchoredPosition = new Vector2(0, sequence.CurrentLevelIndex == 80 ? -105 : -56);
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
            hintPanel.gameObject.SetActive(!sequence.HintsDisabled && !IsOpen && hintRemaining > 0 && !string.IsNullOrEmpty(hint.text));

            if (IsOpen)
            {
                root.transform.SetAsLastSibling();
                controls.interactable = !sequence.LoadingMenuPreview;
                browser?.Fit();
                ringside?.Fit();
                if (!controls.interactable) return;
                var selection = EventSystem.current?.currentSelectedGameObject;
                if (selection != lastSelection && selection != null)
                    PlaySound(CelComicUi.Theme?.MoveSound);
                lastSelection = selection;
                if (page == Page.Browser)
                {
                    if (previousChapter.WasPressedThisFrame()) browser.StepChapter(-1);
                    else if (nextChapter.WasPressedThisFrame()) browser.StepChapter(1);
                    else if (jump.WasPressedThisFrame()) browser.FocusChapters();
                }
                if (pause?.WasPressedThisFrame() == true || back.WasPressedThisFrame()) GoBack();
            }
            else if (pause?.WasPressedThisFrame() == true)
            {
                Show(Page.Pause);
            }
        }

        private void Show(Page nextPage, int encounter = 0)
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
            ringside = null;
            first = null;

            shade.color = page == Page.Browser ? new Color32(17, 18, 55, 255)
                : page == Page.ConfirmNew ? RingsideUi.Hint : Color.clear;
            if (page == Page.Browser)
            {
                var viewport = (RectTransform)PauseMenu.CreateUiObject("Level Select", root.transform).transform;
                panel = viewport;
                CelComicUi.Stretch(viewport);
                browser = new ArenaBrowser(viewport, sequence, encounter, GoBack, ConfirmSound);
                browser.SetPrompts(gamepad);
                first = browser.First;
            }
            else
            {
                var viewport = (RectTransform)PauseMenu.CreateUiObject("Ringside", root.transform).transform;
                panel = viewport;
                CelComicUi.Stretch(viewport);
                ringside = new RingsideMenuLayout(viewport, page == Page.Main, page == Page.ConfirmNew);
                BuildPage();
                ShowErrors();
            }

            Canvas.ForceUpdateCanvases();
            browser?.Fit();
            ringside?.Fit();
            EventSystem.current?.SetSelectedGameObject(first == null ? null : first.gameObject);
            lastSelection = EventSystem.current?.currentSelectedGameObject;
        }

        private void BuildPage()
        {
            switch (page)
            {
                case Page.Main:
                    if (sequence.EditorPreview) Description("EDITOR PREVIEW  /  SAVE ISOLATED", 32);
                    bool finished = sequence.NextUnfinished >= sequence.LevelCount;
                    bool available = sequence.CanSelect(sequence.NextUnfinished);
                    Button(finished ? "Replay Campaign" : available ? "Continue" : "Next Chapter Not Yet Available",
                        () => { if (finished) OpenBrowser(Page.Main); else sequence.ContinueCampaign(); },
                        finished || available, primary: true);
                    Button("Level Select", () => OpenBrowser(Page.Main));
                    Button("New Campaign", () => Show(Page.ConfirmNew), !sequence.EditorPreview);
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.Pause:
                    ringside.Heading("PAUSED", LevelTag());
                    Button("Resume", Close, primary: true);
                    Button("Retry Level", sequence.RestartCurrentLevel);
                    Button("Level Select", () => OpenBrowser(Page.Pause));
                    Button("Main Menu", ReturnToMain);
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.ConfirmNew:
                    ringside.Heading("NEW CAMPAIGN?", "CAMPAIGN");
                    Description("This clears saved level completion, best scores and chapter unlocks.", 80);
                    Button("Cancel", () => Show(Page.Main), primary: true);
                    Button("Clear Progress and Start", () =>
                    {
                        sequence.NewCampaign();
                        if (!sequence.TransitionInProgress) Show(Page.ConfirmNew);
                    }, danger: true);
                    break;
                case Page.Milestone:
                    bool campaignComplete = sequence.Screen == CampaignScreen.CampaignComplete;
                    int chapter = sequence.Campaign.ChapterFor(sequence.CurrentLevelIndex);
                    ringside.Heading(campaignComplete ? "CAMPAIGN COMPLETE" : $"CHAPTER {chapter + 1:00} COMPLETE",
                        $"LEVEL {sequence.CurrentLevelIndex + 1:00}");
                    if (sequence.LastClearScore.IsScored)
                    {
                        RingsideUi.ScoreColumns(ringside.Content, sequence.LastClearScore, 65);
                        if (sequence.LastClearIsBest)
                            RingsideUi.Label(ringside.Content, "NEW BEST", 24, 36, RingsideUi.Gold);
                    }
                    bool nextAvailable = sequence.CanSelect(sequence.CurrentLevelIndex + 1);
                    if (!campaignComplete)
                    {
                        if (nextAvailable && sequence.CurrentLevelIndex + 1 == sequence.Campaign.ChapterEnd(chapter))
                            Description($"Chapter {chapter + 2:00} unlocked.", 45);
                        Button(nextAvailable ? "Continue" : "Next Chapter Not Yet Available",
                            () => sequence.SelectLevel(sequence.CurrentLevelIndex + 1), nextAvailable, primary: true);
                    }
                    Button("Level Select", () => OpenBrowser(Page.Milestone), primary: campaignComplete);
                    Button("Main Menu", ReturnToMain);
                    break;
                case Page.Failure:
                    ringside.Heading("LEVEL FAILED", LevelTag());
                    Description("Completed levels and chapter unlocks are saved.", 52);
                    Button("Retry Level", sequence.RestartCurrentLevel, primary: true);
                    Button("Level Select", () => OpenBrowser(Page.Failure));
                    Button("Main Menu", ReturnToMain);
                    break;
            }
        }

        private void OpenBrowser(Page origin)
        {
            browserOrigin = origin;
            int preferred = origin == Page.Main && sequence.Screen != CampaignScreen.ReplayComplete
                ? sequence.NextUnfinished : sequence.CurrentLevelIndex;
            Show(Page.Browser, Mathf.Clamp(preferred, 0, sequence.LevelCount - 1));
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
            if (!string.IsNullOrEmpty(sequence.Progress.Error)) Description(sequence.Progress.Error, 64);
            if (!string.IsNullOrEmpty(sequence.LoadError)) Description(sequence.LoadError, 64);
        }

        private string LevelTag() => sequence.CurrentLevelIndex < 0 ? string.Empty
            : $"CHAPTER {sequence.Campaign.ChapterFor(sequence.CurrentLevelIndex) + 1:00} / LEVEL {sequence.CurrentLevelIndex + 1:00}";

        private void Description(string text, float height) =>
            RingsideUi.Label(ringside.Content, text, 26, height, RingsideUi.Sky);

        private void Button(string text, UnityAction action, bool enabled = true, bool primary = false, bool danger = false)
        {
            var button = RingsideUi.Action(ringside.Content, text, () =>
            {
                ConfirmSound();
                action();
            }, enabled, primary, danger: danger);
            if (first == null && enabled) first = button;
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
            previousChapter?.Dispose();
            nextChapter?.Dispose();
            jump?.Dispose();
            back?.Dispose();
            if (menuAudio != null) Destroy(menuAudio);
            FeedbackTimeController.SetPaused(false);
        }
    }
}
