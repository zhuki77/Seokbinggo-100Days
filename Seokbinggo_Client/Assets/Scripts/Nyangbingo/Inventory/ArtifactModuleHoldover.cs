using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// 업의 비늘(eop_scale) — 연료·수명 소진 후 모듈 효과를 잠시 유지한다.
    /// </summary>
    public sealed class ArtifactModuleHoldover : IGameSecondsTickable
    {
        private readonly Dictionary<string, float> remainingSeconds =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public bool TryBegin(string objectId, float seconds)
        {
            if (string.IsNullOrWhiteSpace(objectId) || seconds <= 0f ||
                float.IsNaN(seconds) || float.IsInfinity(seconds))
                return false;
            remainingSeconds[objectId] = Mathf.Max(
                remainingSeconds.TryGetValue(objectId, out var existing) ? existing : 0f,
                seconds);
            return true;
        }

        public bool IsActive(string objectId) =>
            !string.IsNullOrWhiteSpace(objectId) &&
            remainingSeconds.TryGetValue(objectId, out var remaining) &&
            remaining > 0f;

        public void Clear(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return;
            remainingSeconds.Remove(objectId);
        }

        public void Tick(float deltaGameSeconds)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) ||
                float.IsInfinity(deltaGameSeconds) || remainingSeconds.Count == 0)
                return;
            expired.Clear();
            foreach (var pair in remainingSeconds)
            {
                var next = Mathf.Max(0f, pair.Value - deltaGameSeconds);
                if (next <= 0f) expired.Add(pair.Key);
                else remainingSeconds[pair.Key] = next;
            }
            for (var index = 0; index < expired.Count; index++)
                remainingSeconds.Remove(expired[index]);
        }

        private readonly List<string> expired = new List<string>();
    }
}
