using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Pooled screen-space presentation for recently damaged enemy health.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class EnemyHealthBarCanvas : MonoBehaviour
    {
        private const float BarWidth = 64f;
        private const float BarHeight = 7f;
        private const float StateLabelHeight = 16f;
        private const float WorldHeightOffset = 1.5f;

        private sealed class BarView
        {
            public RectTransform Root;
            public RectTransform Fill;
            public Text StateLabel;
            public bool WasPublished;
        }

        private readonly Dictionary<int, BarView> activeViews = new Dictionary<int, BarView>();
        private readonly Stack<BarView> availableViews = new Stack<BarView>();
        private readonly List<int> hiddenIds = new List<int>();
        private RectTransform canvasRect;
        private Canvas canvas;
        private UnityEngine.Camera worldCamera;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasRect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            EnemyHealthBarCanvasRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyHealthBarCanvasRegistry.Unregister(this);
        }

        public void BeginFrame()
        {
            foreach (BarView view in activeViews.Values)
            {
                view.WasPublished = false;
            }
        }

        public void Publish(
            int displayId,
            Vector3 worldPosition,
            float normalizedHealth,
            string stateLabel)
        {
            worldCamera ??= UnityEngine.Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition + Vector3.up * WorldHeightOffset);
            if (screenPosition.z <= 0f)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    out Vector2 localPosition))
            {
                return;
            }

            if (!activeViews.TryGetValue(displayId, out BarView view))
            {
                view = GetOrCreateView();
                activeViews.Add(displayId, view);
            }

            view.WasPublished = true;
            view.Root.anchoredPosition = localPosition;
            view.Fill.anchorMax = new Vector2(Mathf.Clamp01(normalizedHealth), 1f);
            view.StateLabel.text = stateLabel;
            view.StateLabel.gameObject.SetActive(!string.IsNullOrEmpty(stateLabel));
        }

        public void EndFrame()
        {
            hiddenIds.Clear();
            foreach (KeyValuePair<int, BarView> pair in activeViews)
            {
                if (!pair.Value.WasPublished)
                {
                    hiddenIds.Add(pair.Key);
                }
            }

            foreach (int displayId in hiddenIds)
            {
                BarView view = activeViews[displayId];
                activeViews.Remove(displayId);
                view.Root.gameObject.SetActive(false);
                availableViews.Push(view);
            }
        }

        private BarView GetOrCreateView()
        {
            if (availableViews.Count > 0)
            {
                BarView pooledView = availableViews.Pop();
                pooledView.Root.gameObject.SetActive(true);
                return pooledView;
            }

            GameObject rootObject = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootObject.layer = gameObject.layer;
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(canvasRect, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(BarWidth, BarHeight + StateLabelHeight);

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
            background.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.layer = gameObject.layer;
            RectTransform fill = (RectTransform)fillObject.transform;
            fill.SetParent(root, false);
            fill.anchorMin = new Vector2(0f, StateLabelHeight / (BarHeight + StateLabelHeight));
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(1.5f, 1.5f);
            fill.offsetMax = new Vector2(-1.5f, -1.5f);
            fill.pivot = new Vector2(0f, 0.5f);

            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.9f, 0.2f, 0.16f, 0.95f);
            fillImage.raycastTarget = false;

            GameObject labelObject = new GameObject("State", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = gameObject.layer;
            RectTransform labelTransform = (RectTransform)labelObject.transform;
            labelTransform.SetParent(root, false);
            labelTransform.anchorMin = new Vector2(0f, 0f);
            labelTransform.anchorMax = new Vector2(1f, 0f);
            labelTransform.pivot = new Vector2(0.5f, 0f);
            labelTransform.sizeDelta = new Vector2(0f, StateLabelHeight);

            Text stateLabel = labelObject.GetComponent<Text>();
            stateLabel.alignment = TextAnchor.MiddleCenter;
            stateLabel.color = Color.white;
            stateLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stateLabel.fontSize = 11;
            stateLabel.raycastTarget = false;

            return new BarView
            {
                Root = root,
                Fill = fill,
                StateLabel = stateLabel,
                WasPublished = true
            };
        }
    }
}
