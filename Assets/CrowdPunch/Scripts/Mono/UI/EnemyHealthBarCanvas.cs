using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Pooled screen-space presentation for enemy health and Armored shield counts.
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
            public GameObject HealthBar;
            public RectTransform Fill;
            public Text StateLabel;
            public GameObject ShieldIndicator;
            public UnityEngine.UI.Image[] ShieldIcons;
            public bool WasPublished;
        }

        [SerializeField] private bool showHealthBars = true;
        [SerializeField] private bool showStateLabels = true;

        private readonly Dictionary<int, BarView> activeViews = new Dictionary<int, BarView>();
        private readonly Stack<BarView> availableViews = new Stack<BarView>();
        private readonly List<int> hiddenIds = new List<int>();
        private RectTransform canvasRect;
        private Canvas canvas;
        private UnityEngine.Camera worldCamera;
        private Sprite shieldSprite;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasRect = (RectTransform)transform;
            shieldSprite = Resources.Load<Sprite>("ArmorShield");
        }

        private void OnEnable()
        {
            EnemyHealthBarCanvasRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyHealthBarCanvasRegistry.Unregister(this);
        }

        public void Configure(bool healthBarsVisible, bool stateLabelsVisible)
        {
            showHealthBars = healthBarsVisible;
            showStateLabels = stateLabelsVisible;
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
            bool healthVisible,
            bool ignoreGlobalHealthBarOption,
            string stateLabel)
        {
            bool displayHealth = (showHealthBars || ignoreGlobalHealthBarOption) && healthVisible;
            bool displayState = showStateLabels && !string.IsNullOrEmpty(stateLabel);
            if (!displayHealth && !displayState)
            {
                return;
            }

            if (!TryProject(worldPosition, out Vector2 localPosition)) return;

            if (!activeViews.TryGetValue(displayId, out BarView view))
            {
                view = GetOrCreateView();
                activeViews.Add(displayId, view);
            }

            view.WasPublished = true;
            view.Root.anchoredPosition = localPosition;
            view.Fill.anchorMax = new Vector2(Mathf.Clamp01(normalizedHealth), 1f);
            view.StateLabel.text = stateLabel;
            view.HealthBar.SetActive(displayHealth);
            view.StateLabel.gameObject.SetActive(displayState);
            view.ShieldIndicator.SetActive(false);
        }

        public void PublishShields(int displayId, Vector3 worldPosition, byte remaining)
        {
            if (remaining == 0 || !TryProject(worldPosition, out Vector2 localPosition)) return;
            if (!activeViews.TryGetValue(displayId, out BarView view))
            {
                view = GetOrCreateView();
                activeViews.Add(displayId, view);
            }
            view.WasPublished = true;
            view.Root.anchoredPosition = localPosition;
            view.HealthBar.SetActive(false);
            view.StateLabel.gameObject.SetActive(false);
            view.ShieldIndicator.SetActive(true);
            for (int index = 0; index < view.ShieldIcons.Length; index++)
                view.ShieldIcons[index].gameObject.SetActive(index < remaining);
        }

        private bool TryProject(Vector3 worldPosition, out Vector2 localPosition)
        {
            localPosition = default;
            worldCamera ??= UnityEngine.Camera.main;
            if (worldCamera == null) return false;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition + Vector3.up * WorldHeightOffset);
            return screenPosition.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out localPosition);
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

            GameObject rootObject = new GameObject("Enemy Status", typeof(RectTransform));
            rootObject.layer = gameObject.layer;
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(canvasRect, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(BarWidth, BarHeight + StateLabelHeight);

            GameObject healthBarObject = new GameObject("Health Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            healthBarObject.layer = gameObject.layer;
            RectTransform healthBar = (RectTransform)healthBarObject.transform;
            healthBar.SetParent(root, false);
            healthBar.anchorMin = new Vector2(0f, StateLabelHeight / (BarHeight + StateLabelHeight));
            healthBar.anchorMax = Vector2.one;
            healthBar.offsetMin = Vector2.zero;
            healthBar.offsetMax = Vector2.zero;

            Image background = healthBarObject.GetComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
            background.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.layer = gameObject.layer;
            RectTransform fill = (RectTransform)fillObject.transform;
            fill.SetParent(healthBar, false);
            fill.anchorMin = Vector2.zero;
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

            GameObject shieldIndicator = new GameObject("Shields", typeof(RectTransform));
            shieldIndicator.layer = gameObject.layer;
            RectTransform shieldRow = (RectTransform)shieldIndicator.transform;
            shieldRow.SetParent(root, false);
            shieldRow.anchorMin = Vector2.zero;
            shieldRow.anchorMax = new Vector2(1f, 0f);
            shieldRow.pivot = new Vector2(.5f, 0f);
            shieldRow.anchoredPosition = new Vector2(0f, 3f);
            shieldRow.sizeDelta = new Vector2(0f, 22f);
            var icons = new UnityEngine.UI.Image[3];
            for (int index = 0; index < icons.Length; index++)
            {
                var iconObject = new GameObject("Shield " + (index + 1), typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                iconObject.layer = gameObject.layer;
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.SetParent(shieldRow, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .5f);
                iconRect.anchoredPosition = new Vector2((index - 1) * 20f, 0f);
                iconRect.sizeDelta = new Vector2(16f, 19f);
                var icon = iconObject.GetComponent<UnityEngine.UI.Image>();
                icon.sprite = shieldSprite;
                icon.color = Color.white;
                icon.raycastTarget = false;
                icons[index] = icon;
            }
            shieldIndicator.SetActive(false);

            return new BarView
            {
                Root = root,
                HealthBar = healthBarObject,
                Fill = fill,
                StateLabel = stateLabel,
                ShieldIndicator = shieldIndicator,
                ShieldIcons = icons,
                WasPublished = true
            };
        }
    }
}
