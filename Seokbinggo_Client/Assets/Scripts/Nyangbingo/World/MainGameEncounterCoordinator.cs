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
        [SerializeField] private CharacterArtCatalog characterArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
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
        private bool activeBossIsForcedInvasion;
        private bool initialized;
        private bool restoringSnapshot;
        private RegularEncounterStateRecord restoredRegularEncounter;
        private int spawnSequence;
        private int debugBossIndex;
        private BossCombatController activeBossCombat;
        private MainGameTurretRuntime placedObjectRuntime;
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
        public static bool ShouldPauseFieldYokaiForBoss(bool forcedInvasion) => !forcedInvasion;
        public event Action RaidSlotAvailable;

        public bool HasActiveYokaiWithin(Vector2 position, float radius)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f) return false;
            var radiusSquared = radius * radius;
            return spawnedYokai.Any(entry => IsAlive(entry) && entry.health.gameObject.activeInHierarchy &&
                ((Vector2)entry.health.transform.position - position).sqrMagnitude <= radiusSquared);
        }

        public void CopyActiveThreatTransforms(List<Transform> results)
        {
            if (results == null) return;
            results.Clear();
            foreach (var entry in spawnedYokai)
                if (IsAlive(entry) && entry.health.gameObject.activeInHierarchy)
                    results.Add(entry.health.transform);
            var bossHealth = bossManager != null ? bossManager.ActiveHealth : null;
            if (bossHealth != null && !bossHealth.IsDead && bossHealth.gameObject.activeInHierarchy)
                results.Add(bossHealth.transform);
        }

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, BossManager manager, MainGameRaidTarget target,
            CharacterArtCatalog artCatalog = null, GameplayArtCatalog gameplayArt = null)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            bossManager = manager;
            raidTarget = target;
            characterArtCatalog = artCatalog;
            gameplayArtCatalog = gameplayArt;
        }

        private void Start() => Initialize();

#if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                var bossId = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                    ? "mother_bulgasari"
                    : Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                        ? "imugi"
                        : Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                            ? "gangcheol_boss"
                            : "king_dokkaebi";
                TryStartEditorBossEncounter(bossId);
            }
            if (Input.GetKeyDown(KeyCode.J)) DefeatAllYokaiForEditorTest();
            if (Input.GetKeyDown(KeyCode.K)) DefeatActiveBossForEditorTest();
            if (Input.GetKeyDown(KeyCode.F12))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    GrantEoduksiniVisualTestKit();
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    GrantRoofVisualTestKit();
                else
                    SpawnEoduksiniForEditorTest();
            }
        }

        private void SpawnEoduksiniForEditorTest()
        {
            var spawned = TrySpawn(YokaiKind.Eoduksini, 0);
            Debug.Log(spawned
                ? "[Nyangbingo] F12 Eoduksini visual test spawn completed."
                : "[Nyangbingo] F12 Eoduksini visual test spawn failed: MainGame initialization or spawn terrain required.");
        }

        private void GrantEoduksiniVisualTestKit()
        {
            var inventory = runtimeServices?.PlayerInventory;
            var lanternGranted = inventory != null && inventory.TryAdd("lantern", 1);
            var coalGranted = inventory != null && inventory.TryAdd("coal", 3);
            Debug.Log(lanternGranted && coalGranted
                ? "[Nyangbingo] Shift+F12 Eoduksini visual test kit granted: lantern x1, coal x3."
                : "[Nyangbingo] Shift+F12 test kit grant was incomplete: check inventory capacity.");
        }

        private void GrantRoofVisualTestKit()
        {
            var inventory = runtimeServices?.PlayerInventory;
            var granted = inventory != null && inventory.TryAdd("roof", 8);
            Debug.Log(granted
                ? "[Nyangbingo] Ctrl+F12 roof visual test kit granted: roof x8."
                : "[Nyangbingo] Ctrl+F12 roof test kit grant failed: check inventory capacity.");
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
            placedObjectRuntime ??= GetComponent<MainGameTurretRuntime>();
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

        public Health SpawnBoss(BossDefinition definition) => CreateBoss(definition, true);

        private Health CreateBoss(BossDefinition definition, bool forcedInvasion)
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
            var bossRenderer = visualObject.AddComponent<SpriteRenderer>();
            var bossArt = characterArtCatalog != null ? characterArtCatalog.Find(definition.Id) : null;
            if (bossArt?.Sprite == null)
                RuntimePlaceholderVisual.Configure(bossRenderer, new Color(1f, .25f, .2f), 1.3f, 15);
            bossObject.AddComponent<RuntimeDamageFlash>();
            bossObject.AddComponent<RuntimeWorldDamagePopup>();
            health.ConfigureForRuntime(definition.HitPoints);
            var combat = bossObject.AddComponent<BossCombatController>();
            if (!combat.ConfigureForRuntime(definition, raidTarget) || !runtimeServices.Register(combat))
            {
                Destroy(bossObject);
                return null;
            }
            combat.ConfigureWarningArt(gameplayArtCatalog);
            if (bossArt?.Sprite != null)
            {
                var characterAnimator = visualObject.AddComponent<RuntimeCharacterSpriteAnimator>();
                characterAnimator.Configure(bossArt, 15);
                characterAnimator.Bind(combat);
            }
            if (string.Equals(definition.Id, "imugi", StringComparison.Ordinal))
            {
                var bodySprite = characterArtCatalog?.FindSprite("imugi_body");
                if (bodySprite != null)
                    bossObject.AddComponent<RuntimeImugiBodyVisual>().Configure(bodySprite, 14);
            }
            activeBossCombat = combat;
            activeBossIsForcedInvasion = forcedInvasion;
            forcedBossSpawnPending = forcedInvasion;
            return health;
        }

        public bool TryStartPlayerSummonedBoss(BossDefinition definition, IBossSummonSite summonSite)
        {
            if (!initialized || definition == null || bossManager == null || bossManager.IsBossActive ||
                runtimeServices?.PlayerInventory == null || bootstrap?.TimeService?.IsNight != true) return false;
            var health = CreateBoss(definition, false);
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
            activeBossIsForcedInvasion = false;
            forcedBossSpawnPending = false;
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

            var health = CreateBoss(definition, false);
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
            save.regularEncounter = CaptureRegularEncounterState();
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
            restoredRegularEncounter = null;
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
            if (!TryStageRegularEncounterRestore(save.regularEncounter)) return false;
            RebuildBaekjungBindings();
            return true;
        }

        public void EndRestore(bool succeeded)
        {
            if (!restoringSnapshot) return;
            restoringSnapshot = false;
            var regularEncounter = restoredRegularEncounter;
            restoredRegularEncounter = null;
            if (!succeeded || !bootstrap.TimeService.IsNight) return;
            if (regularEncounter != null && regularEncounter.hasValue)
                RestoreRegularEncounter(regularEncounter);
            else
                HandleNightStart();
            for (var index = 0; index < forcedBossBindings.Count; index++)
                forcedBossBindings[index].TryStartForCurrentNight();
        }

        private RegularEncounterStateRecord CaptureRegularEncounterState()
        {
            var state = new RegularEncounterStateRecord
            {
                hasValue = true,
                day = bootstrap.TimeService.Day,
                isNight = bootstrap.TimeService.IsNight,
                discardRegularForCurrentNight = discardRegularForCurrentNight
            };
            if (!state.isNight) return state;

            for (var index = 0; index < spawnedYokai.Count; index++)
            {
                var entry = spawnedYokai[index];
                var definition = entry?.brain?.Definition;
                if (!entry.raid && IsAlive(entry) && definition != null)
                    state.remainingRegularYokaiIds.Add(definition.Id);
            }
            foreach (var definition in pendingRegular)
                if (definition != null) state.remainingRegularYokaiIds.Add(definition.Id);
            return state;
        }

        private bool TryStageRegularEncounterRestore(RegularEncounterStateRecord state)
        {
            if (state == null || !state.hasValue)
            {
                restoredRegularEncounter = null;
                return true;
            }
            if (state.day != bootstrap.TimeService.Day || state.isNight != bootstrap.TimeService.IsNight)
                return false;
            for (var index = 0; index < state.remainingRegularYokaiIds.Count; index++)
            {
                var definition = gameDataCatalog.FindYokai(state.remainingRegularYokaiIds[index]);
                if (definition == null || !definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid)) return false;
            }
            restoredRegularEncounter = state;
            return true;
        }

        private void RestoreRegularEncounter(RegularEncounterStateRecord state)
        {
            pendingRegular.Clear();
            currentDayCurve = gameDataCatalog.FindDayCurve(bootstrap.TimeService.Day);
            discardRegularForCurrentNight = state.discardRegularForCurrentNight;
            regularSpawningEnabled = !discardRegularForCurrentNight && baekjungScheduler?.IsActive != true;
            if (regularSpawningEnabled)
            {
                for (var index = 0; index < state.remainingRegularYokaiIds.Count; index++)
                    pendingRegular.Enqueue(gameDataCatalog.FindYokai(state.remainingRegularYokaiIds[index]));
                TryFillRegularSlots();
            }
            Debug.Log($"[Nyangbingo] MainGameEncounterCoordinator: saved regular encounter restored " +
                      $"(discard={discardRegularForCurrentNight}, remaining={state.remainingRegularYokaiIds.Count}).");
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
            if (!ShouldPauseFieldYokaiForBoss(activeBossIsForcedInvasion))
            {
                forcedBossSpawnPending = false;
                return;
            }
            PauseYokaiForBossEncounter();
            discardRegularForCurrentNight = true;
            regularSpawningEnabled = false;
            pendingRegular.Clear();
        }

        private void HandleBossEnded(BossDefinition definition, bool defeated)
        {
            var wasForcedInvasion = activeBossIsForcedInvasion;
            activeBossIsForcedInvasion = false;
            if (activeBossCombat != null)
            {
                runtimeServices?.Unregister(activeBossCombat);
                var bossObject = activeBossCombat.gameObject;
                activeBossCombat = null;
                if (bossObject != null) Destroy(bossObject);
            }
            if (!wasForcedInvasion)
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
                entry.brain.SetBossEncounterPaused(true);
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
                    entry.brain.SetBossEncounterPaused(false);
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
            var usesEoduksiniPresentation = definition.Kind == YokaiKind.Eoduksini;
            var visualObject = usesEoduksiniPresentation ? new GameObject("Visual") : yokaiObject;
            if (usesEoduksiniPresentation) visualObject.transform.SetParent(yokaiObject.transform, false);
            var yokaiRenderer = visualObject.AddComponent<SpriteRenderer>();
            var yokaiArt = characterArtCatalog != null
                ? characterArtCatalog.Find(definition.Id)
                : null;
            if (yokaiArt?.Sprite == null)
                RuntimePlaceholderVisual.Configure(yokaiRenderer,
                    raid ? new Color(1f, .45f, .8f) : new Color(.8f, .35f, 1f), .8f, 10);
            yokaiObject.AddComponent<RuntimeDamageFlash>();
            yokaiObject.AddComponent<RuntimeWorldDamagePopup>();
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            if (yokaiArt?.Sprite != null)
            {
                var characterAnimator = visualObject.AddComponent<RuntimeCharacterSpriteAnimator>();
                characterAnimator.Configure(yokaiArt, 10);
                characterAnimator.Bind(brain);
            }
            var loot = yokaiObject.AddComponent<YokaiLoot>();
            loot.ConfigureForRuntime(definition);
            var counters = placedObjectRuntime != null
                ? new CounterAuraSensor(yokaiObject.transform, placedObjectRuntime.ActiveCounterAuras)
                : null;
            brain.ConfigureForRuntime(definition, raidTarget, counters, YokaiSpawnTrack.Raid);
            if (usesEoduksiniPresentation)
            {
                var presentation = visualObject.AddComponent<RuntimeEoduksiniVisual>();
                presentation.ConfigureForRuntime(brain, yokaiRenderer);
            }
            var healthBar = yokaiObject.AddComponent<RuntimeWorldHealthBar>();
            healthBar.ConfigureForRuntime(health, yokaiRenderer, usesEoduksiniPresentation ? 2f : 1f);
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

    /// <summary>Runtime-spawned yokai health presentation kept separate from combat state.</summary>
    public sealed class RuntimeWorldHealthBar : MonoBehaviour
    {
        private const float BarWidth = 1f;
        private const float BarHeight = .11f;

        private Health health;
        private Transform barRoot;
        private SpriteRenderer fillRenderer;
        private TextMesh valueText;
        private TextMesh valueShadow;

        public float FillRatio => health == null || health.MaxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)health.Current / health.MaxHealth);

        public void ConfigureForRuntime(Health targetHealth, SpriteRenderer characterRenderer,
            float maximumVisualScale = 1f)
        {
            Unbind();
            health = targetHealth;
            if (health == null) return;

            var offset = characterRenderer != null && characterRenderer.sprite != null
                ? Mathf.Max(.72f, characterRenderer.sprite.bounds.max.y * Mathf.Max(1f, maximumVisualScale) + .24f)
                : .72f;
            BuildPresentation(offset);
            health.Damaged += HandleDamaged;
            Refresh();
        }

        private void BuildPresentation(float verticalOffset)
        {
            var rootObject = new GameObject("HealthBar");
            barRoot = rootObject.transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, verticalOffset, 0f);

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(barRoot, false);
            var background = backgroundObject.AddComponent<SpriteRenderer>();
            RuntimePlaceholderVisual.Configure(background, new Color(.05f, .04f, .04f, .92f), 1f, 30);
            background.transform.localScale = new Vector3(BarWidth + .08f, BarHeight + .06f, 1f);

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(barRoot, false);
            fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            RuntimePlaceholderVisual.Configure(fillRenderer, new Color(.85f, .18f, .12f, 1f), 1f, 31);

            var textObject = new GameObject("Value");
            textObject.transform.SetParent(barRoot, false);
            textObject.transform.localPosition = new Vector3(0f, .2f, 0f);
            valueText = textObject.AddComponent<TextMesh>();
            valueText.anchor = TextAnchor.LowerCenter;
            valueText.alignment = TextAlignment.Center;
            valueText.fontSize = MainGameEffectPresenter.WorldPopupFontSize;
            valueText.characterSize = MainGameEffectPresenter.WorldPopupCharacterSize;
            valueText.fontStyle = FontStyle.Bold;
            valueText.color = Color.white;
            var textRenderer = valueText.GetComponent<MeshRenderer>();
            if (textRenderer != null) textRenderer.sortingOrder = 33;

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(textObject.transform, false);
            shadowObject.transform.localPosition = new Vector3(.018f, -.018f, .01f);
            valueShadow = shadowObject.AddComponent<TextMesh>();
            valueShadow.anchor = valueText.anchor;
            valueShadow.alignment = valueText.alignment;
            valueShadow.fontSize = valueText.fontSize;
            valueShadow.characterSize = valueText.characterSize;
            valueShadow.fontStyle = valueText.fontStyle;
            valueShadow.color = new Color(0f, 0f, 0f, .9f);
            var shadowRenderer = valueShadow.GetComponent<MeshRenderer>();
            if (shadowRenderer != null) shadowRenderer.sortingOrder = 32;
        }

        private void HandleDamaged(DamageTag _, int __) => Refresh();

        private void Refresh()
        {
            if (health == null) return;
            var ratio = FillRatio;
            if (fillRenderer != null)
            {
                fillRenderer.transform.localScale = new Vector3(BarWidth * ratio, BarHeight, 1f);
                fillRenderer.transform.localPosition = new Vector3(-BarWidth * (1f - ratio) * .5f, 0f, 0f);
                fillRenderer.color = ratio > .5f
                    ? new Color(.2f, .8f, .3f, 1f)
                    : ratio > .25f
                        ? new Color(1f, .65f, .12f, 1f)
                        : new Color(.9f, .16f, .12f, 1f);
            }
            var value = health.Current.ToString();
            if (valueText != null) valueText.text = value;
            if (valueShadow != null) valueShadow.text = value;
        }

        private void Unbind()
        {
            if (health != null) health.Damaged -= HandleDamaged;
            if (barRoot != null) Destroy(barRoot.gameObject);
            barRoot = null;
            fillRenderer = null;
            valueText = null;
            valueShadow = null;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= HandleDamaged;
        }
    }
}
