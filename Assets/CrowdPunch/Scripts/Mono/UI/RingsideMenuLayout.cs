using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // Responsive presentation for the non-browser campaign pages. Routing remains in CampaignMenu.
    internal sealed class RingsideMenuLayout
    {
        private const float ReferenceWidth = 1920;
        private const float ReferenceHeight = 1080;
        private readonly RectTransform viewport, frame;
        private readonly bool levelClear;
        private Vector2 fittedSize;
        internal RectTransform Content { get; }

        internal RingsideMenuLayout(RectTransform viewport, bool main, bool confirmation, bool levelClear)
        {
            this.viewport = viewport;
            this.levelClear = levelClear;
            if (!confirmation)
            {
                var backdrop = PauseMenu.CreateUiObject("Ringside scrim", viewport).AddComponent<RingsideBackdrop>();
                backdrop.MainMenu = main;
                backdrop.LevelClear = levelClear;
                backdrop.raycastTarget = false;
                CelComicUi.Stretch(backdrop.rectTransform);
            }

            frame = (RectTransform)PauseMenu.CreateUiObject("Ringside frame", viewport).transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(confirmation ? .5f : .05f, .5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(confirmation ? 850 : 0, ReferenceHeight);

            if (main)
            {
                var trim = RingsideUi.Image(viewport, "Red top rule", RingsideUi.Red).rectTransform;
                trim.anchorMin = Vector2.up;
                trim.anchorMax = Vector2.one;
                trim.offsetMin = new Vector2(0, -8);
                trim.offsetMax = Vector2.zero;
                var wordmark = Stack(frame, "Wordmark", 690, true, -64);
                CelComicUi.AddInkShadow(RingsideUi.Label(wordmark, "CROWD", 168, 153, RingsideUi.Paper, true), 5);
                CelComicUi.AddInkShadow(RingsideUi.Label(wordmark, "PUNCH", 168, 153, RingsideUi.Gold, true), 5);
            }

            Content = Stack(frame, "Campaign Panel", confirmation ? 850 : main ? 630 : levelClear ? 810 : 720,
                main, -445, confirmation);
            if (levelClear)
                Content.GetComponent<VerticalLayoutGroup>().spacing = 10;
            if (main)
                RingsideUi.Label(Content, "CAMPAIGN", 24, 36, RingsideUi.Muted);
            Fit();
        }

        internal void Fit()
        {
            Vector2 size = viewport.rect.size;
            if (size == fittedSize || size.x <= 0 || size.y <= 0) return;
            fittedSize = size;
            frame.localScale = Vector3.one * Mathf.Min(size.x / ReferenceWidth, size.y / ReferenceHeight);
        }

        internal void Heading(string title, string kicker = null)
        {
            if (!string.IsNullOrEmpty(kicker))
                RingsideUi.Label(Content, kicker, 24, 34, RingsideUi.Muted);
            int size = levelClear ? 146 : 104;
            var label = RingsideUi.Label(Content, title, size, size + 8, RingsideUi.Paper, true);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 58;
            label.resizeTextMaxSize = size;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            CelComicUi.AddInkShadow(label, 4);
            RingsideUi.Rule(Content, RingsideUi.Red, levelClear ? 6 : 4);
        }

        private static RectTransform Stack(Transform parent, string name, float width, bool top, float y, bool boxed = false)
        {
            var rect = (RectTransform)PauseMenu.CreateUiObject(name, parent).transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(boxed ? .5f : 0, top ? 1 : .5f);
            rect.anchoredPosition = new Vector2(0, top ? y : 0);
            rect.sizeDelta = new Vector2(width, 0);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = top && name == "Wordmark" ? 0 : 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            if (boxed)
            {
                layout.padding = new RectOffset(32, 32, 26, 26);
                var image = rect.gameObject.AddComponent<Image>();
                image.color = RingsideUi.Ink;
                image.raycastTarget = false;
            }
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }
    }
}
