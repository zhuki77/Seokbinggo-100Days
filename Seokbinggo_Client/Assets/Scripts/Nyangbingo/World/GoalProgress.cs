using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Save;

namespace Nyangbingo.World
{
    /// <summary>목표는 현재 상태가 아닌 달성 이력이다. 상태 판정은 A, 표시와 저장은 소비자가 맡는다.</summary>
    public sealed class GoalProgress
    {
        private readonly GoalDefinition[] ordered;
        private readonly GoalDefinition[] selectable;
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        private string selected = string.Empty;
        public event Action Changed;
        public event Action<GoalDefinition> Completed;

        public GoalProgress(IEnumerable<GoalDefinition> definitions)
        {
            ordered = (definitions ?? throw new ArgumentNullException(nameof(definitions)))
                .Where(value => value != null).OrderBy(value => value.Order).ToArray();
            selectable = ordered.Where(g => g.Track == "select").ToArray();
            if (ordered.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                throw new ArgumentException("Duplicate goal IDs.", nameof(definitions));
        }

        public bool FirstShelterComplete => ordered.Where(g => g.Track == "auto").All(g => IsComplete(g.Id));
        public IReadOnlyList<GoalDefinition> SelectableGoals => selectable;
        public bool IsComplete(string id) => completed.Contains(id ?? string.Empty);
        public GoalDefinition Current => ordered.FirstOrDefault(g => g.Track == "auto" && !IsComplete(g.Id)) ??
            ordered.FirstOrDefault(g => g.Id == selected && g.Track == "select" && !IsComplete(g.Id)) ??
            ordered.FirstOrDefault(g => g.Track == "select" && !IsComplete(g.Id));
        public string SelectedGoalId => Current?.Track == "select" ? Current.Id : selected;

        public void Evaluate(Func<GoalDefinition, bool> isTrue, bool notifyCompletion = true)
        {
            if (isTrue == null) throw new ArgumentNullException(nameof(isTrue));
            var changed = false;
            foreach (var goal in ordered)
                if (!IsComplete(goal.Id) && isTrue(goal))
                {
                    completed.Add(goal.Id);
                    changed = true;
                    if (notifyCompletion) Completed?.Invoke(goal);
                }
            if (changed) Changed?.Invoke();
        }

        public bool Select(string id)
        {
            if (!FirstShelterComplete || !ordered.Any(g => g.Id == id && g.Track == "select") || IsComplete(id))
                return false;
            selected = id;
            Changed?.Invoke();
            return true;
        }

        public GoalProgressRecord Capture() => new GoalProgressRecord
        {
            completedGoalIds = ordered.Where(g => IsComplete(g.Id)).Select(g => g.Id).ToList(),
            selectedGoalId = SelectedGoalId ?? string.Empty
        };

        public void Restore(GoalProgressRecord record)
        {
            completed.Clear();
            foreach (var id in record?.completedGoalIds ?? new List<string>())
                if (ordered.Any(g => g.Id == id)) completed.Add(id);
            selected = ordered.Any(g => g.Id == record?.selectedGoalId && g.Track == "select")
                ? record.selectedGoalId : string.Empty;
            Changed?.Invoke();
        }
    }
}
