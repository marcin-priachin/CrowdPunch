using System.Collections;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Loads a fixed sequence of closed gauntlet scenes around the persistent Bootstrap scene.</summary>
    public sealed class GauntletSequence : MonoBehaviour
    {
#if UNITY_EDITOR
        public const string EditorStartLevelKey = "CrowdPunch.LevelLauncher.StartLevel";
#endif
        [SerializeField] private string[] levelSceneNames;
        [SerializeField] private string[] levelDisplayNames;
        [SerializeField] private bool loadFirstLevelOnStart = true;

        private int currentLevelIndex = -1;
        private Scene currentLevelScene;
        private uint observedCompletionSequence;
        private uint observedFailureSequence;
        private bool transitionInProgress;

        public int LevelCount => levelSceneNames?.Length ?? 0;
        public int CurrentLevelIndex => currentLevelIndex;
        public bool TransitionInProgress => transitionInProgress;
        public bool RunComplete { get; private set; }
        public bool RunFailed { get; private set; }
        public uint LevelEntrySequence { get; private set; }
        public string OpeningHint { get; private set; }

        private void Start()
        {
            observedCompletionSequence = GauntletCompletionRegistry.Sequence;
            observedFailureSequence = GauntletFailureRegistry.Sequence;
#if UNITY_EDITOR
            // A one-shot editor override goes through the normal additive loading path (LOOP-002/006).
            string editorStartLevel = UnityEditor.SessionState.GetString(EditorStartLevelKey, string.Empty);
            UnityEditor.SessionState.EraseString(EditorStartLevelKey);
            int editorStartIndex = levelSceneNames == null ? -1 : System.Array.IndexOf(levelSceneNames, editorStartLevel);
            if (editorStartIndex >= 0)
            {
                StartCoroutine(LoadLevel(editorStartIndex));
                return;
            }
#endif
            if (loadFirstLevelOnStart && levelSceneNames is { Length: > 0 })
            {
                StartCoroutine(LoadLevel(0));
            }
        }

        private void Update()
        {
            if (!transitionInProgress && observedFailureSequence != GauntletFailureRegistry.Sequence)
            {
                observedFailureSequence = GauntletFailureRegistry.Sequence;
                RunFailed = true;
                RunComplete = false;
            }
            if (RunFailed) return;
            uint completionSequence = GauntletCompletionRegistry.Sequence;
            if (transitionInProgress || completionSequence == observedCompletionSequence)
            {
                return;
            }

            observedCompletionSequence = completionSequence;
            if (levelSceneNames != null && currentLevelIndex + 1 < levelSceneNames.Length)
            {
                StartCoroutine(LoadLevel(currentLevelIndex + 1));
            }
            else if (currentLevelIndex >= 0)
            {
                RunComplete = true;
            }
        }

        public void RestartCurrentLevel()
        {
            if (!transitionInProgress && currentLevelIndex >= 0)
            {
                StartCoroutine(LoadLevel(currentLevelIndex));
            }
        }

        public string GetLevelName(int levelIndex)
        {
            if (levelIndex >= 0 && levelIndex < LevelCount && levelDisplayNames != null
                && levelIndex < levelDisplayNames.Length && !string.IsNullOrWhiteSpace(levelDisplayNames[levelIndex]))
                return levelDisplayNames[levelIndex];
            return levelIndex >= 0 && levelIndex < LevelCount
                ? levelSceneNames[levelIndex]
                : string.Empty;
        }

        /// <summary>Loads the selected authored gauntlet. Selecting the active gauntlet restarts it.</summary>
        public void SelectLevel(int levelIndex)
        {
            if (transitionInProgress || levelIndex < 0 || levelIndex >= LevelCount)
            {
                return;
            }

            StartCoroutine(LoadLevel(levelIndex));
        }

        private IEnumerator LoadLevel(int levelIndex)
        {
            string sceneName = levelSceneNames[levelIndex];
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"Gauntlet scene name at index {levelIndex} is empty.", this);
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Could not load gauntlet scene '{sceneName}'. Add it to Build Settings.", this);
                yield break;
            }

            transitionInProgress = true;
            RunComplete = false;
            RunFailed = false;
            FeedbackTimeController.SetTransition(true);

            if (currentLevelScene.IsValid() && currentLevelScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(currentLevelScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return load;
            currentLevelScene = SceneManager.GetSceneByName(sceneName);
            currentLevelIndex = levelIndex;

            GauntletLevel level = FindLevelMarker(currentLevelScene);
            if (level == null)
            {
                Debug.LogError($"Gauntlet scene '{sceneName}' requires one {nameof(GauntletLevel)}.", this);
            }
            else
            {
                PlacePlayer(level.PlayerEntryPoint);
                OpeningHint = level.OpeningHint;
                LevelEntrySequence++;
            }

            observedCompletionSequence = GauntletCompletionRegistry.Sequence;
            FeedbackTimeController.SetTransition(false);
            observedFailureSequence = GauntletFailureRegistry.Sequence;
            transitionInProgress = false;
        }

        private static GauntletLevel FindLevelMarker(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GauntletLevel marker = root.GetComponentInChildren<GauntletLevel>(true);
                if (marker != null)
                {
                    return marker;
                }
            }

            return null;
        }

        private static void PlacePlayer(Transform entryPoint)
        {
            PlayerEcsBridge bridge = Object.FindFirstObjectByType<PlayerEcsBridge>(FindObjectsInactive.Include);
            if (bridge == null)
            {
                Debug.LogError($"Could not place the player because no {nameof(PlayerEcsBridge)} exists.");
                GameRestartRegistry.RequestRestart();
                return;
            }

            if (!bridge.gameObject.activeSelf)
            {
                bridge.gameObject.SetActive(true);
            }

            PlayerController controller = bridge.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.SetLevelEntryPoint(entryPoint.position, entryPoint.rotation);
            }

            bridge.GetComponent<PlayerPunch>()?.ResetPunchState();
            bridge.GetComponent<PlayerHealth>()?.ResetHealth();

            GameRestartRegistry.RequestRestart();
        }
    }
}
