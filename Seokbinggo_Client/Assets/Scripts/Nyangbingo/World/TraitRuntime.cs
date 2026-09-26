using System;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>시작 특성 1회 선택 · 세이브 id 1개 · 효과 훅.</summary>
    public sealed class TraitRuntime
    {
        private readonly GameDataCatalog catalog;
        private readonly Inventory.Inventory inventory;
        private string selectedTraitId = string.Empty;
        private bool startItemGranted;

        public TraitRuntime(GameDataCatalog data, Inventory.Inventory playerInventory)
        {
            catalog = data ?? throw new ArgumentNullException(nameof(data));
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            for (var i = 0; i < TraitRules.AllIds.Length; i++)
                if (catalog.FindTrait(TraitRules.AllIds[i]) == null)
                    throw new InvalidOperationException(
                        $"시작 특성 '{TraitRules.AllIds[i]}' 데이터가 없습니다.");
        }

        public string SelectedTraitId => selectedTraitId ?? string.Empty;
        public bool HasSelection => TraitRules.IsKnownId(SelectedTraitId);
        public bool NeedsSelection => TraitRules.ShouldSelectAtStart(catalog) && !HasSelection;
        public float MiningCriticalBonus =>
            SelectedTraitId == TraitRules.LaborId ? TraitRules.LaborMiningCriticalBonus : 0f;
        public float DayTemperatureRiseMultiplier =>
            SelectedTraitId == TraitRules.CoolId
                ? TraitRules.CoolDayTemperatureRiseMultiplier
                : 1f;

        public event Action Changed;

        public bool TrySelect(string traitId, out string message)
        {
            message = string.Empty;
            if (HasSelection)
            {
                message = "특성은 이미 선택되었습니다.";
                return false;
            }
            if (!TraitRules.IsKnownId(traitId) || catalog.FindTrait(traitId) == null)
            {
                message = "알 수 없는 특성입니다.";
                return false;
            }
            selectedTraitId = traitId;
            if (traitId == TraitRules.RangedId)
                GrantRangedStartItem();
            Changed?.Invoke();
            var definition = catalog.FindTrait(traitId);
            message = $"{definition.DisplayName} 선택";
            return true;
        }

        public bool Restore(string traitId, bool rangedItemAlreadyGranted)
        {
            selectedTraitId = TraitRules.IsKnownId(traitId) ? traitId : string.Empty;
            startItemGranted = rangedItemAlreadyGranted ||
                               SelectedTraitId != TraitRules.RangedId;
            Changed?.Invoke();
            return true;
        }

        public int AdjustMeleeDamage(Health target, int damage) =>
            TraitRules.AdjustMeleeDamage(SelectedTraitId, catalog, target, damage);

        private void GrantRangedStartItem()
        {
            if (startItemGranted) return;
            startItemGranted = true;
            if (inventory.Has(TraitRules.RangedStartItemId, 1)) return;
            inventory.TryAdd(TraitRules.RangedStartItemId, 1);
        }
    }
}
