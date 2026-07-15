using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Core
{
    public static class GameEvents
    {
        public static event Action OnDayStart;
        public static event Action OnNightStart;
        public static event Action OnDawnWarning;
        public static event Action<Vector3Int> OnTilePlaced;
        public static event Action<Vector3Int> OnTileBroken;
        public static event Action<YokaiDefinition> OnYokaiKilled;
        public static event Action<BossDefinition> OnBossSummoned;
        public static event Action<BossDefinition> OnBossDefeated;
        public static event Action OnSealChanged;
        public static event Action OnBaekjungStart;

        public static void RaiseDayStart() => OnDayStart?.Invoke();
        public static void RaiseNightStart() => OnNightStart?.Invoke();
        public static void RaiseDawnWarning() => OnDawnWarning?.Invoke();
        public static void RaiseTilePlaced(Vector3Int position) => OnTilePlaced?.Invoke(position);
        public static void RaiseTileBroken(Vector3Int position) => OnTileBroken?.Invoke(position);
        public static void RaiseYokaiKilled(YokaiDefinition definition) => OnYokaiKilled?.Invoke(definition);
        public static void RaiseBossSummoned(BossDefinition definition) => OnBossSummoned?.Invoke(definition);
        public static void RaiseBossDefeated(BossDefinition definition) => OnBossDefeated?.Invoke(definition);
        public static void RaiseSealChanged() => OnSealChanged?.Invoke();
        public static void RaiseBaekjungStart() => OnBaekjungStart?.Invoke();
    }
}
