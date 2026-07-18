using System;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public interface IBossSummonSite
    {
        bool IsAtDeepAltar(BossDefinition definition);
    }

    public interface IForcedBossSpawnController
    {
        Health SpawnBoss(BossDefinition definition);
    }

    public static class BossEncounterRules
    {
        public static bool ShouldStartForcedEncounter(BossDefinition definition, ITimeSource timeSource, bool alreadyTriggered)
        {
            return definition != null && timeSource != null && !alreadyTriggered && definition.ForcedDay > 0 &&
                   timeSource.Day == definition.ForcedDay && timeSource.IsNight;
        }
    }

    public sealed class BossSummonService
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        private readonly BossManager bossManager;
        private readonly IBossSummonSite summonSite;

        public BossSummonService(Nyangbingo.Inventory.Inventory inventory, BossManager bossManager, IBossSummonSite summonSite = null)
        {
            this.inventory = inventory;
            this.bossManager = bossManager;
            this.summonSite = summonSite;
        }

        // The caller owns the spawned prefab. Reserve the summon item before start events can mutate inventory.
        public bool TryConsumeAndStart(BossDefinition definition, Health spawnedBoss,
            float summonedAtGameSeconds = 0f)
        {
            if (definition == null || definition.SummonItem == null || inventory == null || bossManager == null ||
                !inventory.Has(definition.SummonItem.Id, 1)) return false;
            if (definition.RequiresDeepAltar && (summonSite == null || !summonSite.IsAtDeepAltar(definition))) return false;
            if (!inventory.TryRemove(definition.SummonItem.Id, 1)) return false;
            if (bossManager.TryStart(definition, spawnedBoss, summonedAtGameSeconds)) return true;
            inventory.TryAdd(definition.SummonItem.Id, 1);
            return false;
        }
    }

    public sealed class ForcedBossEncounterBinding : IDisposable
    {
        private readonly BossDefinition definition;
        private readonly ITimeSource timeSource;
        private readonly BossManager bossManager;
        private readonly IForcedBossSpawnController spawnController;
        private bool triggered;
        private bool disposed;

        public ForcedBossEncounterBinding(BossDefinition definition, ITimeSource timeSource, BossManager bossManager,
            IForcedBossSpawnController spawnController, bool alreadyTriggered = false)
        {
            this.definition = definition;
            this.timeSource = timeSource;
            this.bossManager = bossManager;
            this.spawnController = spawnController;
            triggered = alreadyTriggered;
            GameEvents.OnNightStart += HandleNightStart;
        }

        public bool HasTriggered => triggered;

        public bool TryStartForCurrentNight()
        {
            if (disposed || bossManager == null || spawnController == null ||
                !BossEncounterRules.ShouldStartForcedEncounter(definition, timeSource, triggered)) return false;

            var spawnedBoss = spawnController.SpawnBoss(definition);
            if (spawnedBoss == null) return false;
            if (!bossManager.TryStart(definition, spawnedBoss))
            {
                UnityEngine.Object.Destroy(spawnedBoss.gameObject);
                return false;
            }

            triggered = true;
            return true;
        }

        public void RestoreTriggered(bool value) => triggered = value;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnNightStart -= HandleNightStart;
        }

        private void HandleNightStart() => TryStartForCurrentNight();
    }
}
