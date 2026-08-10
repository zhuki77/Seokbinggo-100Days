using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// v46 기믹 무기 지급 진행. 조건 충족 시 1회만 인벤에 지급한다.
    /// </summary>
    public sealed class GimmickWeaponProgress
    {
        public const string FirstFrostClawId = "first_frost_claw";
        public const string BaekjungBundleId = "baekjung_bundle";
        public const string YeouijuClawId = "yeouiju_claw";
        public const string JigwiAshId = "jigwi_ash";
        public const string SangunWhiskerId = "sangun_whisker";
        public const string YeongnoToothId = "yeongno_tooth";

        private readonly HashSet<string> granted = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, ItemDefinition> findItem;

        public GimmickWeaponProgress(Func<string, ItemDefinition> findItemLookup = null)
        {
            findItem = findItemLookup;
        }

        public IReadOnlyCollection<string> GrantedIds => granted;

        public bool HasGranted(string weaponId) =>
            !string.IsNullOrWhiteSpace(weaponId) && granted.Contains(weaponId);

        public bool TryGrant(string weaponId)
        {
            if (string.IsNullOrWhiteSpace(weaponId)) return false;
            return GimmickWeapon.TryGrant(
                weaponId,
                _ => true,
                HasGranted,
                id =>
                {
                    granted.Add(id);
                    TryAcquireIntoInventory(id);
                });
        }

        public void NotifyFirstFrost() => TryGrant(FirstFrostClawId);

        public void NotifyBaekjungSurvived() => TryGrant(BaekjungBundleId);

        public void NotifyImugiCleared() => TryGrant(YeouijuClawId);

        public void NotifyJigwiCleared() => TryGrant(JigwiAshId);

        public void NotifySangunCleared() => TryGrant(SangunWhiskerId);

        public void NotifyYeongnoCleared() => TryGrant(YeongnoToothId);

        public void NotifyBossDefeated(BossDefinition definition)
        {
            if (definition == null) return;
            if (definition.Kind == BossKind.Imugi)
            {
                NotifyImugiCleared();
                return;
            }

            // P0 bosses CSV 확장 전: id 문자열로 내습 보스 기믹을 매핑한다.
            var id = definition.Id ?? string.Empty;
            if (id.IndexOf("jigwi", StringComparison.OrdinalIgnoreCase) >= 0)
                NotifyJigwiCleared();
            else if (id.IndexOf("sangun", StringComparison.OrdinalIgnoreCase) >= 0)
                NotifySangunCleared();
            else if (id.IndexOf("yeongno", StringComparison.OrdinalIgnoreCase) >= 0)
                NotifyYeongnoCleared();
        }

        public List<string> Export()
        {
            var list = new List<string>(granted);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public void Restore(IEnumerable<string> ids)
        {
            granted.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    granted.Add(id);
            }
        }

        private void TryAcquireIntoInventory(string weaponId)
        {
            if (findItem == null) return;
            var item = findItem(weaponId);
            if (item != null) ItemAcquisition.Request(item, 1);
        }
    }
}
