using Nyangbingo.Save;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Combat;
using Nyangbingo.Yokai;
using Nyangbingo.Bosses;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    public sealed class DevBTestBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[Nyangbingo] Dev B test scene ready: inventory, crafting, combat, yokai, boss, and save modules can be wired here.");
            var wood = ItemDefinition.CreateRuntime("wood", "나무");
            var stone = ItemDefinition.CreateRuntime("stone", "돌");
            var workbench = ItemDefinition.CreateRuntime("workbench", "작업대", 1);
            var inventory = new Nyangbingo.Inventory.Inventory(id => id == wood.Id ? wood : id == stone.Id ? stone : id == workbench.Id ? workbench : null);
            inventory.TryAdd(wood.Id, 8); inventory.TryAdd(stone.Id, 12);
            var recipe = RecipeDefinition.CreateRuntime("workbench", CraftingStation.None,
                new[] { new ItemAmount { item = wood, amount = 8 }, new ItemAmount { item = stone, amount = 12 } },
                new ItemAmount { item = workbench, amount = 1 });
            var crafted = new CraftingService(inventory).TryCraft(recipe, CraftingStation.None);
            if (!crafted || inventory.Count(workbench.Id) != 1) Debug.LogError("[Nyangbingo] Crafting test failed.");

            var save = gameObject.AddComponent<SaveManager>();
            var sample = new SaveGame { seed = 100, day = 1, inventory = inventory.Export() };
            save.Save(0, sample);
            if (!save.TryLoad(0, out var loaded) || loaded.inventory.Count != Nyangbingo.Inventory.Inventory.SlotCount)
                Debug.LogError("[Nyangbingo] Save test failed.");
            else Debug.Log("[Nyangbingo] Item acquisition, crafting, and save round-trip completed.");

            var yokaiDefinition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 8, 10f,
                new[] { new ItemAmount { item = wood, amount = 1 } });
            var yokai = new GameObject("TemporaryYokai");
            var health = yokai.AddComponent<Health>();
            health.ConfigureForRuntime(yokaiDefinition.HitPoints);
            var loot = yokai.AddComponent<YokaiLoot>();
            loot.ConfigureForRuntime(yokaiDefinition);
            var droppedWood = 0;
            loot.Dropped += (item, amount) => { if (item == wood) droppedWood += amount; };
            health.ApplyDamage(10, DamageTag.Melee);
            if (health.IsDead && droppedWood == 1) Debug.Log("[Nyangbingo] Combat damage and yokai loot completed.");
            else Debug.LogError("[Nyangbingo] Combat or yokai loot test failed.");
            Destroy(yokai);

            var testTime = gameObject.AddComponent<DevBTestTimeSource>();
            var testSpawner = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(testTime, testSpawner);
            var bossDefinition = BossDefinition.CreateRuntime("goblin_chief", YokaiKind.ClubGoblin, workbench,
                new[] { new ItemAmount { item = wood, amount = 2 } });
            var bossObject = new GameObject("TemporaryBoss");
            var bossHealth = bossObject.AddComponent<Health>();
            bossHealth.ConfigureForRuntime(20);
            var bossDefeated = false;
            bossManager.BossEnded += (_, defeated) => bossDefeated = defeated;
            var started = bossManager.TryStart(bossDefinition, bossHealth);
            bossHealth.ApplyDamage(20, DamageTag.Melee);
            if (started && bossDefeated && testSpawner.IsRegularSpawning)
                Debug.Log("[Nyangbingo] Boss start, spawn pause, defeat, and spawn resume completed.");
            else Debug.LogError("[Nyangbingo] Boss flow test failed.");
            Destroy(bossObject);

            inventory.TryAdd(wood.Id, 2); inventory.TryAdd(stone.Id, 1);
            var smelting = new SmeltingStation(inventory);
            var smeltingRecipe = SmeltingDefinition.CreateRuntime("test_smelting",
                new ItemAmount { item = wood, amount = 2 }, new ItemAmount { item = stone, amount = 1 },
                new ItemAmount { item = workbench, amount = 1 }, 1f);
            if (smelting.TryStart(smeltingRecipe) && smelting.Tick(1f) && inventory.Count(workbench.Id) >= 2)
                Debug.Log("[Nyangbingo] Smelting completed.");
            else Debug.LogError("[Nyangbingo] Smelting test failed.");

            var chest = new ChestProgress();
            var chestDefinition = ChestDefinition.CreateRuntime(new[] { new ItemAmount { item = wood, amount = 1 } });
            if (chest.TryOpen("test-chest", chestDefinition) && !chest.TryOpen("test-chest", chestDefinition))
                Debug.Log("[Nyangbingo] Chest single-open protection completed.");
            else Debug.LogError("[Nyangbingo] Chest test failed.");

            var utilities = new UtilityService(); var fanUsed = false;
            utilities.FanUsed += _ => fanUsed = true;
            if (utilities.TryUse(UtilityDefinition.CreateRuntime(UtilityKind.FoldingFan, 3f)) && fanUsed)
                Debug.Log("[Nyangbingo] Utility event completed.");
            else Debug.LogError("[Nyangbingo] Utility test failed.");
        }
    }
}
