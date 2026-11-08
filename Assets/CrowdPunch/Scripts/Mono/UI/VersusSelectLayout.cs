using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // Reference coordinates match the Versus Select mockup. The whole panel scales inside the existing canvas.
    internal sealed class VersusSelectLayout
    {
        private const float Width = 1024;
        private const float Height = 580;
        internal const int RowsPerColumn = 5;
        private const int ColumnStart = 13;
        private const int ColumnStride = 403;
        private const int ColumnWidth = 390;
        private const int RowHeight = 48;
        private static readonly Color Background = new Color32(17, 18, 55, 255);
        private static readonly Color Panel = new Color32(20, 28, 75, 255);
        private static readonly Color Stripe = new Color32(25, 35, 85, 255);
        private static readonly Color Tab = new Color32(46, 60, 135, 255);
        private static readonly Color Rule = new Color32(66, 83, 156, 255);
        private static readonly Color Blue = new Color32(117, 135, 255, 255);
        private static readonly Color Red = new Color32(243, 80, 74, 255);
        private static readonly Color Gold = new Color32(255, 214, 79, 255);
        private static readonly Color Dark = new Color32(25, 19, 49, 255);
        private static readonly Color Paper = new Color32(255, 244, 222, 255);
        private static readonly Color Pale = new Color32(197, 209, 237, 255);
        private static readonly Color Score = new Color32(199, 236, 255, 255);

        private readonly RectTransform viewport, frame, chapters, ledger, rightHeader;
        private readonly Text chapterTitle, clearCount, selectionTitle, selectionValue, hints;
        private readonly Choice play, back;
        private Vector2 fittedSize;

        internal Button Play => play.Button;
        internal Button Back => back.Button;

        internal VersusSelectLayout(RectTransform viewport, int levelCount, int chapterCount, bool replay,
            UnityAction launch, UnityAction goBack)
        {
            this.viewport = viewport;
            frame = Place(viewport, "Versus Select", 0, 0, Width, Height + (replay ? 94 : 0));
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            Fill(frame, Background);

            Block(frame, "Red trim", 0, 0, Width, 4, Red);
            Block(frame, "Blue trim", 0, frame.sizeDelta.y - 5, Width, 5, Blue);
            var marquee = Block(frame, "Marquee", 20, 20, 984, 109, new Color32(38, 55, 127, 255));
            marquee.gameObject.AddComponent<RectMask2D>();
            Slash(marquee, 586, 140, new Color32(83, 58, 183, 255));
            Slash(marquee, 550, 159, new Color32(238, 73, 77, 255));
            Slash(marquee, 175, 645, new Color32(168, 30, 56, 255));
            for (int x = 513; x < 790; x += 25)
                Slash(marquee, x, 2, new Color32(255, 255, 255, 44));
            Block(marquee, "Gold rule", 0, 105, 984, 4, Gold);
            Label(marquee, "CROWD", 20, 12, 63, 22, 23, Paper, true);
            Label(marquee, "PUNCH", 83, 12, 82, 22, 23, Gold, true);
            CelComicUi.AddInkShadow(Label(marquee, "LEVEL SELECT", 20, 37, 560, 53, 57, Paper, true), 4);
            var catalog = Block(marquee, "Campaign size", 844, 26, 120, 62, Background);
            Block(catalog, "Gold edge", 117, 0, 3, 62, Gold);
            Label(catalog, "CAMPAIGN", 6, 4, 103, 15, 12, Pale, right: true);
            Label(catalog, $"{levelCount:00} LEVELS", 6, 19, 103, 22, 18, Paper, right: true);
            Label(catalog, $"{chapterCount:00} CHAPTERS", 6, 41, 103, 16, 12, Pale, right: true);

            chapters = Block(frame, "Chapter jumps", 20, 144, 153, 337, Panel);
            Block(chapters, "Red edge", 0, 0, 4, 337, Red);
            Label(chapters, "CHAPTER", 18, 6, 121, 23, 16, Paper);
            ledger = Block(frame, "Scoreboard", 188, 144, 816, 337, Panel);
            Block(ledger, "Blue edge", 0, 0, 4, 337, Blue);
            chapterTitle = Label(ledger, string.Empty, 20, 7, 250, 33, 28, Paper, true);
            clearCount = Label(ledger, string.Empty, 542, 8, 254, 30, 15, Pale, right: true);
            ColumnHeader(ColumnStart);
            rightHeader = ColumnHeader(ColumnStart + ColumnStride);

            var deck = Block(frame, "Control deck", 20, 496, 984, 62, new Color32(36, 40, 92, 255));
            Block(deck, "Blue rule", 0, 0, 984, 2, Blue);
            hints = Label(deck, string.Empty, 13, 8, 525, 46, 15, Pale);
            selectionTitle = Label(deck, string.Empty, 555, 8, 167, 17, 13, Pale, right: true);
            selectionValue = Label(deck, string.Empty, 538, 25, 184, 29, 23, Score, right: true);
            back = Action(deck, "Back", 736, 9, 102, 44, goBack);
            play = Action(deck, "Play", 848, 9, 126, 44, launch);
            Fit();
        }

        internal void Fit()
        {
            Vector2 size = viewport.rect.size;
            if (size == fittedSize) return;
            fittedSize = size;
            float scale = Mathf.Min(size.x / Width, size.y / frame.sizeDelta.y);
            frame.localScale = Vector3.one * scale;
        }

        internal void SetPrompts(bool gamepad)
        {
            hints.text = gamepad ? "D-PAD  Move     Y  Chapters     A  Confirm     B  Back"
                : "ARROWS  Move     PG UP / PG DN  Chapter     Y  Chapter List";
        }

        internal Choice Chapter(int offset, UnityAction activate)
        {
            var choice = new Choice(chapters, $"Chapter {offset + 1:00}", 10, 32 + offset * 33, 133, 32,
                22, 12, Tab, activate);
            choice.Number.text = $"{offset + 1:00}";
            return choice;
        }

        internal Choice Level(int slot, UnityAction activate, UnityAction select, UnityAction submit)
        {
            int row = slot % RowsPerColumn;
            int column = slot / RowsPerColumn;
            var choice = new Choice(ledger, $"Level slot {slot + 1}", ColumnStart + column * ColumnStride, 70 + row * RowHeight,
                ColumnWidth, RowHeight, 32, 23, row % 2 == 0 ? Panel : Stripe, activate, withSlash: true);
            Bind(choice.Button, EventTriggerType.Select, select);
            Bind(choice.Button, EventTriggerType.Submit, submit);
            return choice;
        }

        internal void ShowChapter(int chapter, int cleared, int total)
        {
            chapterTitle.text = $"CHAPTER {chapter + 1:00}";
            clearCount.text = $"{cleared} / {total} CLEARED";
            rightHeader.gameObject.SetActive(total > RowsPerColumn);
        }

        internal void ShowSelection(int index, string value, string action, bool playable, bool scored)
        {
            selectionTitle.text = $"LEVEL {index + 1:00}" + (scored ? " / BEST SCORE" : string.Empty);
            selectionValue.text = value;
            play.Number.text = action;
            play.SetAction(playable, primary: true);
        }

        internal (Button retry, Button next) ReplayResult(string result, UnityAction retry, UnityAction next, bool canAdvance)
        {
            var strip = Block(frame, "Replay result", 20, 568, 984, 82, Panel);
            Block(strip, "Red edge", 0, 0, 4, 82, Red);
            Label(strip, "REPLAY COMPLETE", 14, 9, 510, 29, 26, Paper, true);
            var score = Label(strip, result, 14, 40, 664, 30, 17, Score);
            score.resizeTextForBestFit = true;
            score.resizeTextMinSize = 12;
            score.resizeTextMaxSize = 17;
            var retryChoice = Action(strip, "Retry", 689, 19, 116, 44, retry);
            var nextChoice = Action(strip, "Next Level", 816, 19, 158, 44, next);
            nextChoice.SetAction(canAdvance, primary: false);
            return (retryChoice.Button, canAdvance ? nextChoice.Button : null);
        }

        private static Choice Action(Transform parent, string name, float x, float y, float width, float height,
            UnityAction activate)
        {
            var choice = new Choice(parent, name, x, y, width, height, 24, 0, Tab, activate);
            choice.Number.text = name;
            choice.Number.resizeTextForBestFit = true;
            choice.Number.resizeTextMinSize = 17;
            choice.Number.resizeTextMaxSize = 24;
            choice.SetAction(true, primary: false);
            return choice;
        }

        private RectTransform ColumnHeader(float x)
        {
            var header = Block(ledger, "Column headings", x, 45, ColumnWidth, 25, Tab);
            Label(header, "LEVEL", 12, 0, 95, 25, 13, Pale);
            Label(header, "BEST PTS", 230, 0, 146, 25, 13, Pale, right: true);
            return header;
        }

        private static void Bind(Button button, EventTriggerType type, UnityAction action)
        {
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static RectTransform Slash(Transform parent, float x, float width, Color color)
        {
            var slash = Block(parent, "Diagonal", 0, 0, width, 620, color);
            // Rotate around the stripe's centre to keep adjacent slants parallel.
            slash.pivot = new Vector2(.5f, .5f);
            slash.anchoredPosition = new Vector2(x, -55);
            slash.localRotation = Quaternion.Euler(0, 0, -31);
            return slash;
        }

        private static RectTransform Place(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)PauseMenu.CreateUiObject(name, parent).transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Block(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = Place(parent, name, x, y, width, height);
            Fill(rect, color);
            return rect;
        }

        private static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text Label(Transform parent, string value, float x, float y, float width, float height,
            int size, Color color, bool heading = false, bool right = false)
        {
            var text = Place(parent, "Text", x, y, width, height).gameObject.AddComponent<Text>();
            CelComicUi.StyleText(text, heading);
            text.color = color;
            text.text = value;
            text.fontSize = size;
            text.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            return text;
        }

        internal sealed class Choice
        {
            internal readonly Button Button;
            internal readonly Text Number, Value;
            private readonly Image face;
            private readonly GameObject slash;
            private readonly Text cursor;
            private readonly Color resting;

            internal Choice(Transform parent, string name, float x, float y, float width, float height,
                int numberSize, int valueSize, Color resting, UnityAction activate, bool withSlash = false)
            {
                this.resting = resting;
                var rect = Place(parent, name, x, y, width, height);
                var border = Fill(rect, Color.white, true);
                border.canvasRenderer.cullTransparentMesh = false;
                Button = rect.gameObject.AddComponent<Button>();
                Button.targetGraphic = border;
                Button.onClick.AddListener(activate);
                var colors = Button.colors;
                colors.normalColor = colors.disabledColor = Color.clear;
                colors.highlightedColor = Blue;
                colors.selectedColor = colors.pressedColor = Paper;
                colors.fadeDuration = 0;
                Button.colors = colors;
                var fill = Place(rect, "Face", 2, 2, width - 4, height - 4);
                face = Fill(fill, resting);
                if (withSlash)
                {
                    fill.gameObject.AddComponent<RectMask2D>();
                    slash = Slash(fill, 20, 130, new Color32(255, 184, 46, 255)).gameObject;
                    slash.SetActive(false);
                    Block(rect, "Row rule", 2, height - 1, width - 4, 1, Rule);
                    cursor = Label(rect, ">", 12, 0, 14, height, 17, Dark);
                    cursor.gameObject.SetActive(false);
                }
                Number = Label(rect, string.Empty, 12, 0, width - 24, height, numberSize, Paper, true);
                if (valueSize == 0) Number.alignment = TextAnchor.MiddleCenter;
                else Value = Label(rect, string.Empty, 50, 0, width - 62, height, valueSize, Pale, right: true);
            }

            internal void Show(bool selected, string value, bool bright, bool scored = false)
            {
                face.color = selected ? Gold : resting;
                Number.color = selected ? Dark : bright ? Paper : Pale;
                Value.color = selected ? Dark : bright ? Score : Pale;
                Value.text = value;
                if (slash == null) return;
                slash.SetActive(selected);
                cursor.gameObject.SetActive(selected);
                Number.rectTransform.anchoredPosition = new Vector2(selected ? 28 : 12, 0);
                Value.fontSize = scored ? 23 : 14;
            }

            internal void SetAction(bool enabled, bool primary)
            {
                Button.interactable = enabled;
                face.color = enabled && primary ? Gold : resting;
                Number.color = !enabled ? Pale : primary ? Dark : Paper;
            }
        }
    }
}
