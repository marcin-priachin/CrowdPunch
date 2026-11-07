using CrowdPunch.Mono.Levels;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // Presents one catalog entry. The sequence remains the only authority for loading it.
    internal sealed class ArenaBrowser
    {
        private readonly GauntletSequence sequence;
        private readonly Text number, title, objective, chapter, playLabel;
        private readonly Button play, previous, next, backButton;
        private readonly RawImage playGlyph, backGlyph, chaptersGlyph;

        internal int Index { get; private set; }
        internal Button First => play.interactable ? play : next.interactable ? next : previous.interactable ? previous : backButton;

        internal ArenaBrowser(Transform root, GauntletSequence sequence, int index,
            UnityAction showChapters, UnityAction back, UnityAction confirm)
        {
            this.sequence = sequence;
            Index = index;

            var info = Anchor("Encounter", root, Vector2.zero, new Vector2(68, 64), new Vector2(700, 246));
            number = PlaceLabel(info, 25, 206, 32);
            title = PlaceLabel(info, 63, 132, 76, heading: true);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 40;
            title.resizeTextMaxSize = 63;
            objective = PlaceLabel(info, 30, 92, 36);

            play = PlaceButton(info, "Play", new Vector2(0, 6), new Vector2(220, 62), () =>
            {
                if (!sequence.CanSelect(Index)) return;
                confirm();
                sequence.SelectLevel(Index);
            }, primary: true);
            playLabel = play.GetComponentInChildren<Text>();
            playLabel.rectTransform.offsetMin = new Vector2(35, 0);
            playGlyph = PlaceGlyph(play.transform, new Vector2(17, 14), 36, CelComicUi.Ink);
            previous = PlaceButton(info, "<", new Vector2(242, 6), new Vector2(50, 62), () => Step(-1));
            next = PlaceButton(info, ">", new Vector2(298, 6), new Vector2(50, 62), () => Step(1));
            var browseHint = Anchor("Browse hint", info, Vector2.zero, new Vector2(361, 6), new Vector2(92, 62));
            PlaceLabel(browseHint, 23, 0, 62).text = "Browse";
            backButton = PlaceButton(info, "Back", new Vector2(462, 6), new Vector2(136, 62), back);
            backButton.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(30, 0);
            backGlyph = PlaceGlyph(backButton.transform, new Vector2(12, 16), 32, CelComicUi.Paper);

            var chapterBox = Anchor("Chapter shortcut", root, Vector2.one, new Vector2(-64, -48), new Vector2(420, 102));
            chapter = PlaceLabel(chapterBox, 27, 57, 38, heading: true);
            chapter.alignment = TextAnchor.MiddleRight;
            var chapterButton = PlaceButton(chapterBox, "Jump Chapter", new Vector2(174, 0), new Vector2(246, 54), showChapters);
            chapterButton.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(36, 0);
            chaptersGlyph = PlaceGlyph(chapterButton.transform, new Vector2(14, 10), 34, CelComicUi.Paper);

            // Left/right are encounter controls even while another button is focused.
            foreach (var button in new[] { play, previous, next, backButton, chapterButton })
            {
                var nav = button.navigation;
                nav.mode = Navigation.Mode.Vertical;
                button.navigation = nav;
            }
            Refresh();
        }

        internal void Step(int direction)
        {
            int chapterIndex = sequence.Campaign.ChapterFor(Index);
            int destination = Mathf.Clamp(Index + direction, sequence.Campaign.ChapterStart(chapterIndex),
                sequence.Campaign.ChapterEnd(chapterIndex) - 1);
            if (destination == Index) return;
            Index = destination;
            Refresh();
            if (EventSystem.current?.currentSelectedGameObject is GameObject selected &&
                selected.TryGetComponent(out Button button) && !button.interactable)
                EventSystem.current.SetSelectedGameObject(First.gameObject);
        }

        internal void SetPrompts(bool gamepad)
        {
            var theme = CelComicUi.Theme;
            playGlyph.texture = theme?.GetPrompt(CelComicUi.Prompt.Play, gamepad);
            backGlyph.texture = theme?.GetPrompt(CelComicUi.Prompt.Back, gamepad);
            chaptersGlyph.texture = theme?.GetPrompt(CelComicUi.Prompt.Chapters, gamepad);
        }

        private void Refresh()
        {
            var catalog = sequence.Campaign;
            var level = catalog.Get(Index);
            int chapterIndex = catalog.ChapterFor(Index);
            bool cleared = sequence.Progress.IsComplete(level.id);
            bool available = sequence.CanSelect(Index);
            number.text = $"LEVEL {Index + 1:00}" + (cleared ? "    /    CLEARED" : string.Empty);
            number.color = cleared ? CelComicUi.Teal : CelComicUi.Paper;
            title.text = level.title.ToUpperInvariant();
            objective.text = level.objective;
            chapter.text = $"{chapterIndex + 1:00} / {catalog.chapterNames.Length:00}  /  {catalog.chapterNames[chapterIndex].ToUpperInvariant()}";
            CelComicUi.SetButtonAvailable(play, available, primary: true);
            playLabel.text = available ? (cleared ? "Replay" : "Play") : level.Available ? "Locked" : "Unavailable";
            playGlyph.gameObject.SetActive(available);
            CelComicUi.SetButtonAvailable(previous, Index > catalog.ChapterStart(chapterIndex));
            CelComicUi.SetButtonAvailable(next, Index + 1 < catalog.ChapterEnd(chapterIndex));
        }

        internal static RectTransform Anchor(string name, Transform root, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)PauseMenu.CreateUiObject(name, root).transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text PlaceLabel(RectTransform parent, int fontSize, float y, float height, bool heading = false)
        {
            var text = PauseMenu.CreateLabel(parent, string.Empty, fontSize, height);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = new Vector2(0, y);
            text.alignment = TextAnchor.MiddleLeft;
            CelComicUi.StyleText(text, heading);
            CelComicUi.AddInkShadow(text, heading ? 4 : 2);
            return text;
        }

        private static Button PlaceButton(RectTransform parent, string text, Vector2 position, Vector2 size,
            UnityAction action, bool primary = false)
        {
            var button = PauseMenu.CreateButton(parent, text, action, primary);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return button;
        }

        private static RawImage PlaceGlyph(Transform parent, Vector2 position, float size, Color color)
        {
            var image = Anchor("Input prompt", parent, Vector2.zero, position, new Vector2(size, size))
                .gameObject.AddComponent<RawImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
