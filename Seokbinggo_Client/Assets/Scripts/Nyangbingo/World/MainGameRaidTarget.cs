using System;
using System.Collections.Generic;
using Nyangbingo.Data;
using Nyangbingo.Combat;
using Nyangbingo.Inventory;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 최종 플레이어/성벽 프리팹이 들어오기 전까지 제품 씬의 요괴 AI가 공유하는 아트 비의존 표적이다.
    /// 누적 벽 피해는 디버그·HUD 연결 지점으로만 보관하며 실제 건축물 내구도 모델로 교체할 수 있다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class MainGameRaidTarget : MonoBehaviour, IYokaiTarget, IWallMaterialTarget, IYokaiCombatTarget,
        IYokaiLootTarget, IYokaiTheftReceiptSource, IYokaiCounterSource,
        Nyangbingo.Bosses.IBossCombatTarget
    {
        [SerializeField] private YokaiWallMaterial wallMaterial = YokaiWallMaterial.Ice;
        private Inventory.Inventory playerInventory;
        private EquipmentSystem equipmentSystem;
        private MainGameWorldDropRuntime worldDrops;
        private MainGameBootstrap bootstrap;
        private int sealPenaltyStartDay = 4;
        private readonly List<ItemAmount> pendingStolenItems = new List<ItemAmount>();
        private readonly StatSheet statSheet = new StatSheet();
        public const float PaceWallDamageDeficitPercent = 40f;
        public const float PaceWallDamageMultiplier = 1.3f;

        public Transform TargetTransform => transform;
        public YokaiWallMaterial WallMaterial => wallMaterial;
        public bool IsInLanternRange => false;
        public bool IsInSieveRange => false;
        public bool HasGroundLoot => worldDrops != null && worldDrops.ActiveDropCount > 0;
        public bool IsInventoryTheftBlocked
        {
            get
            {
                statSheet.Recalculate(equipmentSystem);
                return statSheet.BlocksInventoryTheft;
            }
        }
        public float SieveStopSeconds => 0f;
        public float SieveCooldownSeconds => 0f;
        public float SieveDamageMultiplier => 0f;
        public float EoduksiniLanternPauseSeconds => 0f;
        public float EoduksiniBloomCooldownSeconds => 0f;
        public float EoduksiniLanternDamageMultiplier => 0f;
        public float AccumulatedWallDamage { get; private set; }
        public event Action<float> WallDamaged;

        public void ConfigureTheftRuntime(Inventory.Inventory inventory,
            EquipmentSystem equipment, MainGameWorldDropRuntime drops)
        {
            playerInventory = inventory;
            equipmentSystem = equipment;
            worldDrops = drops;
        }

        public bool ConfigureWallPaceRuntime(
            MainGameBootstrap mainBootstrap, int firstPenaltyDay)
        {
            if (mainBootstrap == null || firstPenaltyDay <= 0) return false;
            bootstrap = mainBootstrap;
            sealPenaltyStartDay = firstPenaltyDay;
            return true;
        }

        public bool TryStealGroundLoot()
        {
            pendingStolenItems.Clear();
            if (worldDrops == null ||
                !worldDrops.TryStealNearestStack(
                    transform.position, out var item, out var amount))
                return false;
            pendingStolenItems.Add(new ItemAmount { item = item, amount = amount });
            return true;
        }

        public bool TryStealInventory(int maxSlots, int maxAmount)
        {
            pendingStolenItems.Clear();
            if (IsInventoryTheftBlocked || playerInventory == null) return false;
            if (!playerInventory.TryRemoveFromOccupiedSlots(
                    maxSlots, maxAmount, out var removedStacks))
                return false;
            foreach (var stack in removedStacks)
            {
                var item = playerInventory.FindItem(stack.itemId);
                if (item != null && stack.amount > 0)
                    pendingStolenItems.Add(new ItemAmount { item = item, amount = stack.amount });
            }
            return pendingStolenItems.Count > 0;
        }

        public IReadOnlyList<ItemAmount> TakeStolenItems()
        {
            var receipt = pendingStolenItems.ToArray();
            pendingStolenItems.Clear();
            return receipt;
        }

        public void DamageWall(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            var timeService = bootstrap?.TimeService;
            var sealSystem = bootstrap?.SealSystem;
            var adjusted = timeService?.CurrentDayCurve != null && sealSystem != null
                ? CalculatePaceAdjustedWallDamage(
                    amount,
                    timeService.CurrentDayCurve.PaceSealPercent,
                    sealSystem.SealPercent * 100f,
                    timeService.Day,
                    sealPenaltyStartDay)
                : amount;
            AccumulatedWallDamage += adjusted;
            WallDamaged?.Invoke(adjusted);
        }

        public static float CalculatePaceAdjustedWallDamage(
            float baseDamage, float recommendedSealPercent, float currentSealPercent,
            int day, int firstPenaltyDay)
        {
            if (baseDamage <= 0f || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage))
                return 0f;
            if (float.IsNaN(recommendedSealPercent) || float.IsInfinity(recommendedSealPercent) ||
                float.IsNaN(currentSealPercent) || float.IsInfinity(currentSealPercent) ||
                day <= 0 || firstPenaltyDay <= 0)
                return baseDamage;
            var behindPace = day >= firstPenaltyDay &&
                             recommendedSealPercent - currentSealPercent >=
                             PaceWallDamageDeficitPercent;
            return baseDamage * (behindPace ? PaceWallDamageMultiplier : 1f);
        }

        public bool TryApplyContactDamage(int amount)
        {
            if (amount <= 0) return false;
            var health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            var before = health.Current;
            health.ApplyDamage(amount, Nyangbingo.Core.DamageTag.Melee);
            if (health.Current >= before) return false;
            Nyangbingo.Core.GameEvents.RaisePlayerDamaged();
            return true;
        }

        public bool TryApplyBossSpecialDamage(int amount, Nyangbingo.Core.DamageTag tag,
            Vector2 knockback,
            Nyangbingo.Core.DamageDelivery delivery = Nyangbingo.Core.DamageDelivery.Direct)
        {
            if (amount <= 0) return false;
            var health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            var before = health.Current;
            health.ApplyDamage(amount, tag, delivery);
            if (health.Current >= before) return false;
            if (knockback.sqrMagnitude > Mathf.Epsilon)
            {
                var playerController = GetComponent<MainGamePlayerController>();
                if (playerController == null || !playerController.TryApplyBossKnockback(knockback))
                {
                    var body = GetComponent<Rigidbody2D>();
                    if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
                        body.position += knockback;
                    else
                        health.TryApplyKnockback(knockback);
                }
            }
            Nyangbingo.Core.GameEvents.RaisePlayerDamaged();
            return true;
        }
    }
}
