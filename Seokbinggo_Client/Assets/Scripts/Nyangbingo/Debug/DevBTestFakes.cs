using System;
using System.Collections.Generic;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    public sealed class DevBTestGameSecondsSource : MonoBehaviour, IGameSecondsSource
    {
        public float GameSeconds { get; set; }
    }

    public sealed class DevBTestTimeSource : MonoBehaviour, ISaveableTimeSource
    {
        public int Day { get; set; } = 1;
        public bool IsNight { get; set; } = true;
        public float TimeOfDayGameSeconds { get; set; }
        public event Action Dawn;
        public void RaiseDawn() => Dawn?.Invoke();
        public bool RestoreTimeState(int day, float timeOfDayGameSeconds, bool isNight)
        {
            if (day < 1 || timeOfDayGameSeconds < 0f || float.IsNaN(timeOfDayGameSeconds) ||
                float.IsInfinity(timeOfDayGameSeconds)) return false;
            Day = day;
            TimeOfDayGameSeconds = timeOfDayGameSeconds;
            IsNight = isNight;
            return true;
        }
    }
    public sealed class DevBTestSpawnController : MonoBehaviour, IRegularSpawnController
    {
        public bool IsRegularSpawning { get; private set; } = true;
        public void SetRegularSpawning(bool enabled) => IsRegularSpawning = enabled;
    }

    public sealed class DevBTestBossSummonSite : IBossSummonSite
    {
        public bool IsDeepAltar { get; set; }
        public bool IsAtDeepAltar(Nyangbingo.Data.BossDefinition definition) => IsDeepAltar;
    }

    public sealed class DevBTestForcedBossSpawnController : IForcedBossSpawnController
    {
        public int SpawnCount { get; private set; }
        public Nyangbingo.Combat.Health LastSpawnedHealth { get; private set; }

        public Nyangbingo.Combat.Health SpawnBoss(Nyangbingo.Data.BossDefinition definition)
        {
            if (definition == null) return null;
            var bossObject = new GameObject($"TemporaryForcedBoss_{definition.Id}");
            LastSpawnedHealth = bossObject.AddComponent<Nyangbingo.Combat.Health>();
            LastSpawnedHealth.ConfigureForRuntime(definition.HitPoints);
            SpawnCount++;
            return LastSpawnedHealth;
        }
    }

    public readonly struct DevBTestBaekjungSpawnRecord
    {
        public DevBTestBaekjungSpawnRecord(YokaiKind kind, int waveIndex)
        {
            Kind = kind;
            WaveIndex = waveIndex;
        }

        public YokaiKind Kind { get; }
        public int WaveIndex { get; }
    }

    public sealed class DevBTestBaekjungSpawnController : IBaekjungSpawnController
    {
        private readonly List<DevBTestBaekjungSpawnRecord> records = new List<DevBTestBaekjungSpawnRecord>();

        public int ActiveRaidCount { get; private set; }
        public int ResidentCount { get; private set; }
        public IReadOnlyList<DevBTestBaekjungSpawnRecord> Records => records;
        public event Action RaidSlotAvailable;

        public bool TrySpawn(YokaiKind kind, int waveIndex)
        {
            records.Add(new DevBTestBaekjungSpawnRecord(kind, waveIndex));
            ActiveRaidCount++;
            return true;
        }

        public void DefeatAll()
        {
            if (ActiveRaidCount <= 0) return;
            ActiveRaidCount = 0;
            RaidSlotAvailable?.Invoke();
        }

        public void DefeatRaid(int amount)
        {
            if (amount <= 0 || ActiveRaidCount <= 0) return;
            ActiveRaidCount = Math.Max(0, ActiveRaidCount - amount);
            RaidSlotAvailable?.Invoke();
        }

        public void SeedActive(int amount) => ActiveRaidCount = Math.Max(0, amount);
        public void SeedResident(int amount) => ResidentCount = Math.Max(0, amount);

        public int Count(YokaiKind kind, int waveIndex)
        {
            var count = 0;
            for (var i = 0; i < records.Count; i++)
                if (records[i].Kind == kind && records[i].WaveIndex == waveIndex) count++;
            return count;
        }
    }

    public sealed class DevBTestLootRandomSource : ILootRandomSource
    {
        private readonly Queue<float> values;

        public DevBTestLootRandomSource(params float[] values)
        {
            this.values = new Queue<float>(values ?? System.Array.Empty<float>());
        }

        public int CallCount { get; private set; }

        public float Next01()
        {
            CallCount++;
            return values.Count > 0 ? values.Dequeue() : 0f;
        }
    }

    public sealed class DevBTestChestSource : IChestSource
    {
        private readonly List<string> ids = new List<string>();
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();

        public DevBTestChestSource(int count, float xOffset = 0f)
        {
            for (var i = 0; i < count; i++)
            {
                var id = $"chest_{i:00}";
                ids.Add(id);
                positions.Add(id, new Vector2(i * 2f + xOffset, -i));
            }
        }

        public IReadOnlyList<string> ChestIds => ids;
        public Vector2 GetChestPosition(string chestId) => positions[chestId];
    }

    public sealed class DevBTestYokaiTarget : MonoBehaviour, IYokaiTarget, IYokaiLootTarget, IYokaiCounterSource, IWallMaterialTarget
    {
        public Transform TargetTransform => transform;
        public bool IsInLanternRange { get; set; }
        public bool IsInSieveRange { get; set; }
        public bool HasGroundLoot { get; set; }
        public bool IsInventoryTheftBlocked { get; set; }
        public float SieveStopSeconds { get; set; }
        public float SieveCooldownSeconds { get; set; }
        public float SieveDamageMultiplier { get; set; }
        public float EoduksiniLanternPauseSeconds { get; set; }
        public float EoduksiniBloomCooldownSeconds { get; set; }
        public float EoduksiniLanternDamageMultiplier { get; set; }
        public YokaiWallMaterial WallMaterial { get; set; }
        public int GroundLootStealCount { get; private set; }
        public int InventoryStealCount { get; private set; }
        public int LastInventoryStealSlots { get; private set; }
        public int LastInventoryStealLimit { get; private set; }
        public float WallDamageReceived { get; private set; }

        public void DamageWall(float amount) => WallDamageReceived += amount;
        public bool TryStealGroundLoot()
        {
            if (!HasGroundLoot) return false;
            HasGroundLoot = false;
            GroundLootStealCount++;
            return true;
        }
        public bool TryStealInventory(int maxSlots, int maxAmount)
        {
            if (IsInventoryTheftBlocked || maxSlots <= 0 || maxAmount <= 0) return false;
            InventoryStealCount++;
            LastInventoryStealSlots = maxSlots;
            LastInventoryStealLimit = maxAmount;
            return true;
        }
    }

    public sealed class DevBTestGaekgwiTarget : MonoBehaviour, IYokaiTarget, IBossCombatTarget
    {
        public Transform TargetTransform => transform;
        public int SpecialHitCount { get; private set; }
        public int LastDamage { get; private set; }
        public DamageTag LastDamageTag { get; private set; }
        public DamageDelivery LastDamageDelivery { get; private set; }
        public Vector2 LastKnockback { get; private set; }

        public void DamageWall(float amount) { }

        public bool TryApplyContactDamage(int amount) => amount > 0;

        public bool TryApplyBossSpecialDamage(int amount, DamageTag tag, Vector2 knockback,
            DamageDelivery delivery = DamageDelivery.Direct)
        {
            if (amount <= 0) return false;
            SpecialHitCount++;
            LastDamage = amount;
            LastDamageTag = tag;
            LastDamageDelivery = delivery;
            LastKnockback = knockback;
            return true;
        }
    }
}
