using System;
using System.Globalization;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v72 A-4/A-5: 날짜와 독립된 단조 증가 폭염 단계. 관문 네임드 처치만 단계를 올리며,
    /// 단계별 낮 화상·나무 밀도 값은 globals.csv에서 함께 읽는다.
    /// </summary>
    public sealed class HeatStageService
    {
        private readonly int stageCount;
        private readonly string gateOneToTwo;
        private readonly string gateTwoToThree;
        private readonly float[] dayFireDamageByStage;
        private readonly float[] treeDensityByStage;

        private HeatStageService(int count, string firstGate, string secondGate,
            float[] fireDamage, float[] treeDensity)
        {
            stageCount = count;
            gateOneToTwo = firstGate;
            gateTwoToThree = secondGate;
            dayFireDamageByStage = fireDamage;
            treeDensityByStage = treeDensity;
            Current = 1;
        }

        public int Current { get; private set; }
        public int StageCount => stageCount;
        public float DayFireDamagePerSecond => dayFireDamageByStage[Current - 1];

        public float ResolveDayFireDamagePerSecond(int stageReduction, int stageEscalation = 0)
        {
            var effectiveStage = PlayerTemperatureState.CalculateEffectiveHeatStage(
                Current, stageReduction, stageEscalation, stageCount);
            return dayFireDamageByStage[effectiveStage - 1];
        }

        public float ResolveDayFireDamagePerSecond(int day, int stageReduction, int stageEscalation = 0)
        {
            var effectiveStage = PlayerTemperatureState.CalculateEffectiveHeatStage(
                Current, stageReduction, stageEscalation, stageCount);
            if (day >= DayCurveCombatRules.ExpansionLinearDayFireDamageStartDay)
                return effectiveStage * DayCurveCombatRules.ExpansionDayFireDamagePerStage;
            return dayFireDamageByStage[effectiveStage - 1];
        }

        public float TreeDensityMultiplier => treeDensityByStage[Current - 1];
        public event Action<int> Changed;

        public static bool TryCreate(GameDataCatalog catalog, out HeatStageService service)
        {
            service = null;
            if (catalog == null ||
                !TryReadInt(catalog, GlobalKeys.HeatStageCount, out var count) || count != 3 ||
                Read(catalog, GlobalKeys.HeatStageTrigger) != "named_boss_kill" ||
                string.IsNullOrWhiteSpace(Read(catalog, GlobalKeys.HeatStageGateOneToTwo)) ||
                string.IsNullOrWhiteSpace(Read(catalog, GlobalKeys.HeatStageGateTwoToThree)) ||
                !TryReadCurve(catalog, GlobalKeys.DayFireDamageByStage, count, out var fireDamage) ||
                !TryReadCurve(catalog, GlobalKeys.TreeDecayByStage, count, out var treeDensity))
                return false;

            service = new HeatStageService(
                count,
                Read(catalog, GlobalKeys.HeatStageGateOneToTwo),
                Read(catalog, GlobalKeys.HeatStageGateTwoToThree),
                fireDamage,
                treeDensity);
            return true;
        }

        public bool OnNamedKill(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId) || Current >= stageCount) return false;
            var expected = Current == 1 ? gateOneToTwo : gateTwoToThree;
            if (!string.Equals(bossId, expected, StringComparison.Ordinal)) return false;
            Current++;
            Changed?.Invoke(Current);
            return true;
        }

        public bool Restore(int heatStage)
        {
            if (heatStage < 1 || heatStage > stageCount) return false;
            if (Current == heatStage) return true;
            Current = heatStage;
            Changed?.Invoke(Current);
            return true;
        }

        public static bool TryParseStageCurve(string raw, int expectedCount, out float[] values)
        {
            values = null;
            if (string.IsNullOrWhiteSpace(raw) || expectedCount <= 0) return false;
            var parts = raw.Split(new[] { '/', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != expectedCount) return false;
            var parsed = new float[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                if (!float.TryParse(parts[index].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out parsed[index]) ||
                    float.IsNaN(parsed[index]) || float.IsInfinity(parsed[index]) || parsed[index] < 0f)
                    return false;
            }
            values = parsed;
            return true;
        }

        private static bool TryReadCurve(GameDataCatalog catalog, string key, int count, out float[] values) =>
            TryParseStageCurve(Read(catalog, key), count, out values);

        private static bool TryReadInt(GameDataCatalog catalog, string key, out int value)
        {
            var definition = catalog.FindGlobal(key);
            if (definition != null) return definition.TryGetInt(out value);
            value = 0;
            return false;
        }

        private static string Read(GameDataCatalog catalog, string key) => catalog.FindGlobal(key)?.Value;
    }
}
