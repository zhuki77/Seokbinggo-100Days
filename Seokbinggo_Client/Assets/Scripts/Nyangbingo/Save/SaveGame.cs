using System;
using System.Collections.Generic;
using System.IO;
using Nyangbingo.Inventory;
using UnityEngine;
using Nyangbingo.Core;

namespace Nyangbingo.Save
{
    [Serializable]
    public struct BossRecord { public string bossId; public int count; public int firstDay; }

    [Serializable]
    public struct CodexRecord { public string yokaiId; public int kills; }

    [Serializable]
    public struct TurretFuelRecord { public string objectId; public int fuel; }

    [Serializable]
    public struct EquipmentRecord { public string slot; public string equipmentId; }

    [Serializable]
    public struct SmeltingRecord { public string stationId; public string recipeId; public float remainingSeconds; }

    [Serializable]
    public struct SmeltingOutputRecord { public string stationId; public string itemId; public int amount; }

    [Serializable]
    public sealed class SaveGame
    {
        public int seed; public int day = 1; public float timeOfDaySec;
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<string> unlockedRecipes = new List<string>();
        public List<string> placedObjects = new List<string>();
        public List<string> tilemapDiff = new List<string>();
        public List<string> modulesDone = new List<string>();
        public float sealPct;
        public int yokaiTears;
        public List<BossRecord> bossRecords = new List<BossRecord>();
        public List<CodexRecord> dogam = new List<CodexRecord>();
        public bool magpieJoined;
        public Vector2 magpieNestPosition;
        public List<TurretFuelRecord> turretFuel = new List<TurretFuelRecord>();
        public List<EquipmentRecord> equipment = new List<EquipmentRecord>();
        public List<SmeltingRecord> smelting = new List<SmeltingRecord>();
        public List<SmeltingOutputRecord> smeltingOutputs = new List<SmeltingOutputRecord>();
        public List<string> openedChestIds = new List<string>();
    }

    public sealed class SaveManager : MonoBehaviour
    {
        public const int SlotCount = 3;
        public void Save(int slot, SaveGame data)
        {
            ValidateSlot(slot);
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
        }
        public bool TryLoad(int slot, out SaveGame data)
        {
            ValidateSlot(slot);
            var path = PathFor(slot);
            data = File.Exists(path) ? JsonUtility.FromJson<SaveGame>(File.ReadAllText(path)) : null;
            return data != null;
        }
        public void Delete(int slot) { ValidateSlot(slot); var path = PathFor(slot); if (File.Exists(path)) File.Delete(path); }
        private static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"nyangbingo-save-{slot}.json");
        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
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
