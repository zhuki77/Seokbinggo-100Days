using System;
using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Core;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiRewardPolicy
    {
        int ScaleTearAmount(int baseAmount);
        int ScaleDropAmount(int baseAmount);
        float ScaleSignatureChance(float baseChance);
    }

    public interface ILootRandomSource
    {
        float Next01();
    }

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

    public sealed class YokaiLoot : MonoBehaviour
    {
        private sealed class UnityLootRandomSource : ILootRandomSource
        {
            public float Next01() => UnityEngine.Random.value;
        }

        private static readonly ILootRandomSource DefaultRandomSource = new UnityLootRandomSource();

        [SerializeField] private YokaiDefinition definition;
        [SerializeField] private Health health;
        private ILootRandomSource randomSource;
        private IYokaiRewardPolicy rewardPolicy;
        private readonly List<ItemAmount> stolenItems = new List<ItemAmount>();
        private bool theftSucceeded;
        public event Action<ItemDefinition, int> Dropped;

        public void ConfigureForRuntime(YokaiDefinition value, ILootRandomSource random = null, IYokaiRewardPolicy rewards = null)
        {
            definition = value;
            randomSource = random;
            rewardPolicy = rewards;
        }

        public void RecordStolenItems(IReadOnlyList<ItemAmount> items)
        {
            if (items == null || items.Count == 0) return;
            foreach (var stack in items)
                if (stack.item != null && stack.amount > 0)
                    stolenItems.Add(stack);
            theftSucceeded |= stolenItems.Count > 0;
        }

        public List<InventorySlot> CaptureStolenItems()
        {
            var result = new List<InventorySlot>(stolenItems.Count);
            foreach (var stack in stolenItems)
                if (stack.item != null && stack.amount > 0)
                    result.Add(new InventorySlot
                    {
                        itemId = stack.item.Id,
                        amount = stack.amount
                    });
            return result;
        }

        public bool RestoreStolenItems(IEnumerable<InventorySlot> items,
            Func<string, ItemDefinition> findItem)
        {
            stolenItems.Clear();
            theftSucceeded = false;
            if (items == null) return true;
            foreach (var stack in items)
            {
                var item = findItem?.Invoke(stack.itemId);
                if (item == null || stack.amount <= 0 || stack.amount > item.MaxStack)
                {
                    stolenItems.Clear();
                    return false;
                }
                stolenItems.Add(new ItemAmount { item = item, amount = stack.amount });
            }
            theftSucceeded = stolenItems.Count > 0;
            return true;
        }

        private void Reset() => health = GetComponent<Health>();
        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null) health.Died += DropAll;
        }
        private void OnDisable()
        {
            if (health != null) health.Died -= DropAll;
        }
        private void DropAll()
        {
            if (definition == null) return;
            foreach (var drop in definition.Drops)
                if (drop.item != null && drop.amount > 0)
                    Grant(drop.item, ScaleDropAmount(drop.amount));

            var tearAmount = rewardPolicy != null
                ? rewardPolicy.ScaleTearAmount(definition.TearDrop)
                : definition.TearDrop;
            Grant(definition.TearItem, tearAmount);
            if (theftSucceeded && definition.Kind == YokaiKind.Yagwanggwi)
            {
                foreach (var stolen in stolenItems)
                    Grant(stolen.item, stolen.amount);
                Grant(definition.TearItem, definition.TearBonus);
            }

            var signatureChance = rewardPolicy != null
                ? rewardPolicy.ScaleSignatureChance(definition.SignatureChance)
                : Mathf.Clamp01(definition.SignatureChance);
            if (float.IsNaN(signatureChance) || float.IsInfinity(signatureChance)) signatureChance = 0f;
            signatureChance = Mathf.Clamp01(signatureChance);
            var signatureConditionSatisfied =
                definition.SignatureCondition != YokaiSignatureCondition.StealSuccess ||
                theftSucceeded;
            if (definition.SignatureItem != null && signatureChance > 0f &&
                signatureConditionSatisfied)
            {
                var roll = (randomSource ?? DefaultRandomSource).Next01();
                if (!float.IsNaN(roll) && !float.IsInfinity(roll) && roll >= 0f && roll <= 1f &&
                    roll < signatureChance) Grant(definition.SignatureItem, 1);
            }

            GameEvents.RaiseYokaiKilled(definition);
        }

        private int ScaleDropAmount(int baseAmount) =>
            rewardPolicy == null ? baseAmount : rewardPolicy.ScaleDropAmount(baseAmount);

        private void Grant(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            Dropped?.Invoke(item, amount);
            WorldItemDropRequest.Request(item, amount, transform.position);
        }
    }
}
