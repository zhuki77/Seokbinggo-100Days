using System;
using System.Collections.Generic;
using System.IO;
using Nyangbingo.Inventory;
using UnityEngine;
using Nyangbingo.Core;
using Nyangbingo.Bosses;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Combat;

namespace Nyangbingo.Save
{
    [Serializable]
    public struct BossRecord { public string bossId; public int count; public int firstDay; }

    [Serializable]
    public struct ForcedBossEncounterRecord { public string bossId; public bool triggered; }

    [Serializable]
    public struct CodexRecord { public string yokaiId; public int kills; }

    [Serializable]
    public sealed class RunStatsRecord
    {
        public int minedTiles;
        public int deaths;
    }

    [Serializable]
    public struct TurretFuelRecord
    {
        public string objectId;
        public int fuel;
        public float remainingGameSeconds;
        public bool storesGameSeconds;
    }

    [Serializable]
    public struct TileChangeRecord
    {
        public int x;
        public int y;
        public int z;
        public string tileId;
        public bool placed;
    }

    [Serializable]
    public struct PlacedObjectRecord
    {
        public string objectId;
        public string definitionId;
        public Vector2 position;
        public float rotationDegrees;
    }

    [Serializable]
    public sealed class GoalBadgeRecord
    {
        public bool workbenchCrafted;
        public int insulationWallsPlaced;
        public bool furnaceBuilt;
        public bool dismissed;
    }

    [Serializable]
    public struct CoolingSourceStateRecord
    {
        public string objectId;
        public string definitionId;
        public float remainingGameSeconds;
    }

    [Serializable]
    public struct DeathTearPouchRecord
    {
        public string pouchId;
        public int amount;
        public Vector2 position;
        public int expireOnDay;
    }

    [Serializable]
    public struct WorldDropStateRecord
    {
        public string itemId;
        public int amount;
        public Vector2 position;
        public Vector2 velocity;
        public float pickupDelay;
    }

    [Serializable]
    public sealed class YokaiStateRecord
    {
        public string instanceId;
        public string yokaiId;
        public Vector3 position;
        public Vector2 velocity;
        public int currentHealth;
        public int maxHealth;
        public bool raid;
        public int behaviorState;
        public float sieveStopRemaining;
        public float sieveCooldownRemaining;
        public float lanternPauseRemaining;
        public float bloomCooldownRemaining;
        public Vector3 dawnFleeDirection;
        public float contactAttackRemaining;
        public float frostSlowFraction;
        public float frostSlowRemaining;
        public bool gaekgwiPatternInitialized;
        public int gaekgwiPatternState;
        public float gaekgwiCooldownRemaining;
        public float gaekgwiTelegraphRemaining;
        public float gaekgwiDashRemaining;
        public Vector2 gaekgwiDashDirection;
        public List<InventorySlot> stolenItems = new List<InventorySlot>();
    }

    [Serializable]
    public struct ChestStateRecord
    {
        public string chestId;
        public Vector2 position;
        public bool opened;
        public List<InventorySlot> contents;
    }

    [Serializable]
    public struct DoorStateRecord
    {
        public int x;
        public int y;
        public bool isOpen;
    }

    [Serializable]
    public struct WallDamageStateRecord
    {
        public int x;
        public int y;
        public float damageTaken;
    }

    [Serializable]
    public struct CatnipPatchStateRecord
    {
        public string patchId;
        public int harvestedDay;
    }

    [Serializable]
    public struct HempPatchStateRecord
    {
        public string patchId;
        public bool harvested;
    }

    [Serializable]
    public struct TreeHarvestStateRecord
    {
        public string treeId;
        public bool harvested;
    }

    [Serializable]
    public struct RebarHarvestStateRecord
    {
        public string rebarId;
        public bool harvested;
    }

    [Serializable]
    public sealed class PlayerStateRecord
    {
        public bool hasValue;
        public Vector3 position;
        public int currentHealth;
        public int maxHealth;
        public bool hasTemperature;
        public float temperature;
    }

    [Serializable]
    public sealed class TimeStateRecord
    {
        public bool hasValue;
        public int day = 1;
        public float timeOfDayGameSeconds;
        public bool isNight;
    }

    [Serializable]
    public sealed class ActiveBossStateRecord
    {
        public bool active;
        public string bossId;
        public Vector3 position;
        public int currentHealth;
        public int maxHealth;
        public float summonedAtGameSeconds;
    }

    [Serializable]
    public sealed class RegularEncounterStateRecord
    {
        public bool hasValue;
        public int day = 1;
        public bool isNight;
        public bool discardRegularForCurrentNight;
        public List<string> remainingRegularYokaiIds = new List<string>();
        public bool usesDetailedYokaiState;
        public List<YokaiStateRecord> activeYokai = new List<YokaiStateRecord>();
        public List<string> pendingRegularYokaiIds = new List<string>();
        public List<string> pendingRaidYokaiIds = new List<string>();
        public List<ResidentYokaiDayRecord> residentLastKilledDays =
            new List<ResidentYokaiDayRecord>();
    }

    [Serializable]
    public struct ResidentYokaiDayRecord
    {
        public string yokaiId;
        public int lastKilledDay;
    }

    [Serializable]
    public struct EquipmentRecord { public string slot; public string equipmentId; }

    [Serializable]
    public struct SmeltingRecord
    {
        public string stationId;
        public string recipeId;
        public float remainingSeconds;
        public bool isActive;
        public int queueIndex;
    }

    [Serializable]
    public struct SmeltingOutputRecord { public string stationId; public string itemId; public int amount; }

    [Serializable]
    public struct UtilityCooldownRecord { public string kind; public float remainingGameSeconds; }

    [Serializable]
    public struct PendingItemRecord { public string itemId; public int amount; }

    [Serializable]
    public sealed class CraftingProcessRecord
    {
        public bool active;
        public string recipeId;
        public float remainingGameSeconds;
    }

    [Serializable]
    public sealed class SaveGame
    {
        public const int CurrentSchemaVersion = 22;
        /// <summary>v71: heatStage·승리조건 개편으로 구 세이브는 이어하기 불가(새 게임 유도).</summary>
        public const int MinimumCompatibleSchemaVersion = 22;
        private const string FoxRainCharmId = "fox_rain_charm";
        private const int RefundItemMaxStack = 99;
        public int schemaVersion = CurrentSchemaVersion;
        public int seed; public int day = 1; public float timeOfDaySec;
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<string> unlockedRecipes = new List<string>();
        public List<string> placedObjects = new List<string>();
        public List<string> tilemapDiff = new List<string>();
        public List<PlacedObjectRecord> placedObjectRecords = new List<PlacedObjectRecord>();
        public List<JangdokStorageRecord> jangdokStorages = new List<JangdokStorageRecord>();
        public List<CoolingSourceStateRecord> coolingSources = new List<CoolingSourceStateRecord>();
        public List<DeathTearPouchRecord> deathTearPouches = new List<DeathTearPouchRecord>();
        public List<WorldDropStateRecord> worldDrops = new List<WorldDropStateRecord>();
        public List<DoorStateRecord> doorStates = new List<DoorStateRecord>();
        public List<WallDamageStateRecord> wallDamage = new List<WallDamageStateRecord>();
        public List<CatnipPatchStateRecord> catnipPatches = new List<CatnipPatchStateRecord>();
        public List<HempPatchStateRecord> hempPatches = new List<HempPatchStateRecord>();
        public List<TreeHarvestStateRecord> harvestedTrees = new List<TreeHarvestStateRecord>();
        public List<RebarHarvestStateRecord> harvestedRebar = new List<RebarHarvestStateRecord>();
        public List<TileChangeRecord> tileChanges = new List<TileChangeRecord>();
        /// <summary>A-16: 배경(벽지) 변경 이력. 구버전 세이브에서는 null일 수 있으며 NormalizeAfterLoad가 빈 목록으로 채운다.</summary>
        public List<TileChangeRecord> backgroundChanges = new List<TileChangeRecord>();
        public List<string> modulesDone = new List<string>();
        public int seokbinggoStage;
        public int altarClears;
        /// <summary>폭염 단계(1~3). 날짜만으로 역산하지 말고 반드시 저장한다(B-UI-v71 B-9-1).</summary>
        public int heatStage = 1;
        public List<string> gimmickWeaponsGranted = new List<string>();
        public List<string> frostPendingCells = new List<string>();
        /// <summary>진단·표시용 잔존. 승리 판정에는 쓰지 않는다(win_condition=final_boss_kill).</summary>
        public float sealPct;
        public int yokaiTears;
        public List<BossRecord> bossRecords = new List<BossRecord>();
        public List<ForcedBossEncounterRecord> forcedBossEncounters = new List<ForcedBossEncounterRecord>();
        public List<CodexRecord> dogam = new List<CodexRecord>();
        public bool magpieJoined;
        public Vector2 magpieNestPosition;
        public int magpieKillCount;
        public bool magpieBaekjungSurvived;
        public List<InventorySlot> magpieStorage = new List<InventorySlot>();
        public List<TurretFuelRecord> turretFuel = new List<TurretFuelRecord>();
        public List<EquipmentRecord> equipment = new List<EquipmentRecord>();
        public List<string> ownedEquipmentIds = new List<string>();
        public string activeSlotItemId = string.Empty;
        public bool activeSlotUsingItem;
        public float portableLanternFuelSeconds;
        public List<UtilityCooldownRecord> utilityCooldowns = new List<UtilityCooldownRecord>();
        public List<PendingItemRecord> pendingItemAcquisitions = new List<PendingItemRecord>();
        public CraftingProcessRecord activeCrafting = new CraftingProcessRecord();
        public List<SmeltingRecord> smelting = new List<SmeltingRecord>();
        public List<SmeltingOutputRecord> smeltingOutputs = new List<SmeltingOutputRecord>();
        public List<string> openedChestIds = new List<string>();
        public List<ChestStateRecord> chests = new List<ChestStateRecord>();
        public PlayerStateRecord playerState = new PlayerStateRecord();
        public TimeStateRecord timeState = new TimeStateRecord();
        [NonSerialized] public ActiveBossStateRecord activeBoss = new ActiveBossStateRecord();
        public RegularEncounterStateRecord regularEncounter = new RegularEncounterStateRecord();
        public BaekjungSchedulerState baekjungProgress = new BaekjungSchedulerState();
        public float baekjungTearRemainder;
        public RunStatsRecord stats = new RunStatsRecord();
        public GoalBadgeRecord goalBadges = new GoalBadgeRecord();
        [NonSerialized] private bool sourceSchemaCaptured;
        [NonSerialized] private int sourceSchemaVersion;

        public int SourceSchemaVersion => sourceSchemaCaptured ? sourceSchemaVersion : schemaVersion;

        public void NormalizeAfterLoad()
        {
            var loadedSchemaVersion = schemaVersion;
            if (!sourceSchemaCaptured)
            {
                sourceSchemaVersion = loadedSchemaVersion;
                sourceSchemaCaptured = true;
            }
            var isLegacySchema = schemaVersion <= 0;
            if (inventory == null) inventory = new List<InventorySlot>();
            if (unlockedRecipes == null) unlockedRecipes = new List<string>();
            if (placedObjects == null) placedObjects = new List<string>();
            if (tilemapDiff == null) tilemapDiff = new List<string>();
            if (placedObjectRecords == null) placedObjectRecords = new List<PlacedObjectRecord>();
            if (jangdokStorages == null) jangdokStorages = new List<JangdokStorageRecord>();
            if (coolingSources == null) coolingSources = new List<CoolingSourceStateRecord>();
            if (deathTearPouches == null) deathTearPouches = new List<DeathTearPouchRecord>();
            if (worldDrops == null) worldDrops = new List<WorldDropStateRecord>();
            if (doorStates == null) doorStates = new List<DoorStateRecord>();
            if (wallDamage == null) wallDamage = new List<WallDamageStateRecord>();
            if (tileChanges == null) tileChanges = new List<TileChangeRecord>();
            if (backgroundChanges == null) backgroundChanges = new List<TileChangeRecord>();
            if (modulesDone == null) modulesDone = new List<string>();
            if (gimmickWeaponsGranted == null) gimmickWeaponsGranted = new List<string>();
            if (frostPendingCells == null) frostPendingCells = new List<string>();
            if (bossRecords == null) bossRecords = new List<BossRecord>();
            if (forcedBossEncounters == null) forcedBossEncounters = new List<ForcedBossEncounterRecord>();
            if (dogam == null) dogam = new List<CodexRecord>();
            magpieKillCount = Mathf.Max(0, magpieKillCount);
            if (magpieStorage == null) magpieStorage = new List<InventorySlot>();
            if (turretFuel == null) turretFuel = new List<TurretFuelRecord>();
            if (equipment == null) equipment = new List<EquipmentRecord>();
            if (ownedEquipmentIds == null) ownedEquipmentIds = new List<string>();
            if (activeSlotItemId == null) activeSlotItemId = string.Empty;
            if (utilityCooldowns == null) utilityCooldowns = new List<UtilityCooldownRecord>();
            if (pendingItemAcquisitions == null) pendingItemAcquisitions = new List<PendingItemRecord>();
            if (catnipPatches == null) catnipPatches = new List<CatnipPatchStateRecord>();
            if (hempPatches == null) hempPatches = new List<HempPatchStateRecord>();
            if (harvestedTrees == null) harvestedTrees = new List<TreeHarvestStateRecord>();
            if (harvestedRebar == null) harvestedRebar = new List<RebarHarvestStateRecord>();
            if (activeCrafting == null) activeCrafting = new CraftingProcessRecord();
            if (smelting == null) smelting = new List<SmeltingRecord>();
            if (smeltingOutputs == null) smeltingOutputs = new List<SmeltingOutputRecord>();
            if (openedChestIds == null) openedChestIds = new List<string>();
            if (chests == null) chests = new List<ChestStateRecord>();
            for (var i = 0; i < chests.Count; i++)
            {
                var record = chests[i];
                if (record.contents == null) record.contents = new List<InventorySlot>();
                chests[i] = record;
            }
            if (playerState == null) playerState = new PlayerStateRecord();
            if (timeState == null) timeState = new TimeStateRecord();
            activeBoss = new ActiveBossStateRecord();
            if (regularEncounter == null) regularEncounter = new RegularEncounterStateRecord();
            if (regularEncounter.remainingRegularYokaiIds == null)
                regularEncounter.remainingRegularYokaiIds = new List<string>();
            if (regularEncounter.activeYokai == null)
                regularEncounter.activeYokai = new List<YokaiStateRecord>();
            if (regularEncounter.pendingRegularYokaiIds == null)
                regularEncounter.pendingRegularYokaiIds = new List<string>();
            if (regularEncounter.pendingRaidYokaiIds == null)
                regularEncounter.pendingRaidYokaiIds = new List<string>();
            if (regularEncounter.residentLastKilledDays == null)
                regularEncounter.residentLastKilledDays = new List<ResidentYokaiDayRecord>();
            regularEncounter.day = Math.Max(1, regularEncounter.day);
            regularEncounter.remainingRegularYokaiIds.RemoveAll(string.IsNullOrWhiteSpace);
            regularEncounter.activeYokai.RemoveAll(record => record == null);
            regularEncounter.pendingRegularYokaiIds.RemoveAll(string.IsNullOrWhiteSpace);
            regularEncounter.pendingRaidYokaiIds.RemoveAll(string.IsNullOrWhiteSpace);
            if (baekjungProgress == null) baekjungProgress = new BaekjungSchedulerState();
            if (stats == null) stats = new RunStatsRecord();
            if (goalBadges == null) goalBadges = new GoalBadgeRecord();
            goalBadges.insulationWallsPlaced = Math.Max(0, goalBadges.insulationWallsPlaced);
            stats.minedTiles = Math.Max(0, stats.minedTiles);
            stats.deaths = Math.Max(0, stats.deaths);
            if (isLegacySchema)
            {
                for (var i = 0; i < smelting.Count; i++)
                {
                    var record = smelting[i];
                    record.isActive = true;
                    record.queueIndex = -1;
                    smelting[i] = record;
                }
            }
            if (loadedSchemaVersion < 7) MigrateHapjukseonIds();
            if (loadedSchemaVersion < 8) MigrateV24Ids();
            seokbinggoStage = Math.Clamp(seokbinggoStage, 0, 6);
            altarClears = Math.Max(0, altarClears);
            heatStage = Math.Clamp(heatStage <= 0 ? 1 : heatStage, 1, 3);
            gimmickWeaponsGranted.RemoveAll(string.IsNullOrWhiteSpace);
            frostPendingCells.RemoveAll(string.IsNullOrWhiteSpace);
            if (schemaVersion < CurrentSchemaVersion) schemaVersion = CurrentSchemaVersion;
        }

        private void MigrateHapjukseonIds()
        {
            for (var i = 0; i < inventory.Count; i++)
            {
                var slot = inventory[i];
                if (slot.itemId == FanItemIds.LegacyFoldingFan)
                {
                    slot.itemId = FanItemIds.Hapjukseon;
                    inventory[i] = slot;
                }
            }

            for (var i = 0; i < pendingItemAcquisitions.Count; i++)
            {
                var record = pendingItemAcquisitions[i];
                if (record.itemId == FanItemIds.LegacyFoldingFan)
                {
                    record.itemId = FanItemIds.Hapjukseon;
                    pendingItemAcquisitions[i] = record;
                }
            }

            for (var i = 0; i < smeltingOutputs.Count; i++)
            {
                var record = smeltingOutputs[i];
                if (record.itemId == FanItemIds.LegacyFoldingFan)
                {
                    record.itemId = FanItemIds.Hapjukseon;
                    smeltingOutputs[i] = record;
                }
            }

            for (var i = 0; i < placedObjects.Count; i++)
                if (placedObjects[i] == FanItemIds.LegacyFoldingFan) placedObjects[i] = FanItemIds.Hapjukseon;

            for (var i = 0; i < placedObjectRecords.Count; i++)
            {
                var record = placedObjectRecords[i];
                if (record.definitionId == FanItemIds.LegacyFoldingFan)
                {
                    record.definitionId = FanItemIds.Hapjukseon;
                    placedObjectRecords[i] = record;
                }
            }

            for (var i = 0; i < utilityCooldowns.Count; i++)
            {
                var record = utilityCooldowns[i];
                if (record.kind == "FoldingFan")
                {
                    record.kind = UtilityKind.Hapjukseon.ToString();
                    utilityCooldowns[i] = record;
                }
            }
        }

        private void MigrateV24Ids()
        {
            var migrations = IdMigrationRuntime.LoadOfficial();
            var refundRule = migrations.Find(IdMigrationDomain.Item, FoxRainCharmId);
            if (refundRule == null || refundRule.Action != IdMigrationAction.RemoveRefund)
                throw new InvalidOperationException("The fox-rain removal/refund migration is missing.");

            long foxRainCharmCount = 0;
            for (var i = 0; i < inventory.Count; i++)
            {
                var slot = inventory[i];
                if (slot.itemId == FoxRainCharmId)
                {
                    if (slot.amount > 0) foxRainCharmCount += slot.amount;
                    inventory[i] = default;
                    continue;
                }
                slot.itemId = MigrateItemId(migrations, slot.itemId);
                inventory[i] = slot;
            }

            MigrateItemIdList(migrations, unlockedRecipes);
            MigrateItemIdList(migrations, placedObjects, preserveDuplicates: true);
            for (var i = placedObjectRecords.Count - 1; i >= 0; i--)
            {
                var record = placedObjectRecords[i];
                record.definitionId = MigrateItemId(migrations, record.definitionId);
                if (string.IsNullOrWhiteSpace(record.definitionId)) placedObjectRecords.RemoveAt(i);
                else placedObjectRecords[i] = record;
            }

            for (var i = equipment.Count - 1; i >= 0; i--)
            {
                var record = equipment[i];
                record.equipmentId = MigrateItemId(migrations, record.equipmentId);
                if (string.IsNullOrWhiteSpace(record.equipmentId)) equipment.RemoveAt(i);
                else equipment[i] = record;
            }
            MigrateItemIdList(migrations, ownedEquipmentIds);

            for (var i = pendingItemAcquisitions.Count - 1; i >= 0; i--)
            {
                var record = pendingItemAcquisitions[i];
                if (record.itemId == FoxRainCharmId)
                {
                    if (record.amount > 0) foxRainCharmCount += record.amount;
                    pendingItemAcquisitions.RemoveAt(i);
                    continue;
                }
                record.itemId = MigrateItemId(migrations, record.itemId);
                if (string.IsNullOrWhiteSpace(record.itemId)) pendingItemAcquisitions.RemoveAt(i);
                else pendingItemAcquisitions[i] = record;
            }

            activeCrafting.recipeId = MigrateItemId(migrations, activeCrafting.recipeId);
            if (string.IsNullOrWhiteSpace(activeCrafting.recipeId)) activeCrafting = new CraftingProcessRecord();

            for (var i = 0; i < smelting.Count; i++)
            {
                var record = smelting[i];
                record.stationId = MigrateItemId(migrations, record.stationId);
                record.recipeId = migrations.Migrate(IdMigrationDomain.Smelting, record.recipeId);
                smelting[i] = record;
            }
            for (var i = smeltingOutputs.Count - 1; i >= 0; i--)
            {
                var record = smeltingOutputs[i];
                record.stationId = MigrateItemId(migrations, record.stationId);
                record.itemId = MigrateItemId(migrations, record.itemId);
                if (string.IsNullOrWhiteSpace(record.itemId)) smeltingOutputs.RemoveAt(i);
                else smeltingOutputs[i] = record;
            }

            MigrateBossRecords(migrations);
            MigrateForcedBossEncounters(migrations);
            MigrateCodexRecords(migrations);
            activeBoss = new ActiveBossStateRecord();
            utilityCooldowns.RemoveAll(record => record.kind == "FoxRainCharm");

            AddRemovalRefund(foxRainCharmCount, refundRule.RefundItemId, refundRule.RefundAmount);
        }

        private void AddRemovalRefund(long removedCount, string refundItemId, int refundAmount)
        {
            var remaining = Math.Min((long)int.MaxValue, removedCount * refundAmount);
            for (var i = 0; i < inventory.Count && remaining > 0; i++)
            {
                var slot = inventory[i];
                if (slot.itemId != refundItemId || slot.amount <= 0 || slot.amount >= RefundItemMaxStack) continue;
                var added = (int)Math.Min(remaining, RefundItemMaxStack - slot.amount);
                slot.amount += added;
                remaining -= added;
                inventory[i] = slot;
            }
            for (var i = 0; i < inventory.Count && remaining > 0; i++)
            {
                if (!string.IsNullOrWhiteSpace(inventory[i].itemId)) continue;
                var added = (int)Math.Min(remaining, RefundItemMaxStack);
                inventory[i] = new InventorySlot { itemId = refundItemId, amount = added };
                remaining -= added;
            }
            while (inventory.Count < Inventory.Inventory.SlotCount && remaining > 0)
            {
                var added = (int)Math.Min(remaining, RefundItemMaxStack);
                inventory.Add(new InventorySlot { itemId = refundItemId, amount = added });
                remaining -= added;
            }
            while (remaining > 0)
            {
                var added = (int)Math.Min(remaining, RefundItemMaxStack);
                pendingItemAcquisitions.Add(new PendingItemRecord { itemId = refundItemId, amount = added });
                remaining -= added;
            }
        }

        private static string MigrateItemId(IdMigrationPolicy migrations, string id) =>
            migrations.Migrate(IdMigrationDomain.Item, id);

        private static void MigrateItemIdList(IdMigrationPolicy migrations, List<string> ids,
            bool preserveDuplicates = false)
        {
            var unique = preserveDuplicates ? null : new HashSet<string>(StringComparer.Ordinal);
            for (var i = ids.Count - 1; i >= 0; i--)
            {
                var migrated = MigrateItemId(migrations, ids[i]);
                if (string.IsNullOrWhiteSpace(migrated) || (unique != null && !unique.Add(migrated))) ids.RemoveAt(i);
                else ids[i] = migrated;
            }
        }

        private void MigrateBossRecords(IdMigrationPolicy migrations)
        {
            var merged = new Dictionary<string, BossRecord>(StringComparer.Ordinal);
            foreach (var source in bossRecords)
            {
                var record = source;
                record.bossId = migrations.Migrate(IdMigrationDomain.Boss, record.bossId);
                if (string.IsNullOrWhiteSpace(record.bossId)) continue;
                if (merged.TryGetValue(record.bossId, out var existing))
                {
                    existing.count = (int)Math.Min(int.MaxValue, (long)Math.Max(0, existing.count) + Math.Max(0, record.count));
                    if (existing.firstDay <= 0 || record.firstDay > 0 && record.firstDay < existing.firstDay)
                        existing.firstDay = record.firstDay;
                    merged[record.bossId] = existing;
                }
                else merged.Add(record.bossId, record);
            }
            bossRecords.Clear();
            bossRecords.AddRange(merged.Values);
            bossRecords.Sort((left, right) => string.CompareOrdinal(left.bossId, right.bossId));
        }

        private void MigrateForcedBossEncounters(IdMigrationPolicy migrations)
        {
            var merged = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var source in forcedBossEncounters)
            {
                var bossId = migrations.Migrate(IdMigrationDomain.Boss, source.bossId);
                if (string.IsNullOrWhiteSpace(bossId)) continue;
                merged[bossId] = source.triggered || merged.TryGetValue(bossId, out var triggered) && triggered;
            }
            forcedBossEncounters.Clear();
            foreach (var pair in merged)
                forcedBossEncounters.Add(new ForcedBossEncounterRecord { bossId = pair.Key, triggered = pair.Value });
            forcedBossEncounters.Sort((left, right) => string.CompareOrdinal(left.bossId, right.bossId));
        }

        private void MigrateCodexRecords(IdMigrationPolicy migrations)
        {
            var merged = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var source in dogam)
            {
                var yokaiId = migrations.Migrate(IdMigrationDomain.Yokai, source.yokaiId);
                if (string.IsNullOrWhiteSpace(yokaiId)) continue;
                var kills = Math.Max(0, source.kills);
                merged[yokaiId] = merged.TryGetValue(yokaiId, out var existing)
                    ? (int)Math.Min(int.MaxValue, (long)existing + kills)
                    : kills;
            }
            dogam.Clear();
            foreach (var pair in merged)
                dogam.Add(new CodexRecord { yokaiId = pair.Key, kills = pair.Value });
            dogam.Sort((left, right) => string.CompareOrdinal(left.yokaiId, right.yokaiId));
        }
    }

    public static class ForcedBossEncounterSaveAdapter
    {
        public static void Capture(SaveGame save, BossDefinition definition, ForcedBossEncounterBinding binding)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (binding == null) throw new ArgumentNullException(nameof(binding));

            save.NormalizeAfterLoad();
            save.forcedBossEncounters.RemoveAll(record => record.bossId == definition.Id);
            save.forcedBossEncounters.Add(new ForcedBossEncounterRecord
            {
                bossId = definition.Id,
                triggered = binding.HasTriggered
            });
        }

        public static bool Restore(SaveGame save, BossDefinition definition, ForcedBossEncounterBinding binding)
        {
            if (save == null || definition == null || binding == null) return false;
            save.NormalizeAfterLoad();

            var triggered = false;
            var found = false;
            for (var i = 0; i < save.forcedBossEncounters.Count; i++)
            {
                var record = save.forcedBossEncounters[i];
                if (record.bossId != definition.Id) continue;
                if (found) return false;
                triggered = record.triggered;
                found = true;
            }

            if (!found)
            {
                var foundBossRecord = false;
                for (var i = 0; i < save.bossRecords.Count; i++)
                {
                    var record = save.bossRecords[i];
                    if (record.bossId != definition.Id) continue;
                    if (foundBossRecord || record.count <= 0) return false;
                    foundBossRecord = true;
                    triggered = true;
                }
            }

            binding.RestoreTriggered(triggered);
            return true;
        }
    }

    public static class BossRecordSaveAdapter
    {
        public static bool Validate(SaveGame save, Func<string, BossDefinition> findBoss)
        {
            if (save == null || findBoss == null) return false;
            save.NormalizeAfterLoad();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.bossRecords.Count; i++)
            {
                var record = save.bossRecords[i];
                if (string.IsNullOrWhiteSpace(record.bossId) || record.count <= 0 || record.firstDay <= 0 ||
                    !ids.Add(record.bossId)) return false;
                var definition = findBoss(record.bossId);
                if (definition == null || definition.Id != record.bossId) return false;
            }
            save.bossRecords.Sort((left, right) => string.CompareOrdinal(left.bossId, right.bossId));
            return true;
        }
    }

    public sealed class BossRecordBinding : IDisposable
    {
        private readonly SaveGame save;
        private readonly ITimeSource timeSource;
        private readonly BossManager bossManager;
        private readonly Func<string, BossDefinition> findBoss;
        private bool disposed;

        public BossRecordBinding(SaveGame save, ITimeSource timeSource, BossManager bossManager,
            Func<string, BossDefinition> findBoss)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            this.bossManager = bossManager ?? throw new ArgumentNullException(nameof(bossManager));
            this.findBoss = findBoss ?? throw new ArgumentNullException(nameof(findBoss));
            if (!BossRecordSaveAdapter.Validate(save, findBoss))
                throw new ArgumentException("Boss record save data is invalid.", nameof(save));
            bossManager.BossEnded += HandleBossEnded;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bossManager.BossEnded -= HandleBossEnded;
        }

        private void HandleBossEnded(BossDefinition definition, bool defeated)
        {
            if (disposed || !defeated || definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                findBoss(definition.Id) != definition) return;

            for (var i = 0; i < save.bossRecords.Count; i++)
            {
                var record = save.bossRecords[i];
                if (record.bossId != definition.Id) continue;
                if (record.count < int.MaxValue) record.count++;
                if (record.firstDay <= 0) record.firstDay = Math.Max(1, timeSource.Day);
                save.bossRecords[i] = record;
                return;
            }

            save.bossRecords.Add(new BossRecord
            {
                bossId = definition.Id,
                count = 1,
                firstDay = Math.Max(1, timeSource.Day)
            });
            save.bossRecords.Sort((left, right) => string.CompareOrdinal(left.bossId, right.bossId));
        }
    }

    public static class YokaiCodexSaveAdapter
    {
        public static bool Validate(SaveGame save, Func<string, YokaiDefinition> findYokai)
        {
            if (save == null || findYokai == null) return false;
            save.NormalizeAfterLoad();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.dogam.Count; i++)
            {
                var record = save.dogam[i];
                if (string.IsNullOrWhiteSpace(record.yokaiId) || record.kills <= 0 || !ids.Add(record.yokaiId))
                    return false;
                var definition = findYokai(record.yokaiId);
                if (definition == null || definition.Id != record.yokaiId) return false;
            }
            save.dogam.Sort((left, right) => string.CompareOrdinal(left.yokaiId, right.yokaiId));
            return true;
        }
    }

    public sealed class YokaiCodexBinding : IDisposable
    {
        private readonly SaveGame save;
        private readonly Func<string, YokaiDefinition> findYokai;
        private bool disposed;

        public YokaiCodexBinding(SaveGame save, Func<string, YokaiDefinition> findYokai)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.findYokai = findYokai ?? throw new ArgumentNullException(nameof(findYokai));
            if (!YokaiCodexSaveAdapter.Validate(save, findYokai))
                throw new ArgumentException("Yokai codex save data is invalid.", nameof(save));
            GameEvents.OnYokaiKilled += HandleYokaiKilled;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnYokaiKilled -= HandleYokaiKilled;
        }

        private void HandleYokaiKilled(YokaiDefinition definition)
        {
            if (disposed || definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                findYokai(definition.Id) != definition) return;
            for (var i = 0; i < save.dogam.Count; i++)
            {
                var record = save.dogam[i];
                if (record.yokaiId != definition.Id) continue;
                if (record.kills < int.MaxValue) record.kills++;
                save.dogam[i] = record;
                return;
            }
            save.dogam.Add(new CodexRecord { yokaiId = definition.Id, kills = 1 });
            save.dogam.Sort((left, right) => string.CompareOrdinal(left.yokaiId, right.yokaiId));
        }
    }

    public sealed class YokaiCodexCard
    {
        internal YokaiCodexCard(string entryId, bool isBoss, string displayName, string appearanceHint,
            string sourceText, int killCount, int firstKillDay)
        {
            EntryId = entryId;
            IsBoss = isBoss;
            IsUnlocked = killCount > 0;
            KillCount = Math.Max(0, killCount);
            FirstKillDay = Math.Max(0, firstKillDay);
            DisplayName = IsUnlocked ? displayName : "?";
            AppearanceHint = IsUnlocked ? appearanceHint : string.Empty;
            SourceText = IsUnlocked ? sourceText : string.Empty;
        }

        public string EntryId { get; }
        public bool IsBoss { get; }
        public bool IsUnlocked { get; }
        public bool UsesInkSilhouette => !IsUnlocked;
        public int KillCount { get; }
        public int FirstKillDay { get; }
        public string DisplayName { get; }
        public string AppearanceHint { get; }
        public string SourceText { get; }
    }

    public sealed class YokaiCodexPresentationModel
    {
        public const int ExpectedCardCount = 9;
        public const int GridColumns = 3;
        public static readonly Vector2 GridCardSize = new Vector2(72f, 96f);
        public static readonly Vector2 EnlargedCardSize = new Vector2(192f, 256f);

        private readonly GameDataCatalog catalog;
        private readonly SaveGame save;
        private readonly List<YokaiCodexCard> cards = new List<YokaiCodexCard>(ExpectedCardCount);
        private string selectedEntryId;
        private bool backVisible;

        public YokaiCodexPresentationModel(GameDataCatalog catalog, SaveGame save)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            if (!catalog.IsValid) throw new ArgumentException("Game data catalog is invalid.", nameof(catalog));
            Refresh();
        }

        public IReadOnlyList<YokaiCodexCard> Cards => cards;
        public bool HasEnlargedCard => SelectedCard != null;
        public bool IsBackVisible => backVisible;
        public YokaiCodexCard SelectedCard
        {
            get
            {
                if (string.IsNullOrEmpty(selectedEntryId)) return null;
                for (var i = 0; i < cards.Count; i++)
                    if (cards[i].EntryId == selectedEntryId) return cards[i];
                return null;
            }
        }

        public void Refresh()
        {
            save.NormalizeAfterLoad();
            var yokaiKills = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < save.dogam.Count; i++)
            {
                var record = save.dogam[i];
                if (!string.IsNullOrWhiteSpace(record.yokaiId) && record.kills > 0)
                    yokaiKills[record.yokaiId] = record.kills;
            }

            var bossRecords = new Dictionary<string, BossRecord>(StringComparer.Ordinal);
            for (var i = 0; i < save.bossRecords.Count; i++)
            {
                var record = save.bossRecords[i];
                if (!string.IsNullOrWhiteSpace(record.bossId) && record.count > 0)
                    bossRecords[record.bossId] = record;
            }

            cards.Clear();
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < catalog.Yokai.Count; i++)
            {
                var definition = catalog.Yokai[i];
                var kills = yokaiKills.TryGetValue(definition.Id, out var savedKills) ? savedKills : 0;
                var firstKillDay = 0;
                if (definition.Kind == YokaiKind.Gangcheori || definition.Kind == YokaiKind.Imugi)
                {
                    for (var bossIndex = 0; bossIndex < catalog.Bosses.Count; bossIndex++)
                    {
                        var boss = catalog.Bosses[bossIndex];
                        var representsSameYokai =
                            definition.Kind == YokaiKind.Gangcheori && boss.Kind == BossKind.Gangcheori ||
                            definition.Kind == YokaiKind.Imugi && boss.Kind == BossKind.Imugi;
                        if (!representsSameYokai || !bossRecords.TryGetValue(boss.Id, out var record)) continue;
                        kills = Math.Max(kills, record.count);
                        firstKillDay = record.firstDay;
                    }
                }
                AddCard(entryIds, definition.Id, false, definition.DisplayName, definition.AppearanceHint,
                    CodexSourceFor(definition.Kind), kills, firstKillDay);
            }

            for (var i = 0; i < catalog.Bosses.Count; i++)
            {
                var definition = catalog.Bosses[i];
                if (definition.Kind == BossKind.Gangcheori || definition.Kind == BossKind.Imugi) continue;
                bossRecords.TryGetValue(definition.Id, out var record);
                AddCard(entryIds, definition.Id, true, definition.DisplayName, definition.RecommendedDay,
                    CodexSourceFor(definition.Kind), record.count, record.firstDay);
            }

            if (cards.Count != ExpectedCardCount)
                throw new InvalidOperationException($"Yokai codex requires exactly {ExpectedCardCount} unique cards, but found {cards.Count}.");
            if (SelectedCard == null)
            {
                selectedEntryId = null;
                backVisible = false;
            }
            else if (!SelectedCard.IsUnlocked) backVisible = false;
        }

        public bool TryTapCard(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId)) return false;
            if (selectedEntryId == entryId) return TryFlipSelected();
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].EntryId != entryId) continue;
                selectedEntryId = entryId;
                backVisible = false;
                return true;
            }
            return false;
        }

        public bool TryFlipSelected()
        {
            var selected = SelectedCard;
            if (selected == null || !selected.IsUnlocked) return false;
            backVisible = !backVisible;
            return true;
        }

        public void TapOutside()
        {
            selectedEntryId = null;
            backVisible = false;
        }

        private void AddCard(HashSet<string> entryIds, string entryId, bool isBoss, string displayName,
            string appearanceHint, string sourceText, int killCount, int firstKillDay)
        {
            if (string.IsNullOrWhiteSpace(entryId) || !entryIds.Add(entryId))
                throw new InvalidOperationException($"Yokai codex contains an invalid or duplicate entry ID '{entryId}'.");
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(sourceText))
                throw new InvalidOperationException($"Yokai codex entry '{entryId}' is missing display or source text.");
            cards.Add(new YokaiCodexCard(entryId, isBoss, displayName, appearanceHint, sourceText,
                killCount, firstKillDay));
        }

        private static string CodexSourceFor(YokaiKind kind)
        {
            switch (kind)
            {
                case YokaiKind.ClubGoblin: return "구비 도깨비 씨름담 — 사람에게 씨름을 걸고 방망이를 휘두르는 익살꾼.";
                case YokaiKind.Bulgasari: return "《송남잡지》 — 쇠를 먹으며 자라나는 불가사리 전승.";
                case YokaiKind.Yagwanggwi: return "《동국세시기》 — 설날 밤 신발을 훔쳐 가는 야광귀 전승.";
                case YokaiKind.Eoduksini: return "어둑시니 구전 — 바라볼수록 어둠 속에서 거대해지는 요괴.";
                case YokaiKind.Gangcheori: return "《성호사설》 — 지나간 자리에 가뭄을 남긴다는 강철 전승.";
                case YokaiKind.Gaekgwi: return "객귀 구전 — 타향에서 죽어 돌아갈 곳을 잃고 떠도는 혼령.";
                case YokaiKind.Imugi: return "이무기 구전 — 물 아래에서 여의주를 기다리며 용이 되기를 바라는 뱀.";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown yokai codex source.");
            }
        }

        private static string CodexSourceFor(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.GoblinChief: return "구비 도깨비 씨름담 — 씨름 한판을 걸어오는 도깨비 이야기.";
                case BossKind.MotherBulgasari: return "《송남잡지》 — 쇠를 먹으며 자라나는 불가사리 전승.";
                case BossKind.Imugi: return "이무기 구전 — 물 아래에서 여의주를 기다리며 용이 되기를 바라는 뱀.";
                case BossKind.Gangcheori: return "《성호사설》 — 지나간 자리에 가뭄을 남긴다는 강철 전승.";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown boss codex source.");
            }
        }
    }

    public static class EquipmentCollectionSaveAdapter
    {
        public static bool Capture(SaveGame save, EquipmentCollection collection)
        {
            if (save == null || collection == null) return false;
            save.NormalizeAfterLoad();
            save.ownedEquipmentIds = collection.Export();
            return true;
        }

        public static bool Restore(SaveGame save, EquipmentCollection collection)
        {
            if (save == null || collection == null) return false;
            save.NormalizeAfterLoad();
            return collection.TryImport(save.ownedEquipmentIds);
        }
    }

    public static class ActiveSlotSaveAdapter
    {
        public static bool Capture(SaveGame save, ActiveSlotSystem activeSlot)
        {
            if (save == null || activeSlot == null) return false;
            save.NormalizeAfterLoad();
            save.activeSlotItemId = activeSlot.EquippedItemId;
            save.activeSlotUsingItem = activeSlot.IsUsingEquippedItem;
            return true;
        }

        public static bool Restore(SaveGame save, ActiveSlotSystem activeSlot)
        {
            if (save == null || activeSlot == null) return false;
            save.NormalizeAfterLoad();
            return activeSlot.TryRestore(save.activeSlotItemId, save.activeSlotUsingItem);
        }
    }

    public static class PortableLanternSaveAdapter
    {
        public static bool Capture(SaveGame save, PortableLanternRuntime lantern)
        {
            if (save == null || lantern == null) return false;
            save.NormalizeAfterLoad();
            save.portableLanternFuelSeconds = lantern.FuelRemainingSeconds;
            return true;
        }

        public static bool Restore(SaveGame save, PortableLanternRuntime lantern)
        {
            if (save == null || lantern == null) return false;
            save.NormalizeAfterLoad();
            return lantern.TryRestore(save.portableLanternFuelSeconds);
        }
    }

    public static class UtilityCooldownSaveAdapter
    {
        public static bool Capture(SaveGame save, UtilityService service)
        {
            if (save == null || service == null) return false;
            save.NormalizeAfterLoad();
            save.utilityCooldowns.Clear();
            var cooldowns = service.ExportCooldowns();
            foreach (UtilityKind kind in Enum.GetValues(typeof(UtilityKind)))
                if (cooldowns.TryGetValue(kind, out var remaining) && remaining > .0001f)
                    save.utilityCooldowns.Add(new UtilityCooldownRecord
                    {
                        kind = kind.ToString(),
                        remainingGameSeconds = remaining
                    });
            return true;
        }

        public static bool Restore(SaveGame save, UtilityService service)
        {
            if (save == null || service == null) return false;
            save.NormalizeAfterLoad();
            var cooldowns = new Dictionary<UtilityKind, float>();
            for (var i = 0; i < save.utilityCooldowns.Count; i++)
            {
                var record = save.utilityCooldowns[i];
                if (!TryParseKind(record.kind, out var kind) || cooldowns.ContainsKey(kind) ||
                    record.remainingGameSeconds <= 0f || float.IsNaN(record.remainingGameSeconds) ||
                    float.IsInfinity(record.remainingGameSeconds)) return false;
                cooldowns.Add(kind, record.remainingGameSeconds);
            }
            return service.RestoreCooldowns(cooldowns);
        }

        private static bool TryParseKind(string value, out UtilityKind kind)
        {
            if (value == "FoldingFan")
            {
                kind = UtilityKind.Hapjukseon;
                return true;
            }
            return Enum.TryParse(value, out kind);
        }
    }

    public static class CraftingProcessSaveAdapter
    {
        public static bool Capture(SaveGame save, CraftingProcess process)
        {
            if (save == null || process == null) return false;
            save.NormalizeAfterLoad();
            save.activeCrafting = process.IsCrafting
                ? new CraftingProcessRecord
                {
                    active = true,
                    recipeId = process.Active.Id,
                    remainingGameSeconds = process.RemainingSeconds
                }
                : new CraftingProcessRecord();
            return true;
        }

        public static bool Restore(SaveGame save, CraftingProcess process, Func<string, RecipeDefinition> findRecipe)
        {
            if (save == null || process == null || findRecipe == null) return false;
            save.NormalizeAfterLoad();
            var record = save.activeCrafting;
            if (!record.active)
                return string.IsNullOrEmpty(record.recipeId) && record.remainingGameSeconds == 0f &&
                       process.RestoreState(null, 0f);
            if (string.IsNullOrWhiteSpace(record.recipeId)) return false;
            var recipe = findRecipe(record.recipeId);
            return recipe != null && process.RestoreState(recipe, record.remainingGameSeconds);
        }
    }

    public static class RecipeBookSaveAdapter
    {
        public static bool Capture(SaveGame save, RecipeBook recipeBook)
        {
            if (save == null || recipeBook == null) return false;
            save.NormalizeAfterLoad();
            save.unlockedRecipes = recipeBook.Export();
            return true;
        }

        public static bool Restore(SaveGame save, RecipeBook recipeBook, Func<string, RecipeDefinition> findRecipe)
        {
            if (save == null || recipeBook == null || findRecipe == null) return false;
            save.NormalizeAfterLoad();
            var validatedIds = new List<string>(save.unlockedRecipes.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.unlockedRecipes.Count; i++)
            {
                var id = save.unlockedRecipes[i];
                if (string.IsNullOrWhiteSpace(id) || !uniqueIds.Add(id)) return false;
                var definition = findRecipe(id);
                if (definition == null || definition.Id != id) return false;
                validatedIds.Add(id);
            }
            recipeBook.Import(validatedIds);
            return true;
        }
    }

    public static class PendingItemAcquisitionSaveAdapter
    {
        public static bool Capture(SaveGame save, InventoryRuntime inventoryRuntime)
        {
            if (save == null || inventoryRuntime == null) return false;
            save.NormalizeAfterLoad();
            save.pendingItemAcquisitions.Clear();
            var pending = inventoryRuntime.ExportPending();
            for (var i = 0; i < pending.Count; i++)
            {
                var reward = pending[i];
                if (reward.item == null || reward.amount <= 0) return false;
                save.pendingItemAcquisitions.Add(new PendingItemRecord
                {
                    itemId = reward.item.Id,
                    amount = reward.amount
                });
            }
            return true;
        }

        public static bool Restore(SaveGame save, InventoryRuntime inventoryRuntime,
            Func<string, ItemDefinition> findItem)
        {
            if (save == null || inventoryRuntime == null || findItem == null) return false;
            save.NormalizeAfterLoad();
            var restored = new List<ItemAmount>(save.pendingItemAcquisitions.Count);
            for (var i = 0; i < save.pendingItemAcquisitions.Count; i++)
            {
                var record = save.pendingItemAcquisitions[i];
                if (string.IsNullOrWhiteSpace(record.itemId) || record.amount <= 0) return false;
                var item = findItem(record.itemId);
                if (item == null || item.Id != record.itemId) return false;
                restored.Add(new ItemAmount { item = item, amount = record.amount });
            }
            return inventoryRuntime.TryRestorePending(restored);
        }
    }

#if false // Moved to SaveManager.cs so Unity can serialize the MonoBehaviour by script GUID.
    public sealed class SaveManager : MonoBehaviour
    {
        public const int SlotCount = 1;
        private static readonly int[] DemoDays = { 1, 15, 30 };
        public void Save(int slot, SaveGame data)
        {
            ValidateSlot(slot);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion > SaveGame.CurrentSchemaVersion)
                throw new ArgumentException("Cannot save data from a newer schema version.", nameof(data));
            data.NormalizeAfterLoad();
            WriteAtomically(PathFor(slot), JsonUtility.ToJson(data, true));
        }

        public bool TrySaveManual(int slot, SaveGame data, BossManager bossManager)
        {
            ValidateSlot(slot);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (bossManager == null || bossManager.IsBossActive) return false;
            Save(slot, data);
            return true;
        }

        public void SaveAtDawn(int slot, SaveGame data) => Save(slot, data);
        public bool TryLoad(int slot, out SaveGame data)
        {
            ValidateSlot(slot);
            var path = PathFor(slot);
            if (!File.Exists(path))
            {
                data = null;
                return false;
            }

            try
            {
                return TryDeserialize(File.ReadAllText(path), out data);
            }
            catch (IOException)
            {
                data = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                data = null;
                return false;
            }
        }
        public bool HasSave(int slot)
        {
            ValidateSlot(slot);
            return File.Exists(PathFor(slot));
        }
        public bool TryLoadLatest(out int slot, out SaveGame data)
        {
            slot = -1;
            data = null;
            var latestWrite = DateTime.MinValue;
            for (var candidate = 0; candidate < SlotCount; candidate++)
            {
                var path = PathFor(candidate);
                if (!File.Exists(path) || !TryLoad(candidate, out var loaded)) continue;
                DateTime written;
                try { written = File.GetLastWriteTimeUtc(path); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                if (slot >= 0 && written <= latestWrite) continue;
                slot = candidate;
                data = loaded;
                latestWrite = written;
            }
            return slot >= 0;
        }
        public bool TryCopyDemoToAutoSave(int day, out SaveGame data)
        {
            data = null;
            if (Array.IndexOf(DemoDays, day) < 0) return false;
            var path = Path.Combine(Application.streamingAssetsPath, "DemoSaves", $"day-{day}.json");
            try
            {
                if (!File.Exists(path) || !TryDeserialize(File.ReadAllText(path), out data)) return false;
                Save(0, data);
                return true;
            }
            catch (IOException) { data = null; return false; }
            catch (UnauthorizedAccessException) { data = null; return false; }
        }
        public static bool TryDeserialize(string json, out SaveGame data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<SaveGame>(json);
                if (parsed == null) return false;
                if (!json.Contains("\"schemaVersion\"")) parsed.schemaVersion = 0;
                if (parsed.schemaVersion > SaveGame.CurrentSchemaVersion) return false;
                parsed.NormalizeAfterLoad();
                data = parsed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public void Delete(int slot) { ValidateSlot(slot); var path = PathFor(slot); if (File.Exists(path)) File.Delete(path); }
        private static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"nyangbingo-save-{slot}.json");
        private static void WriteAtomically(string path, string contents)
        {
            var temporaryPath = path + ".tmp";
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                File.WriteAllText(temporaryPath, contents);
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

#endif
    public sealed class RunStatsBinding : IDisposable
    {
        private readonly SaveGame save;
        private bool disposed;

        public RunStatsBinding(SaveGame save)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            save.NormalizeAfterLoad();
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void HandleTileBroken(Vector3Int _)
        {
            if (!disposed && save.stats.minedTiles < int.MaxValue) save.stats.minedTiles++;
        }

        private void HandlePlayerDied()
        {
            if (!disposed && save.stats.deaths < int.MaxValue) save.stats.deaths++;
        }
    }

    public static class ProgressionSaveAdapter
    {
        public static void Capture(SaveGame save, Inventory.Inventory inventory, EquipmentSystem equipment,
            string stationId, SmeltingStation smelting)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (smelting == null) throw new ArgumentNullException(nameof(smelting));
            if (string.IsNullOrWhiteSpace(stationId)) throw new ArgumentException("Station ID is required.", nameof(stationId));

            save.NormalizeAfterLoad();
            save.inventory = inventory.Export();
            save.equipment.Clear();
            var equipped = equipment.Export();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (equipped.TryGetValue(slot, out var definition) && definition != null)
                    save.equipment.Add(new EquipmentRecord { slot = slot.ToString(), equipmentId = definition.Id });

            save.smelting.RemoveAll(record => record.stationId == stationId);
            save.smeltingOutputs.RemoveAll(record => record.stationId == stationId);
            if (smelting.Active != null)
                save.smelting.Add(new SmeltingRecord
                {
                    stationId = stationId,
                    recipeId = smelting.Active.Id,
                    remainingSeconds = smelting.RemainingSeconds,
                    isActive = true,
                    queueIndex = -1
                });
            for (var i = 0; i < smelting.Queue.Count; i++)
                save.smelting.Add(new SmeltingRecord
                {
                    stationId = stationId,
                    recipeId = smelting.Queue[i].Id,
                    isActive = false,
                    queueIndex = i
                });
            for (var i = 0; i < smelting.Completed.Count; i++)
                save.smeltingOutputs.Add(new SmeltingOutputRecord
                {
                    stationId = stationId,
                    itemId = smelting.Completed[i].item.Id,
                    amount = smelting.Completed[i].amount
                });
        }

        public static bool Restore(SaveGame save, Inventory.Inventory inventory, EquipmentSystem equipment,
            Func<string, EquipmentDefinition> findEquipment, string stationId, SmeltingStation smelting,
            Func<string, SmeltingDefinition> findSmelting, Func<string, ItemDefinition> findItem)
        {
            if (save == null || inventory == null || equipment == null || findEquipment == null ||
                string.IsNullOrWhiteSpace(stationId) || smelting == null || findSmelting == null || findItem == null)
                return false;
            save.NormalizeAfterLoad();

            var restoredEquipment = new Dictionary<EquipmentSlot, EquipmentDefinition>();
            for (var i = 0; i < save.equipment.Count; i++)
            {
                var record = save.equipment[i];
                if (!Enum.TryParse(record.slot, out EquipmentSlot slot) || restoredEquipment.ContainsKey(slot)) return false;
                var definition = findEquipment(record.equipmentId);
                if (definition == null || !string.Equals(definition.Id, record.equipmentId, StringComparison.Ordinal))
                    return false;
                restoredEquipment.Add(slot, definition);
            }

            SmeltingDefinition active = null;
            var remainingSeconds = 0f;
            var queueRecords = save.smelting.FindAll(record => record.stationId == stationId && !record.isActive);
            queueRecords.Sort((left, right) => left.queueIndex.CompareTo(right.queueIndex));
            var restoredQueue = new List<SmeltingDefinition>();
            for (var i = 0; i < save.smelting.Count; i++)
            {
                var record = save.smelting[i];
                if (record.stationId != stationId || !record.isActive) continue;
                if (active != null) return false;
                active = findSmelting(record.recipeId);
                if (active == null) return false;
                remainingSeconds = record.remainingSeconds;
            }
            for (var i = 0; i < queueRecords.Count; i++)
            {
                if (queueRecords[i].queueIndex != i) return false;
                var definition = findSmelting(queueRecords[i].recipeId);
                if (definition == null) return false;
                restoredQueue.Add(definition);
            }

            var restoredOutputs = new List<ItemAmount>();
            for (var i = 0; i < save.smeltingOutputs.Count; i++)
            {
                var record = save.smeltingOutputs[i];
                if (record.stationId != stationId) continue;
                var item = findItem(record.itemId);
                if (item == null || record.amount <= 0) return false;
                restoredOutputs.Add(new ItemAmount { item = item, amount = record.amount });
            }

            if (!inventory.CanImport(save.inventory)) return false;
            if (!equipment.CanImport(restoredEquipment)) return false;
            if (!smelting.RestoreState(active, remainingSeconds, restoredQueue, restoredOutputs)) return false;
            if (!equipment.TryImport(restoredEquipment)) return false;
            return inventory.TryImport(save.inventory);
        }
    }

    public static class WorldSaveAdapter
    {
        /// <summary>
        /// 상자 개수는 시드·대형 동굴 수에 따라 가변(동굴당 0~2).
        /// 캡처/복원은 "생성된 IChestSource와 저장 목록이 동일 집합"만 요구한다.
        /// </summary>
        private const int MaxChestCount = 64;

        public static bool CaptureWorld(SaveGame save, IEnumerable<TileChangeRecord> tileChanges,
            IEnumerable<PlacedObjectRecord> placedObjects, IChestSource chestSource, ChestProgress chestProgress)
        {
            if (save == null || tileChanges == null || placedObjects == null || chestSource == null || chestProgress == null)
                return false;
            if (!TryValidateWorldRecords(tileChanges, placedObjects, out var validatedTiles, out var validatedObjects))
                return false;
            if (!TryValidateChestSource(chestSource, out var chestIds)) return false;
            save.NormalizeAfterLoad();

            var capturedChests = new List<ChestStateRecord>(chestIds.Count);
            var capturedOpenedIds = new List<string>();
            chestIds.Sort(StringComparer.Ordinal);
            for (var i = 0; i < chestIds.Count; i++)
            {
                var chestId = chestIds[i];
                var opened = chestProgress.IsOpened(chestId);
                var contents = opened
                    ? chestProgress.ExportContents(chestId)
                    : new List<InventorySlot>();
                if (opened && contents.Count != ChestProgress.StorageSlotCount) return false;
                capturedChests.Add(new ChestStateRecord
                {
                    chestId = chestId,
                    position = chestSource.GetChestPosition(chestId),
                    opened = opened,
                    contents = contents
                });
                if (opened) capturedOpenedIds.Add(chestId);
            }
            save.tileChanges = validatedTiles;
            save.placedObjectRecords = validatedObjects;
            save.chests = capturedChests;
            save.openedChestIds = capturedOpenedIds;
            return true;
        }

        public static bool ValidateWorldRecords(SaveGame save)
        {
            return save != null && TryValidateWorldRecords(save.tileChanges, save.placedObjectRecords,
                out _, out _);
        }

        private static bool TryValidateWorldRecords(IEnumerable<TileChangeRecord> tileChanges,
            IEnumerable<PlacedObjectRecord> placedObjects, out List<TileChangeRecord> validatedTiles,
            out List<PlacedObjectRecord> validatedObjects)
        {
            validatedTiles = null;
            validatedObjects = null;
            if (tileChanges == null || placedObjects == null) return false;

            var tiles = new List<TileChangeRecord>();
            foreach (var record in tileChanges)
            {
                if (string.IsNullOrWhiteSpace(record.tileId)) return false;
                tiles.Add(record);
            }

            var objects = new List<PlacedObjectRecord>();
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in placedObjects)
            {
                if (string.IsNullOrWhiteSpace(record.objectId) || string.IsNullOrWhiteSpace(record.definitionId) ||
                    !objectIds.Add(record.objectId) || float.IsNaN(record.position.x) ||
                    float.IsInfinity(record.position.x) || float.IsNaN(record.position.y) ||
                    float.IsInfinity(record.position.y) || float.IsNaN(record.rotationDegrees) ||
                    float.IsInfinity(record.rotationDegrees)) return false;
                objects.Add(record);
            }

            validatedTiles = tiles;
            validatedObjects = objects;
            return true;
        }

        public static bool RestoreChests(SaveGame save, IChestSource chestSource, ChestProgress chestProgress)
        {
            if (save == null || chestSource == null || chestProgress == null) return false;
            if (!TryValidateChestSource(chestSource, out var generatedChestIds)) return false;
            save.NormalizeAfterLoad();
            if (save.chests.Count == 0)
            {
                // 구버전(schema): chests 목록 없이 openedChestIds만 있던 경우 + 신규 0상자 월드.
                var legacyGeneratedIds = new HashSet<string>(generatedChestIds, StringComparer.Ordinal);
                var legacyOpenedIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < save.openedChestIds.Count; i++)
                {
                    var chestId = save.openedChestIds[i];
                    if (string.IsNullOrWhiteSpace(chestId) || !legacyGeneratedIds.Contains(chestId) || !legacyOpenedIds.Add(chestId))
                        return false;
                }
                chestProgress.Import(save.openedChestIds);
                return true;
            }

            var generatedIds = new HashSet<string>(generatedChestIds, StringComparer.Ordinal);
            if (generatedIds.Count != save.chests.Count) return false;
            var openedIds = new List<string>();
            var restoredContents =
                new Dictionary<string, List<InventorySlot>>(StringComparer.Ordinal);
            var savedIds = new HashSet<string>(StringComparer.Ordinal);
            var migratedLegacyCoordinates = false;
            for (var i = 0; i < save.chests.Count; i++)
            {
                var record = save.chests[i];
                if (!savedIds.Add(record.chestId) || !generatedIds.Contains(record.chestId)) return false;
                var generatedPosition = chestSource.GetChestPosition(record.chestId);
                if ((generatedPosition - record.position).sqrMagnitude > .0001f)
                {
                    if (save.SourceSchemaVersion >= SaveGame.CurrentSchemaVersion) return false;
                    record.position = generatedPosition;
                    save.chests[i] = record;
                    migratedLegacyCoordinates = true;
                }
                if (record.opened)
                {
                    openedIds.Add(record.chestId);
                    var slots = save.SourceSchemaVersion >= 20
                        ? record.contents
                        : new List<InventorySlot>();
                    if (slots == null ||
                        save.SourceSchemaVersion >= 20 && slots.Count != ChestProgress.StorageSlotCount ||
                        restoredContents.ContainsKey(record.chestId))
                        return false;
                    restoredContents.Add(record.chestId, slots);
                }
                else if (save.SourceSchemaVersion >= 20 && record.contents != null &&
                         record.contents.Count > 0)
                    return false;
            }
            if (migratedLegacyCoordinates)
                Debug.Log($"[Nyangbingo] 구버전 저장(schema {save.SourceSchemaVersion})의 상자 좌표를 " +
                          "현재 seed 생성 결과로 이관했습니다. 상자 ID와 개봉 상태는 유지됩니다.");
            return chestProgress.TryImport(openedIds, restoredContents);
        }

        private static bool TryValidateChestSource(IChestSource chestSource, out List<string> chestIds)
        {
            chestIds = null;
            if (chestSource?.ChestIds == null) return false;
            if (chestSource.ChestIds.Count > MaxChestCount) return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var positions = new HashSet<Vector2>();
            chestIds = new List<string>(chestSource.ChestIds.Count);
            for (var i = 0; i < chestSource.ChestIds.Count; i++)
            {
                var chestId = chestSource.ChestIds[i];
                if (string.IsNullOrWhiteSpace(chestId) || !ids.Add(chestId)) return false;
                var position = chestSource.GetChestPosition(chestId);
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y) || !positions.Add(position)) return false;
                chestIds.Add(chestId);
            }
            return true;
        }

        public static bool CaptureTurretFuel(SaveGame save, string objectId, TurretController turret)
        {
            if (save == null || string.IsNullOrWhiteSpace(objectId) || turret == null) return false;
            save.NormalizeAfterLoad();
            save.turretFuel.RemoveAll(record => record.objectId == objectId);
            save.turretFuel.Add(new TurretFuelRecord
            {
                objectId = objectId,
                remainingGameSeconds = turret.FuelRemaining,
                storesGameSeconds = true
            });
            return true;
        }

        public static bool RestoreTurretFuel(SaveGame save, string objectId, TurretController turret)
        {
            if (save == null || string.IsNullOrWhiteSpace(objectId) || turret == null) return false;
            save.NormalizeAfterLoad();
            var found = false;
            var matched = default(TurretFuelRecord);
            for (var i = 0; i < save.turretFuel.Count; i++)
            {
                var record = save.turretFuel[i];
                if (record.objectId != objectId) continue;
                if (found) return false;
                found = true;
                matched = record;
            }
            if (!found) return false;
            return matched.storesGameSeconds
                ? turret.RestoreFuelSeconds(matched.remainingGameSeconds)
                : turret.RestoreFuelUnits(matched.fuel);
        }
    }

    public static class PlayerTimeBossSaveAdapter
    {
        public static bool Capture(SaveGame save, Transform player, Health playerHealth,
            ISaveableTimeSource timeSource, BossManager bossManager)
        {
            if (save == null || player == null || playerHealth == null || timeSource == null || bossManager == null)
                return false;
            if (!IsFinite(player.position) || playerHealth.MaxHealth <= 0 || playerHealth.Current <= 0 ||
                playerHealth.Current > playerHealth.MaxHealth || timeSource.Day < 1 ||
                timeSource.TimeOfDayGameSeconds < 0f || float.IsNaN(timeSource.TimeOfDayGameSeconds) ||
                float.IsInfinity(timeSource.TimeOfDayGameSeconds)) return false;
            save.NormalizeAfterLoad();

            save.playerState = new PlayerStateRecord
            {
                hasValue = true,
                position = player.position,
                currentHealth = playerHealth.Current,
                maxHealth = playerHealth.MaxHealth
            };
            save.timeState = new TimeStateRecord
            {
                hasValue = true,
                day = timeSource.Day,
                timeOfDayGameSeconds = timeSource.TimeOfDayGameSeconds,
                isNight = timeSource.IsNight
            };
            save.day = timeSource.Day;
            save.timeOfDaySec = timeSource.TimeOfDayGameSeconds;

            save.activeBoss = new ActiveBossStateRecord();
            return true;
        }

        public static bool Restore(SaveGame save, Transform player, Health playerHealth,
            ISaveableTimeSource timeSource, BossManager bossManager)
        {
            if (save == null || player == null || playerHealth == null || timeSource == null || bossManager == null ||
                bossManager.IsBossActive) return false;
            save.NormalizeAfterLoad();
            if (!save.playerState.hasValue || !save.timeState.hasValue || save.playerState.maxHealth <= 0 ||
                save.playerState.currentHealth <= 0 || save.playerState.currentHealth > save.playerState.maxHealth ||
                !IsFinite(save.playerState.position) || save.timeState.day < 1 ||
                save.timeState.timeOfDayGameSeconds < 0f || float.IsNaN(save.timeState.timeOfDayGameSeconds) ||
                float.IsInfinity(save.timeState.timeOfDayGameSeconds))
                return false;
            var originalPlayerPosition = player.position;
            var originalPlayerMaxHealth = playerHealth.MaxHealth;
            var originalPlayerCurrentHealth = playerHealth.Current;
            var originalPlayerDefense = playerHealth.Defense;
            var originalDamageMultiplier = playerHealth.DamageTakenMultiplier;
            var originalFireMultiplier = playerHealth.FireDamageMultiplier;
            var originalKnockbackImmune = playerHealth.IsKnockbackImmune;
            var originalDay = timeSource.Day;
            var originalTimeOfDay = timeSource.TimeOfDayGameSeconds;
            var originalIsNight = timeSource.IsNight;

            void RollbackPlayerAndTime()
            {
                player.position = originalPlayerPosition;
                playerHealth.ConfigureForRuntime(originalPlayerMaxHealth, originalPlayerDefense);
                playerHealth.RestoreCurrent(originalPlayerCurrentHealth);
                playerHealth.SetDamageTakenMultiplier(originalDamageMultiplier);
                playerHealth.SetFireDamageMultiplier(originalFireMultiplier);
                playerHealth.SetKnockbackImmune(originalKnockbackImmune);
                timeSource.RestoreTimeState(originalDay, originalTimeOfDay, originalIsNight);
            }

            if (!timeSource.RestoreTimeState(save.timeState.day, save.timeState.timeOfDayGameSeconds, save.timeState.isNight))
                return false;

            player.position = save.playerState.position;
            playerHealth.ConfigureForRuntime(save.playerState.maxHealth, playerHealth.Defense);
            if (!playerHealth.RestoreCurrent(save.playerState.currentHealth))
            {
                RollbackPlayerAndTime();
                return false;
            }
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    public interface ISaveSnapshotProvider { SaveGame CaptureSnapshot(); }

#if false // Moved to DawnAutoSave.cs so Unity can serialize the MonoBehaviour by script GUID.
    public sealed class DawnAutoSave : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private MonoBehaviour timeSourceComponent;
        [SerializeField] private MonoBehaviour snapshotProviderComponent;
        [Range(0, SaveManager.SlotCount - 1)][SerializeField] private int slot;
        private ITimeSource timeSource;
        private ISaveSnapshotProvider snapshotProvider;

        private void Awake()
        {
            timeSource = timeSourceComponent as ITimeSource;
            snapshotProvider = snapshotProviderComponent as ISaveSnapshotProvider;
        }
        private void OnEnable() { if (timeSource != null) timeSource.Dawn += SaveAtDawn; }
        private void OnDisable() { if (timeSource != null) timeSource.Dawn -= SaveAtDawn; }
        private void SaveAtDawn()
        {
            if (saveManager != null && snapshotProvider != null)
            {
                var snapshot = snapshotProvider.CaptureSnapshot();
                if (snapshot != null) saveManager.SaveAtDawn(slot, snapshot);
            }
        }

        /// <summary>
        /// 인스펙터 드래그 배선 대신 코드로 직접 연결할 때 쓰는 진입점(세션 루트/디버그 하네스 전용).
        /// <see cref="Nyangbingo.World.WorldSessionSaveProviderAdapter"/>처럼 순수 C# 객체(WorldSessionController)를
        /// 감싸는 MonoBehaviour는 씬 생성 시점에 미리 인스펙터에 꽂아둘 수 없어 런타임 배선이 필요하다.
        /// 이미 활성화된 뒤 호출해도 이전 timeSource 구독을 정리하고 새 timeSource로 다시 구독한다.
        /// </summary>
        public void Configure(SaveManager manager, ITimeSource source, ISaveSnapshotProvider provider, int saveSlot)
        {
            if (timeSource != null) timeSource.Dawn -= SaveAtDawn;

            saveManager = manager;
            timeSource = source;
            snapshotProvider = provider;
            slot = Mathf.Clamp(saveSlot, 0, SaveManager.SlotCount - 1);

            if (isActiveAndEnabled && timeSource != null) timeSource.Dawn += SaveAtDawn;
        }
    }
#endif
}
