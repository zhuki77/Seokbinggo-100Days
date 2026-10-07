using System;
using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.World
{
    /// <summary>CSV priority/repeat policy. Only visible messages consume their display budget.</summary>
    public sealed class GuidePriorityQueue
    {
        public sealed class Entry
        {
            public GuideMessageDefinition Definition;
            public string Text, Context;
            public bool Active;
            public long Sequence;
            public float Remaining;
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly HashSet<string> consumed = new HashSet<string>();
        private long sequence;
        private int day;
        public IEnumerable<Entry> Entries => entries.Values;
        public int Day => day;
        public List<string> CaptureConsumed() => new List<string>(consumed);
        public void RestoreConsumed(int currentDay, IEnumerable<string> ids)
        {
            Reset(currentDay);
            if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) consumed.Add(id);
        }
        public void Clear(string id)
        {
            if (entries.TryGetValue(id, out var entry)) entry.Active = false;
        }

        public void Reset(int currentDay)
        {
            entries.Clear(); consumed.Clear(); sequence = 0; day = currentDay;
        }
        public void BeginDay(int currentDay)
        {
            if (day == currentDay) return;
            // Event budgets belong to their day, never replay yesterday's successes after dawn.
            Reset(currentDay);
        }
        public void SetState(GuideMessageDefinition definition, bool active, string text, string context = "")
        {
            if (definition == null) return;
            if (!entries.TryGetValue(definition.Id, out var entry))
                entries.Add(definition.Id, entry = new Entry { Definition = definition });
            var changed = !entry.Active || entry.Context != context;
            entry.Text = text; entry.Context = context;
            if (active && changed)
            {
                entry.Sequence = ++sequence;
                var once = definition.Repeat == "once_per_day" || definition.Repeat == "once_per_dawn";
                entry.Remaining = once && consumed.Contains(definition.Id) ? 0f : 3f;
            }
            entry.Active = active;
        }
        public void Fire(GuideMessageDefinition definition, string text)
        {
            if (definition == null) return;
            if (!entries.TryGetValue(definition.Id, out var entry))
                entries.Add(definition.Id, entry = new Entry { Definition = definition });
            if ((definition.Repeat == "once_per_day" || definition.Repeat == "once_per_dawn") &&
                consumed.Contains(definition.Id)) return;
            entry.Text = text; entry.Active = true; entry.Sequence = ++sequence; entry.Remaining = 3f;
        }
        public Entry Select(bool menuOpen)
        {
            if (menuOpen) return null;
            Entry result = null;
            foreach (var entry in entries.Values)
            {
                if (!entry.Active || string.IsNullOrEmpty(entry.Text)) continue;
                if (entry.Definition.Repeat != "while_active" && entry.Remaining <= 0f) continue;
                if (result == null || entry.Definition.Tier < result.Definition.Tier ||
                    (entry.Definition.Tier == result.Definition.Tier && entry.Sequence > result.Sequence)) result = entry;
            }
            return result;
        }
        public void Advance(Entry visible, float seconds)
        {
            if (visible == null || !visible.Active || visible.Definition.Repeat == "while_active" ||
                seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            visible.Remaining = Math.Max(0f, visible.Remaining - seconds);
            if (visible.Remaining > 0f) return;
            if (visible.Definition.Repeat == "once_per_day" || visible.Definition.Repeat == "once_per_dawn")
                consumed.Add(visible.Definition.Id);
            else visible.Active = false;
        }
    }
}
