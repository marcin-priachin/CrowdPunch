using System.Collections;
using System.IO;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Mono.Levels
{
    public enum CampaignScreen { Playing, MainMenu, ReplayComplete, ChapterComplete, CampaignComplete }

    /// <summary>Owns additive encounter loading, completion and campaign transitions (LOOP-002/006).</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GauntletSequence : MonoBehaviour
    {
#if UNITY_EDITOR
        public const string EditorStartLevelKey = "CrowdPunch.LevelLauncher.StartLevel";
        public const string EditorLegacyKey = "CrowdPunch.LevelLauncher.Legacy";
#endif
        [SerializeField] private CampaignCatalog campaign;
        [SerializeField] private string[] levelSceneNames;
        [SerializeField] private string[] levelDisplayNames;
        [SerializeField] private bool loadFirstLevelOnStart = true;

        private int currentLevelIndex = -1;
        private Scene currentLevelScene;
        private uint observedCompletionSequence;
        private uint observedFailureSequence;
        private bool transitionInProgress;
        private bool legacyMode;
        private bool replay;
        private int editorStartIndex = -1;
        private PlayerHealth playerHealth;

        public CampaignCatalog Campaign => campaign;
        public bool HasCampaign => campaign != null && !legacyMode;
        public bool EditorPreview { get; private set; }
        public CampaignProgress Progress { get; private set; }
        public CampaignScreen Screen { get; private set; } = CampaignScreen.MainMenu;
        public string LoadError { get; private set; }
        public bool IsReplay => replay;
        public int NextUnfinished => Progress.NextUnfinished(campaign);
        public bool CanSelect(int index) => HasCampaign && campaign.Get(index)?.Available == true
            && (EditorPreview || Progress.IsUnlocked(campaign, index));

        public int LevelCount => HasCampaign ? campaign.Count : levelSceneNames?.Length ?? 0;
        public int CurrentLevelIndex => currentLevelIndex;
        public bool TransitionInProgress => transitionInProgress;
        public bool RunComplete { get; private set; }
        public bool RunFailed { get; private set; }
        public uint LevelEntrySequence { get; private set; }
        public string OpeningHint { get; private set; }

        private void Awake()
        {
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
#if UNITY_EDITOR
            legacyMode = UnityEditor.SessionState.GetBool(EditorLegacyKey, false);
            UnityEditor.SessionState.EraseBool(EditorLegacyKey);
            string requested = UnityEditor.SessionState.GetString(EditorStartLevelKey, string.Empty);
            UnityEditor.SessionState.EraseString(EditorStartLevelKey);
            if (!string.IsNullOrEmpty(requested))
            {
                EditorPreview = true;
                if (HasCampaign)
                {
                    for (int i = 0; i < campaign.Count; i++)
                        if (campaign.Get(i).scenePath == requested) editorStartIndex = i;
                }
                else editorStartIndex = levelSceneNames == null ? -1 : System.Array.IndexOf(levelSceneNames, requested);
            }
#endif
            if (HasCampaign)
                Progress = new CampaignProgress(Path.Combine(Application.persistentDataPath,
                    EditorPreview ? "campaign-editor-preview.json" : "campaign-v1.json"));
        }

        private void Start()
        {
            // Bootstrap can instantiate the GameObject player after this sequence's early Awake.
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            observedCompletionSequence = GauntletCompletionRegistry.Sequence;
            observedFailureSequence = GauntletFailureRegistry.Sequence;
            if (editorStartIndex >= 0)
            {
                StartCoroutine(LoadLevel(editorStartIndex));
                return;
            }
            if (!HasCampaign && loadFirstLevelOnStart && levelSceneNames is { Length: > 0 })
            {
                StartCoroutine(LoadLevel(0));
            }
        }

        private void Update()
        {
            if (currentLevelIndex < 0 || (HasCampaign && Screen != CampaignScreen.Playing)) return;
            if (!transitionInProgress && playerHealth != null && playerHealth.CurrentHealth <= 0)
                RunFailed = true;
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
            if (HasCampaign)
            {
                if (!EditorPreview) Progress.Complete(campaign.Get(currentLevelIndex).id);
                if (replay) Screen = CampaignScreen.ReplayComplete;
                else if (currentLevelIndex == campaign.Count - 1) Screen = CampaignScreen.CampaignComplete;
                else if ((currentLevelIndex + 1) % 10 == 0) Screen = CampaignScreen.ChapterComplete;
                else if (CanSelect(currentLevelIndex + 1)) StartCoroutine(LoadLevel(currentLevelIndex + 1));
                else Screen = CampaignScreen.ChapterComplete;
                return;
            }
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
            if (HasCampaign) return campaign.Get(levelIndex)?.title ?? string.Empty;
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
            if (HasCampaign && !CanSelect(levelIndex)) return;
            replay = HasCampaign && Progress.IsComplete(campaign.Get(levelIndex).id);
            StartCoroutine(LoadLevel(levelIndex));
        }

        public void ContinueCampaign()
        {
            if (HasCampaign && CanSelect(NextUnfinished)) SelectLevel(NextUnfinished);
        }

        public void NewCampaign()
        {
            if (!HasCampaign || transitionInProgress || EditorPreview) return;
            if (Progress.Reset()) SelectLevel(0);
        }

        public void ReturnToMenu()
        {
            if (!HasCampaign || transitionInProgress) return;
            Screen = CampaignScreen.MainMenu;
        }

        private IEnumerator LoadLevel(int levelIndex)
        {
            string sceneName = HasCampaign ? campaign.Get(levelIndex).scenePath : levelSceneNames[levelIndex];
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"Gauntlet scene name at index {levelIndex} is empty.", this);
                yield break;
            }

            bool available = Application.CanStreamedLevelBeLoaded(sceneName);
#if UNITY_EDITOR
            string editorPath = HasCampaign ? sceneName : $"Assets/CrowdPunch/Scenes/Gauntlets/{sceneName}.unity";
            available |= File.Exists(editorPath);
#endif
            if (!available)
            {
                LoadError = $"Could not load level '{sceneName}'.";
                Debug.LogError(LoadError, this);
                yield break;
            }

            LoadError = null;
            transitionInProgress = true;
            Screen = CampaignScreen.Playing;
            RunComplete = false;
            RunFailed = false;
            FeedbackTimeController.SetTransition(true);
            FeedbackTimeController.SetPaused(false);

            if (currentLevelScene.IsValid() && currentLevelScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(currentLevelScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            AsyncOperation load;
#if UNITY_EDITOR
            load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(editorPath,
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
#endif
            yield return load;
            currentLevelScene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneName));
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
