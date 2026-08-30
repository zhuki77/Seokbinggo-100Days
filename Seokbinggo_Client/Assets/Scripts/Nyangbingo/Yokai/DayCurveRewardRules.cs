using System;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    /// <summary>
    /// day-curve / day-curve-ext drop_mult — 요괴 재료 드랍·눈물 수량에 소수 배율을 적용한다.
    /// </summary>
    public sealed class DayCurveRewardRules : IYokaiRewardPolicy
    {
        private readonly float dropMultiplier;
        private float remainder;

        public DayCurveRewardRules(float dropMultiplier)
        {
            if (float.IsNaN(dropMultiplier) || float.IsInfinity(dropMultiplier))
                dropMultiplier = 1f;
            this.dropMultiplier = Mathf.Max(0f, dropMultiplier);
        }

        public float Remainder => remainder;

        public int ScaleTearAmount(int baseAmount) => ScaleAmount(baseAmount);

        public int ScaleDropAmount(int baseAmount) => ScaleAmount(baseAmount);

        public float ScaleSignatureChance(float baseChance) => baseChance;

        public void RestoreRemainder(float value)
        {
            remainder = float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp(value, 0f, .9999f);
        }

        private int ScaleAmount(int baseAmount)
        {
            if (baseAmount <= 0 || dropMultiplier <= 1f + .0001f) return baseAmount;
            var scaledAmount = baseAmount * dropMultiplier + remainder;
            var grantedAmount = Mathf.FloorToInt(scaledAmount + .0001f);
            remainder = Mathf.Clamp(scaledAmount - grantedAmount, 0f, .9999f);
            return grantedAmount;
        }
    }

    public sealed class ChainedYokaiRewardPolicy : IYokaiRewardPolicy
    {
        private readonly IYokaiRewardPolicy[] policies;

        private ChainedYokaiRewardPolicy(IYokaiRewardPolicy[] policies) =>
            this.policies = policies ?? Array.Empty<IYokaiRewardPolicy>();

        public static IYokaiRewardPolicy Create(params IYokaiRewardPolicy[] policies)
        {
            if (policies == null || policies.Length == 0) return null;
            IYokaiRewardPolicy combined = null;
            foreach (var policy in policies)
            {
                if (policy == null) continue;
                combined = combined == null ? policy : new ChainedYokaiRewardPolicy(combined, policy);
            }
            return combined;
        }

        private ChainedYokaiRewardPolicy(IYokaiRewardPolicy first, IYokaiRewardPolicy second) =>
            policies = new[] { first, second };

        public int ScaleTearAmount(int baseAmount)
        {
            var amount = baseAmount;
            for (var index = 0; index < policies.Length; index++)
                amount = policies[index].ScaleTearAmount(amount);
            return amount;
        }

        public int ScaleDropAmount(int baseAmount)
        {
            var amount = baseAmount;
            for (var index = 0; index < policies.Length; index++)
                amount = policies[index].ScaleDropAmount(amount);
            return amount;
        }

        public float ScaleSignatureChance(float baseChance)
        {
            var chance = baseChance;
            for (var index = 0; index < policies.Length; index++)
                chance = policies[index].ScaleSignatureChance(chance);
            return chance;
        }
    }
}
