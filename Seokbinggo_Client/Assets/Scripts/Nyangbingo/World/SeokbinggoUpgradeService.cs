using System;
using System.Collections.Generic;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 석빙고 단계 승급 런타임. materials는 카탈로그 모듈을 우선하고, 없으면 Builtin.
    /// </summary>
    public sealed class SeokbinggoUpgradeService
    {
        private readonly GameDataCatalog catalog;
        private readonly Func<Inventory.Inventory> inventorySource;

        public SeokbinggoUpgradeService(GameDataCatalog gameDataCatalog,
            Func<Inventory.Inventory> resolveInventory)
        {
            catalog = gameDataCatalog;
            inventorySource = resolveInventory ?? throw new ArgumentNullException(nameof(resolveInventory));
        }

        public int Stage { get; private set; }

        public int TurretSlotCap => SeokbinggoRules.TurretSlotCap(Stage);

        public bool IsSmithyUnlocked => SeokbinggoRules.IsSmithyUnlocked(Stage);

        public bool IsMaxStage => Stage >= SeokbinggoRules.MaxStage;

        public string NextModuleId => SeokbinggoRules.ModuleIdForNextStage(Stage);

        public void RestoreStage(int stage) =>
            Stage = Mathf.Clamp(stage, 0, SeokbinggoRules.MaxStage);

        public bool TryGetNextMaterials(out IReadOnlyList<(string itemId, int amount)> materials)
        {
            materials = Array.Empty<(string, int)>();
            var next = Stage + 1;
            if (next < 1 || next > SeokbinggoRules.MaxStage) return false;

            var module = catalog?.FindModule(SeokbinggoRules.ModuleIdForNextStage(Stage));
            if (module?.Materials != null && module.Materials.Length > 0)
            {
                var list = new List<(string, int)>(module.Materials.Length);
                for (var i = 0; i < module.Materials.Length; i++)
                {
                    var entry = module.Materials[i];
                    if (entry.item == null || string.IsNullOrEmpty(entry.item.Id) || entry.amount <= 0)
                        return false;
                    list.Add((entry.item.Id, entry.amount));
                }

                materials = list;
                return true;
            }

            return SeokbinggoRules.TryGetBuiltinMaterials(next, out materials);
        }

        public bool CanUpgrade(out string reason)
        {
            reason = null;
            if (IsMaxStage)
            {
                reason = "석빙고가 이미 최고 단계입니다.";
                return false;
            }

            var inventory = inventorySource();
            if (inventory == null)
            {
                reason = "인벤토리를 찾을 수 없습니다.";
                return false;
            }

            if (!TryGetNextMaterials(out var materials))
            {
                reason = "다음 단계 재료표를 찾을 수 없습니다.";
                return false;
            }

            for (var i = 0; i < materials.Count; i++)
            {
                var (itemId, amount) = materials[i];
                if (!inventory.Has(itemId, amount))
                {
                    reason = $"재료 부족: {itemId} ×{amount}";
                    return false;
                }
            }

            return true;
        }

        public bool TryUpgrade(out string message)
        {
            message = null;
            if (!CanUpgrade(out var reason))
            {
                message = reason;
                return false;
            }

            var inventory = inventorySource();
            if (!TryGetNextMaterials(out var materials))
            {
                message = "다음 단계 재료표를 찾을 수 없습니다.";
                return false;
            }

            var removed = new List<(string itemId, int amount)>();
            for (var i = 0; i < materials.Count; i++)
            {
                var (itemId, amount) = materials[i];
                if (!inventory.TryRemove(itemId, amount))
                {
                    for (var r = 0; r < removed.Count; r++)
                        inventory.TryAdd(removed[r].itemId, removed[r].amount);
                    message = $"재료 소모 실패: {itemId}";
                    return false;
                }

                removed.Add((itemId, amount));
            }

            Stage++;
            message = $"석빙고 {Stage}단계 승급 · 터렛 슬롯 {TurretSlotCap}" +
                      (IsSmithyUnlocked && Stage == SeokbinggoRules.SmithyUnlockStage
                          ? " · 대장간 해금"
                          : string.Empty);
            Debug.Log($"[Nyangbingo] Seokbinggo upgraded to stage={Stage}.");
            return true;
        }
    }
}
