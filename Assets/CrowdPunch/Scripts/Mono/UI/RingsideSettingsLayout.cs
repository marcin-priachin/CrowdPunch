using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    internal sealed class RingsideSettingsLayout
    {
        private static readonly Color Panel = new Color32(21, 30, 75, 255);
        private readonly RectTransform viewport, frame, content, footer;
        private readonly CanvasGroup page;
        private readonly ScrollRect scroll;
        private readonly Text prompts, status;
        private readonly Button[] tabs;
        private readonly List<Button> rows = new List<Button>();
        private readonly List<Button> actions = new List<Button>();
        private RectTransform modal, modalBox;
        private Button modalBack;
        private Text modalText;
        private int selectedTab;

        internal Button First => tabs[selectedTab];
        internal int RowCount => rows.Count;
        internal bool HasModal => modal != null;

        internal RingsideSettingsLayout(RectTransform viewport, string[] labels, Action<int, bool> select)
        {
            this.viewport = viewport;
            var scrim = PauseMenu.CreateUiObject("Ringside settings scrim", viewport).AddComponent<RingsideBackdrop>();
            scrim.raycastTarget = false;
            CelComicUi.Stretch(scrim.rectTransform);
            frame = Place(viewport, "Settings frame", 0, 0, 1260, 850);
            frame.anchorMin = frame.anchorMax = new Vector2(.045f, .5f);
            frame.pivot = new Vector2(0, .5f);
            frame.anchoredPosition = Vector2.zero;
            Block(frame, "Ink panel", 0, 0, 1260, 850, RingsideUi.Ink);
            page = frame.gameObject.AddComponent<CanvasGroup>();
            var heading = Label(frame, "SETTINGS", 28, 14, 850, 82, 80, true);
            CelComicUi.AddInkShadow(heading, 4);
            Block(frame, "Red rule", 28, 101, 1204, 5, RingsideUi.Red);
            Block(frame, "Tab rail", 28, 121, 263, 558, Panel);
            tabs = new Button[labels.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i] = PlacedButton(frame, labels[i], 36, 130 + i * 70, 247, 62, () => select(index, false), 28);
                Block((RectTransform)tabs[i].transform, "Active tab", 3, 5, 5, 52, RingsideUi.Gold);
                Hook(tabs[i], EventTriggerType.Move, data =>
                {
                    if (index != selectedTab && data is AxisEventData axis && axis.moveDir == MoveDirection.Right)
                        select(index, true);
                });
            }

            var pane = Block(frame, "Settings rows", 310, 121, 922, 558, Panel);
            var clip = Block(pane, "Scroll viewport", 10, 8, 902, 542, Panel);
            clip.GetComponent<Image>().raycastTarget = true;
            clip.gameObject.AddComponent<RectMask2D>();
            scroll = pane.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = clip;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 56;
            var track = Block(pane, "Scrollbar", 914, 8, 6, 542, RingsideUi.Ink);
            track.GetComponent<Image>().raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            bar.handleRect = Block(track, "Thumb", 0, 0, 6, 0, RingsideUi.Sky);
            CelComicUi.Stretch(bar.handleRect);
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            content = Place(clip, "Options", 0, 0, 902, 0);
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 6;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            status = Label(frame, "", 314, 687, 910, 44, 25);
            status.color = RingsideUi.Gold;
            footer = Block(frame, "Control deck", 28, 740, 1204, 82, Panel);
            Block(footer, "Footer rule", 0, 0, 1204, 3, RingsideUi.Sky);
            prompts = Label(footer, "", 16, 8, 510, 64, 22);
            Fit();
        }

        internal void Fit()
        {
            Vector2 size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            frame.localScale = Vector3.one * Mathf.Min(size.x * .91f / 1260f, size.y * .85f / 850f);
            if (modalBox != null) modalBox.localScale = frame.localScale;
        }

        internal void BeginTab(int tab)
        {
            selectedTab = tab;
            for (int i = 0; i < tabs.Length; i++) tabs[i].transform.Find("Active tab").gameObject.SetActive(i == tab);
            foreach (Transform child in content) Remove(child);
            foreach (var action in actions) Remove(action.transform.parent);
            actions.Clear();
            rows.Clear();
            status.text = "";
        }

        internal Button AddRow(string label, string value, UnityAction click, out Text valueLabel, bool large)
        {
            int size = large ? 33 : 27;
            int height = large ? 76 : 64;
            Button button = RingsideUi.Action(content, "", click, true, false, height, size);
            button.name = label;
            var rect = (RectTransform)button.transform;
            Label(rect, label, 18, 0, 450, height, size);
            valueLabel = Label(rect, value, 480, 0, 395, height, size);
            valueLabel.alignment = TextAnchor.MiddleRight;
            valueLabel.color = RingsideUi.Sky;
            rows.Add(button);
            Hook(button, EventTriggerType.Select, _ => Reveal(rect));
            Hook(button, EventTriggerType.Scroll, data => scroll.OnScroll((PointerEventData)data));
            return button;
        }

        internal void AddNote(string text, bool large)
        {
            var note = RingsideUi.Label(content, "  " + text, large ? 29 : 24, large ? 66 : 54, RingsideUi.Muted);
            note.verticalOverflow = VerticalWrapMode.Overflow;
        }

        internal Slider AddSlider(Button row, int min, int max, int value, bool large, UnityAction<float> change)
        {
            var root = Place((RectTransform)row.transform, "Slider", 465, large ? 12 : 6, 295, 52);
            var slider = root.gameObject.AddComponent<Slider>();
            var hit = Block(root, "Slider track area", 0, 0, 295, 52, Color.clear);
            hit.GetComponent<Image>().raycastTarget = true;
            Block(root, "Track", 10, 22, 275, 8, RingsideUi.Ink);
            var fillArea = Place(root, "Fill area", 10, 22, 275, 8);
            slider.fillRect = Block(fillArea, "Fill", 0, 0, 275, 8, RingsideUi.Gold);
            CelComicUi.Stretch(slider.fillRect);
            var handleArea = Place(root, "Handle travel", 10, 0, 275, 52);
            var handle = Block(handleArea, "Handle", 0, 0, 20, 0, RingsideUi.Paper);
            handle.pivot = new Vector2(.5f, .5f);
            handle.sizeDelta = new Vector2(20, -24);
            slider.handleRect = handle;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(change);
            Hook(slider, EventTriggerType.PointerDown, _ => row.Select());
            Hook(slider, EventTriggerType.Scroll, data => scroll.OnScroll((PointerEventData)data));
            return slider;
        }

        internal void AddArrows(Button row, Action<int> step)
        {
            var rect = (RectTransform)row.transform;
            var left = PlacedButton(rect, "<", 490, 7, 44, 48, () => { row.Select(); step(-1); }, 24);
            var right = PlacedButton(rect, ">", 849, 7, 44, 48, () => { row.Select(); step(1); }, 24);
            left.navigation = right.navigation = new Navigation { mode = Navigation.Mode.None };
            foreach (Transform child in row.transform)
                if (child.TryGetComponent<Text>(out var label) && label.alignment == TextAnchor.MiddleRight)
                    Position(label.rectTransform, 542, 0, 288, (int)label.rectTransform.rect.height);
        }

        internal Button AddFooter(string text, int x, int width, UnityAction click, bool primary = false)
        {
            var button = PlacedButton(footer, text, x, 12, width, 58, click, 24, primary);
            actions.Add(button);
            return button;
        }

        internal void Link(Button focus = null)
        {
            Button lastTab = tabs[selectedTab];
            for (int i = 0; i < tabs.Length; i++)
                Navigate(tabs[i], i > 0 ? tabs[i - 1] : null, i + 1 < tabs.Length ? tabs[i + 1] : actions[0], null,
                    i == selectedTab ? (rows.Count > 0 ? rows[0] : actions[0]) : null);
            for (int i = 0; i < rows.Count; i++)
                Navigate(rows[i], i > 0 ? rows[i - 1] : lastTab, i + 1 < rows.Count ? rows[i + 1] : actions[0], lastTab, null);
            for (int i = 0; i < actions.Count; i++)
                Navigate(actions[i], rows.Count > 0 ? rows[rows.Count - 1] : lastTab, null,
                    i > 0 ? actions[i - 1] : lastTab, i + 1 < actions.Count ? actions[i + 1] : null);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            (focus ?? lastTab).Select();
        }

        internal Button RowAt(int index) => index < rows.Count ? rows[index] : null;
        internal int RowIndex(Button row) => rows.IndexOf(row);
        internal void Status(string text) => status.text = text;
        internal void Prompts(string text) => prompts.text = text;

        internal void ShowModal(string text, string accept, UnityAction confirm, UnityAction cancel)
        {
            // The overlay sits outside the disabled page CanvasGroup.
            modal = Block(viewport, "Confirmation overlay", 0, 0, 0, 0, new Color32(12, 17, 49, 220));
            CelComicUi.Stretch(modal);
            modal.GetComponent<Image>().raycastTarget = true;
            var box = modalBox = Block(modal, "Confirmation", 0, 0, 810, 265, Panel);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            box.anchoredPosition = Vector2.zero;
            box.localScale = frame.localScale;
            Block(box, "Red trim", 0, 0, 810, 5, RingsideUi.Red);
            modalText = Label(box, text, 25, 25, 760, 122, 36, true);
            modalText.alignment = TextAnchor.MiddleCenter;
            modalBack = PlacedButton(box, accept == null ? "Cancel" : "Revert", 149, 174, 246, 62, cancel, 29);
            if (accept != null)
            {
                Button keep = PlacedButton(box, accept, 415, 174, 246, 62, confirm, 29, true);
                Navigate(modalBack, null, null, null, keep);
                Navigate(keep, null, null, modalBack, null);
            }
            modalBack.Select();
            page.interactable = false;
        }

        internal void ModalText(string text) { if (modalText != null) modalText.text = text; }

        internal bool IsCancelTarget(Vector2 point)
        {
            if (modalBack == null) return false;
            var canvas = viewport.GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)modalBack.transform, point, camera);
        }

        internal void CloseModal()
        {
            if (modal != null) Remove(modal);
            modal = null;
            modalBox = null;
            modalBack = null;
            page.interactable = true;
        }

        private void Reveal(RectTransform row)
        {
            if (content.rect.height <= scroll.viewport.rect.height) return;
            float top = -row.anchoredPosition.y - row.rect.height * (1 - row.pivot.y);
            float bottom = top + row.rect.height;
            float shownTop = content.anchoredPosition.y;
            float shownBottom = shownTop + scroll.viewport.rect.height;
            if (top < shownTop || bottom > shownBottom)
            {
                scroll.StopMovement();
                float desired = top < shownTop ? top : bottom - scroll.viewport.rect.height;
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(desired / (content.rect.height - scroll.viewport.rect.height));
            }
        }

        internal static void Hook(Selectable control, EventTriggerType type, UnityAction<BaseEventData> action)
        {
            var trigger = control.GetComponent<EventTrigger>() ?? control.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        private static void Navigate(Selectable control, Selectable up, Selectable down, Selectable left, Selectable right) =>
            control.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down,
                selectOnLeft = left, selectOnRight = right };

        private static Button PlacedButton(RectTransform parent, string text, int x, int y, int width, int height,
            UnityAction click, int size, bool primary = false)
        {
            var holder = Place(parent, text, x, y, width, height);
            var button = RingsideUi.Action(holder, text, click, true, primary, height, size);
            CelComicUi.Stretch((RectTransform)button.transform);
            return button;
        }

        private static Text Label(RectTransform parent, string text, int x, int y, int width, int height, int size, bool heading = false)
        {
            var label = RingsideUi.Label(parent, text, size, height, RingsideUi.Paper, heading);
            Position(label.rectTransform, x, y, width, height);
            return label;
        }

        private static RectTransform Block(RectTransform parent, string name, int x, int y, int width, int height, Color color)
        {
            var rect = RingsideUi.Image(parent, name, color).rectTransform;
            Position(rect, x, y, width, height);
            return rect;
        }

        private static RectTransform Place(RectTransform parent, string name, int x, int y, int width, int height)
        {
            var rect = (RectTransform)PauseMenu.CreateUiObject(name, parent).transform;
            Position(rect, x, y, width, height);
            return rect;
        }

        private static void Position(RectTransform rect, int x, int y, int width, int height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Remove(Transform item)
        {
            item.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(item.gameObject);
        }
    }
}
