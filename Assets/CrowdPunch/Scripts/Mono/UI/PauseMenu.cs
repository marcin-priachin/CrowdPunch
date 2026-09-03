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
        private float timeScaleBeforePause = 1f;
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
                Time.timeScale = timeScaleBeforePause;
            }
        }

        private void Update()
        {
            if (pauseAction != null && pauseAction.WasPressedThisFrame())
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
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                menuRoot.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(firstButton.gameObject);
            }
            else
            {
                Time.timeScale = timeScaleBeforePause;
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
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.7f);
            panelRect.sizeDelta = new Vector2(420f, 520f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(panel.transform, "PAUSED", 42, 72f);
            firstButton = CreateButton(panel.transform, "Resume", () => SetPaused(false));
            CreateLabel(panel.transform, "SELECT LEVEL", 22, 48f);

            if (gauntletSequence != null)
            {
                for (int i = 0; i < gauntletSequence.LevelCount; i++)
                {
                    int levelIndex = i;
                    string sceneName = gauntletSequence.GetLevelName(i);
                    CreateButton(panel.transform, FormatLevelName(sceneName), () => SelectLevel(levelIndex));
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

        private static void CreateLabel(Transform parent, string value, int fontSize, float height)
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
