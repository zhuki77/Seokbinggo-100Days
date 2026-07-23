using System;
using System.IO;
using Nyangbingo.Bosses;
using UnityEngine;

namespace Nyangbingo.Save
{
    public sealed class SaveManager : MonoBehaviour
    {
        public const int SlotCount = 1;
        private const int LegacySlotCount = 3;
        private static readonly int[] DemoDays = { 1, 15, 30 };
        public event Action<int> Saved;

        private void Awake()
        {
            for (var slot = SlotCount; slot < LegacySlotCount; slot++)
                DeleteFilesForSlot(slot);
        }

        public void Save(int slot, SaveGame data)
        {
            ValidateSlot(slot);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion > SaveGame.CurrentSchemaVersion)
                throw new ArgumentException("Cannot save data from a newer schema version.", nameof(data));
            data.NormalizeAfterLoad();
            WriteAtomically(PathFor(slot), JsonUtility.ToJson(data, true));
            Saved?.Invoke(slot);
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
            if (!File.Exists(path)) { data = null; return false; }
            try { return TryDeserialize(File.ReadAllText(path), out data); }
            catch (IOException) { data = null; return false; }
            catch (UnauthorizedAccessException) { data = null; return false; }
        }

        public bool HasSave(int slot) { ValidateSlot(slot); return File.Exists(PathFor(slot)); }

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

        public bool HasDemoSave(int day)
        {
            if (Array.IndexOf(DemoDays, day) < 0) return false;
            var path = Path.Combine(Application.streamingAssetsPath, "DemoSaves", $"day-{day}.json");
            try
            {
                return File.Exists(path) && TryDeserialize(File.ReadAllText(path), out _);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
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
            catch (Exception) { return false; }
        }

        public void Delete(int slot)
        {
            ValidateSlot(slot);
            DeleteFilesForSlot(slot);
        }

        public void DeleteAll()
        {
            for (var slot = 0; slot < SlotCount; slot++)
                Delete(slot);
        }

        private static string PathFor(int slot) =>
            Path.Combine(Application.persistentDataPath, $"nyangbingo-save-{slot}.json");

        private static void DeleteFilesForSlot(int slot)
        {
            var path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            var temporaryPath = path + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

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
}
