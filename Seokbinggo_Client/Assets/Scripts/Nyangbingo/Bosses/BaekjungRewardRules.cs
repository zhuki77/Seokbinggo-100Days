using System;
using Nyangbingo.Data;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public sealed class BaekjungRewardRules : IYokaiRewardPolicy
    {
        private readonly DayEventDefinition definition;
        private float tearRemainder;

        public BaekjungRewardRules(DayEventDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public float TearRemainder => tearRemainder;

        public int ScaleTearAmount(int baseAmount)
        {
            if (baseAmount <= 0) return 0;

            var scaledAmount = baseAmount * Mathf.Max(0f, definition.TearMultiplier) + tearRemainder;
            var grantedAmount = Mathf.FloorToInt(scaledAmount + .0001f);
            tearRemainder = Mathf.Clamp(scaledAmount - grantedAmount, 0f, .9999f);
            return grantedAmount;
        }

        public float ScaleSignatureChance(float baseChance)
        {
            if (baseChance <= 0f) return 0f;
            return Mathf.Clamp01(baseChance * Mathf.Max(0f, definition.SignatureMultiplier));
        }

        public void RestoreTearRemainder(float value)
        {
            tearRemainder = float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp(value, 0f, .9999f);
        }
    }
}
