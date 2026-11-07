using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    internal sealed class CelComicUi : ScriptableObject
    {
        internal static readonly Color Ink = new Color32(23, 28, 42, 255);
        internal static readonly Color Paper = new Color32(255, 241, 214, 255);
        internal static readonly Color Gold = new Color32(255, 196, 77, 255);
        internal static readonly Color Teal = new Color32(106, 222, 202, 255);
        internal static readonly Color Muted = new Color32(181, 182, 188, 255);
        internal static readonly Color Shade = new Color32(15, 19, 29, 178);

        [SerializeField] private Font display;
        [SerializeField] private Font body;
        [SerializeField] private Texture2D buttonTexture;
        [SerializeField] private AudioClip moveSound;
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private Texture2D[] controllerPrompts;
        [SerializeField] private Texture2D[] keyboardPrompts;

        private static CelComicUi theme;
        private Sprite buttonSprite;
        internal static CelComicUi Theme => theme != null ? theme : theme = Resources.Load<CelComicUi>("CelComicUi");
        internal AudioClip MoveSound => moveSound;
        internal AudioClip ConfirmSound => confirmSound;

        internal enum Prompt { Play, Back, Chapters }

        internal Texture2D GetPrompt(Prompt prompt, bool gamepad)
        {
            var textures = gamepad ? controllerPrompts : keyboardPrompts;
            return textures != null && (int)prompt < textures.Length ? textures[(int)prompt] : null;
        }

        internal static void StyleText(Text text, bool heading = false)
        {
            var font = heading ? Theme?.display : Theme?.body;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Normal;
            text.color = Paper;
            text.raycastTarget = false;
            if (heading) text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        internal static void AddInkShadow(Text text, float distance = 3)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(7, 9, 14, 235);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        internal static void StyleButton(Button button, bool primary)
        {
            var border = (Image)button.targetGraphic;
            var sprite = Theme?.GetButtonSprite();
            border.color = Color.white;
            ApplyButtonSprite(border, sprite);

            var face = PauseMenu.CreateUiObject("Face", button.transform).AddComponent<Image>();
            Stretch(face.rectTransform, 3);
            face.transform.SetAsFirstSibling();
            face.raycastTarget = false;
            face.color = primary ? Gold : Ink;
            ApplyButtonSprite(face, sprite);

            var colors = button.colors;
            colors.normalColor = primary ? Gold : new Color32(135, 138, 153, 255);
            colors.highlightedColor = Paper;
            colors.selectedColor = Paper;
            colors.pressedColor = Gold;
            colors.disabledColor = new Color32(74, 78, 92, 255);
            colors.fadeDuration = .08f;
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>();
            StyleText(label);
            label.color = primary ? Ink : Paper;
        }

        internal static void SetButtonAvailable(Button button, bool available, bool primary = false)
        {
            button.interactable = available;
            var face = button.transform.Find("Face").GetComponent<Image>();
            face.color = available ? (primary ? Gold : Ink) : new Color32(48, 53, 66, 255);
            button.GetComponentInChildren<Text>().color = available ? (primary ? Ink : Paper) : Muted;
        }

        internal static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private Sprite GetButtonSprite()
        {
            if (buttonSprite == null && buttonTexture != null)
                buttonSprite = Sprite.Create(buttonTexture, new Rect(0, 0, buttonTexture.width, buttonTexture.height),
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(5, 5, 5, 5));
            return buttonSprite;
        }

        private static void ApplyButtonSprite(Image image, Sprite sprite)
        {
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
    }
}
