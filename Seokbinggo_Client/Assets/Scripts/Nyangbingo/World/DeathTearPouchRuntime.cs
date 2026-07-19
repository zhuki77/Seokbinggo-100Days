using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 사망 시 빠진 요괴 눈물을 회수 가능한 월드 주머니로 보관한다. 주머니는 사망마다 하나씩 생기며,
    /// 사망 다음 날의 밤이 끝나는 새벽(D+2)에 만료된다.
    /// </summary>
    public sealed class DeathTearPouchRuntime : IDisposable
    {
        public const string TearItemId = "yokai_tear";
        private readonly Inventory.Inventory inventory;
        private readonly DayNightService timeService;
        private readonly List<DeathTearPouchRecord> active = new List<DeathTearPouchRecord>();
        private int nextSequence = 1;
        private bool disposed;

        public DeathTearPouchRuntime(Inventory.Inventory playerInventory, DayNightService clock)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            timeService.Dawn += HandleDawn;
        }

        public IReadOnlyList<DeathTearPouchRecord> Active => active;
        public event Action Changed;

        public int DropTwentyPercent(Vector2 position)
        {
            if (disposed || !IsFinite(position)) return 0;
            var amount = inventory.Count(TearItemId) / 5;
            if (amount <= 0 || !inventory.TryRemove(TearItemId, amount)) return 0;

            var expireOnDay = timeService.Day >= int.MaxValue - 1 ? int.MaxValue : timeService.Day + 2;
            active.Add(new DeathTearPouchRecord
            {
                pouchId = BuildUniqueId(timeService.Day),
                amount = amount,
                position = position,
                expireOnDay = expireOnDay
            });
            Changed?.Invoke();
            return amount;
        }

        public bool TryCollect(string pouchId)
        {
            if (disposed || string.IsNullOrWhiteSpace(pouchId)) return false;
            var index = active.FindIndex(record => record.pouchId == pouchId);
            if (index < 0) return false;
            var record = active[index];
            if (!inventory.TryAdd(TearItemId, record.amount)) return false;
            active.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        public bool TryCollectWithin(Vector2 position, float radius)
        {
            if (disposed || !IsFinite(position) || float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                return false;
            var radiusSquared = radius * radius;
            var candidate = active
                .Where(record => (record.position - position).sqrMagnitude <= radiusSquared)
                .OrderBy(record => (record.position - position).sqrMagnitude)
                .ThenBy(record => record.pouchId, StringComparer.Ordinal)
                .FirstOrDefault();
            return !string.IsNullOrEmpty(candidate.pouchId) && TryCollect(candidate.pouchId);
        }

        public List<DeathTearPouchRecord> Export() => active
            .OrderBy(record => record.pouchId, StringComparer.Ordinal)
            .ToList();

        public bool Restore(IEnumerable<DeathTearPouchRecord> records)
        {
            if (disposed || records == null) return false;
            var restored = new List<DeathTearPouchRecord>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.pouchId) || !ids.Add(record.pouchId) || record.amount <= 0 ||
                    record.expireOnDay < 2 || !IsFinite(record.position)) return false;
                if (record.expireOnDay > timeService.Day) restored.Add(record);
            }
            active.Clear();
            active.AddRange(restored.OrderBy(record => record.pouchId, StringComparer.Ordinal));
            nextSequence = Math.Max(1, active.Count + 1);
            Changed?.Invoke();
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            timeService.Dawn -= HandleDawn;
            active.Clear();
        }

        private void HandleDawn()
        {
            if (disposed) return;
            var removed = active.RemoveAll(record => record.expireOnDay <= timeService.Day);
            if (removed > 0) Changed?.Invoke();
        }

        private string BuildUniqueId(int day)
        {
            string id;
            do id = $"death_tears_{Math.Max(1, day)}_{nextSequence++}";
            while (active.Any(record => record.pouchId == id));
            return id;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
