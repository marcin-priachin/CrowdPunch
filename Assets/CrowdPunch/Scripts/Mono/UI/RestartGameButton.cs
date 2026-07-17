using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// UI button action that requests a soft gameplay restart.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class RestartGameButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            EnsureEventSystem();
        }

        private void OnEnable()
        {
            button?.onClick.AddListener(RestartGame);
        }

        private void OnDisable()
        {
            button?.onClick.RemoveListener(RestartGame);
        }

        private static void RestartGame()
        {
            GameBootstrap bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.RestartGame();
                return;
            }

            Time.timeScale = 1f;
            GameRestartRegistry.RequestRestart();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
