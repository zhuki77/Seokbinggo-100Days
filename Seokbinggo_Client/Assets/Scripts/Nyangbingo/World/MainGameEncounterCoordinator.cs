using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
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
        private const float BossScale = 2f;

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
        private readonly Dictionary<YokaiKind, int> residentLastKilledDays =
            new Dictionary<YokaiKind, int>();
        private readonly List<ForcedBossEncounterBinding> forcedBossBindings = new List<ForcedBossEncounterBinding>();
        private readonly List<BossDefinition> forcedBossDefinitions = new List<BossDefinition>();
        private BaekjungScheduler baekjungScheduler;
        private BaekjungTimeBinding baekjungTimeBinding;
        private BaekjungWaveSpawner baekjungWaveSpawner;
        private BaekjungRegularSpawnGate baekjungRegularSpawnGate;
        private BaekjungRewardRules baekjungRewardRules;
        private DayCurveDefinition currentDayCurve;
        private bool regularSpawningEnabled = true;
        private bool discardRegularForCurrentNight;
        private bool forcedBossSpawnPending;
        private bool activeBossIsForcedInvasion;
        private bool initialized;
        private bool restoringSnapshot;
        private bool restoredDetailedEncounterDuringTransaction;
        private RegularEncounterStateRecord restoredRegularEncounter;
        private Dictionary<YokaiKind, int> stagedResidentLastKilledDays;
        private ResidentYokaiRules residentRules;
        private int spawnSequence;
        private int debugBossIndex;
        private BossCombatController activeBossCombat;
        private MainGameTurretRuntime placedObjectRuntime;
        private MainGameCoreRaidTarget coreRaidTarget;
        private float baseVicinityRadius = 28f;
        private readonly List<SpawnedYokai> bossPausedYokai = new List<SpawnedYokai>();

        private sealed class SpawnedYokai
        {
            public string instanceId;
            public Health health;
            public YokaiBrain brain;
            public bool raid;
            public YokaiSpawnTrack spawnTrack;
        }

        public int ActiveRaidCount => spawnedYokai.Count(entry => entry.raid && IsAlive(entry));
        public int ActiveRegularCount => spawnedYokai.Count(entry =>
            !entry.raid && entry.spawnTrack == YokaiSpawnTrack.Raid && IsAlive(entry));
        public int PendingRegularCount => pendingRegular.Count;
        public bool IsRegularSpawningEnabled => regularSpawningEnabled && !discardRegularForCurrentNight;
        public BossManager BossManager => bossManager;
        public BaekjungScheduler BaekjungScheduler => baekjungScheduler;
        public Transform PlayerTransform => raidTarget != null ? raidTarget.transform : null;
        public Health PlayerHealth => raidTarget != null ? raidTarget.GetComponent<Health>() : null;
        public bool CanSerializeProgress => initialized && bossManager != null && !bossManager.IsBossActive;
        public static bool ShouldPauseFieldYokaiForBoss(bool forcedInvasion) => !forcedInvasion;
        public static int ResolveRegularSpawnCap(int maxActive, bool includesForcedInvasionBoss) =>
            Math.Max(0, maxActive - (includesForcedInvasionBoss ? 1 : 0));
        public static bool ShouldSpawnResident(
            int day, int firstDay, int lastKilledDay, int activeCount, int maxPerSpecies) =>
            day >= firstDay && day > lastKilledDay &&
            activeCount >= 0 && activeCount < maxPerSpecies;
        public static bool IsResidentDepth(int surfaceY, int cellY)
            => IsResidentDepth(surfaceY, cellY, 91, 135);
        public static bool IsResidentDepth(
            int surfaceY, int cellY, int minDepth, int maxDepth)
        {
            var depth = surfaceY - cellY + 1;
            return depth >= minDepth && depth <= maxDepth;
        }

        public static bool TryMapForcedBossToCompositionKind(
            BossKind bossKind, out YokaiKind yokaiKind)
        {
            // v34 day 30 represents Imugi in both day-curve composition and bosses.csv.
            // The composition entry reserves the encounter slot; the actual combatant is
            // created exclusively through ForcedBossEncounterBinding.
            if (bossKind == BossKind.Imugi)
            {
                yokaiKind = YokaiKind.Imugi;
                return true;
            }

            yokaiKind = default;
            return false;
        }
        public event Action RaidSlotAvailable;

        public bool TryGetActiveGaekgwi(out YokaiDefinition definition, out Health health)
        {
            var entry = spawnedYokai.FirstOrDefault(candidate =>
                IsAlive(candidate) && candidate.brain?.Definition?.Kind == YokaiKind.Gaekgwi);
            definition = entry?.brain?.Definition;
            health = entry?.health;
            return definition != null && health != null;
        }

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
                        ? "imugi_boss"
                        : "king_dokkaebi";
                TryStartEditorBossEncounter(bossId);
            }
            if (Input.GetKeyDown(KeyCode.J)) DefeatAllYokaiForEditorTest();
            if (Input.GetKeyDown(KeyCode.K)) DefeatActiveBossForEditorTest();
            if (Input.GetKeyDown(KeyCode.F12))
            {
                var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                var alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (alt && shift)
                    SpawnYokaiForEditorTest(
                        YokaiKind.Gaekgwi, "Alt+Shift+F12", "Gaekgwi");
                else if (alt)
                    SpawnYokaiForEditorTest(
                        YokaiKind.Gangcheori, "Alt+F12", "Gangcheori");
                else if (shift)
                    GrantEoduksiniVisualTestKit();
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    GrantRoofVisualTestKit();
                else
                    SpawnYokaiForEditorTest(
                        YokaiKind.Eoduksini, "F12", "Eoduksini");
            }
        }

        private void SpawnYokaiForEditorTest(
            YokaiKind kind, string shortcut, string displayName)
        {
            if (!initialized && !Initialize())
            {
                Debug.LogError(
                    $"[Nyangbingo] {shortcut} {displayName} visual test spawn failed: " +
                    "MainGame initialization failed.");
                return;
            }

            var definition = gameDataCatalog?.Yokai.FirstOrDefault(candidate =>
                candidate != null && candidate.Kind == kind);
            var spawnTrack = ResolveInstanceSpawnTrack(definition);
            if (definition == null || spawnTrack == YokaiSpawnTrack.None)
            {
                Debug.LogError(
                    $"[Nyangbingo] {shortcut} {displayName} visual test spawn failed: " +
                    "yokai definition or spawn track missing.");
                return;
            }

            if (!TryGetSpawnPosition(
                    definition, WorldMobPhysicsBody.ForYokai(definition.Kind), out var position))
            {
                Debug.LogError(
                    $"[Nyangbingo] {shortcut} {displayName} visual test spawn failed: " +
                    "no valid spawn terrain near the player.");
                return;
            }

            var spawned = SpawnYokaiAt(
                definition,
                raid: spawnTrack == YokaiSpawnTrack.Raid,
                position,
                restoredInstanceId: null,
                spawnTrack) != null;
            Debug.Log(spawned
                ? $"[Nyangbingo] {shortcut} {displayName} visual test spawn completed " +
                  $"({spawnTrack})."
                : $"[Nyangbingo] {shortcut} {displayName} visual test spawn failed during creation.");
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
            if (!ResidentYokaiRules.TryCreate(gameDataCatalog.Globals, out residentRules))
            {
                // Keep old scenes playable until the v34.1 CSV reimport creates the six new SOs.
                // The importer and regression gate still require the exact 100-row contract.
                residentRules = ResidentYokaiRules.CreateConfirmedV341Defaults();
                Debug.LogWarning("[Nyangbingo] Resident v34.1 globals are not wired into the catalog yet; " +
                                 "using the confirmed 1/day_dawn/next_day_dawn/24/12/last_killed_day defaults.");
            }
            ResetResidentProgress();
            baseVicinityRadius = ReadPositiveGlobal(GlobalKeys.BaseVicinityRadius, 28f);
            raidTarget.ConfigureStealthRuntime(() =>
                runtimeServices?.Talismans?.IgnoresYokaiAggro == true ||
                SuppressesSurfaceFirstStrike());
            var coreTargetObject = new GameObject("IceCoreRaidTarget");
            coreTargetObject.transform.SetParent(transform, false);
            coreRaidTarget = coreTargetObject.AddComponent<MainGameCoreRaidTarget>();
            coreRaidTarget.Configure(raidTarget, runtimeServices.Invasion);

            bossManager.ConfigureForRuntime(bootstrap.TimeService, this);
            baekjungScheduler = new BaekjungScheduler(gameDataCatalog.DayEvents);
            baekjungScheduler.Started += HandleBaekjungStarted;
            baekjungWaveSpawner = new BaekjungWaveSpawner(baekjungScheduler, this);
            baekjungRegularSpawnGate = new BaekjungRegularSpawnGate(baekjungScheduler, this);
            baekjungTimeBinding = new BaekjungTimeBinding(bootstrap.TimeService, baekjungScheduler);
            runtimeServices.Register(baekjungTimeBinding);

            GameEvents.OnNightStart += HandleNightStart;
            GameEvents.OnDayStart += HandleDayStart;
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

        public Health SpawnBoss(BossDefinition definition) => CreateBoss(definition, true);

        private Health CreateBoss(BossDefinition definition, bool forcedInvasion)
        {
            if (definition == null) return null;
            var locomotion = WorldMobPhysicsBody.ForBoss(definition.Kind);
            if (!TryGetSpawnPosition(locomotion, out var position)) return null;
            var bossObject = new GameObject($"Boss_{definition.Id}");
            bossObject.transform.SetParent(transform, false);
            bossObject.transform.localScale = Vector3.one * BossScale;
            bossObject.transform.position = position;
            var health = bossObject.AddComponent<Health>();
            var collider = bossObject.AddComponent<CircleCollider2D>();
            // The boss root is enlarged for presentation. Divide every movement collider by
            // that scale so the world-space radius remains the value returned by
            // PhysicalRadiusForBoss; otherwise grounded bosses become 1.3 tiles wide and
            // continuously detect ordinary terrain as a step obstacle.
            var movementColliderScale = BossScale;
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
            var bossRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(bossRenderer);
            var bossArtId = definition.Kind == BossKind.Imugi ? "imugi" : definition.Id;
            var bossArt = characterArtCatalog != null ? characterArtCatalog.Find(bossArtId) : null;
            if (bossArt?.Sprite == null)
                RuntimePlaceholderVisual.Configure(bossRenderer, new Color(1f, .25f, .2f), 1.3f, 15);
            if (locomotion == WorldMobLocomotion.Grounded)
                visualObject.transform.localPosition = Vector3.up *
                    RuntimeCharacterSpriteAnimator.CalculateGroundedVisualLocalY(
                        collider, bossRenderer);
            bossObject.AddComponent<RuntimeDamageFlash>();
            bossObject.AddComponent<RuntimeWorldDamagePopup>();
            health.ConfigureForRuntime(definition.HitPoints);
            var combat = bossObject.AddComponent<BossCombatController>();
            if (!combat.ConfigureForRuntime(definition, raidTarget, gameDataCatalog) || !runtimeServices.Register(combat))
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
            if (definition.Kind == BossKind.Imugi)
            {
                var bodySprite = characterArtCatalog?.FindSprite("imugi_body");
                if (bodySprite != null)
                    bossObject.AddComponent<RuntimeImugiBodyVisual>().Configure(
                        bodySprite,
                        characterArtCatalog?.FindSprite("imugi_pre_tail"),
                        characterArtCatalog?.FindSprite("imugi_post_tail"),
                        14);
            }
            else if (definition.Kind == BossKind.Gangcheori)
            {
                var bodySprite = characterArtCatalog?.FindSprite("gangcheol_body");
                if (bodySprite != null)
                    bossObject.AddComponent<RuntimeGangcheoriBodyVisual>()
                        .Configure(
                            bodySprite,
                            characterArtCatalog?.FindSprite("gangcheol_pre_tail"),
                            characterArtCatalog?.FindSprite("gangcheol_post_tail"),
                            bossRenderer,
                            14);
            }
            // Body and tail hurtboxes are created after the movement core. Reapply the player
            // collision policy so large composite creatures cannot be pushed by the player.
            physicsBody.IgnoreCollisionWith(raidTarget.transform);
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
            stagedResidentLastKilledDays = null;
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
            var residentProgress = stagedResidentLastKilledDays;
            stagedResidentLastKilledDays = null;
            if (!succeeded)
            {
                if (detailedRestored) ClearSpawnedYokai();
                return;
            }
            ApplyResidentProgress(residentProgress);
            if (!bootstrap.TimeService.IsNight)
            {
                ReconcileResidentYokai();
                return;
            }
            if (detailedRestored)
            {
                ReconcileResidentYokai();
                return;
            }
            if (regularEncounter != null && regularEncounter.hasValue)
                RestoreRegularEncounter(regularEncounter);
            else
                HandleNightStart();
            ReconcileResidentYokai();
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
            if (!CaptureResidentProgress(state)) return null;
            if (!state.isNight) return state;

            for (var index = 0; index < spawnedYokai.Count; index++)
            {
                var entry = spawnedYokai[index];
                var definition = entry?.brain?.Definition;
                if (!IsAlive(entry) || definition == null ||
                    entry.spawnTrack == YokaiSpawnTrack.Resident) continue;
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
            if (!TryStageResidentProgress(state)) return false;
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
                        (definition.Kind == YokaiKind.Gaekgwi) !=
                        record.gaekgwiPatternInitialized ||
                        string.IsNullOrWhiteSpace(record.instanceId) || !instanceIds.Add(record.instanceId) ||
                        !IsFinite(record.position) || !IsFinite(record.velocity) ||
                        record.maxHealth <= 0 || record.currentHealth <= 0 ||
                        record.currentHealth > record.maxHealth)
                        return false;
                    if (!ValidateYokaiBrainState(record) ||
                        !ValidateStolenItems(record))
                        return false;
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
                            gameDataCatalog.FindYokai(state.pendingRegularYokaiIds[index]));

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
                        gameDataCatalog.FindYokai(state.remainingRegularYokaiIds[index]));
                TryFillRegularSlots();
            }
            Debug.Log($"[Nyangbingo] MainGameEncounterCoordinator: saved regular encounter restored " +
                      $"(discard={discardRegularForCurrentNight}, remaining={state.remainingRegularYokaiIds.Count}).");
            return true;
        }

        private bool CaptureResidentProgress(RegularEncounterStateRecord state)
        {
            if (state == null) return false;
            state.residentLastKilledDays.Clear();
            foreach (var kind in ResidentKinds)
            {
                var definition = FindResidentYokai(kind);
                if (definition == null) return false;
                state.residentLastKilledDays.Add(new ResidentYokaiDayRecord
                {
                    yokaiId = definition.Id,
                    lastKilledDay = residentLastKilledDays.TryGetValue(kind, out var killedDay)
                        ? Mathf.Max(0, killedDay)
                        : 0
                });
            }
            return true;
        }

        private bool TryStageResidentProgress(RegularEncounterStateRecord state)
        {
            var staged = new Dictionary<YokaiKind, int>();
            foreach (var kind in ResidentKinds) staged[kind] = 0;
            var records = state.residentLastKilledDays;
            if (records == null || records.Count == 0)
            {
                stagedResidentLastKilledDays = staged;
                return true;
            }
            if (records.Count != ResidentKinds.Length) return false;

            var seen = new HashSet<YokaiKind>();
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                var definition = gameDataCatalog.FindYokai(record.yokaiId);
                if (definition == null ||
                    !definition.SupportsSpawnTrack(YokaiSpawnTrack.Resident) ||
                    !IsResidentKind(definition.Kind) ||
                    !seen.Add(definition.Kind) ||
                    record.lastKilledDay < 0 ||
                    record.lastKilledDay > bootstrap.TimeService.Day)
                    return false;
                staged[definition.Kind] = record.lastKilledDay;
            }
            stagedResidentLastKilledDays = staged;
            return true;
        }

        private void ApplyResidentProgress(Dictionary<YokaiKind, int> progress)
        {
            ResetResidentProgress();
            if (progress == null) return;
            foreach (var pair in progress)
                if (IsResidentKind(pair.Key))
                    residentLastKilledDays[pair.Key] = Mathf.Max(0, pair.Value);
        }

        private void ResetResidentProgress()
        {
            residentLastKilledDays.Clear();
            foreach (var kind in ResidentKinds) residentLastKilledDays[kind] = 0;
        }

        private void ReconcileResidentYokai()
        {
            if (!initialized || restoringSnapshot || residentRules == null ||
                bootstrap?.IsWorldReady != true || bootstrap.TimeService == null ||
                raidTarget == null || bossManager?.IsBossActive == true)
                return;

            var day = bootstrap.TimeService.Day;
            foreach (var kind in ResidentKinds)
            {
                residentLastKilledDays.TryGetValue(kind, out var killedDay);
                var activeCount = spawnedYokai.Count(entry => IsAlive(entry) &&
                        entry.spawnTrack == YokaiSpawnTrack.Resident &&
                        entry.brain.Definition.Kind == kind);
                if (!ShouldSpawnResident(
                        day, FirstResidentDay(kind), killedDay,
                        activeCount, residentRules.MaxPerSpecies))
                    continue;

                var definition = FindResidentYokai(kind);
                if (definition == null ||
                    !TryGetResidentSpawnPosition(kind, out var position))
                    continue;
                SpawnYokaiAt(
                    definition, raid: false, position, restoredInstanceId: null,
                    YokaiSpawnTrack.Resident);
            }
        }

        private bool TryGetResidentSpawnPosition(YokaiKind kind, out Vector3 position)
        {
            position = default;
            var tileService = bootstrap?.TileService;
            var result = bootstrap?.Session?.LastResult ?? default;
            if (tileService == null || result.surfaceHeights == null ||
                result.surfaceHeights.Length != tileService.Width)
                return false;

            var protectedCells = BuildResidentProtectedMask(tileService, result.altarPosition);
            var playerPosition = (Vector2)raidTarget.transform.position;
            var playerDistanceSquared =
                residentRules.MinPlayerDistance * residentRules.MinPlayerDistance;
            var betweenDistanceSquared =
                residentRules.MinBetweenDistance * residentRules.MinBetweenDistance;
            var otherResidents = spawnedYokai
                .Where(entry => IsAlive(entry) &&
                    entry.spawnTrack == YokaiSpawnTrack.Resident &&
                    entry.brain.Definition.Kind != kind)
                .Select(entry => (Vector2)entry.health.transform.position)
                .ToArray();
            var candidates = new List<Vector3Int>();
            for (var x = 1; x < tileService.Width - 1; x++)
            {
                var surfaceY = result.surfaceHeights[x];
                for (var depth = residentRules.MinDepth;
                     depth <= residentRules.MaxDepth;
                     depth++)
                {
                    var y = surfaceY - depth + 1;
                    var cell = new Vector3Int(x, y, 0);
                    if (!IsResidentDepth(
                            surfaceY, y, residentRules.MinDepth, residentRules.MaxDepth) ||
                        y < 1 || y >= tileService.Height - 1 ||
                        protectedCells[x, y] ||
                        !IsResidentSpawnCellOpen(tileService, cell))
                        continue;
                    var worldPosition = new Vector2(x + .5f, y + .5f);
                    if ((worldPosition - playerPosition).sqrMagnitude <
                        playerDistanceSquared)
                        continue;
                    var tooCloseToOtherResident = false;
                    for (var index = 0; index < otherResidents.Length; index++)
                        if ((worldPosition - otherResidents[index]).sqrMagnitude <
                            betweenDistanceSquared)
                        {
                            tooCloseToOtherResident = true;
                            break;
                        }
                    if (!tooCloseToOtherResident) candidates.Add(cell);
                }
            }
            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[Nyangbingo] No valid T3 resident spawn cell for {kind} " +
                                 $"(depth={residentRules.MinDepth}..{residentRules.MaxDepth}, " +
                                 "player>=24, resident>=12, ice/altar>10).");
                return false;
            }

            var seed = unchecked(tileService.Seed * 397 ^
                                 bootstrap.TimeService.Day * 31 ^
                                 (int)kind * 7919);
            var definition = FindResidentYokai(kind);
            if (definition == null || !TerrainSpawnRules.TryChooseCell(
                    definition, candidates, gameDataCatalog, tileService, result, seed,
                    out var selected))
                return false;
            position = tileService.GetCellCenterWorld(selected);
            return true;
        }

        private static bool[,] BuildResidentProtectedMask(
            TileService tileService, Vector2Int altarPosition)
        {
            var mask = new bool[tileService.Width, tileService.Height];
            MarkResidentProtectedSquare(mask, altarPosition, 10);
            for (var x = 0; x < tileService.Width; x++)
                for (var y = 0; y < tileService.Height; y++)
                {
                    var id = tileService.GetTile(new Vector3Int(x, y, 0)).elementType;
                    if (id == WorldTileTypes.IceLake || id == WorldTileTypes.IceAltar)
                        MarkResidentProtectedSquare(mask, new Vector2Int(x, y), 10);
                }
            return mask;
        }

        private static void MarkResidentProtectedSquare(
            bool[,] mask, Vector2Int center, int radius)
        {
            if (mask == null) return;
            var minX = Mathf.Max(0, center.x - radius);
            var maxX = Mathf.Min(mask.GetLength(0) - 1, center.x + radius);
            var minY = Mathf.Max(0, center.y - radius);
            var maxY = Mathf.Min(mask.GetLength(1) - 1, center.y + radius);
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                    mask[x, y] = true;
        }

        private static bool IsResidentSpawnCellOpen(
            TileService tileService, Vector3Int cell) =>
            tileService.GetTile(cell).IsAir &&
            tileService.GetTile(cell + Vector3Int.up).IsAir &&
            tileService.GetTile(cell + Vector3Int.left).IsAir &&
            tileService.GetTile(cell + Vector3Int.right).IsAir;

        private YokaiDefinition FindResidentYokai(YokaiKind kind) =>
            gameDataCatalog?.Yokai.FirstOrDefault(candidate =>
                candidate != null && candidate.Kind == kind &&
                candidate.SupportsSpawnTrack(YokaiSpawnTrack.Resident));

        private static readonly YokaiKind[] ResidentKinds =
            { YokaiKind.Eoduksini, YokaiKind.Gangcheori };

        private static bool IsResidentKind(YokaiKind kind) =>
            kind == YokaiKind.Eoduksini || kind == YokaiKind.Gangcheori;

        private static int FirstResidentDay(YokaiKind kind) =>
            kind == YokaiKind.Eoduksini ? 16 :
            kind == YokaiKind.Gangcheori ? 18 :
            int.MaxValue;

        private void HandleWorldReady()
        {
            if (raidTarget == null || bootstrap?.TileService == null) return;
            var tileService = bootstrap.TileService;
            var centerX = Mathf.Clamp(tileService.Width / 2, 1, tileService.Width - 2);
            var centerY = Mathf.Clamp(Mathf.RoundToInt(tileService.Height * .82f), 2, tileService.Height - 2);
            raidTarget.transform.position =
                tileService.GetCellCenterWorld(new Vector3Int(centerX, centerY, 0));
            runtimeServices.PlayerTemperature.SetTrackedTransform(raidTarget.transform);
            if (!restoringSnapshot)
            {
                ReconcileResidentYokai();
                for (var index = 0; index < forcedBossBindings.Count; index++)
                    forcedBossBindings[index].TryStartForCurrentNight();
            }
        }

        private void HandleDayStart() => ReconcileResidentYokai();

        private void HandleNightStart()
        {
            discardRegularForCurrentNight = false;
            regularSpawningEnabled = baekjungScheduler?.IsActive != true;
            ClearPendingRegular();
            var day = bootstrap != null && bootstrap.TimeService != null ? bootstrap.TimeService.Day : 0;
            currentDayCurve = gameDataCatalog.FindDayCurve(day);

            if (currentDayCurve == null) return;

            var includesForcedInvasionBoss = TryGetForcedInvasionCompositionKind(
                day, out var forcedInvasionKind);
            var composition = currentDayCurve.SpawnComposition;
            for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
            {
                var group = composition[groupIndex];
                var definition = gameDataCatalog.Yokai.FirstOrDefault(candidate =>
                    candidate != null && candidate.Kind == group.kind &&
                    candidate.SupportsSpawnTrack(YokaiSpawnTrack.Raid));
                var amount = Math.Max(0, group.amount);
                if (includesForcedInvasionBoss && group.kind == forcedInvasionKind)
                    amount = Math.Max(0, amount - 1);
                for (var count = 0; definition != null && count < amount; count++)
                    EnqueuePendingRegular(definition);
            }
            TryFillRegularSlots();
        }

        private void HandleDawnWarning()
        {
            ClearPendingRegular();
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
            if (currentDayCurve == null) return;
            var reservesForcedBossSlot = TryGetForcedInvasionCompositionKind(
                bootstrap.TimeService.Day, out _);
            var cap = ResolveRegularSpawnCap(currentDayCurve.MaxActive, reservesForcedBossSlot);
            while (pendingRegular.Count > 0 && ActiveRegularCount + ActiveRaidCount < cap)
            {
                var definition = pendingRegular.Peek();
                if (SpawnYokai(definition, false) == null)
                    return;
                pendingRegular.Dequeue();
            }
        }

        private bool TryGetForcedInvasionCompositionKind(int day, out YokaiKind kind)
        {
            kind = default;
            if (gameDataCatalog?.Bosses == null || day <= 0) return false;
            var definition = gameDataCatalog.Bosses.FirstOrDefault(candidate =>
                candidate != null && candidate.ForcedDay > 0 && candidate.ForcedDay == day);
            return definition != null &&
                   TryMapForcedBossToCompositionKind(definition.Kind, out kind);
        }

        private YokaiBrain SpawnYokai(YokaiDefinition definition, bool raid)
        {
            if (definition == null || raidTarget == null ||
                !TryGetSpawnPosition(
                    definition, WorldMobPhysicsBody.ForYokai(definition.Kind), out var position)) return null;
            return SpawnYokaiAt(
                definition, raid, position, null, ResolveInstanceSpawnTrack(definition));
        }

        private YokaiBrain SpawnSavedYokai(YokaiStateRecord record)
        {
            if (record == null) return null;
            var definition = gameDataCatalog.FindYokai(record.yokaiId);
            var brain = SpawnYokaiAt(
                definition, record.raid, record.position, record.instanceId,
                ResolveInstanceSpawnTrack(definition));
            if (brain == null) return null;
            var health = brain.GetComponent<Health>();
            var restoredHealth = Mathf.Clamp(
                Mathf.RoundToInt(record.currentHealth * health.MaxHealth / (float)record.maxHealth),
                1, health.MaxHealth);
            var loot = brain.GetComponent<YokaiLoot>();
            if (!health.RestoreCurrent(restoredHealth) ||
                !brain.RestoreSaveState(record) ||
                loot == null ||
                !loot.RestoreStolenItems(record.stolenItems, gameDataCatalog.FindItem))
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
            string restoredInstanceId, YokaiSpawnTrack instanceSpawnTrack)
        {
            if (definition == null || raidTarget == null || !IsFinite(position) ||
                instanceSpawnTrack == YokaiSpawnTrack.None ||
                !definition.SupportsSpawnTrack(instanceSpawnTrack)) return null;
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
            var bodyTiles = definition.UsesArenaBody
                ? 1
                : WorldV72Rules.BodyTilesForHitPoints(definition.HitPoints);
            if (!definition.UsesArenaBody && definition.BodyTiles != bodyTiles)
            {
                Debug.LogError($"[Nyangbingo] yokai body_tiles mismatch: {definition.Id} " +
                               $"csv={definition.BodyTiles}, derived={bodyTiles}.");
                Destroy(yokaiObject);
                return null;
            }
            collider.radius = .42f * bodyTiles;
            yokaiObject.AddComponent<Rigidbody2D>();
            var physicsBody = yokaiObject.AddComponent<WorldMobPhysicsBody>();
            var locomotion = WorldMobPhysicsBody.ForYokai(definition.Kind);
            physicsBody.ConfigureForRuntime(
                locomotion,
                bootstrap.TileService,
                material => definition.WallDamageFor(material) > 0f);
            physicsBody.IgnoreCollisionWith(raidTarget.transform);
            var usesEoduksiniPresentation = definition.Kind == YokaiKind.Eoduksini;
            var usesGroundedVisualRoot = locomotion == WorldMobLocomotion.Grounded;
            var visualObject = usesEoduksiniPresentation || usesGroundedVisualRoot
                ? new GameObject("Visual")
                : yokaiObject;
            if (visualObject != yokaiObject)
                visualObject.transform.SetParent(yokaiObject.transform, false);
            var yokaiRenderer = visualObject.AddComponent<SpriteRenderer>();
            var yokaiArt = characterArtCatalog != null
                ? characterArtCatalog.Find(definition.Id)
                : null;
            if (yokaiArt?.Sprite == null)
                RuntimePlaceholderVisual.Configure(yokaiRenderer,
                    raid ? new Color(1f, .45f, .8f) : new Color(.8f, .35f, 1f), .8f, 10);
            if (usesGroundedVisualRoot)
                visualObject.transform.localPosition = Vector3.up *
                    RuntimeCharacterSpriteAnimator.CalculateGroundedVisualLocalY(
                        collider, yokaiRenderer);
            yokaiObject.AddComponent<RuntimeDamageFlash>();
            yokaiObject.AddComponent<RuntimeWorldDamagePopup>();
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            RuntimeCharacterSpriteAnimator characterAnimator = null;
            if (yokaiArt?.Sprite != null)
            {
                characterAnimator = visualObject.AddComponent<RuntimeCharacterSpriteAnimator>();
                characterAnimator.Configure(yokaiArt, 10);
                characterAnimator.Bind(brain);
            }
            if (definition.Kind == YokaiKind.Gangcheori)
            {
                var bodySprite = characterArtCatalog?.FindSprite("gangcheol_body");
                if (bodySprite != null)
                    yokaiObject.AddComponent<RuntimeGangcheoriBodyVisual>()
                        .Configure(
                            bodySprite,
                            characterArtCatalog?.FindSprite("gangcheol_pre_tail"),
                            characterArtCatalog?.FindSprite("gangcheol_post_tail"),
                            yokaiRenderer,
                            9);
            }
            var loot = yokaiObject.AddComponent<YokaiLoot>();
            loot.ConfigureForRuntime(definition, rewards: raid && baekjungScheduler?.IsActive == true
                ? baekjungRewardRules
                : null);
            var targetCounters = raidTarget as IYokaiCounterSource;
            var counters = placedObjectRuntime != null
                ? new CounterAuraSensor(yokaiObject.transform,
                    placedObjectRuntime.ActiveCounterAuras, targetCounters)
                : targetCounters;
            var selectedTarget = ResolveSpawnTarget(position, out var usesAggroRadius);
            var suppressFirstStrike = SuppressesSurfaceFirstStrike();
            brain.ConfigureForRuntime(
                definition, selectedTarget, counters, instanceSpawnTrack,
                gateByAggroRadius: usesAggroRadius || suppressFirstStrike,
                startEngaged: !usesAggroRadius && !suppressFirstStrike);
            if (definition.Kind == YokaiKind.Gangcheori)
            {
                var breath = yokaiObject.AddComponent<GangcheoriBreathController>();
                if (breath.ConfigureForRuntime(
                        definition, raidTarget, gameplayArtCatalog, yokaiRenderer))
                    brain.BindGangcheoriBreath(breath);
                else
                    Destroy(breath);
            }
            if (definition.Kind == YokaiKind.Gaekgwi && yokaiArt?.Sprite != null)
            {
                var presentation = visualObject.AddComponent<RuntimeGaekgwiVisual>();
                presentation.ConfigureForRuntime(brain, yokaiRenderer, characterAnimator, yokaiArt);
            }
            if (usesEoduksiniPresentation)
            {
                var presentation = visualObject.AddComponent<RuntimeEoduksiniVisual>();
                presentation.ConfigureForRuntime(brain, yokaiRenderer);
            }
            // Gangcheori's body and tail colliders are added after its movement core.
            physicsBody.IgnoreCollisionWith(raidTarget.transform);
            var healthBar = yokaiObject.AddComponent<RuntimeWorldHealthBar>();
            healthBar.ConfigureForRuntime(health, yokaiRenderer, usesEoduksiniPresentation ? 2f : 1f);
            health.Died += () => HandleYokaiEnded(health, true);
            brain.FledOffscreen += ignored => HandleYokaiEnded(health, false);
            spawnedYokai.Add(new SpawnedYokai
            {
                instanceId = instanceId,
                health = health,
                brain = brain,
                raid = raid,
                spawnTrack = instanceSpawnTrack
            });
            runtimeServices.Register(brain);
            return brain;
        }

        private void HandleYokaiEnded(Health health, bool killed)
        {
            var entry = spawnedYokai.FirstOrDefault(candidate => candidate.health == health);
            if (entry == null) return;
            spawnedYokai.Remove(entry);
            runtimeServices.Unregister(entry.brain);
            if (entry.spawnTrack == YokaiSpawnTrack.Resident)
            {
                if (killed && entry.brain?.Definition != null)
                    residentLastKilledDays[entry.brain.Definition.Kind] =
                        Mathf.Max(1, bootstrap.TimeService.Day);
            }
            else if (entry.raid) RaidSlotAvailable?.Invoke();
            else
            {
                TryFillRegularSlots();
            }
            if (health != null) Destroy(health.gameObject);
        }

        private bool TryGetSpawnPosition(WorldMobLocomotion locomotion, out Vector3 position)
        {
            position = default;
            if (bootstrap?.TileService == null || raidTarget == null) return false;
            var center = bootstrap.TileService.WorldToCell(raidTarget.transform.position);
            var maximumRange = Mathf.Max(minimumSpawnRange, maximumSpawnRange);
            var candidates = locomotion == WorldMobLocomotion.Grounded
                ? bootstrap.TileService.GetValidSurfaceSpawnPositions(
                    center, minimumSpawnRange, maximumRange)
                : bootstrap.TileService.GetValidSpawnPositions(
                    center, minimumSpawnRange, maximumRange);
            if (candidates.Count == 0) return false;
            var cell = candidates[spawnSequence % candidates.Count];
            position = bootstrap.TileService.GetCellCenterWorld(cell);
            return true;
        }

        private bool TryGetSpawnPosition(
            YokaiDefinition definition, WorldMobLocomotion locomotion, out Vector3 position)
        {
            position = default;
            if (definition == null || bootstrap?.TileService == null || raidTarget == null ||
                bootstrap.Session?.HasWorld != true)
                return false;
            var center = bootstrap.TileService.WorldToCell(raidTarget.transform.position);
            var maximumRange = Mathf.Max(minimumSpawnRange, maximumSpawnRange);
            var candidates = locomotion == WorldMobLocomotion.Grounded
                ? bootstrap.TileService.GetValidSurfaceSpawnPositions(
                    center, minimumSpawnRange, maximumRange)
                : bootstrap.TileService.GetValidSpawnPositions(
                    center, minimumSpawnRange, maximumRange);
            if (!TerrainSpawnRules.TryChooseCell(
                    definition, candidates, gameDataCatalog, bootstrap.TileService,
                    bootstrap.Session.LastResult, spawnSequence, out var cell))
                return false;
            position = bootstrap.TileService.GetCellCenterWorld(cell);
            return true;
        }

        private IYokaiTarget ResolveSpawnTarget(Vector3 spawnPosition, out bool usesAggroRadius)
        {
            usesAggroRadius = true;
            var coreCell = bootstrap?.SealSystem?.SealCoreCell;
            if (!coreCell.HasValue || coreRaidTarget == null || bootstrap?.TileService == null)
                return raidTarget;
            coreRaidTarget.SetCorePosition(bootstrap.TileService, coreCell.Value);
            if (!WorldV72Rules.ShouldTargetCore(
                    spawnPosition, coreRaidTarget.transform.position, baseVicinityRadius))
                return raidTarget;
            usesAggroRadius = false;
            return coreRaidTarget;
        }

        private float ReadPositiveGlobal(string key, float fallback)
        {
            var definition = gameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) && value > 0f
                ? value
                : fallback;
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

        private static YokaiSpawnTrack ResolveInstanceSpawnTrack(YokaiDefinition definition)
        {
            if (definition == null) return YokaiSpawnTrack.None;
            if (definition.SupportsSpawnTrack(YokaiSpawnTrack.Raid))
                return YokaiSpawnTrack.Raid;
            return definition.SupportsSpawnTrack(YokaiSpawnTrack.Resident)
                ? YokaiSpawnTrack.Resident
                : YokaiSpawnTrack.None;
        }

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
            record.frostSlowFraction >= 0f && record.frostSlowFraction <= 1f &&
            (!record.gaekgwiPatternInitialized ||
             record.gaekgwiPatternState >= 0 && record.gaekgwiPatternState <= 2 &&
             IsFiniteNonNegative(record.gaekgwiCooldownRemaining) &&
             IsFiniteNonNegative(record.gaekgwiTelegraphRemaining) &&
             IsFiniteNonNegative(record.gaekgwiDashRemaining) &&
             record.gaekgwiDashRemaining <= YokaiBrain.GaekgwiDashDistanceTiles + .0001f &&
             IsFinite(record.gaekgwiDashDirection));

        private bool ValidateStolenItems(YokaiStateRecord record)
        {
            if (record?.stolenItems == null || record.stolenItems.Count == 0) return true;
            var definition = gameDataCatalog?.FindYokai(record.yokaiId);
            if (definition?.Kind != YokaiKind.Yagwanggwi) return false;
            for (var index = 0; index < record.stolenItems.Count; index++)
            {
                var stack = record.stolenItems[index];
                var item = gameDataCatalog.FindItem(stack.itemId);
                if (item == null || stack.amount <= 0 || stack.amount > item.MaxStack)
                    return false;
            }
            return true;
        }

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
            GameEvents.OnDayStart -= HandleDayStart;
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
            ClearPendingRegular();
            ClearSpawnedYokai();
            bossPausedYokai.Clear();
            initialized = false;
        }

        private void ClearPendingRegular()
        {
            pendingRegular.Clear();
        }

        private void EnqueuePendingRegular(YokaiDefinition definition)
        {
            if (definition == null) return;
            pendingRegular.Enqueue(definition);
        }

        private static bool IsAlive(SpawnedYokai entry) =>
            entry != null && entry.health != null && !entry.health.IsDead && entry.brain != null;

        private bool SuppressesSurfaceFirstStrike()
        {
            if (runtimeServices?.ArtifactVerbs == null || runtimeServices.EquipmentSystem == null ||
                raidTarget == null || bootstrap?.TimeService == null || bootstrap.TileService == null)
                return false;
            var context = ArtifactActivationContextFactory.Build(
                bootstrap.TileService, raidTarget.transform.position, bootstrap.TimeService);
            return runtimeServices.ArtifactVerbs.SuppressesFirstStrike(
                runtimeServices.EquipmentSystem, context);
        }
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
