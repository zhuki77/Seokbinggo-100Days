using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.World
{
    /// <summary>
    /// 31일 이후 파도 밤 상태 머신 — 표/상수는 CSV·globals, 스폰 실행은 Encounter가 담당.
    /// </summary>
    public sealed class WaveNightController
    {
        private readonly List<WaveNightRules.WaveRow> rows;
        private readonly int period;
        private readonly int offset;
        private readonly float advanceSec;
        private readonly int yokaiCap;
        private readonly string waveMultTarget;

        private int day;
        private bool active;
        private bool bigNight;
        private int maxWave;
        private int currentWave;
        private int scoreWave;
        private int nightKills;
        private float lastAppliedMult = 1f;

        public WaveNightController(
            IReadOnlyList<WaveNightRules.WaveRow> rows,
            int period,
            int offset,
            float advanceSec,
            int yokaiCap,
            string waveMultTarget)
        {
            this.rows = rows != null
                ? new List<WaveNightRules.WaveRow>(rows)
                : new List<WaveNightRules.WaveRow>();
            this.period = period > 0 ? period : 10;
            this.offset = offset;
            this.advanceSec = advanceSec > 0f ? advanceSec : 108f;
            this.yokaiCap = yokaiCap > 0 ? yokaiCap : 8;
            this.waveMultTarget = string.IsNullOrWhiteSpace(waveMultTarget) ? "hp_only" : waveMultTarget;
        }

        public bool IsActive => active;
        public int CurrentWave => currentWave;
        public int MaxWave => maxWave;
        public bool IsBigNight => bigNight;
        public float CurrentHpMult => lastAppliedMult;
        public string WaveMultTarget => waveMultTarget;
        public int YokaiCap => yokaiCap;

        public bool BeginNight(int nightDay)
        {
            day = nightDay;
            active = WaveNightRules.UsesNightWaves(nightDay) && rows.Count > 0;
            if (!active)
            {
                Reset();
                return false;
            }

            bigNight = WaveNightRules.IsBigNight(nightDay, period, offset);
            maxWave = WaveNightRules.MaxWaveForNight(bigNight);
            currentWave = 0;
            scoreWave = 1;
            nightKills = 0;
            lastAppliedMult = 1f;
            return true;
        }

        public void EndNight()
        {
            active = false;
            Reset();
        }

        public void RegisterKill()
        {
            if (!active) return;
            nightKills++;
            scoreWave = Math.Min(maxWave, 1 + nightKills / yokaiCap);
        }

        /// <summary>
        /// 시간·점수 기준으로 파도를 올리고, 새로 열린 파도마다 빈 슬롯분만 composition을 반환한다.
        /// </summary>
        public bool TryAdvance(float nightElapsedSec, int aliveCount, out List<PendingWaveSpawn> spawns)
        {
            spawns = null;
            if (!active) return false;

            var target = Math.Min(maxWave,
                WaveNightRules.CurrentWave(nightElapsedSec, scoreWave, advanceSec));
            if (target <= currentWave) return false;

            spawns = new List<PendingWaveSpawn>();
            var free = WaveNightRules.FreeSlots(yokaiCap, aliveCount);
            for (var wave = currentWave + 1; wave <= target && free > 0; wave++)
            {
                if (!WaveNightRules.TryFindRow(rows, day, wave, out var row)) continue;
                var composition = WaveNightRules.ParseComposition(row.Composition);
                var mult = row.Mult > 0f ? row.Mult : WaveNightRules.MultForWave(wave);
                lastAppliedMult = mult;
                free = AppendComposition(spawns, composition, mult, free);
                currentWave = wave;
            }

            if (target > currentWave) currentWave = target;
            return spawns.Count > 0;
        }

        public float ApplyHpMult(float baseHp) =>
            WaveNightRules.ApplyHpMult(baseHp, lastAppliedMult, waveMultTarget);

        public float ApplyHpMult(float baseHp, float mult) =>
            WaveNightRules.ApplyHpMult(baseHp, mult, waveMultTarget);

        private static int AppendComposition(
            List<PendingWaveSpawn> spawns,
            DayCurveSpawnAmount[] composition,
            float mult,
            int free)
        {
            if (composition == null || free <= 0) return free;
            for (var i = 0; i < composition.Length && free > 0; i++)
            {
                var group = composition[i];
                var amount = Math.Max(0, group.amount);
                while (amount > 0 && free > 0)
                {
                    spawns.Add(new PendingWaveSpawn(group.kind, mult));
                    amount--;
                    free--;
                }
            }

            return free;
        }

        private void Reset()
        {
            day = 0;
            bigNight = false;
            maxWave = 0;
            currentWave = 0;
            scoreWave = 1;
            nightKills = 0;
            lastAppliedMult = 1f;
        }

        public readonly struct PendingWaveSpawn
        {
            public PendingWaveSpawn(YokaiKind kind, float hpMult)
            {
                Kind = kind;
                HpMult = hpMult;
            }

            public YokaiKind Kind { get; }
            public float HpMult { get; }
        }
    }
}
