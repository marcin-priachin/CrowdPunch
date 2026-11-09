using CrowdPunch.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    internal static class RingsideUi
    {
        internal static readonly Color Ink = new Color32(17, 18, 55, 255);
        internal static readonly Color Paper = new Color32(255, 244, 222, 255);
        internal static readonly Color Gold = new Color32(255, 214, 79, 255);
        internal static readonly Color Red = new Color32(238, 73, 77, 255);
        internal static readonly Color Sky = new Color32(199, 236, 255, 255);
        internal static readonly Color Muted = new Color32(197, 209, 237, 255);
        internal static readonly Color Hint = new Color32(21, 30, 80, 185);

        internal static void OutlineHud(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        internal static Image Image(Transform parent, string name, Color color)
        {
            var image = PauseMenu.CreateUiObject(name, parent).AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static void Rule(Transform parent, Color color, float height)
        {
            var image = Image(parent, "Rule", color);
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        internal static Text Label(Transform parent, string value, int size, float height, Color color, bool heading = false)
        {
            var text = PauseMenu.CreateLabel(parent, value, size, height);
            CelComicUi.StyleText(text, heading);
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        internal static Button Action(Transform parent, string label, UnityAction activate,
            bool enabled, bool primary, float height = 75, int size = 42, bool danger = false, bool levelClear = false)
        {
            var border = Image(parent, label, Color.white);
            border.raycastTarget = true;
            border.canvasRenderer.cullTransparentMesh = false;
            border.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            button.onClick.AddListener(activate);
            var colors = button.colors;
            colors.normalColor = colors.disabledColor = Color.clear;
            colors.highlightedColor = new Color32(117, 135, 255, 255);
            colors.selectedColor = colors.pressedColor = Paper;
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.interactable = enabled;

            Color faceColor = primary ? Gold : danger ? new Color32(168, 45, 67, 255) : new Color32(32, 41, 91, 238);
            var face = Image(border.transform, "Face", enabled ? faceColor : new Color32(26, 31, 62, 235));
            CelComicUi.Stretch(face.rectTransform, 2);
            if (enabled && primary)
            {
                face.gameObject.AddComponent<RectMask2D>();
                var slash = Image(face.transform, "Gold diagonal", new Color32(255, 184, 46, 255)).rectTransform;
                slash.anchorMin = slash.anchorMax = new Vector2(levelClear ? 1 : 0, .5f);
                slash.anchoredPosition = new Vector2(height * (levelClear ? .50f : .52f), 0);
                slash.sizeDelta = new Vector2(height * 2.35f, height * 4);
                slash.localRotation = Quaternion.Euler(0, 0, -28);
            }
            var edge = Image(face.transform, "Lower edge", new Color32(101, 115, 199, 142)).rectTransform;
            edge.anchorMax = Vector2.right;
            edge.offsetMin = Vector2.zero;
            edge.offsetMax = new Vector2(0, 1);

            var text = Label(face.transform, label, size, height,
                !enabled ? Muted : primary ? Ink : Paper, heading: !levelClear);
            CelComicUi.Stretch(text.rectTransform);
            float inset = size * .45f;
            text.rectTransform.offsetMin = new Vector2(primary && enabled && !levelClear ? inset * 2 : inset, 0);
            text.rectTransform.offsetMax = new Vector2(primary && enabled && levelClear ? -inset * 3 : -inset, 0);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.RoundToInt(size * .65f);
            text.resizeTextMaxSize = size;

            if (enabled && primary)
            {
                var cursor = Label(face.transform, ">", Mathf.RoundToInt(size * .65f), height, Ink);
                cursor.rectTransform.anchorMin = levelClear ? Vector2.right : Vector2.zero;
                cursor.rectTransform.anchorMax = levelClear ? Vector2.one : Vector2.up;
                cursor.rectTransform.offsetMin = new Vector2(inset * (levelClear ? -1.75f : .55f), 0);
                cursor.rectTransform.offsetMax = new Vector2(inset * (levelClear ? -.55f : 1.75f), 0);
            }
            return button;
        }

        internal static RectTransform ScoreColumns(Transform parent, LevelClearScore score, int size)
        {
            var row = PauseMenu.CreateUiObject("Clear statistics", parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = size * .16f;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = size * 1.47f;
            AddStat($"{score.Points:N0}", "POINTS", 1.5f);
            AddStat($"{score.Defeats:N0}", "DEFEATED", 1);
            AddStat($"{score.Punches:N0}", "PUNCHES", 1);
            return (RectTransform)row.transform;

            void AddStat(string value, string label, float weight)
            {
                var column = PauseMenu.CreateUiObject(label, row.transform);
                column.AddComponent<LayoutElement>().flexibleWidth = weight;
                var stack = column.AddComponent<VerticalLayoutGroup>();
                stack.childControlHeight = stack.childControlWidth = true;
                stack.childForceExpandHeight = false;
                Label(column.transform, value, size, size * .93f, Sky, heading: true);
                int captionSize = Mathf.RoundToInt(size * .34f);
                Label(column.transform, label, captionSize, captionSize * 1.35f, Muted);
                Rule(column.transform, new Color32(118, 135, 255, 175), 2);
            }
        }
    }
}
