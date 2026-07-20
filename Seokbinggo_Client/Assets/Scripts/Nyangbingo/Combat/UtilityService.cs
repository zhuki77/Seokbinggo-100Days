using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Combat
{
    public sealed class UtilityService : IGameSecondsTickable
    {
        private readonly Dictionary<UtilityKind, float> cooldowns = new Dictionary<UtilityKind, float>();
        private readonly Nyangbingo.Inventory.Inventory inventory;
        public event Action<float> FanUsed;
        public event Action<float> AlarmPlaced;

        public UtilityService(Nyangbingo.Inventory.Inventory inventory = null)
        {
            this.inventory = inventory;
        }

        public bool TryUse(UtilityDefinition utility)
            => TryActivate(utility, requireInventoryOwnership: true);

        /// <summary>설치된 방울 금줄이 침입을 감지했을 때 인벤토리 소유 여부와 무관하게 공용 쿨다운을 발동한다.</summary>
        public bool TryTriggerInstalledBellRope(UtilityDefinition utility)
        {
            if (utility == null || utility.Kind != UtilityKind.BellRope) return false;
            return TryActivate(utility, requireInventoryOwnership: false);
        }

        private bool TryActivate(UtilityDefinition utility, bool requireInventoryOwnership)
        {
            if (utility == null || !IsSupported(utility.Kind)) return false;
            if (GetCooldownRemaining(utility.Kind) > 0f) return false;
            if (requireInventoryOwnership && inventory != null)
            {
                if (string.IsNullOrWhiteSpace(utility.Id) || !inventory.Has(utility.Id, 1)) return false;
                if (utility.Consumable && !inventory.TryRemove(utility.Id, 1)) return false;
            }
            else if (requireInventoryOwnership && utility.Consumable) return false;

            var used = false;
            switch (utility.Kind)
            {
                case UtilityKind.Hapjukseon: FanUsed?.Invoke(utility.Value); used = true; break;
                case UtilityKind.BellRope: AlarmPlaced?.Invoke(utility.Value); used = true; break;
            }

            if (used && utility.CooldownSeconds > 0f) cooldowns[utility.Kind] = utility.CooldownSeconds;
            return used;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds)) return;
            var kinds = new List<UtilityKind>(cooldowns.Keys);
            for (var i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                var remaining = Math.Max(0f, cooldowns[kind] - deltaGameSeconds);
                if (remaining <= .0001f) cooldowns.Remove(kind);
                else cooldowns[kind] = remaining;
            }
        }

        public float GetCooldownRemaining(UtilityKind kind) =>
            cooldowns.TryGetValue(kind, out var remaining) ? remaining : 0f;

        public Dictionary<UtilityKind, float> ExportCooldowns() =>
            new Dictionary<UtilityKind, float>(cooldowns);

        public bool RestoreCooldowns(IReadOnlyDictionary<UtilityKind, float> values)
        {
            if (values == null) return false;
            foreach (var pair in values)
                if (!IsSupported(pair.Key) || pair.Value < 0f || float.IsNaN(pair.Value) || float.IsInfinity(pair.Value))
                    return false;

            cooldowns.Clear();
            foreach (var pair in values)
                if (pair.Value > .0001f) cooldowns.Add(pair.Key, pair.Value);
            return true;
        }

        private static bool IsSupported(UtilityKind kind) => kind == UtilityKind.Hapjukseon ||
            kind == UtilityKind.BellRope;
    }
}
