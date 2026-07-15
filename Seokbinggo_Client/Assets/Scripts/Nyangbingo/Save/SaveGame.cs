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
    public struct ChestStateRecord
    {
        public string chestId;
        public Vector2 position;
        public bool opened;
    }

    [Serializable]
    public sealed class PlayerStateRecord
    {
        public bool hasValue;
        public Vector3 position;
        public int currentHealth;
        public int maxHealth;
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
        public const int CurrentSchemaVersion = 6;
        public int schemaVersion = CurrentSchemaVersion;
        public int seed; public int day = 1; public float timeOfDaySec;
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<string> unlockedRecipes = new List<string>();
        public List<string> placedObjects = new List<string>();
        public List<string> tilemapDiff = new List<string>();
        public List<PlacedObjectRecord> placedObjectRecords = new List<PlacedObjectRecord>();
        public List<TileChangeRecord> tileChanges = new List<TileChangeRecord>();
        public List<string> modulesDone = new List<string>();
        public float sealPct;
        public int yokaiTears;
        public List<BossRecord> bossRecords = new List<BossRecord>();
        public List<ForcedBossEncounterRecord> forcedBossEncounters = new List<ForcedBossEncounterRecord>();
        public List<CodexRecord> dogam = new List<CodexRecord>();
        public bool magpieJoined;
        public Vector2 magpieNestPosition;
        public List<TurretFuelRecord> turretFuel = new List<TurretFuelRecord>();
        public List<EquipmentRecord> equipment = new List<EquipmentRecord>();
        public List<string> ownedEquipmentIds = new List<string>();
        public List<UtilityCooldownRecord> utilityCooldowns = new List<UtilityCooldownRecord>();
        public List<PendingItemRecord> pendingItemAcquisitions = new List<PendingItemRecord>();
        public CraftingProcessRecord activeCrafting = new CraftingProcessRecord();
        public List<SmeltingRecord> smelting = new List<SmeltingRecord>();
        public List<SmeltingOutputRecord> smeltingOutputs = new List<SmeltingOutputRecord>();
        public List<string> openedChestIds = new List<string>();
        public List<ChestStateRecord> chests = new List<ChestStateRecord>();
        public PlayerStateRecord playerState = new PlayerStateRecord();
        public TimeStateRecord timeState = new TimeStateRecord();
        public ActiveBossStateRecord activeBoss = new ActiveBossStateRecord();
        public BaekjungSchedulerState baekjungProgress = new BaekjungSchedulerState();
        public float baekjungTearRemainder;

        public void NormalizeAfterLoad()
        {
            var isLegacySchema = schemaVersion <= 0;
            if (inventory == null) inventory = new List<InventorySlot>();
            if (unlockedRecipes == null) unlockedRecipes = new List<string>();
            if (placedObjects == null) placedObjects = new List<string>();
            if (tilemapDiff == null) tilemapDiff = new List<string>();
            if (placedObjectRecords == null) placedObjectRecords = new List<PlacedObjectRecord>();
            if (tileChanges == null) tileChanges = new List<TileChangeRecord>();
            if (modulesDone == null) modulesDone = new List<string>();
            if (bossRecords == null) bossRecords = new List<BossRecord>();
            if (forcedBossEncounters == null) forcedBossEncounters = new List<ForcedBossEncounterRecord>();
            if (dogam == null) dogam = new List<CodexRecord>();
            if (turretFuel == null) turretFuel = new List<TurretFuelRecord>();
            if (equipment == null) equipment = new List<EquipmentRecord>();
            if (ownedEquipmentIds == null) ownedEquipmentIds = new List<string>();
            if (utilityCooldowns == null) utilityCooldowns = new List<UtilityCooldownRecord>();
            if (pendingItemAcquisitions == null) pendingItemAcquisitions = new List<PendingItemRecord>();
            if (activeCrafting == null) activeCrafting = new CraftingProcessRecord();
            if (smelting == null) smelting = new List<SmeltingRecord>();
            if (smeltingOutputs == null) smeltingOutputs = new List<SmeltingOutputRecord>();
            if (openedChestIds == null) openedChestIds = new List<string>();
            if (chests == null) chests = new List<ChestStateRecord>();
            if (playerState == null) playerState = new PlayerStateRecord();
            if (timeState == null) timeState = new TimeStateRecord();
            if (activeBoss == null) activeBoss = new ActiveBossStateRecord();
            if (baekjungProgress == null) baekjungProgress = new BaekjungSchedulerState();
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
            if (schemaVersion < CurrentSchemaVersion) schemaVersion = CurrentSchemaVersion;
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
                if (!Enum.TryParse(record.kind, out UtilityKind kind) || cooldowns.ContainsKey(kind) ||
                    record.remainingGameSeconds <= 0f || float.IsNaN(record.remainingGameSeconds) ||
                    float.IsInfinity(record.remainingGameSeconds)) return false;
                cooldowns.Add(kind, record.remainingGameSeconds);
            }
            return service.RestoreCooldowns(cooldowns);
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

    public sealed class SaveManager : MonoBehaviour
    {
        public const int SlotCount = 3;
        public void Save(int slot, SaveGame data)
        {
            ValidateSlot(slot);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion > SaveGame.CurrentSchemaVersion)
                throw new ArgumentException("Cannot save data from a newer schema version.", nameof(data));
            data.NormalizeAfterLoad();
            WriteAtomically(PathFor(slot), JsonUtility.ToJson(data, true));
        }
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
        private const int RequiredChestCount = 20;

        public static bool CaptureWorld(SaveGame save, IEnumerable<TileChangeRecord> tileChanges,
            IEnumerable<PlacedObjectRecord> placedObjects, IChestSource chestSource, ChestProgress chestProgress)
        {
            if (save == null || tileChanges == null || placedObjects == null || chestSource == null || chestProgress == null)
                return false;
            if (!TryValidateWorldRecords(tileChanges, placedObjects, out var validatedTiles, out var validatedObjects))
                return false;
            if (!TryValidateChestSource(chestSource, out var chestIds)) return false;
            save.NormalizeAfterLoad();

            save.tileChanges = validatedTiles;
            save.placedObjectRecords = validatedObjects;
            save.chests.Clear();
            save.openedChestIds.Clear();

            chestIds.Sort(StringComparer.Ordinal);
            for (var i = 0; i < chestIds.Count; i++)
            {
                var chestId = chestIds[i];
                var opened = chestProgress.IsOpened(chestId);
                save.chests.Add(new ChestStateRecord
                {
                    chestId = chestId,
                    position = chestSource.GetChestPosition(chestId),
                    opened = opened
                });
                if (opened) save.openedChestIds.Add(chestId);
            }
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
            var tilePositions = new HashSet<Vector3Int>();
            foreach (var record in tileChanges)
            {
                if (string.IsNullOrWhiteSpace(record.tileId)) return false;
                var position = new Vector3Int(record.x, record.y, record.z);
                if (!tilePositions.Add(position)) return false;
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

            if (save.chests.Count != RequiredChestCount) return false;
            var generatedIds = new HashSet<string>(generatedChestIds, StringComparer.Ordinal);
            if (generatedIds.Count != save.chests.Count) return false;
            var openedIds = new List<string>();
            var savedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.chests.Count; i++)
            {
                var record = save.chests[i];
                if (!savedIds.Add(record.chestId) || !generatedIds.Contains(record.chestId)) return false;
                if ((chestSource.GetChestPosition(record.chestId) - record.position).sqrMagnitude > .0001f) return false;
                if (record.opened) openedIds.Add(record.chestId);
            }
            chestProgress.Import(openedIds);
            return true;
        }

        private static bool TryValidateChestSource(IChestSource chestSource, out List<string> chestIds)
        {
            chestIds = null;
            if (chestSource?.ChestIds == null || chestSource.ChestIds.Count != RequiredChestCount) return false;

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
            if (!IsFinite(player.position) || playerHealth.MaxHealth <= 0 || playerHealth.Current < 0 ||
                playerHealth.Current > playerHealth.MaxHealth || timeSource.Day < 1 ||
                timeSource.TimeOfDayGameSeconds < 0f || float.IsNaN(timeSource.TimeOfDayGameSeconds) ||
                float.IsInfinity(timeSource.TimeOfDayGameSeconds)) return false;
            if (bossManager.IsBossActive && (bossManager.ActiveDefinition == null || bossManager.ActiveHealth == null ||
                bossManager.ActiveHealth.IsDead || !IsFinite(bossManager.ActiveHealth.transform.position) ||
                float.IsNaN(bossManager.ActiveSummonedAtGameSeconds) ||
                float.IsInfinity(bossManager.ActiveSummonedAtGameSeconds) ||
                bossManager.ActiveSummonedAtGameSeconds < 0f)) return false;
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

            save.activeBoss = new ActiveBossStateRecord { active = bossManager.IsBossActive };
            if (bossManager.IsBossActive)
            {
                save.activeBoss.bossId = bossManager.ActiveDefinition.Id;
                save.activeBoss.position = bossManager.ActiveHealth.transform.position;
                save.activeBoss.currentHealth = bossManager.ActiveHealth.Current;
                save.activeBoss.maxHealth = bossManager.ActiveHealth.MaxHealth;
                save.activeBoss.summonedAtGameSeconds = bossManager.ActiveSummonedAtGameSeconds;
            }
            return true;
        }

        public static bool Restore(SaveGame save, Transform player, Health playerHealth,
            ISaveableTimeSource timeSource, BossManager bossManager, Func<string, BossDefinition> findBoss,
            Func<BossDefinition, int, Health> spawnBoss)
        {
            if (save == null || player == null || playerHealth == null || timeSource == null || bossManager == null ||
                findBoss == null || spawnBoss == null || bossManager.IsBossActive) return false;
            save.NormalizeAfterLoad();
            if (!save.playerState.hasValue || !save.timeState.hasValue || save.playerState.maxHealth <= 0 ||
                save.playerState.currentHealth < 0 || save.playerState.currentHealth > save.playerState.maxHealth ||
                !IsFinite(save.playerState.position) || save.timeState.day < 1 ||
                save.timeState.timeOfDayGameSeconds < 0f || float.IsNaN(save.timeState.timeOfDayGameSeconds) ||
                float.IsInfinity(save.timeState.timeOfDayGameSeconds))
                return false;
            BossDefinition activeDefinition = null;
            if (save.activeBoss.active)
            {
                if (!save.timeState.isNight || string.IsNullOrWhiteSpace(save.activeBoss.bossId) ||
                    save.activeBoss.maxHealth <= 0 || save.activeBoss.currentHealth <= 0 ||
                    save.activeBoss.currentHealth > save.activeBoss.maxHealth ||
                    !IsFinite(save.activeBoss.position) || float.IsNaN(save.activeBoss.summonedAtGameSeconds) ||
                    float.IsInfinity(save.activeBoss.summonedAtGameSeconds) ||
                    save.activeBoss.summonedAtGameSeconds < 0f) return false;
                activeDefinition = findBoss(save.activeBoss.bossId);
                if (activeDefinition == null || activeDefinition.Id != save.activeBoss.bossId) return false;
            }
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
            if (!save.activeBoss.active) return true;
            var bossHealth = spawnBoss(activeDefinition, save.activeBoss.maxHealth);
            if (bossHealth == null)
            {
                RollbackPlayerAndTime();
                return false;
            }
            bossHealth.transform.position = save.activeBoss.position;
            bossHealth.ConfigureForRuntime(save.activeBoss.maxHealth);
            if (!bossHealth.RestoreCurrent(save.activeBoss.currentHealth) ||
                !bossManager.RestoreActive(activeDefinition, bossHealth, save.activeBoss.summonedAtGameSeconds))
            {
                UnityEngine.Object.Destroy(bossHealth.gameObject);
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
            if (saveManager != null && snapshotProvider != null) saveManager.Save(slot, snapshotProvider.CaptureSnapshot());
        }
    }
}
