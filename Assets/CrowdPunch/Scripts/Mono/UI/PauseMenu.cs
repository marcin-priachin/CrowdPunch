using CrowdPunch.Mono.Levels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Owns pause input and the short, gamepad-navigable run menu.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string PauseActionName = "Game/Pause";

        private InputActionAsset inputActions;
        private InputAction pauseAction;
        private GauntletSequence gauntletSequence;
        private GameObject menuRoot;
        private Button firstButton;
        private Text menuTitle;
        private bool showingCompletion;
        private Text openingHint;
        private uint observedLevelEntry;
        private float hintSecondsRemaining;

        private bool isPaused;

        public void Configure(InputActionAsset actions)
        {
            if (inputActions == actions && pauseAction != null)
            {
                return;
            }

            pauseAction?.Disable();
            inputActions = actions;
            InputActionMap gameActions = inputActions == null ? null : inputActions.FindActionMap("Game");
            pauseAction = gameActions?.FindAction("Pause");
            if (pauseAction == null)
            {
                string reason = inputActions == null
                    ? "the InputActionAsset reference is missing"
                    : $"the loaded '{inputActions.name}' asset does not contain that action";
                Debug.LogError($"{nameof(PauseMenu)} requires '{PauseActionName}'; {reason}.", this);
                return;
            }

            if (isActiveAndEnabled)
            {
                pauseAction.Enable();
            }
        }

        private void Awake()
        {
            gauntletSequence = FindFirstObjectByType<GauntletSequence>();
            EnsureEventSystem();
            BuildMenu();
        }

        private void OnEnable()
        {
            pauseAction?.Enable();
        }

        private void OnDisable()
        {
            pauseAction?.Disable();
            if (isPaused)
            {
                SetPaused(false);
            }
        }

        private void OnDestroy()
        {
            if (isPaused)
            {
                CrowdPunch.Mono.Player.FeedbackTimeController.SetPaused(false);
            }
        }

        private void Update()
        {
            if (gauntletSequence != null && observedLevelEntry != gauntletSequence.LevelEntrySequence)
            {
                observedLevelEntry = gauntletSequence.LevelEntrySequence;
                openingHint.text = gauntletSequence.OpeningHint;
                hintSecondsRemaining = 10f;
            }
            hintSecondsRemaining = Mathf.Max(0, hintSecondsRemaining - Time.deltaTime);
            openingHint.gameObject.SetActive(!isPaused && hintSecondsRemaining > 0 && !string.IsNullOrEmpty(openingHint.text));
            bool complete = gauntletSequence != null && gauntletSequence.RunComplete;
            if (complete && !showingCompletion)
            {
                showingCompletion = true;
                menuTitle.text = "RUN COMPLETE";
                firstButton.GetComponentInChildren<Text>().text = "Play Again";
                SetPaused(true);
            }
            else if (!complete && showingCompletion)
            {
                showingCompletion = false;
                menuTitle.text = "PAUSED";
                firstButton.GetComponentInChildren<Text>().text = "Resume";
            }

            if (!complete && pauseAction != null && pauseAction.WasPressedThisFrame())
            {
                SetPaused(!isPaused);
            }
        }

        private void SetPaused(bool paused)
        {
            if (isPaused == paused)
            {
                return;
            }

            isPaused = paused;
            if (paused)
            {
                CrowdPunch.Mono.Player.FeedbackTimeController.SetPaused(true);
                menuRoot.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(firstButton.gameObject);
            }
            else
            {
                CrowdPunch.Mono.Player.FeedbackTimeController.SetPaused(false);
                menuRoot.SetActive(false);
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        private void SelectLevel(int levelIndex)
        {
            SetPaused(false);
            gauntletSequence?.SelectLevel(levelIndex);
        }

        private void BuildMenu()
        {
            openingHint = CreateLabel(transform, string.Empty, 20, 44f);
            openingHint.name = "Opening Hint";
            openingHint.raycastTarget = false;
            openingHint.rectTransform.anchorMin = openingHint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            openingHint.rectTransform.anchoredPosition = new Vector2(0, -36f);
            openingHint.rectTransform.sizeDelta = new Vector2(1000f, 44f);
            openingHint.gameObject.SetActive(false);
            menuRoot = CreateUiObject("Pause Menu", transform);
            RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image shade = menuRoot.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

            GameObject panel = CreateUiObject("Menu Panel", menuRoot.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 620f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            menuTitle = CreateLabel(panel.transform, "PAUSED", 42, 64f);
            firstButton = CreateButton(panel.transform, "Resume", () =>
            {
                if (gauntletSequence != null && gauntletSequence.RunComplete) SelectLevel(0);
                else SetPaused(false);
            });
            CreateLabel(panel.transform, "SELECT LEVEL", 22, 48f);

            GameObject levelGrid = CreateUiObject("Levels", panel.transform);
            GridLayoutGroup grid = levelGrid.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(343f, 50f);
            grid.spacing = new Vector2(14f, 10f);
            LayoutElement gridSize = levelGrid.AddComponent<LayoutElement>();
            int rows = Mathf.CeilToInt((gauntletSequence?.LevelCount ?? 0) / 2f);
            gridSize.preferredHeight = Mathf.Max(0f, rows * 60f - 10f);
            if (gauntletSequence != null)
            {
                for (int i = 0; i < gauntletSequence.LevelCount; i++)
                {
                    int levelIndex = i;
                    string sceneName = gauntletSequence.GetLevelName(i);
                    CreateButton(levelGrid.transform, FormatLevelName(sceneName), () => SelectLevel(levelIndex));
                }
            }

            CreateButton(panel.transform, "Exit Game", ExitGame);
            menuRoot.SetActive(false);
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject result = new GameObject(objectName, typeof(RectTransform));
            result.layer = parent.gameObject.layer;
            result.transform.SetParent(parent, false);
            return result;
        }

        private static Text CreateLabel(Transform parent, string value, int fontSize, float height)
        {
            GameObject labelObject = CreateUiObject(value, parent);
            LayoutElement element = labelObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return label;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(label, parent);
            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.preferredHeight = 58f;
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.17f, 0.24f, 0.96f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.52f, 0.78f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            GameObject textObject = CreateUiObject("Label", buttonObject.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return button;
        }

        private static string FormatLevelName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? "Unnamed Level" : sceneName.Replace('_', ' ');
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
