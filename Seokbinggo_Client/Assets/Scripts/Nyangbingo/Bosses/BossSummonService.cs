using Nyangbingo.Data;
using Nyangbingo.Inventory;

namespace Nyangbingo.Bosses
{
    public sealed class BossSummonService
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        private readonly BossManager bossManager;
        public BossSummonService(Nyangbingo.Inventory.Inventory inventory, BossManager bossManager)
        {
            this.inventory = inventory;
            this.bossManager = bossManager;
        }

        // The caller spawns the prefab; the item is consumed only after a valid night-time start.
        public bool TryConsumeAndStart(BossDefinition definition, Nyangbingo.Combat.Health spawnedBoss)
        {
            if (definition == null || definition.SummonItem == null || inventory == null || !inventory.Has(definition.SummonItem.Id, 1)) return false;
            if (!bossManager.TryStart(definition, spawnedBoss)) return false;
            return inventory.TryRemove(definition.SummonItem.Id, 1);
        }
    }
}
