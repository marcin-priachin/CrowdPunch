using CrowdPunch.Mono.Levels;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    // LOOP-006/007 and SCORE-001. The browser reads the campaign, while the sequence owns loading and saves.
    internal sealed class ArenaBrowser
    {
        private readonly GauntletSequence sequence;
        private readonly VersusSelectLayout view;
        private readonly VersusSelectLayout.Choice[] chapters, rows;
        private readonly UnityAction confirm;
        private readonly Button retry, next;
        private int chapter, start, count, index;

        internal Button First => rows[index - start].Button;

        internal ArenaBrowser(RectTransform root, GauntletSequence sequence, int index,
            UnityAction goBack, UnityAction confirm)
        {
            this.sequence = sequence;
            this.confirm = confirm;
            var catalog = sequence.Campaign;
            bool replay = sequence.Screen == CampaignScreen.ReplayComplete;
            view = new VersusSelectLayout(root, catalog.Count, catalog.chapterNames.Length, replay, Play, goBack);
            chapters = new VersusSelectLayout.Choice[catalog.chapterNames.Length];
            int rowCount = 0;
            for (int i = 0; i < chapters.Length; i++)
            {
                int destination = i;
                chapters[i] = view.Chapter(i, () => JumpToChapter(destination));
                rowCount = Mathf.Max(rowCount, catalog.ChapterEnd(i) - catalog.ChapterStart(i));
            }
            rows = new VersusSelectLayout.Choice[rowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                rows[i] = view.Level(i, () => Select(start + slot), () => Select(start + slot), () => Play(start + slot));
            }
            if (replay)
            {
                var last = sequence.LastClearScore;
                var best = sequence.Progress.GetBestClear(catalog.Get(sequence.CurrentLevelIndex).id);
                string summary = last.IsScored ? LevelScoreFeedback.Format(last) : string.Empty;
                if (best.IsScored)
                    summary += (last.IsScored ? "   /   " : string.Empty) + $"BEST {best.Points:N0} pts";
                int advance = sequence.CurrentLevelIndex + 1;
                (retry, next) = view.ReplayResult(summary, () =>
                {
                    confirm();
                    sequence.RestartCurrentLevel();
                }, () =>
                {
                    confirm();
                    sequence.SelectLevel(advance);
                }, sequence.CanSelect(advance));
            }
            OpenChapter(catalog.ChapterFor(index), index);
        }

        internal void Fit() => view.Fit();
        internal void SetPrompts(bool gamepad) => view.SetPrompts(gamepad);
        internal void FocusChapters() => Focus(chapters[chapter].Button);

        internal void StepChapter(int direction)
        {
            int target = chapter + direction;
            if (target >= 0 && target < chapters.Length) JumpToChapter(target);
        }

        private void JumpToChapter(int target)
        {
            if (target != chapter)
            {
                var catalog = sequence.Campaign;
                int first = catalog.ChapterStart(target);
                int unfinished = sequence.NextUnfinished;
                int destination = unfinished >= first && unfinished < catalog.ChapterEnd(target) ? unfinished : first;
                OpenChapter(target, destination);
            }
            Focus(First);
        }

        private void OpenChapter(int target, int destination)
        {
            var catalog = sequence.Campaign;
            chapter = target;
            start = catalog.ChapterStart(chapter);
            count = catalog.ChapterEnd(chapter) - start;
            int cleared = 0;
            for (int i = 0; i < count; i++)
                if (sequence.Progress.IsComplete(catalog.Get(start + i).id)) cleared++;
            view.ShowChapter(chapter, cleared, count);
            for (int i = 0; i < chapters.Length; i++)
            {
                int first = catalog.ChapterStart(i);
                bool open = sequence.CanSelect(first);
                chapters[i].Show(i == chapter, open ? "OPEN" : catalog.Get(first).Available ? "LOCKED" : "UNAVAILABLE", open);
            }
            for (int i = 0; i < rows.Length; i++)
                rows[i].Button.gameObject.SetActive(i < count);
            index = destination;
            RefreshRows();
        }

        private void Select(int destination)
        {
            if (index == destination) return;
            index = destination;
            RefreshRows();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < count; i++)
            {
                int levelIndex = start + i;
                var level = sequence.Campaign.Get(levelIndex);
                bool playable = sequence.CanSelect(levelIndex);
                bool cleared = sequence.Progress.IsComplete(level.id);
                var best = sequence.Progress.GetBestClear(level.id);
                string status = playable ? "READY" : level.Available ? "LOCKED" : "UNAVAILABLE";
                bool scored = cleared && best.IsScored;
                string score = scored ? $"{best.Points:N0}" : cleared ? "CLEARED" : status;
                var row = rows[i];
                row.Number.text = $"{levelIndex + 1:00}";
                row.Show(levelIndex == index, score, cleared || playable, scored);
                if (levelIndex == index)
                {
                    string selected = scored ? $"{score} pts" : score;
                    string action = playable ? cleared ? "Replay" : "Play" : level.Available ? "Locked" : "Unavailable";
                    view.ShowSelection(index, selected, action, playable, scored);
                }
            }
            LinkNavigation();
        }

        private void LinkNavigation()
        {
            var play = view.Play.interactable ? view.Play : null;
            var deck = play ?? view.Back;
            var chapterButton = chapters[chapter].Button;
            for (int i = 0; i < chapters.Length; i++)
            {
                Link(chapters[i].Button, up: i > 0 ? chapters[i - 1].Button : null,
                    down: i + 1 < chapters.Length ? chapters[i + 1].Button : view.Back, right: First);
            }
            for (int i = 0; i < count; i++)
            {
                int perColumn = VersusSelectLayout.RowsPerColumn;
                bool left = i < perColumn;
                Button up = i % perColumn == 0 ? null : rows[i - 1].Button;
                Button down = i % perColumn == perColumn - 1 || i + 1 == count ? deck : rows[i + 1].Button;
                Button other = i + perColumn < count ? rows[i + perColumn].Button : null;
                Link(rows[i].Button, up, down, left ? chapterButton : rows[i - perColumn].Button, other);
            }
            Link(view.Back, First, retry, chapterButton, play);
            Link(view.Play, First, next ?? retry, view.Back);
            if (retry != null) Link(retry, view.Back, right: next);
            if (next != null) Link(next, deck, left: retry);
        }

        private void Play() => Play(index);

        private void Play(int destination)
        {
            if (!sequence.CanSelect(destination)) return;
            confirm();
            sequence.SelectLevel(destination);
        }

        private static void Focus(Button button) => EventSystem.current?.SetSelectedGameObject(button.gameObject);

        private static void Link(Button button, Button up = null, Button down = null, Button left = null, Button right = null)
        {
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }
    }
}
