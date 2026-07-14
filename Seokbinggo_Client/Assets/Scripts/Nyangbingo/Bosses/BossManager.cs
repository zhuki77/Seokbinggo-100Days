using System;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public interface IRegularSpawnController { void SetRegularSpawning(bool enabled); }

    public sealed class BossManager : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour timeSourceComponent;
        [SerializeField] private MonoBehaviour spawnControllerComponent;
        private ITimeSource timeSource;
        private IRegularSpawnController spawnController;
        private ActiveBoss active;
        public bool IsBossActive => active != null;
        public event Action<BossDefinition> BossStarted;
        public event Action<BossDefinition, bool> BossEnded; // true when defeated

        private sealed class ActiveBoss
        {
            public BossDefinition definition;
            public Health health;
        }

        private void Awake()
        {
            timeSource = timeSourceComponent as ITimeSource;
            spawnController = spawnControllerComponent as IRegularSpawnController;
        }
        private void OnEnable() { if (timeSource != null) timeSource.Dawn += HandleDawn; }
        private void OnDisable() { if (timeSource != null) timeSource.Dawn -= HandleDawn; }

        public void ConfigureForRuntime(ITimeSource time, IRegularSpawnController spawner)
        {
            if (timeSource != null) timeSource.Dawn -= HandleDawn;
            timeSource = time; spawnController = spawner;
            if (isActiveAndEnabled && timeSource != null) timeSource.Dawn += HandleDawn;
        }

        public bool TryStart(BossDefinition definition, Health spawnedBoss)
        {
            if (definition == null || spawnedBoss == null || active != null || timeSource == null || !timeSource.IsNight) return false;
            active = new ActiveBoss { definition = definition, health = spawnedBoss };
            active.health.Died += HandleDefeated;
            spawnController?.SetRegularSpawning(false);
            BossStarted?.Invoke(definition);
            return true;
        }

        private void HandleDefeated()
        {
            if (active == null) return;
            active.health.Died -= HandleDefeated;
            EndActiveBoss(true);
        }
        private void HandleDawn() { if (active != null) EndActiveBoss(false); }
        private void EndActiveBoss(bool defeated)
        {
            var definition = active.definition;
            if (!defeated && active.health != null) Destroy(active.health.gameObject);
            active = null;
            spawnController?.SetRegularSpawning(true);
            BossEnded?.Invoke(definition, defeated);
        }
    }
}
