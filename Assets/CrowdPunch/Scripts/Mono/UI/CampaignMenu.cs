using CrowdPunch.Configuration;
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
        private enum Page { Main, Browser, Pause, Settings, ConfirmNew, Milestone, Failure, LevelClear }

        private GauntletSequence sequence;
        private GameObject root;
        private Image shade;
        private CanvasGroup controls, healthHud;
        private Transform panel;
        private Button first;
        private ArenaBrowser browser;
        private RingsideMenuLayout ringside;
        private RingsideSettings settings;
        private SettingsBindings settingsBindings;
        private Page page, browserOrigin, settingsOrigin;
        private Button settingsEntry;
        private CampaignScreen observedScreen;
        private string observedLoadError;
        private bool observedFailure, gamepad;
        private uint entry;
        private InputAction previousChapter, nextChapter, jump, back;
        private AudioSource menuAudio;
        private GameObject lastSelection;
        public bool IsOpen => root != null && root.activeSelf;
        internal bool IsBrowsingLevels => IsOpen && page == Page.Browser;

        public void Configure(GauntletSequence value)
        {
            sequence = value;
            gameObject.AddComponent<LevelScoreFeedback>().Configure(sequence, this);
            var playerHealth = GetComponentInChildren<PlayerHealthBar>();
            playerHealth?.StyleRingside();
            healthHud = GetHudGroup(playerHealth);
            var restart = GetComponentInChildren<RestartGameButton>();
            if (restart != null) restart.gameObject.SetActive(false);

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
            back = MenuAction("Menu Back", "<Gamepad>/buttonEast", "<Keyboard>/escape");
            gamepad = Gamepad.current != null;
            InputSystem.onActionChange += TrackInputDevice;

            observedScreen = sequence.Screen;
            Show(Page.Main);
        }

        internal void ConfigureInput(PlayerMovementSettings movement, InputAction punch, InputAction pause)
        {
            settingsBindings = new SettingsBindings(movement?.FindMoveAction(), movement?.FindLookAction(),
                punch, movement?.FindDashAction(), pause);
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
                observedScreen = sequence.Screen;
                observedFailure = false;
                Close();
            }
            if (sequence.ShowingReplayShot && observedScreen != sequence.Screen)
            {
                Close();
                SetHudVisible(false);
            }
            else if (observedScreen != sequence.Screen)
            {
                observedScreen = sequence.Screen;
                if (observedScreen == CampaignScreen.Playing) Close();
                else if (observedScreen == CampaignScreen.MainMenu) Show(Page.Main);
                else if (observedScreen == CampaignScreen.ReplayComplete) OpenBrowser(Page.Main);
                else if (observedScreen == CampaignScreen.LevelComplete) Show(Page.LevelClear);
                else Show(Page.Milestone);
            }
            else if (sequence.Screen == CampaignScreen.MainMenu && !sequence.TransitionInProgress
                && (!IsOpen || observedLoadError != sequence.LoadError))
            {
                Show(Page.Main);
            }
            if (!sequence.TransitionInProgress) observedLoadError = sequence.LoadError;
            if (sequence.RunFailed && !observedFailure)
            {
                observedFailure = true;
                Show(Page.Failure);
            }
            if (IsOpen)
            {
                root.transform.SetAsLastSibling();
                controls.interactable = !sequence.LoadingMenuPreview;
                browser?.Fit();
                ringside?.Fit();
                if (!controls.interactable) return;
                settings?.Tick(gamepad);
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
                if (back.WasPressedThisFrame() || page != Page.Settings && pause?.WasPressedThisFrame() == true) GoBack();
            }
            else if (sequence.Screen == CampaignScreen.Playing && pause?.WasPressedThisFrame() == true)
            {
                Show(Page.Pause);
            }
        }

        private void Show(Page nextPage, int encounter = 0)
        {
            settings?.Dispose();
            settings = null;
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
            settingsEntry = null;

            shade.color = page == Page.Browser ? new Color32(17, 18, 55, 255)
                : page == Page.ConfirmNew ? RingsideUi.ConfirmationShade : Color.clear;
            if (page == Page.Browser)
            {
                var viewport = (RectTransform)PauseMenu.CreateUiObject("Level Select", root.transform).transform;
                panel = viewport;
                CelComicUi.Stretch(viewport);
                browser = new ArenaBrowser(viewport, sequence, encounter, GoBack, ConfirmSound);
                browser.SetPrompts(gamepad);
                first = browser.First;
            }
            else if (page == Page.Settings)
            {
                var viewport = (RectTransform)PauseMenu.CreateUiObject("Ringside Settings", root.transform).transform;
                panel = viewport;
                CelComicUi.Stretch(viewport);
                settings = new RingsideSettings(viewport, settingsBindings, gamepad, ReturnFromSettings, PreviewAudio);
                first = settings.First;
            }
            else
            {
                var viewport = (RectTransform)PauseMenu.CreateUiObject("Ringside", root.transform).transform;
                panel = viewport;
                CelComicUi.Stretch(viewport);
                ringside = new RingsideMenuLayout(viewport, page == Page.Main, page == Page.ConfirmNew,
                    levelClear: page == Page.LevelClear);
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
                    settingsEntry = Button("Settings", () => OpenSettings(Page.Main));
                    Button("Exit Game", PauseMenu.ExitGame);
                    break;
                case Page.Pause:
                    ringside.Heading("PAUSED", LevelTag());
                    Button("Resume", Close, primary: true);
                    Button("Retry Level", sequence.RestartCurrentLevel);
                    Button("Level Select", () => OpenBrowser(Page.Pause));
                    settingsEntry = Button("Settings", () => OpenSettings(Page.Pause));
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
                case Page.LevelClear:
                    ringside.Heading("LEVEL CLEAR!", LevelTag());
                    ShowClearScore(92);
                    Button("Next Level", () =>
                    {
                        sequence.AdvanceFromLevelClear();
                        if (!sequence.TransitionInProgress && !string.IsNullOrEmpty(sequence.LoadError))
                            Show(Page.LevelClear);
                    }, sequence.CanSelect(sequence.CurrentLevelIndex + 1), primary: true);
                    Button("Retry", sequence.RestartCurrentLevel);
                    Button("Level Select", () => OpenBrowser(Page.LevelClear));
                    Button("Main Menu", ReturnToMain);
                    break;
                case Page.Milestone:
                    bool campaignComplete = sequence.Screen == CampaignScreen.CampaignComplete;
                    int chapter = sequence.Campaign.ChapterFor(sequence.CurrentLevelIndex);
                    ringside.Heading(campaignComplete ? "CAMPAIGN COMPLETE" : $"CHAPTER {chapter + 1:00} COMPLETE",
                        $"LEVEL {sequence.CurrentLevelIndex + 1:00}");
                    ShowClearScore(65);
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
                case Page.Settings: settings.Back(); break;
                case Page.Browser:
                    if (sequence.Screen == CampaignScreen.ReplayComplete) ReturnToMain();
                    else Show(browserOrigin);
                    break;
                case Page.ConfirmNew: Show(Page.Main); break;
            }
        }

        private void OpenSettings(Page origin)
        {
            settingsOrigin = origin;
            Show(Page.Settings);
        }

        private void ReturnFromSettings()
        {
            Show(settingsOrigin);
            settingsEntry?.Select();
            lastSelection = settingsEntry != null ? settingsEntry.gameObject : null;
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

        private void ShowClearScore(int size)
        {
            if (!sequence.LastClearScore.IsScored) return;
            RingsideUi.ScoreColumns(ringside.Content, sequence.LastClearScore, size);
            if (sequence.LastClearIsBest)
                RingsideUi.Label(ringside.Content, "NEW BEST", 24, 36, RingsideUi.Gold);
        }

        private Button Button(string text, UnityAction action, bool enabled = true, bool primary = false, bool danger = false)
        {
            var button = RingsideUi.Action(ringside.Content, text, () =>
            {
                ConfirmSound();
                action();
            }, enabled, primary, size: PlayerPreferences.Enabled(PlayerPreference.LargeText) ? 51 : 42,
                danger: danger, levelClear: page == Page.LevelClear);
            if (first == null && enabled) first = button;
            return button;
        }

        private void Close()
        {
            if (!IsOpen) return;
            settings?.Dispose();
            settings = null;
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
            if (clip != null && menuAudio.isActiveAndEnabled) menuAudio.PlayOneShot(clip, PlayerPreferences.Gain(PlayerPreference.MenuSounds));
        }

        private void PreviewAudio(PlayerPreference preference)
        {
            if (preference == PlayerPreference.Music) return;
            if (preference == PlayerPreference.Master || preference == PlayerPreference.MenuSounds)
            {
                ConfirmSound();
                return;
            }
            var profile = Resources.Load<GameplaySfxProfiles>("GameplaySfxProfiles")?.Default;
            var clips = preference == PlayerPreference.CrowdImpacts ? profile?.CrowdCollision : profile?.PunchConfirmed;
            if (clips == null || clips.Length == 0 || clips[0] == null || !menuAudio.isActiveAndEnabled) return;
            float gain = PlayerPreferences.Gain(PlayerPreference.Effects);
            if (preference == PlayerPreference.CrowdImpacts) gain *= PlayerPreferences.Gain(PlayerPreference.CrowdImpacts);
            menuAudio.PlayOneShot(clips[0], gain);
        }

        private void OnDestroy()
        {
            settings?.Dispose();
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
