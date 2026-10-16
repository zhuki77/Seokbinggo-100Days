using System;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// 장비 인형 위쪽 발톱 칸. 인벤에 들어 있는 것만으로는 효과가 없고, 이 칸에 끼워야 한다.
    /// </summary>
    public sealed class ClawSlotSystem
    {
        public const int VisualSlotIndex = 6;

        private readonly Inventory inventory;
        private readonly Func<string, ItemDefinition> findItem;
        private string equippedItemId = string.Empty;

        public event Action Changed;
        public string EquippedItemId => equippedItemId;
        public bool HasEquippedItem => !string.IsNullOrEmpty(equippedItemId);

        public ClawSlotSystem(Inventory playerInventory, Func<string, ItemDefinition> itemResolver)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            findItem = itemResolver ?? throw new ArgumentNullException(nameof(itemResolver));
        }

        public static bool IsClawItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            switch (itemId)
            {
                case "iron_claw":
                case "icesteel_claw":
                case "frostclaw_gauntlet":
                case EvolvedClawCombatRules.SangunClawId:
                case EvolvedClawCombatRules.PerfectClawId:
                case GimmickWeaponProgress.FirstFrostClawId:
                case GimmickWeaponProgress.YeouijuClawId:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>빈 칸은 맨발톱(1). 채굴 초는 1~3만 있으므로 상위 발톱은 3으로 캔다.</summary>
        public static int ResolveMiningTier(string equippedItemId)
        {
            switch (equippedItemId)
            {
                case "iron_claw": return 2;
                case "icesteel_claw":
                case "frostclaw_gauntlet":
                case EvolvedClawCombatRules.SangunClawId:
                case EvolvedClawCombatRules.PerfectClawId:
                case GimmickWeaponProgress.FirstFrostClawId:
                case GimmickWeaponProgress.YeouijuClawId:
                    return 3;
                default: return 1;
            }
        }

        public string ResolveCombatProfileId() =>
            IsClawItem(equippedItemId) ? equippedItemId : "bare_claw";

        /// <summary>기믹 발톱의 배율 기준. 빈 칸은 맨발톱이다.</summary>
        public static string ResolveMiningProfileId(string equippedItemId) =>
            ResolveMiningTier(equippedItemId) switch
            {
                3 => "icesteel_claw",
                2 => "iron_claw",
                _ => "bare_claw"
            };

        public bool TryEquip(string itemId, int sourceIndex = -1, Inventory sourceInventory = null)
        {
            if (!IsClawItem(itemId) || sourceInventory == null && equippedItemId == itemId || findItem(itemId) == null)
                return false;
            return (sourceInventory ?? inventory).TryExchangeEquipment(itemId, equippedItemId, () =>
            {
                equippedItemId = itemId;
                Changed?.Invoke();
                GameEvents.RaiseEquipmentEquipped();
            }, sourceIndex);
        }

        public bool TryUnequip(int targetIndex = -1, Inventory targetInventory = null)
        {
            if (!HasEquippedItem) return false;
            return (targetInventory ?? inventory).TryExchangeEquipment(null, equippedItemId, () =>
            {
                equippedItemId = string.Empty;
                Changed?.Invoke();
                GameEvents.RaiseEquipmentUnequipped();
            }, targetIndex: targetIndex);
        }

        public bool TryRestore(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                equippedItemId = string.Empty;
                Changed?.Invoke();
                return true;
            }
            if (!IsClawItem(itemId) || findItem(itemId) == null) return false;
            equippedItemId = itemId;
            Changed?.Invoke();
            return true;
        }
    }
}
