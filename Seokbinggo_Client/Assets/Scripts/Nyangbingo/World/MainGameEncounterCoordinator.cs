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
        IRegularSpawnController, IForcedBossSpawnController, IBaekjungSpawnController, IGameSecondsTickable
    {
        private const float BossScale = 2f;
        private const float GroundBossVisualLift = .5f;
        private const string NightWavesCsvPath = "Assets/Data/CSV/night-waves.csv";

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private BossManager bossManager;
        [SerializeField] private MainGameRaidTarget raidTarget;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private TextAsset nightWavesCsv;
        [Min(1)][SerializeField] private int minimumSpawnRange = 12;
        [Min(1)][SerializeField] private int maximumSpawnRange = 24;

        private readonly Queue<YokaiDefinition> pendingRegular = new Queue<YokaiDefinition>();
        private readonly Queue<float> pendingRegularHpMult = new Queue<float>();
        private readonly List<SpawnedYokai> spawnedYokai = new List<SpawnedYokai>();
        private readonly List<ForcedBossEncounterBinding> forcedBossBindings = new List<ForcedBossEncounterBinding>();
        private readonly List<BossDefinition> forcedBossDefinitions = new List<BossDefinition>();
        private BaekjungScheduler baekjungScheduler;
        private BaekjungTimeBinding baekjungTimeBinding;
        private BaekjungWaveSpawner baekjungWaveSpawner;
        private BaekjungRegularSpawnGate baekjungRegularSpawnGate;
        private BaekjungRewardRules baekjungRewardRules;
        private DayCurveDefinition currentDayCurve;
        private WaveNightController waveNight;
        private bool regularSpawningEnabled = true;
        private bool discardRegularForCurrentNight;
        private bool forcedBossSpawnPending;
        private bool activeBossIsForcedInvasion;
        private bool initialized;
        private bool restoringSnapshot;
        private bool restoredDetailedEncounterDuringTransaction;
        private RegularEncounterStateRecord restoredRegularEncounter;
        private int spawnSequence;
        private int debugBossIndex;
        private BossCombatController activeBossCombat;
        private MainGameTurretRuntime placedObjectRuntime;
        private readonly List<SpawnedYokai> bossPausedYokai = new List<SpawnedYokai>();
        private float pendingSpawnHpMult = 1f;

        private sealed class SpawnedYokai
        {
            public string instanceId;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            ClearPendingRegular();
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
            waveNight = CreateWaveNightController();
            baekjungScheduler = new BaekjungScheduler(gameDataCatalog.DayEvents);
            baekjungScheduler.Started += HandleBaekjungStarted;
            baekjungWaveSpawner = new BaekjungWaveSpawner(baekjungScheduler, this);
            baekjungRegularSpawnGate = new BaekjungRegularSpawnGate(baekjungScheduler, this);
            baekjungTimeBinding = new BaekjungTimeBinding(bootstrap.TimeService, baekjungScheduler);
            runtimeServices.Register(baekjungTimeBinding);
            runtimeServices.Register(this);

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
                ClearPendingRegular();
            }
            regularSpawningEnabled = enabled && bootstrap?.TimeService?.IsNight == true;
            if (IsRegularSpawningEnabled) TryFillRegularSlots();
        }

        public void Tick(float deltaGameSeconds)
        {
            if (!initialized || waveNight == null || !waveNight.IsActive) return;
            if (bootstrap?.TimeService?.IsNight != true || !IsRegularSpawningEnabled) return;
            TryAdvanceWaveNight();
        }

        public Health SpawnBoss(BossDefinition definition) => CreateBoss(definition, true);

        private Health CreateBoss(BossDefinition definition, bool forcedInvasion)
        {
            if (definition == null || !TryGetSpawnPosition(out var position)) return null;
            var bossObject = new GameObject($"Boss_{definition.Id}");
            bossObject.transform.SetParent(transform, false);
            bossObject.transform.localScale = Vector3.one * BossScale;
            bossObject.transform.position = position;
            var health = bossObject.AddComponent<Health>();
            var collider = bossObject.AddComponent<CircleCollider2D>();
            var locomotion = WorldMobPhysicsBody.ForBoss(definition.Kind);
            var movementColliderScale =
                locomotion == WorldMobLocomotion.Flying ? BossScale : 1f;
            collider.radius =
                WorldMobPhysicsBody.PhysicalRadiusForBoss(definition.Kind) /
                movementColliderScale;
            collider.offset =
                Vector2.up * (WorldMobPhysicsBody.ColliderVerticalOffsetForBoss(definition.Kind) /
                              movementColliderScale);
            bossObject.AddComponent<Rigidbody2D>();
            var physicsBody = bossObject.AddComponent<WorldMobPhysicsBody>();
            physicsBody.ConfigureForRuntime(locomotion, bootstrap.TileService);
            physicsBody.IgnoreCollisionWith(raidTarget.transform);
            if (locomotion == WorldMobLocomotion.Flying)
            {
                var hurtboxObject = new GameObject("BossHurtbox");
                hurtboxObject.transform.SetParent(bossObject.transform, false);
                var hurtbox = hurtboxObject.AddComponent<CircleCollider2D>();
                hurtbox.radius = .65f;
                hurtbox.isTrigger = true;
                ConfigureDetachedHurtboxBody(hurtboxObject.AddComponent<Rigidbody2D>());
            }
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(bossObject.transform, false);
            if (locomotion == WorldMobLocomotion.Grounded)
                visualObject.transform.localPosition =
                    Vector3.up * (GroundBossVisualLift / BossScale);
            var bossRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(bossRenderer);
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
                if (definition.Kind == BossKind.Imugi)
                    characterAnimator.SetFacing(Vector2.right);
                combat.BindCharacterAnimator(characterAnimator);
                characterAnimator.Bind(combat);
            }
            if (string.Equals(definition.Id, "imugi", StringComparison.Ordinal))
            {
                var bodySprite = characterArtCatalog?.FindSprite("imugi_body");
                if (bodySprite != null)
                    bossObject.AddComponent<RuntimeImugiBodyVisual>().Configure(bodySprite, 14);
            }
            else if (definition.Kind == BossKind.Gangcheori)
            {
                var bodySprite = characterArtCatalog?.FindSprite("gangcheol_body");
                if (bodySprite != null)
                    bossObject.AddComponent<RuntimeGangcheoriBodyVisual>()
                        .Configure(bodySprite, bossRenderer, 14);
            }
            activeBossCombat = combat;
            activeBossIsForcedInvasion = forcedInvasion;
            forcedBossSpawnPending = forcedInvasion;
            return health;
        }

        private static void ConfigureDetachedHurtboxBody(Rigidbody2D body)
        {
            if (body == null) return;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.None;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            var encounterState = CaptureRegularEncounterState();
            if (encounterState == null) return false;
            save.regularEncounter = encounterState;
            save.baekjungProgress = baekjungScheduler.CaptureState();
            save.baekjungTearRemainder = baekjungRewardRules?.TearRemainder ?? 0f;
            for (var index = 0; index < forcedBossBindings.Count; index++)
                ForcedBossEncounterSaveAdapter.Capture(
                    save, forcedBossDefinitions[index], forcedBossBindings[index]);
            return true;
        }

        public bool BeginRestore()
        {
            if (!CanSerializeProgress) return false;
            restoringSnapshot = true;
            restoredDetailedEncounterDuringTransaction = false;
            restoredRegularEncounter = null;
            ClearPendingRegular();
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
            if (float.IsNaN(save.baekjungTearRemainder) || float.IsInfinity(save.baekjungTearRemainder) ||
                save.baekjungTearRemainder < 0f || save.baekjungTearRemainder >= 1f)
                return false;
            if (!TryStageRegularEncounterRestore(save.regularEncounter)) return false;
            RebuildBaekjungBindings();
            RestoreBaekjungRewardRules(save.baekjungTearRemainder);
            if (bootstrap.TimeService.IsNight && restoredRegularEncounter?.usesDetailedYokaiState == true)
            {
                if (!RestoreRegularEncounter(restoredRegularEncounter)) return false;
                restoredRegularEncounter = null;
                restoredDetailedEncounterDuringTransaction = true;
            }
            return true;
        }

        public void EndRestore(bool succeeded)
        {
            if (!restoringSnapshot) return;
            restoringSnapshot = false;
            var detailedRestored = restoredDetailedEncounterDuringTransaction;
            restoredDetailedEncounterDuringTransaction = false;
            var regularEncounter = restoredRegularEncounter;
            restoredRegularEncounter = null;
            if (!succeeded)
            {
                if (detailedRestored) ClearSpawnedYokai();
                return;
            }
            if (!bootstrap.TimeService.IsNight || detailedRestored) return;
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
                discardRegularForCurrentNight = discardRegularForCurrentNight,
                usesDetailedYokaiState = true
            };
            if (!state.isNight) return state;

            for (var index = 0; index < spawnedYokai.Count; index++)
            {
                var entry = spawnedYokai[index];
                var definition = entry?.brain?.Definition;
                if (!IsAlive(entry) || definition == null) continue;
                var record = new YokaiStateRecord
                {
                    instanceId = entry.instanceId,
                    yokaiId = definition.Id,
                    position = entry.health.transform.position,
                    velocity = entry.health.GetComponent<Rigidbody2D>()?.linearVelocity ?? Vector2.zero,
                    currentHealth = entry.health.Current,
                    maxHealth = entry.health.MaxHealth,
                    raid = entry.raid
                };
                entry.brain.CaptureSaveState(record);
                state.activeYokai.Add(record);
                if (!entry.raid) state.remainingRegularYokaiIds.Add(definition.Id);
            }
            foreach (var definition in pendingRegular)
                if (definition != null)
                {
                    state.remainingRegularYokaiIds.Add(definition.Id);
                    state.pendingRegularYokaiIds.Add(definition.Id);
                }
            if (baekjungWaveSpawner != null)
                foreach (var kind in baekjungWaveSpawner.CapturePendingKinds())
                {
                    var definition = FindRaidYokai(kind);
                    if (definition == null) return null;
                    state.pendingRaidYokaiIds.Add(definition.Id);
                }
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
            if (state.usesDetailedYokaiState)
            {
                if (!state.isNight && (state.activeYokai.Count > 0 ||
                    state.pendingRegularYokaiIds.Count > 0 || state.pendingRaidYokaiIds.Count > 0))
                    return false;
                var instanceIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < state.activeYokai.Count; index++)
                {
                    var record = state.activeYokai[index];
                    var definition = gameDataCatalog.FindYokai(record.yokaiId);
                    if (definition == null || !definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid) ||
                        string.IsNullOrWhiteSpace(record.instanceId) || !instanceIds.Add(record.instanceId) ||
                        !IsFinite(record.position) || !IsFinite(record.velocity) ||
                        record.maxHealth <= 0 || record.currentHealth <= 0 ||
                        record.currentHealth > record.maxHealth)
                        return false;
                    if (!ValidateYokaiBrainState(record)) return false;
                }
                for (var index = 0; index < state.pendingRegularYokaiIds.Count; index++)
                {
                    var definition = gameDataCatalog.FindYokai(state.pendingRegularYokaiIds[index]);
                    if (definition == null || !definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid))
                        return false;
                }
                if (state.pendingRaidYokaiIds.Count > 0 && baekjungScheduler?.IsActive != true)
                    return false;
                for (var index = 0; index < state.pendingRaidYokaiIds.Count; index++)
                {
                    var definition = gameDataCatalog.FindYokai(state.pendingRaidYokaiIds[index]);
                    if (definition == null || !definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid))
                        return false;
                }
                restoredRegularEncounter = state;
                return true;
            }
            for (var index = 0; index < state.remainingRegularYokaiIds.Count; index++)
            {
                var definition = gameDataCatalog.FindYokai(state.remainingRegularYokaiIds[index]);
                if (definition == null || !definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid)) return false;
            }
            restoredRegularEncounter = state;
            return true;
        }

        private bool RestoreRegularEncounter(RegularEncounterStateRecord state)
        {
            ClearPendingRegular();
            currentDayCurve = gameDataCatalog.FindDayCurve(bootstrap.TimeService.Day);
            discardRegularForCurrentNight = state.discardRegularForCurrentNight;
            regularSpawningEnabled = !discardRegularForCurrentNight && baekjungScheduler?.IsActive != true;
            var day = bootstrap.TimeService.Day;
            if (waveNight != null && WaveNightRules.UsesNightWaves(day))
                waveNight.BeginNight(day);
            else
                waveNight?.EndNight();
            if (state.usesDetailedYokaiState)
            {
                for (var index = 0; index < state.activeYokai.Count; index++)
                    if (SpawnSavedYokai(state.activeYokai[index]) == null)
                    {
                        Debug.LogError("[Nyangbingo] MainGameEncounterCoordinator: detailed yokai restore failed.");
                        ClearSpawnedYokai();
                        return false;
                    }
                if (regularSpawningEnabled)
                    for (var index = 0; index < state.pendingRegularYokaiIds.Count; index++)
                        EnqueuePendingRegular(
                            gameDataCatalog.FindYokai(state.pendingRegularYokaiIds[index]), 1f);

                var pendingRaid = new List<YokaiDefinition>(state.pendingRaidYokaiIds.Count);
                for (var index = 0; index < state.pendingRaidYokaiIds.Count; index++)
                    pendingRaid.Add(gameDataCatalog.FindYokai(state.pendingRaidYokaiIds[index]));
                if (baekjungWaveSpawner == null ||
                    !baekjungWaveSpawner.RestorePendingDefinitions(pendingRaid))
                {
                    Debug.LogError("[Nyangbingo] MainGameEncounterCoordinator: Baekjung pending restore failed.");
                    ClearSpawnedYokai();
                    return false;
                }
                TryFillRegularSlots();
                Debug.Log($"[Nyangbingo] MainGameEncounterCoordinator: detailed encounter restored " +
                          $"(active={state.activeYokai.Count}, pendingRegular={state.pendingRegularYokaiIds.Count}, " +
                          $"pendingRaid={state.pendingRaidYokaiIds.Count}).");
                return true;
            }
            if (regularSpawningEnabled)
            {
                for (var index = 0; index < state.remainingRegularYokaiIds.Count; index++)
                    EnqueuePendingRegular(
                        gameDataCatalog.FindYokai(state.remainingRegularYokaiIds[index]), 1f);
                TryFillRegularSlots();
            }
            Debug.Log($"[Nyangbingo] MainGameEncounterCoordinator: saved regular encounter restored " +
                      $"(discard={discardRegularForCurrentNight}, remaining={state.remainingRegularYokaiIds.Count}).");
            return true;
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
            ClearPendingRegular();
            var day = bootstrap != null && bootstrap.TimeService != null ? bootstrap.TimeService.Day : 0;
            currentDayCurve = gameDataCatalog.FindDayCurve(day);

            if (waveNight != null && waveNight.BeginNight(day))
            {
                TryAdvanceWaveNight();
                return;
            }

            if (currentDayCurve == null) return;

            var composition = currentDayCurve.SpawnComposition;
            for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
            {
                var group = composition[groupIndex];
                var definition = gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                    candidate != null && candidate.Kind == group.kind &&
                    candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));
                for (var count = 0; definition != null && count < Math.Max(0, group.amount); count++)
                    EnqueuePendingRegular(definition, 1f);
            }
            TryFillRegularSlots();
        }

        private void HandleDawnWarning()
        {
            ClearPendingRegular();
            regularSpawningEnabled = false;
            waveNight?.EndNight();
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
            ClearPendingRegular();
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
            if (!IsRegularSpawningEnabled) return;
            var usingWaveNight = waveNight != null && waveNight.IsActive;
            if (!usingWaveNight && currentDayCurve == null) return;
            var reservesForcedBossSlot = gameDataCatalog.Bosses.Any(definition =>
                definition != null && definition.ForcedDay > 0 &&
                definition.ForcedDay == bootstrap.TimeService.Day);
            var baseCap = usingWaveNight
                ? waveNight.YokaiCap
                : currentDayCurve.MaxActive;
            var cap = Math.Max(0, baseCap - (reservesForcedBossSlot ? 1 : 0));
            while (pendingRegular.Count > 0 && ActiveRegularCount + ActiveRaidCount < cap)
            {
                var definition = pendingRegular.Peek();
                pendingSpawnHpMult = pendingRegularHpMult.Count > 0 ? pendingRegularHpMult.Peek() : 1f;
                if (SpawnYokai(definition, false) == null)
                {
                    pendingSpawnHpMult = 1f;
                    return;
                }
                pendingRegular.Dequeue();
                if (pendingRegularHpMult.Count > 0) pendingRegularHpMult.Dequeue();
                pendingSpawnHpMult = 1f;
            }
        }

        private YokaiBrain SpawnYokai(YokaiDefinition definition, bool raid)
        {
            if (definition == null || raidTarget == null || !TryGetSpawnPosition(out var position)) return null;
            return SpawnYokaiAt(definition, raid, position, null);
        }

        private YokaiBrain SpawnSavedYokai(YokaiStateRecord record)
        {
            if (record == null) return null;
            var definition = gameDataCatalog.FindYokai(record.yokaiId);
            var brain = SpawnYokaiAt(definition, record.raid, record.position, record.instanceId);
            if (brain == null) return null;
            var health = brain.GetComponent<Health>();
            var restoredHealth = Mathf.Clamp(
                Mathf.RoundToInt(record.currentHealth * health.MaxHealth / (float)record.maxHealth),
                1, health.MaxHealth);
            if (!health.RestoreCurrent(restoredHealth) || !brain.RestoreSaveState(record))
            {
                var entry = spawnedYokai.FirstOrDefault(candidate => candidate.brain == brain);
                if (entry != null) spawnedYokai.Remove(entry);
                runtimeServices.Unregister(brain);
                Destroy(brain.gameObject);
                return null;
            }
            var body = brain.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = record.velocity;
            return brain;
        }

        private YokaiBrain SpawnYokaiAt(YokaiDefinition definition, bool raid, Vector3 position,
            string restoredInstanceId)
        {
            if (definition == null || raidTarget == null || !IsFinite(position)) return null;
            string instanceId;
            if (string.IsNullOrWhiteSpace(restoredInstanceId))
            {
                do instanceId = $"yokai_{spawnSequence++}";
                while (spawnedYokai.Any(entry =>
                    string.Equals(entry.instanceId, instanceId, StringComparison.Ordinal)));
            }
            else
            {
                instanceId = restoredInstanceId;
                if (instanceId.StartsWith("yokai_", StringComparison.Ordinal) &&
                    int.TryParse(instanceId.Substring("yokai_".Length), out var restoredSequence))
                    spawnSequence = Math.Max(spawnSequence, restoredSequence + 1);
            }
            var yokaiObject = new GameObject($"Yokai_{definition.Id}_{instanceId}");
            yokaiObject.transform.SetParent(transform, false);
            yokaiObject.transform.position = position;
            var health = yokaiObject.AddComponent<Health>();
            var collider = yokaiObject.AddComponent<CircleCollider2D>();
            collider.radius = .42f;
            yokaiObject.AddComponent<Rigidbody2D>();
            var physicsBody = yokaiObject.AddComponent<WorldMobPhysicsBody>();
            physicsBody.ConfigureForRuntime(WorldMobPhysicsBody.ForYokai(definition.Kind), bootstrap.TileService);
            physicsBody.IgnoreCollisionWith(raidTarget.transform);
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
            loot.ConfigureForRuntime(definition, rewards: raid && baekjungScheduler?.IsActive == true
                ? baekjungRewardRules
                : null);
            var counters = placedObjectRuntime != null
                ? new CounterAuraSensor(yokaiObject.transform, placedObjectRuntime.ActiveCounterAuras)
                : null;
            brain.ConfigureForRuntime(definition, raidTarget, counters, YokaiSpawnTrack.Raid);
            if (!raid && health != null && pendingSpawnHpMult > 0f &&
                !Mathf.Approximately(pendingSpawnHpMult, 1f))
            {
                var scaledHp = waveNight != null
                    ? Mathf.Max(1, Mathf.RoundToInt(
                        waveNight.ApplyHpMult(definition.HitPoints, pendingSpawnHpMult)))
                    : Mathf.Max(1, Mathf.RoundToInt(definition.HitPoints * pendingSpawnHpMult));
                health.ConfigureForRuntime(scaledHp);
            }
            if (usesEoduksiniPresentation)
            {
                var presentation = visualObject.AddComponent<RuntimeEoduksiniVisual>();
                presentation.ConfigureForRuntime(brain, yokaiRenderer);
            }
            var healthBar = yokaiObject.AddComponent<RuntimeWorldHealthBar>();
            healthBar.ConfigureForRuntime(health, yokaiRenderer, usesEoduksiniPresentation ? 2f : 1f);
            health.Died += () => HandleYokaiEnded(health, true);
            brain.FledOffscreen += ignored => HandleYokaiEnded(health, false);
            spawnedYokai.Add(new SpawnedYokai
                { instanceId = instanceId, health = health, brain = brain, raid = raid });
            runtimeServices.Register(brain);
            return brain;
        }

        private void HandleYokaiEnded(Health health, bool killed)
        {
            var entry = spawnedYokai.FirstOrDefault(candidate => candidate.health == health);
            if (entry == null) return;
            spawnedYokai.Remove(entry);
            runtimeServices.Unregister(entry.brain);
            if (entry.raid) RaidSlotAvailable?.Invoke();
            else
            {
                if (killed && waveNight != null && waveNight.IsActive) waveNight.RegisterKill();
                TryAdvanceWaveNight();
                TryFillRegularSlots();
            }
            if (health != null) Destroy(health.gameObject);
        }

        private void ClearPendingRegular()
        {
            pendingRegular.Clear();
            pendingRegularHpMult.Clear();
            pendingSpawnHpMult = 1f;
        }

        private void EnqueuePendingRegular(YokaiDefinition definition, float hpMult)
        {
            if (definition == null) return;
            pendingRegular.Enqueue(definition);
            pendingRegularHpMult.Enqueue(hpMult > 0f ? hpMult : 1f);
        }

        private void TryAdvanceWaveNight()
        {
            if (waveNight == null || !waveNight.IsActive || !IsRegularSpawningEnabled) return;
            var elapsed = GetNightElapsedSeconds();
            var alive = ActiveRegularCount + ActiveRaidCount;
            if (!waveNight.TryAdvance(elapsed, alive, out var spawns) || spawns == null) return;
            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                var definition = gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                    candidate != null && candidate.Kind == spawn.Kind &&
                    candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));
                EnqueuePendingRegular(definition, spawn.HpMult);
            }
            TryFillRegularSlots();
        }

        private float GetNightElapsedSeconds()
        {
            var time = bootstrap?.TimeService;
            if (time == null || !time.IsNight) return 0f;
            return Mathf.Max(0f, time.TimeOfDayGameSeconds - time.DayDurationSeconds);
        }

        private WaveNightController CreateWaveNightController()
        {
            var rows = LoadNightWaveRows();
            var period = ReadGlobalInt(GlobalKeys.WaveNightPeriod, 10);
            var offset = ReadGlobalInt(GlobalKeys.WaveNightOffset, 5);
            var advance = ReadGlobalFloat(GlobalKeys.WaveAdvanceSec, 108f);
            var cap = ReadGlobalInt(GlobalKeys.YokaiCap, 8);
            var multTarget = ReadGlobalString(GlobalKeys.WaveMultTarget, "hp_only");
            return new WaveNightController(rows, period, offset, advance, cap, multTarget);
        }

        private List<WaveNightRules.WaveRow> LoadNightWaveRows()
        {
            string csvText = null;
            if (nightWavesCsv != null && !string.IsNullOrWhiteSpace(nightWavesCsv.text))
                csvText = nightWavesCsv.text;
#if UNITY_EDITOR
            else if (System.IO.File.Exists(NightWavesCsvPath))
                csvText = System.IO.File.ReadAllText(NightWavesCsvPath);
#endif
            var rows = WaveNightRules.ParseCsv(csvText);
            return rows.Count > 0 ? rows : WaveNightRules.BuiltinCanonicalRows();
        }

        private int ReadGlobalInt(string key, int fallback)
        {
            var definition = gameDataCatalog != null ? gameDataCatalog.FindGlobal(key) : null;
            return definition != null && definition.TryGetInt(out var value) ? value : fallback;
        }

        private float ReadGlobalFloat(string key, float fallback)
        {
            var definition = gameDataCatalog != null ? gameDataCatalog.FindGlobal(key) : null;
            return definition != null && definition.TryGetFloat(out var value) ? value : fallback;
        }

        private string ReadGlobalString(string key, string fallback)
        {
            var definition = gameDataCatalog != null ? gameDataCatalog.FindGlobal(key) : null;
            return definition != null && !string.IsNullOrWhiteSpace(definition.Value)
                ? definition.Value
                : fallback;
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

        private void HandleBaekjungStarted(DayEventDefinition definition)
        {
            baekjungRewardRules = definition != null ? new BaekjungRewardRules(definition) : null;
        }

        private void RestoreBaekjungRewardRules(float tearRemainder)
        {
            var definition = baekjungScheduler?.ActiveDefinition;
            baekjungRewardRules = definition != null ? new BaekjungRewardRules(definition) : null;
            baekjungRewardRules?.RestoreTearRemainder(tearRemainder);
        }

        private YokaiDefinition FindRaidYokai(YokaiKind kind) =>
            gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                candidate != null && candidate.Kind == kind &&
                candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));

        private static bool ValidateYokaiBrainState(YokaiStateRecord record) =>
            record.behaviorState >= 0 && record.behaviorState <= 4 &&
            IsFinite(record.dawnFleeDirection) &&
            IsFiniteNonNegative(record.sieveStopRemaining) &&
            IsFiniteNonNegative(record.sieveCooldownRemaining) &&
            IsFiniteNonNegative(record.lanternPauseRemaining) &&
            IsFiniteNonNegative(record.bloomCooldownRemaining) &&
            IsFiniteNonNegative(record.contactAttackRemaining) &&
            IsFiniteNonNegative(record.frostSlowRemaining) &&
            !float.IsNaN(record.frostSlowFraction) && !float.IsInfinity(record.frostSlowFraction) &&
            record.frostSlowFraction >= 0f && record.frostSlowFraction <= 1f;

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

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
            if (baekjungScheduler != null)
                baekjungScheduler.Started -= HandleBaekjungStarted;
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
            runtimeServices?.Unregister(this);
            waveNight?.EndNight();
            ClearPendingRegular();
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
        }

        private void Unbind()
        {
            if (health != null) health.Damaged -= HandleDamaged;
            if (barRoot != null) Destroy(barRoot.gameObject);
            barRoot = null;
            fillRenderer = null;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= HandleDamaged;
        }
    }
}
