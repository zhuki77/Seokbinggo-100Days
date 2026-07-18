using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 메인 씬의 밤 일반 스폰, 백중 대기열, 강제 보스와 새벽 도주를 하나의 월드 세션에 묶는다.
    /// 생성 개체는 최종 프리팹 대신 런타임 컴포넌트만 사용하므로 아트 교체와 무관하다.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(MainGameBootstrap), typeof(MainGameRuntimeServices), typeof(BossManager))]
    public sealed class MainGameEncounterCoordinator : MonoBehaviour,
        IRegularSpawnController, IForcedBossSpawnController, IBaekjungSpawnController
    {
        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private BossManager bossManager;
        [SerializeField] private MainGameRaidTarget raidTarget;
        [Min(1)][SerializeField] private int minimumSpawnRange = 12;
        [Min(1)][SerializeField] private int maximumSpawnRange = 24;

        private readonly Queue<YokaiDefinition> pendingRegular = new Queue<YokaiDefinition>();
        private readonly List<SpawnedYokai> spawnedYokai = new List<SpawnedYokai>();
        private readonly List<ForcedBossEncounterBinding> forcedBossBindings = new List<ForcedBossEncounterBinding>();
        private readonly List<BossDefinition> forcedBossDefinitions = new List<BossDefinition>();
        private BaekjungScheduler baekjungScheduler;
        private BaekjungTimeBinding baekjungTimeBinding;
        private BaekjungWaveSpawner baekjungWaveSpawner;
        private BaekjungRegularSpawnGate baekjungRegularSpawnGate;
        private DayCurveDefinition currentDayCurve;
        private bool regularSpawningEnabled = true;
        private bool discardRegularForCurrentNight;
        private bool forcedBossSpawnPending;
        private bool initialized;
        private bool restoringSnapshot;
        private int spawnSequence;
        private int debugBossIndex;
        private BossCombatController activeBossCombat;
        private readonly List<SpawnedYokai> bossPausedYokai = new List<SpawnedYokai>();

        private sealed class SpawnedYokai
        {
            public Health health;
            public YokaiBrain brain;
            public bool raid;
        }

        public int ActiveRaidCount => spawnedYokai.Count(entry => entry.raid && IsAlive(entry));
        public int ActiveRegularCount => spawnedYokai.Count(entry => !entry.raid && IsAlive(entry));
        public int PendingRegularCount => pendingRegular.Count;
        public bool IsRegularSpawningEnabled => regularSpawningEnabled && !discardRegularForCurrentNight;
        public BossManager BossManager => bossManager;
        public BaekjungScheduler BaekjungScheduler => baekjungScheduler;
        public Transform PlayerTransform => raidTarget != null ? raidTarget.transform : null;
        public Health PlayerHealth => raidTarget != null ? raidTarget.GetComponent<Health>() : null;
        public bool CanSerializeProgress => initialized && bossManager != null && !bossManager.IsBossActive;
        public event Action RaidSlotAvailable;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, BossManager manager, MainGameRaidTarget target)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            bossManager = manager;
            raidTarget = target;
        }

        private void Start() => Initialize();

#if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) TryStartEditorBossEncounter(
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                    ? "gangcheol_boss"
                    : "king_dokkaebi");
            if (Input.GetKeyDown(KeyCode.J)) DefeatAllYokaiForEditorTest();
            if (Input.GetKeyDown(KeyCode.K)) DefeatActiveBossForEditorTest();
        }

        private void DefeatAllYokaiForEditorTest()
        {
            discardRegularForCurrentNight = true;
            regularSpawningEnabled = false;
            pendingRegular.Clear();
            var targets = spawnedYokai
                .Where(entry => IsAlive(entry) && entry.health.gameObject.activeInHierarchy)
                .Select(entry => entry.health)
                .ToArray();
            for (var index = 0; index < targets.Length; index++)
                targets[index].ApplyDamage(int.MaxValue, DamageTag.Melee);
            Debug.Log($"[Nyangbingo] J yokai test defeat completed: count={targets.Length}, " +
                      "regular spawning locked for this night.");
        }

        private void DefeatActiveBossForEditorTest()
        {
            var health = bossManager != null ? bossManager.ActiveHealth : null;
            var definition = bossManager != null ? bossManager.ActiveDefinition : null;
            if (health == null || health.IsDead || definition == null) return;
            Debug.Log($"[Nyangbingo] K boss test defeat requested: {definition.Id}.");
            health.ApplyDamage(int.MaxValue, DamageTag.Melee);
        }
#endif

        public bool Initialize()
        {
            if (initialized) return true;
            bootstrap ??= GetComponent<MainGameBootstrap>();
            runtimeServices ??= GetComponent<MainGameRuntimeServices>();
            bossManager ??= GetComponent<BossManager>();
            if (gameDataCatalog == null) gameDataCatalog = bootstrap?.GameDataCatalog;
            if (bootstrap == null || runtimeServices == null || bossManager == null || raidTarget == null ||
                gameDataCatalog == null || !bootstrap.InitializeServices() || !runtimeServices.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameEncounterCoordinator: 메인 세션·데이터·스폰 표적 배선이 필요합니다.");
                return false;
            }

            bossManager.ConfigureForRuntime(bootstrap.TimeService, this);
            baekjungScheduler = new BaekjungScheduler(gameDataCatalog.DayEvents);
            baekjungWaveSpawner = new BaekjungWaveSpawner(baekjungScheduler, this);
            baekjungRegularSpawnGate = new BaekjungRegularSpawnGate(baekjungScheduler, this);
            baekjungTimeBinding = new BaekjungTimeBinding(bootstrap.TimeService, baekjungScheduler);
            runtimeServices.Register(baekjungTimeBinding);

            GameEvents.OnNightStart += HandleNightStart;
            GameEvents.OnDawnWarning += HandleDawnWarning;
            bootstrap.WorldReady += HandleWorldReady;
            bossManager.BossStarted += HandleBossStarted;
            bossManager.BossEnded += HandleBossEnded;
            BuildForcedBossBindings();
            initialized = true;
            HandleWorldReady();
            if (bootstrap.TimeService.IsNight) HandleNightStart();

            Debug.Log($"[Nyangbingo] MainGameEncounterCoordinator: 밤 스폰·요괴 새벽 도주·보스·백중 연결 완료 " +
                      $"(yokai={gameDataCatalog.Yokai.Count}, bosses={gameDataCatalog.Bosses.Count}, " +
                      $"events={gameDataCatalog.DayEvents.Count}, forced={forcedBossBindings.Count}, " +
                      $"activeRegular={ActiveRegularCount}, pendingRegular={PendingRegularCount}, " +
                      $"tickConsumers={runtimeServices.RegisteredConsumerCount}).");
            return true;
        }

        public void SetRegularSpawning(bool enabled)
        {
            if (!enabled && forcedBossSpawnPending)
            {
                forcedBossSpawnPending = false;
                return;
            }
            if (!enabled && bossManager != null && bossManager.IsBossActive)
            {
                discardRegularForCurrentNight = true;
                pendingRegular.Clear();
            }
            regularSpawningEnabled = enabled && bootstrap?.TimeService?.IsNight == true;
            if (IsRegularSpawningEnabled) TryFillRegularSlots();
        }

        public Health SpawnBoss(BossDefinition definition)
        {
            if (definition == null || !TryGetSpawnPosition(out var position)) return null;
            var bossObject = new GameObject($"Boss_{definition.Id}");
            bossObject.transform.SetParent(transform, false);
            bossObject.transform.position = position;
            var health = bossObject.AddComponent<Health>();
            var collider = bossObject.AddComponent<CircleCollider2D>();
            collider.radius = .65f;
            collider.isTrigger = true;
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(bossObject.transform, false);
            RuntimePlaceholderVisual.Configure(visualObject.AddComponent<SpriteRenderer>(),
                new Color(1f, .25f, .2f), 1.3f, 15);
            bossObject.AddComponent<RuntimeDamageFlash>();
            health.ConfigureForRuntime(definition.HitPoints);
            var combat = bossObject.AddComponent<BossCombatController>();
            if (!combat.ConfigureForRuntime(definition, raidTarget) || !runtimeServices.Register(combat))
            {
                Destroy(bossObject);
                return null;
            }
            activeBossCombat = combat;
            forcedBossSpawnPending = definition.ForcedDay > 0 &&
                                     definition.ForcedDay == bootstrap.TimeService.Day;
            return health;
        }

        public bool TryStartPlayerSummonedBoss(BossDefinition definition, IBossSummonSite summonSite)
        {
            if (!initialized || definition == null || bossManager == null || bossManager.IsBossActive ||
                runtimeServices?.PlayerInventory == null || bootstrap?.TimeService?.IsNight != true) return false;
            var health = SpawnBoss(definition);
            if (health == null) return false;
            var service = new BossSummonService(runtimeServices.PlayerInventory, bossManager, summonSite);
            if (service.TryConsumeAndStart(definition, health, bootstrap.TimeService.GameSeconds)) return true;
            DestroyUnstartedBoss(health);
            return false;
        }

        private void DestroyUnstartedBoss(Health health)
        {
            if (activeBossCombat != null && activeBossCombat.gameObject == health?.gameObject)
            {
                runtimeServices?.Unregister(activeBossCombat);
                activeBossCombat = null;
            }
            if (health != null) Destroy(health.gameObject);
        }

#if UNITY_EDITOR
        public bool TryStartEditorBossEncounter(string preferredBossId = "king_dokkaebi")
        {
            if (!initialized || bossManager == null || bossManager.IsBossActive ||
                bootstrap?.TimeService?.IsNight != true || gameDataCatalog?.Bosses == null ||
                gameDataCatalog.Bosses.Count == 0)
            {
                Debug.LogWarning("[Nyangbingo] F8 boss test requires initialized MainGame nighttime with no active boss.");
                return false;
            }

            var definition = gameDataCatalog.FindBoss(preferredBossId);
            for (var offset = 0; offset < gameDataCatalog.Bosses.Count && definition == null; offset++)
            {
                var index = (debugBossIndex + offset) % gameDataCatalog.Bosses.Count;
                definition = gameDataCatalog.Bosses[index];
                if (definition != null) debugBossIndex = (index + 1) % gameDataCatalog.Bosses.Count;
            }
            if (definition == null) return false;

            PauseYokaiForBossEncounter();
            var health = SpawnBoss(definition);
            if (health != null && bossManager.TryStart(definition, health, bootstrap.TimeService.GameSeconds))
            {
                Debug.Log($"[Nyangbingo] F8 boss test started: {definition.Id} ({definition.DisplayName}).");
                return true;
            }

            DestroyUnstartedBoss(health);
            RestoreYokaiAfterBossEncounter(true);
            Debug.LogError($"[Nyangbingo] F8 boss test failed to start: {definition.Id}.");
            return false;
        }

#endif

        public bool TrySpawn(YokaiKind kind, int waveIndex)
        {
            var definition = gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                candidate != null && candidate.Kind == kind && candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));
            return SpawnYokai(definition, true) != null;
        }

        public bool CaptureProgress(SaveGame save)
        {
            if (save == null || !CanSerializeProgress || baekjungScheduler == null ||
                forcedBossDefinitions.Count != forcedBossBindings.Count) return false;
            save.activeBoss = new ActiveBossStateRecord();
            save.baekjungProgress = baekjungScheduler.CaptureState();
            for (var index = 0; index < forcedBossBindings.Count; index++)
                ForcedBossEncounterSaveAdapter.Capture(
                    save, forcedBossDefinitions[index], forcedBossBindings[index]);
            return true;
        }

        public bool BeginRestore()
        {
            if (!CanSerializeProgress) return false;
            restoringSnapshot = true;
            pendingRegular.Clear();
            ClearSpawnedYokai();
            return true;
        }

        public bool RestoreProgress(SaveGame save)
        {
            if (save == null || !restoringSnapshot || bossManager.IsBossActive ||
                forcedBossDefinitions.Count != forcedBossBindings.Count) return false;
            for (var index = 0; index < forcedBossBindings.Count; index++)
                if (!ForcedBossEncounterSaveAdapter.Restore(
                        save, forcedBossDefinitions[index], forcedBossBindings[index]))
                    return false;
            if (!baekjungScheduler.RestoreState(save.baekjungProgress)) return false;
            RebuildBaekjungBindings();
            return true;
        }

        public void EndRestore(bool succeeded)
        {
            if (!restoringSnapshot) return;
            restoringSnapshot = false;
            if (!succeeded || !bootstrap.TimeService.IsNight) return;
            HandleNightStart();
            for (var index = 0; index < forcedBossBindings.Count; index++)
                forcedBossBindings[index].TryStartForCurrentNight();
        }

        private void HandleWorldReady()
        {
            if (raidTarget == null || bootstrap?.TileService == null) return;
            var tileService = bootstrap.TileService;
            var centerX = Mathf.Clamp(tileService.Width / 2, 1, tileService.Width - 2);
            var centerY = Mathf.Clamp(Mathf.RoundToInt(tileService.Height * .82f), 2, tileService.Height - 2);
            raidTarget.transform.position = new Vector3(centerX + .5f, centerY + .5f, 0f);
            runtimeServices.PlayerTemperature.SetTrackedTransform(raidTarget.transform);
            if (!restoringSnapshot)
                for (var index = 0; index < forcedBossBindings.Count; index++)
                    forcedBossBindings[index].TryStartForCurrentNight();
        }

        private void HandleNightStart()
        {
            discardRegularForCurrentNight = false;
            regularSpawningEnabled = baekjungScheduler?.IsActive != true;
            pendingRegular.Clear();
            currentDayCurve = gameDataCatalog.FindDayCurve(bootstrap.TimeService.Day);
            if (currentDayCurve == null) return;

            var composition = currentDayCurve.SpawnComposition;
            for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
            {
                var group = composition[groupIndex];
                var definition = gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                    candidate != null && candidate.Kind == group.kind &&
                    candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));
                for (var count = 0; definition != null && count < Math.Max(0, group.amount); count++)
                    pendingRegular.Enqueue(definition);
            }
            TryFillRegularSlots();
        }

        private void HandleDawnWarning()
        {
            pendingRegular.Clear();
            regularSpawningEnabled = false;
        }

        private void HandleBossStarted(BossDefinition definition)
        {
            PauseYokaiForBossEncounter();
            discardRegularForCurrentNight = true;
            regularSpawningEnabled = false;
            pendingRegular.Clear();
            if (definition != null && definition.ForcedDay > 0 &&
                definition.ForcedDay == bootstrap.TimeService.Day)
                forcedBossSpawnPending = false;
        }

        private void HandleBossEnded(BossDefinition definition, bool defeated)
        {
            if (activeBossCombat != null)
            {
                runtimeServices?.Unregister(activeBossCombat);
                var bossObject = activeBossCombat.gameObject;
                activeBossCombat = null;
                if (bossObject != null) Destroy(bossObject);
            }
            RestoreYokaiAfterBossEncounter(bootstrap?.TimeService?.IsNight == true);
        }

        private void PauseYokaiForBossEncounter()
        {
            if (bossPausedYokai.Count > 0) return;
            for (var index = 0; index < spawnedYokai.Count; index++)
            {
                var entry = spawnedYokai[index];
                if (!IsAlive(entry) || !entry.health.gameObject.activeSelf) continue;
                runtimeServices?.Unregister(entry.brain);
                entry.health.gameObject.SetActive(false);
                bossPausedYokai.Add(entry);
            }
            if (bossPausedYokai.Count > 0)
                Debug.Log($"[Nyangbingo] Boss encounter paused {bossPausedYokai.Count} yokai.");
        }

        private void RestoreYokaiAfterBossEncounter(bool resume)
        {
            for (var index = bossPausedYokai.Count - 1; index >= 0; index--)
            {
                var entry = bossPausedYokai[index];
                if (entry?.health == null || entry.health.IsDead) continue;
                if (resume)
                {
                    entry.health.gameObject.SetActive(true);
                    runtimeServices?.Register(entry.brain);
                }
                else
                {
                    spawnedYokai.Remove(entry);
                    Destroy(entry.health.gameObject);
                }
            }
            bossPausedYokai.Clear();
        }

        private void TryFillRegularSlots()
        {
            if (!IsRegularSpawningEnabled || currentDayCurve == null) return;
            var reservesForcedBossSlot = gameDataCatalog.Bosses.Any(definition =>
                definition != null && definition.ForcedDay > 0 &&
                definition.ForcedDay == bootstrap.TimeService.Day);
            var cap = Math.Max(0, currentDayCurve.MaxActive - (reservesForcedBossSlot ? 1 : 0));
            while (pendingRegular.Count > 0 && ActiveRegularCount + ActiveRaidCount < cap)
            {
                var definition = pendingRegular.Peek();
                if (SpawnYokai(definition, false) == null) return;
                pendingRegular.Dequeue();
            }
        }

        private YokaiBrain SpawnYokai(YokaiDefinition definition, bool raid)
        {
            if (definition == null || raidTarget == null || !TryGetSpawnPosition(out var position)) return null;
            var yokaiObject = new GameObject($"Yokai_{definition.Id}_{spawnSequence++}");
            yokaiObject.transform.SetParent(transform, false);
            yokaiObject.transform.position = position;
            var health = yokaiObject.AddComponent<Health>();
            var collider = yokaiObject.AddComponent<CircleCollider2D>();
            collider.radius = .42f;
            collider.isTrigger = true;
            RuntimePlaceholderVisual.Configure(yokaiObject.AddComponent<SpriteRenderer>(),
                raid ? new Color(1f, .45f, .8f) : new Color(.8f, .35f, 1f), .8f, 10);
            yokaiObject.AddComponent<RuntimeDamageFlash>();
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            var loot = yokaiObject.AddComponent<YokaiLoot>();
            loot.ConfigureForRuntime(definition);
            brain.ConfigureForRuntime(definition, raidTarget, instanceSpawnTrack: YokaiSpawnTrack.Raid);
            health.Died += () => HandleYokaiEnded(health);
            brain.FledOffscreen += ignored => HandleYokaiEnded(health);
            spawnedYokai.Add(new SpawnedYokai { health = health, brain = brain, raid = raid });
            runtimeServices.Register(brain);
            return brain;
        }

        private void HandleYokaiEnded(Health health)
        {
            var entry = spawnedYokai.FirstOrDefault(candidate => candidate.health == health);
            if (entry == null) return;
            spawnedYokai.Remove(entry);
            runtimeServices.Unregister(entry.brain);
            if (entry.raid) RaidSlotAvailable?.Invoke();
            else TryFillRegularSlots();
            if (health != null) Destroy(health.gameObject);
        }

        private bool TryGetSpawnPosition(out Vector3 position)
        {
            position = default;
            if (bootstrap?.TileService == null || raidTarget == null) return false;
            var center = Vector3Int.FloorToInt(raidTarget.transform.position);
            var candidates = bootstrap.TileService.GetValidSpawnPositions(
                center, minimumSpawnRange, Mathf.Max(minimumSpawnRange, maximumSpawnRange));
            if (candidates.Count == 0) return false;
            var cell = candidates[spawnSequence % candidates.Count];
            position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
            return true;
        }

        private void BuildForcedBossBindings()
        {
            for (var index = 0; index < gameDataCatalog.Bosses.Count; index++)
            {
                var definition = gameDataCatalog.Bosses[index];
                if (definition != null && definition.ForcedDay > 0)
                {
                    forcedBossDefinitions.Add(definition);
                    forcedBossBindings.Add(new ForcedBossEncounterBinding(
                        definition, bootstrap.TimeService, bossManager, this));
                }
            }
        }

        private void RebuildBaekjungBindings()
        {
            baekjungRegularSpawnGate?.Dispose();
            baekjungWaveSpawner?.Dispose();
            if (baekjungTimeBinding != null)
            {
                baekjungTimeBinding.Dispose();
                runtimeServices.Unregister(baekjungTimeBinding);
            }
            baekjungWaveSpawner = new BaekjungWaveSpawner(baekjungScheduler, this);
            baekjungRegularSpawnGate = new BaekjungRegularSpawnGate(baekjungScheduler, this);
            baekjungTimeBinding = new BaekjungTimeBinding(bootstrap.TimeService, baekjungScheduler);
            runtimeServices.Register(baekjungTimeBinding);
        }

        private void ClearSpawnedYokai()
        {
            for (var index = spawnedYokai.Count - 1; index >= 0; index--)
            {
                var entry = spawnedYokai[index];
                runtimeServices?.Unregister(entry.brain);
                if (entry.health != null) Destroy(entry.health.gameObject);
            }
            spawnedYokai.Clear();
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            GameEvents.OnNightStart -= HandleNightStart;
            GameEvents.OnDawnWarning -= HandleDawnWarning;
            if (bootstrap != null) bootstrap.WorldReady -= HandleWorldReady;
            if (bossManager != null) bossManager.BossStarted -= HandleBossStarted;
            if (bossManager != null) bossManager.BossEnded -= HandleBossEnded;
            if (activeBossCombat != null)
            {
                runtimeServices?.Unregister(activeBossCombat);
                activeBossCombat = null;
            }
            for (var index = 0; index < forcedBossBindings.Count; index++) forcedBossBindings[index].Dispose();
            forcedBossBindings.Clear();
            forcedBossDefinitions.Clear();
            baekjungRegularSpawnGate?.Dispose();
            baekjungWaveSpawner?.Dispose();
            baekjungTimeBinding?.Dispose();
            runtimeServices?.Unregister(baekjungTimeBinding);
            ClearSpawnedYokai();
            bossPausedYokai.Clear();
            initialized = false;
        }

        private static bool IsAlive(SpawnedYokai entry) =>
            entry != null && entry.health != null && !entry.health.IsDead && entry.brain != null;
    }
}
