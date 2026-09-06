using System;
using Nyangbingo.Core;

namespace Nyangbingo.World
{
    /// <summary>
    /// 화력 터렛 진화 트리 — items.csv 노트 기준 (등탑 DPS·감속·2발/초·도관 무소모·부채꼴).
    /// </summary>
    public static class DamageTurretRules
    {
        public const string EarlyId = SeokbinggoRules.EarlyTurretId;
        public const string SingijeonId = SeokbinggoRules.SingijeonTurretId;
        public const string SeongeId = SeokbinggoRules.SeongeTurretId;
        public const string ColdWaveTowerId = SeokbinggoRules.ColdWaveTurretId;
        public const string IceRootBatteryId = "ice_root_battery";
        public const string ColdWaveBatteryId = "cold_wave_battery";

        /// <summary>석빙고 5단(도관 석빙고)부터 도관 연결로 취급.</summary>
        public const int ConduitLinkMinStage = 5;

        public const float SeongeFrostSlowFraction = 0.35f;
        public const float SeongeFrostSlowSeconds = 2.5f;
        public const float ColdWaveBatteryFanDegrees = 90f;

        public readonly struct Profile
        {
            public readonly string DefinitionId;
            public readonly int Damage;
            public readonly float FireIntervalSeconds;
            public readonly float RangeTiles;
            public readonly float RetargetSeconds;
            public readonly DamageTag DamageTag;
            public readonly bool AppliesFrostSlow;
            public readonly bool FanConeAttack;
            public readonly bool FreeFuelWhenConduitLinked;

            public Profile(string definitionId, int damage, float fireIntervalSeconds, float rangeTiles,
                float retargetSeconds, DamageTag damageTag, bool appliesFrostSlow, bool fanConeAttack,
                bool freeFuelWhenConduitLinked)
            {
                DefinitionId = definitionId;
                Damage = damage;
                FireIntervalSeconds = fireIntervalSeconds;
                RangeTiles = rangeTiles;
                RetargetSeconds = retargetSeconds;
                DamageTag = damageTag;
                AppliesFrostSlow = appliesFrostSlow;
                FanConeAttack = fanConeAttack;
                FreeFuelWhenConduitLinked = freeFuelWhenConduitLinked;
            }
        }

        private static readonly string[] DamageIds =
        {
            EarlyId, SingijeonId, SeongeId, ColdWaveTowerId, IceRootBatteryId, ColdWaveBatteryId
        };

        public static bool IsDamageTurretId(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            for (var index = 0; index < DamageIds.Length; index++)
            {
                if (string.Equals(definitionId, DamageIds[index], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool TryGetProfile(string definitionId, out Profile profile)
        {
            profile = default;
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            if (string.Equals(definitionId, EarlyId, StringComparison.Ordinal))
            {
                profile = new Profile(EarlyId, 10, 1f, 8f, 0.2f, DamageTag.Fire, false, false, false);
                return true;
            }
            if (string.Equals(definitionId, SingijeonId, StringComparison.Ordinal))
            {
                profile = new Profile(SingijeonId, 12, 1f, 8f, 0.2f, DamageTag.Fire, false, false, false);
                return true;
            }
            if (string.Equals(definitionId, SeongeId, StringComparison.Ordinal))
            {
                profile = new Profile(SeongeId, 12, 1f, 8f, 0.2f, DamageTag.Ice, true, false, false);
                return true;
            }
            if (string.Equals(definitionId, ColdWaveTowerId, StringComparison.Ordinal))
            {
                // 유도 2발/초
                profile = new Profile(ColdWaveTowerId, 14, 0.5f, 8f, 0.15f, DamageTag.Ice, true, false, false);
                return true;
            }
            if (string.Equals(definitionId, IceRootBatteryId, StringComparison.Ordinal))
            {
                profile = new Profile(IceRootBatteryId, 13, 0.85f, 9f, 0.2f, DamageTag.Ice, true, false, true);
                return true;
            }
            if (string.Equals(definitionId, ColdWaveBatteryId, StringComparison.Ordinal))
            {
                profile = new Profile(ColdWaveBatteryId, 15, 1.2f, 7f, 0.25f, DamageTag.Ice, true, true, true);
                return true;
            }
            return false;
        }

        public static bool IsConduitLinked(int seokbinggoStage) =>
            seokbinggoStage >= ConduitLinkMinStage;
    }
}
