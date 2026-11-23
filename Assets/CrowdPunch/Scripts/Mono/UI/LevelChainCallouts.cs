using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // SCORE-001. Two reusable callouts keep consecutive punches readable on the campaign canvas.
    internal sealed class LevelChainCallouts
    {
        private const float HeadlineDuration = .85f;
        private const float MinimumReadTime = .40f;
        private const float FadeTime = .20f;
        private const float ShoveTime = .14f;

        private static readonly Vector2 HeadlinePosition = new(0, -122);
        private static readonly Vector2 OutgoingPosition = new(-145, -63);
        private static readonly Vector3 OutgoingScale = new(.80f, .80f, 1f);

        private readonly List<(uint PunchSequence, int Defeats)> pending = new();
        private Callout headline;
        private Callout outgoing;
        private double pushedAt;
        private Vector2 shoveFrom;
        private Vector3 shoveScale;
        private float shoveAngle;
        private float shoveAlpha;

        internal LevelChainCallouts(Transform canvas)
        {
            outgoing = new Callout(canvas);
            headline = new Callout(canvas);
        }

        internal void Queue(uint punchSequence, int defeats, double now)
        {
            if (headline.IsShowing(punchSequence))
            {
                headline.UpdateCount(defeats, now);
                return;
            }

            if (outgoing.IsShowing(punchSequence))
            {
                if (outgoing.UpdateCount(defeats, now)) shoveAlpha = 1;
                return;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].PunchSequence != punchSequence) continue;
                pending[i] = (punchSequence, Mathf.Max(pending[i].Defeats, defeats));
                return;
            }

            pending.Add((punchSequence, defeats));
        }

        internal void Tick(double now)
        {
            AdvanceOutgoing(now);
            AdvanceHeadline(now);
            if (pending.Count == 0) return;

            if (!headline.IsVisible) ShowNext(now);
            if (outgoing.IsVisible || pending.Count == 0) return;

            (headline, outgoing) = (outgoing, headline);
            pushedAt = now;
            shoveFrom = outgoing.Rect.anchoredPosition;
            shoveScale = outgoing.Rect.localScale;
            shoveAngle = Mathf.DeltaAngle(0, outgoing.Rect.localEulerAngles.z);
            shoveAlpha = outgoing.Group.alpha;
            ShowNext(now);
        }

        internal void Reset()
        {
            pending.Clear();
            headline.Hide();
            outgoing.Hide();
        }

        private void ShowNext(double now)
        {
            var award = pending[0];
            pending.RemoveAt(0);
            headline.Show(award.PunchSequence, award.Defeats, now);
            AdvanceHeadline(now);
        }

        private void AdvanceHeadline(double now)
        {
            if (!headline.IsVisible) return;

            float age = (float)(now - headline.UpdatedAt);
            if (age >= HeadlineDuration)
            {
                headline.Hide();
                return;
            }

            float entry = (float)(now - headline.EnteredAt);
            float kick = Mathf.SmoothStep(0f, 1f, entry / .055f);
            float settle = Mathf.SmoothStep(0f, 1f, (entry - .055f) / .15f);
            float width = Mathf.Lerp(Mathf.Lerp(.90f, 1.25f, kick), 1f, settle);
            float height = Mathf.Lerp(Mathf.Lerp(.77f, 1.16f, kick), 1f, settle);
            headline.Rect.localScale = new Vector3(width, height, 1f);
            headline.Rect.anchoredPosition = HeadlinePosition + Vector2.down * (14f * (1f - kick));
            headline.Rect.localRotation = Quaternion.Euler(0, 0, 4f * (1f - settle));
            headline.Group.alpha = Mathf.Clamp01((HeadlineDuration - age) / FadeTime);
            headline.AnimateNumeral(age);
        }

        private void AdvanceOutgoing(double now)
        {
            if (!outgoing.IsVisible) return;

            // A late defeat can refresh the previous badge while the next punch waits for a slot.
            double fadeAt = System.Math.Max(pushedAt + ShoveTime,
                System.Math.Min(outgoing.UpdatedAt, pushedAt) + MinimumReadTime);
            float fade = Mathf.Clamp01((float)(now - fadeAt) / FadeTime);
            if (fade >= 1)
            {
                outgoing.Hide();
                return;
            }

            float shove = 1f - Mathf.Pow(1f - Mathf.Clamp01((float)(now - pushedAt) / ShoveTime), 3f);
            outgoing.Rect.anchoredPosition = Vector2.Lerp(shoveFrom, OutgoingPosition, shove) + new Vector2(-30, 12) * fade;
            outgoing.Rect.localScale = Vector3.Lerp(shoveScale, OutgoingScale, shove);
            outgoing.Rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(shoveAngle, -8f, shove) - 4f * fade);
            outgoing.Group.alpha = shoveAlpha * (1f - fade);
            outgoing.AnimateNumeral((float)(now - outgoing.UpdatedAt));
        }

        private sealed class Callout
        {
            internal readonly RectTransform Rect;
            internal readonly CanvasGroup Group;
            private readonly Text numeral;
            private readonly Text suffix;
            private uint punchSequence;
            private int defeats;

            internal double EnteredAt { get; private set; }
            internal double UpdatedAt { get; private set; }
            internal bool IsVisible => Rect.gameObject.activeSelf;

            internal Callout(Transform canvas)
            {
                var root = PauseMenu.CreateUiObject("Punch Chain Callout", canvas);
                Rect = (RectTransform)root.transform;
                Rect.anchorMin = Rect.anchorMax = new Vector2(.5f, 1);
                Group = root.AddComponent<CanvasGroup>();
                Group.blocksRaycasts = false;
                Group.interactable = false;
                suffix = CreateLabel(Rect, "IN ONE!");
                numeral = CreateLabel(Rect, "0");
                numeral.name = "Chain Count";
                Hide();
            }

            internal bool IsShowing(uint sequence) => IsVisible && punchSequence == sequence;

            internal void Show(uint sequence, int count, double now)
            {
                punchSequence = sequence;
                defeats = 0;
                EnteredAt = now;
                Rect.SetAsLastSibling();
                Rect.gameObject.SetActive(true);
                UpdateCount(count, now);
            }

            internal bool UpdateCount(int count, double now)
            {
                if (count <= defeats) return false;
                defeats = count;
                UpdatedAt = now;
                numeral.text = count.ToString();
                const float halfGap = 5f;
                numeral.rectTransform.anchoredPosition = Vector2.left * (suffix.preferredWidth / 2f + halfGap);
                suffix.rectTransform.anchoredPosition = Vector2.right * (numeral.preferredWidth / 2f + halfGap);
                return true;
            }

            internal void AnimateNumeral(float age)
            {
                float pulse = UpdatedAt > EnteredAt ? 1f - Mathf.SmoothStep(0f, 1f, age / .15f) : 0f;
                numeral.rectTransform.localScale = new Vector3(1f + .12f * pulse, 1f + .26f * pulse, 1f);
            }

            internal void Hide() => Rect.gameObject.SetActive(false);

            private static Text CreateLabel(Transform parent, string value)
            {
                var label = PauseMenu.CreateLabel(parent, value, 34, 76);
                CelComicUi.StyleText(label, heading: true);
                RingsideUi.OutlineHud(label);
                label.color = RingsideUi.Gold;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.rectTransform.sizeDelta = new Vector2(240, 76);
                return label;
            }
        }
    }
}
