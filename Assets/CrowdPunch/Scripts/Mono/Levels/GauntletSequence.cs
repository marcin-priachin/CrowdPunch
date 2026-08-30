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
        [SerializeField] private string[] levelSceneNames;
        [SerializeField] private bool loadFirstLevelOnStart = true;

        private int currentLevelIndex = -1;
        private Scene currentLevelScene;
        private uint observedCompletionSequence;
        private bool transitionInProgress;

        private void Start()
        {
            observedCompletionSequence = GauntletCompletionRegistry.Sequence;
            if (loadFirstLevelOnStart && levelSceneNames is { Length: > 0 })
            {
                StartCoroutine(LoadLevel(0));
            }
        }

        private void Update()
        {
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
        }

        public void RestartCurrentLevel()
        {
            if (!transitionInProgress && currentLevelIndex >= 0)
            {
                StartCoroutine(LoadLevel(currentLevelIndex));
            }
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
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

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
            }

            observedCompletionSequence = GauntletCompletionRegistry.Sequence;
            Time.timeScale = previousTimeScale;
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
            PlayerEcsBridge bridge = Object.FindFirstObjectByType<PlayerEcsBridge>();
            PlayerController controller = bridge == null ? null : bridge.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.SetLevelEntryPoint(entryPoint.position, entryPoint.rotation);
            }

            GameRestartRegistry.RequestRestart();
        }
    }
}
