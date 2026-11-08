using System.Collections.Generic;
using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // UI-only tally. The ECS total remains authoritative for results and saves.
    internal sealed class LevelScoreTally
    {
        private const int MaxFlights = 24;
        private const float RiseTime = .14f;
        private const float CountTime = .18f;

        private struct Flight
        {
            public Text Label;
            public Vector2 Origin;
            public float Age;
            public float TravelTime;
            public float Side;
        }

        private readonly RectTransform root;
        private readonly Text total;
        private readonly List<Vector3> pending = new(MaxFlights);
        private readonly List<Flight> flights = new(MaxFlights);
        private readonly Stack<Text> available = new(MaxFlights);
        private int points, unanimatedPoints, displayedPoints;
        private float countFrom, countElapsed, shownPoints;
        private bool hasScore;

        internal LevelScoreTally(Transform canvas)
        {
            root = (RectTransform)PauseMenu.CreateUiObject("Running Level Score", canvas).transform;
            CelComicUi.Stretch(root);
            var caption = RingsideUi.Label(root, "SCORE", 23, 28, RingsideUi.Sky);
            caption.name = "Score Caption";
            caption.alignment = TextAnchor.MiddleRight;
            RingsideUi.OutlineHud(caption);
            PlaceAtTopRight(caption.rectTransform, -32, 160, 28);
            total = CreateLabel("Score Total", 78);
            var rect = total.rectTransform;
            PlaceAtTopRight(rect, -57, 310, 70);
            total.alignment = TextAnchor.MiddleRight;
            total.color = RingsideUi.Paper;
            var line = RingsideUi.Image(root, "Score Underline", RingsideUi.Gold).rectTransform;
            PlaceAtTopRight(line, -134, 95, 4);
            Reset();
        }

        internal void Queue(Vector3 worldPosition)
        {
            hasScore = true;
            if (pending.Count + flights.Count < MaxFlights) pending.Add(worldPosition);
            else unanimatedPoints += LevelClearScore.PointsPerDefeat;
        }

        internal void Reset()
        {
            pending.Clear();
            foreach (var flight in flights) Recycle(flight.Label);
            flights.Clear();
            hasScore = false;
            points = unanimatedPoints = displayedPoints = 0;
            countFrom = shownPoints = 0;
            countElapsed = CountTime;
            total.text = "0";
            total.rectTransform.localScale = Vector3.one;
            root.gameObject.SetActive(false);
        }

        internal void Tick(bool visible, float deltaTime)
        {
            root.gameObject.SetActive(visible && hasScore);
            if (!visible || !hasScore) return;

            if (pending.Count != 0)
            {
                var camera = UnityEngine.Camera.main;
                foreach (var position in pending) BeginFlight(camera, position);
                pending.Clear();
            }

            int landed = AdvanceFlights(deltaTime);
            if (landed > 0 || flights.Count == 0)
            {
                landed += unanimatedPoints;
                unanimatedPoints = 0;
            }
            if (landed > 0)
            {
                points += landed;
                countFrom = shownPoints;
                countElapsed = 0;
            }

            countElapsed = Mathf.Min(CountTime, countElapsed + deltaTime);
            float settle = 1f - countElapsed / CountTime;
            shownPoints = Mathf.Lerp(countFrom, points, 1f - settle * settle * settle);
            int display = Mathf.FloorToInt(shownPoints);
            if (display != displayedPoints)
            {
                displayedPoints = display;
                total.text = $"{display:N0}";
            }
            total.rectTransform.localScale = Vector3.one * (1f + .14f * settle * settle);
        }

        private void BeginFlight(UnityEngine.Camera camera, Vector3 position)
        {
            if (camera == null || !math.all(math.isfinite((float3)position)))
            {
                unanimatedPoints += LevelClearScore.PointsPerDefeat;
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(position);
            if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var origin))
            {
                unanimatedPoints += LevelClearScore.PointsPerDefeat;
                return;
            }

            var bounds = root.rect;
            origin.x = Mathf.Clamp(origin.x, bounds.xMin + 56, bounds.xMax - 56);
            origin.y = Mathf.Clamp(origin.y, bounds.yMin + 36, bounds.yMax - 48);
            if (!available.TryPop(out var label))
            {
                label = CreateLabel("Defeat Points", 34);
                label.text = $"+{LevelClearScore.PointsPerDefeat:N0}";
            }
            label.rectTransform.anchoredPosition = origin;
            label.gameObject.SetActive(true);
            int lane = flights.Count % 5;
            flights.Add(new Flight
            {
                Label = label, Origin = origin, Side = (lane - 2) * 16,
                TravelTime = .36f + lane * .038f
            });
        }

        private int AdvanceFlights(float deltaTime)
        {
            int landed = 0;
            Vector2 target = root.InverseTransformPoint(total.rectTransform.position) + new Vector3(-56, -35);
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i];
                flight.Age += deltaTime;
                if (flight.Age >= RiseTime + flight.TravelTime)
                {
                    landed += LevelClearScore.PointsPerDefeat;
                    Recycle(flight.Label);
                    flights.RemoveAt(i);
                    continue;
                }
                flights[i] = flight;
                var rise = new Vector2(flight.Side, 38);
                var rect = flight.Label.rectTransform;
                if (flight.Age < RiseTime)
                {
                    float pop = Mathf.SmoothStep(0, 1, flight.Age / RiseTime);
                    rect.anchoredPosition = flight.Origin + rise * pop;
                    rect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, pop);
                }
                else
                {
                    float travel = (flight.Age - RiseTime) / flight.TravelTime;
                    float eased = travel * travel;
                    rect.anchoredPosition = Vector2.Lerp(flight.Origin + rise, target, eased)
                        + Vector2.up * (Mathf.Sin(Mathf.PI * eased) * 58);
                    rect.localScale = Vector3.one * Mathf.Lerp(1f, .58f, eased);
                }
            }
            return landed;
        }

        private Text CreateLabel(string name, int size)
        {
            var label = PauseMenu.CreateLabel(root, string.Empty, size, 54);
            label.name = name;
            CelComicUi.StyleText(label, heading: true);
            RingsideUi.OutlineHud(label);
            label.color = RingsideUi.Gold;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.sizeDelta = new Vector2(134, 54);
            return label;
        }

        private static void PlaceAtTopRight(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-80, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void Recycle(Text label)
        {
            label.gameObject.SetActive(false);
            available.Push(label);
        }
    }
}
