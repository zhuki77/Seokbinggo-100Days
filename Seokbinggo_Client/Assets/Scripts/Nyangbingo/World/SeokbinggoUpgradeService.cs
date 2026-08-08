using System;
using System.Collections.Generic;

namespace Nyangbingo.World
{
    /// <summary>
    /// 석빙고 단계(1..6) 추적. 업그레이드 시 인벤토리 소모는 콜백에 위임한다.
    /// stage &gt;= 4 이면 대장간 해금, 터렛 슬롯 상한은 현재 stage.
    /// </summary>
    public sealed class SeokbinggoUpgradeService
    {
        public const int MinStage = 1;
        public const int MaxStage = 6;

        /// <summary>
        /// itemId, count 를 인벤에서 원자적으로 소모할 수 있으면 true.
        /// 부분 소모 없이 전부 가능하거나 전부 실패해야 한다.
        /// </summary>
        public delegate bool TryConsumeMaterials(IReadOnlyList<KeyValuePair<string, int>> materials);

        private static readonly IReadOnlyList<KeyValuePair<string, int>>[] CostsToReachStage =
        {
            null, // unused (stage 0)
            // stage 1
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("stone", 20),
                new KeyValuePair<string, int>("dirt", 10),
                new KeyValuePair<string, int>("wood", 8)
            },
            // stage 2
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("stone", 30),
                new KeyValuePair<string, int>("ice_shard", 10),
                new KeyValuePair<string, int>("iron_ingot", 2)
            },
            // stage 3
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("stone", 40),
                new KeyValuePair<string, int>("clay", 20),
                new KeyValuePair<string, int>("iron_ingot", 4)
            },
            // stage 4
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("yokai_tear", 160),
                new KeyValuePair<string, int>("icesteel_ingot", 6),
                new KeyValuePair<string, int>("stone", 50)
            },
            // stage 5
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("yokai_tear", 320),
                new KeyValuePair<string, int>("icesteel_ingot", 10),
                new KeyValuePair<string, int>("frost_essence", 4)
            },
            // stage 6
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("yokai_tear", 560),
                new KeyValuePair<string, int>("icesteel_ingot", 16),
                new KeyValuePair<string, int>("frost_essence", 8)
            }
        };

        private int stage;

        public SeokbinggoUpgradeService(int initialStage = MinStage)
        {
            Stage = initialStage;
        }

        public int Stage
        {
            get => stage;
            private set => stage = Math.Clamp(value, MinStage, MaxStage);
        }

        public bool IsSmithyUnlocked => Stage >= 4;
        public int TurretSlotCap => Stage;
        public bool CanUpgrade => Stage < MaxStage;

        public IReadOnlyList<KeyValuePair<string, int>> NextUpgradeCost =>
            CanUpgrade ? CostsToReachStage[Stage + 1] : Array.Empty<KeyValuePair<string, int>>();

        public static IReadOnlyList<KeyValuePair<string, int>> CostToReach(int targetStage)
        {
            if (targetStage < MinStage || targetStage > MaxStage)
                return Array.Empty<KeyValuePair<string, int>>();
            return CostsToReachStage[targetStage];
        }

        public bool TryUpgrade(TryConsumeMaterials tryConsume)
        {
            if (tryConsume == null) throw new ArgumentNullException(nameof(tryConsume));
            if (!CanUpgrade) return false;

            var cost = CostsToReachStage[Stage + 1];
            if (cost == null || cost.Count == 0) return false;
            if (!tryConsume(cost)) return false;

            Stage = Stage + 1;
            return true;
        }

        public void SetStage(int value) => Stage = value;

        /// <summary>
        /// 설치된 석빙고 모듈 ID(seokbinggo_sN)에서 단계를 동기화한다. Stage = max(현재, N).
        /// </summary>
        public void SyncFromPlacedModuleIds(IEnumerable<string> ids)
        {
            if (ids == null) return;
            var maxStage = Stage;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!TryParseSeokbinggoStage(id, out var parsed)) continue;
                if (parsed > maxStage) maxStage = parsed;
            }
            Stage = maxStage;
        }

        private static bool TryParseSeokbinggoStage(string id, out int stage)
        {
            stage = 0;
            const string prefix = "seokbinggo_s";
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var suffix = id.Substring(prefix.Length);
            return int.TryParse(suffix, out stage) && stage >= MinStage && stage <= MaxStage;
        }
    }
}
