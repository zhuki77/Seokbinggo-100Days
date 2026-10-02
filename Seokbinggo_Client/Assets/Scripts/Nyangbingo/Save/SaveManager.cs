using System;
using System.IO;
using Nyangbingo.Bosses;
using UnityEngine;

namespace Nyangbingo.Save
{
    public sealed class SaveManager : MonoBehaviour
    {
        public const int SlotCount = 1;
        private static readonly int[] DemoDays = { 1, 15, 30 };
        public event Action<int> Saved;

        public void ArchiveBeforeNewGame(int slot)
        {
            ValidateSlot(slot);
            var path = PathFor(slot);
            if (!File.Exists(path)) return;
            // 새 게임 이후의 자동 저장으로도 이전 플레이 원본이 사라지지 않게 별도 보존한다.
            File.Copy(path, path + ".before-new-game-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".json");
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
            if (TryReadSave(path, out data)) return true;
            if (!TryReadSave(path + ".bak", out data)) return false;
            Debug.LogWarning("[Nyangbingo] 기본 저장 파일을 읽을 수 없어 이전 정상 백업을 불러옵니다.");
            return true;
        }

        private static bool TryReadSave(string path, out SaveGame data)
        {
            data = null;
            try { return File.Exists(path) && TryDeserialize(File.ReadAllText(path), out data); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public bool HasSave(int slot) { ValidateSlot(slot); return File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak"); }

        public bool TryLoadLatest(out int slot, out SaveGame data)
        {
            slot = -1;
            data = null;
            var latestWrite = DateTime.MinValue;
            for (var candidate = 0; candidate < SlotCount; candidate++)
            {
                var path = PathFor(candidate);
                if (!TryLoad(candidate, out var loaded)) continue;
                DateTime written;
                try { written = File.GetLastWriteTimeUtc(File.Exists(path) ? path : path + ".bak"); }
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
                if (!File.Exists(path) || !TryDeserialize(File.ReadAllText(path), out data) ||
                    !data.isOfficialDemo) return false;
                // Repeated rehearsals must not rotate the player's only backup away.
                ArchiveBeforeDemoLoad(PathFor(0));
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
                return File.Exists(path) && TryDeserialize(File.ReadAllText(path), out var demo) &&
                       demo.isOfficialDemo;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void ArchiveBeforeDemoLoad(string path)
        {
            var suffix = ".before-demo-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
                         "-" + Guid.NewGuid().ToString("N");
            if (File.Exists(path)) File.Copy(path, path + suffix);
            if (File.Exists(path + ".bak")) File.Copy(path + ".bak", path + ".bak" + suffix);
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
                // B-UI-v71: 구 스키마는 heatStage 등이 빠져 이어하기 시 폭염·서리가 깨지므로 거부한다.
                if (parsed.schemaVersion < SaveGame.MinimumCompatibleSchemaVersion) return false;
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

        private static string PathFor(int slot)
        {
            var directory = Application.persistentDataPath;
#if UNITY_EDITOR
            // SessionState survives play-mode domain reload. Only an explicitly
            // armed QA session redirects saves; normal players/builds are unchanged.
            var replayDirectory = UnityEditor.SessionState.GetString("Nyangbingo.QA.SaveDirectory", "");
            if (!string.IsNullOrEmpty(replayDirectory)) directory = replayDirectory;
#endif
            return Path.Combine(directory, $"nyangbingo-save-{slot}.json");
        }

        private static void DeleteFilesForSlot(int slot)
        {
            var path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
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
                if (!TryReadSave(temporaryPath, out _))
                    throw new IOException("저장 직후 데이터 검증에 실패했습니다. 기존 저장 파일은 보존됩니다.");
                if (File.Exists(path))
                {
                    // 읽을 수 없는 원본으로 정상 백업까지 덮어쓰지 않는다.
                    if (TryReadSave(path, out _)) File.Replace(temporaryPath, path, path + ".bak");
                    else File.Replace(temporaryPath, path, path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                }
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
