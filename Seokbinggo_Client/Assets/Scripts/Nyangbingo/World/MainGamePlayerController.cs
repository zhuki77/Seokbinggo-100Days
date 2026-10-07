using System;
using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.UI;
using Nyangbingo.Yokai;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Input = Nyangbingo.Core.GameplayInput;

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [RequireComponent(typeof(MeleeArcAttack), typeof(SpriteRenderer))]
    public sealed class MainGamePlayerController : MonoBehaviour
    {
        public const float GameplayCameraOrthographicSize = 8f;
        public const float PlayerVisualHeightTiles = 2f;
        // Leave clearance inside one-tile shafts and two-tile-high tunnels.
        public const float PlayerColliderWidthTiles = .8f;
        public const float PlayerColliderHeightTiles = 1.8f;
        // Keep the existing root-to-feet distance so old saves retain their standing height.
        public const float PlayerFeetBelowRoot = .38f;

        public static float ColliderFeetBelowRoot(BoxCollider2D collider) => collider != null
            ? (collider.size.y * .5f - collider.offset.y) * Mathf.Abs(collider.transform.lossyScale.y)
            : PlayerFeetBelowRoot;
        public const float FallDamageBounceHeightTiles = .5f;

        private const string MoveSpeedKey = "player_move_speed";
        private const string BareClawId = "bare_claw";
        private const string HapjukseonId = "hapjukseon";
        private const string CheolseonId = "cheolseon";
        private const string SeolpungseonId = FanItemIds.Seolpungseon;
        private const float SeolpungseonFrostSlowFraction = .3f;
        private const float SeolpungseonFrostSlowDurationSeconds = 2f;
        private const string LanternId = "lantern";
        private const string IronClawId = "iron_claw";
        private const string IceSteelClawId = "icesteel_claw";
        private const string IronClawMiningCriticalKey = "claw_t2_mine_crit";
        private const string IceSteelClawSlowKey = "claw_t3_slow";
        private const float IceSteelClawSlowDurationSeconds = 2f;
        private const string FallDamageThresholdKey = "fall_damage_threshold_tiles";
        private const string FallDamagePerTileKey = "fall_damage_per_tile";
        private const string IceShardItemId = "ice_shard";
        private const string IceShardTemperatureReliefKey = "ice_shard_temp_relief";
        private const string NestBedId = "nest_bed";
        private const float TearPouchPickupRadius = .75f;
        private const float CatnipHarvestRadius = 1.5f;
        // 채굴은 전투 사거리(1.5)와 분리. 본값은 globals.csv player_mining_reach_tiles.
        private const float DefaultMiningReach = 4f;
        public const float InsulationWallBareClawMiningSeconds = 3f;
        /// <summary>설치물 회수 채굴 시간(맨발톱 기준). 상위 발톱은 절반씩.</summary>
        public const float PlacedObjectBareClawMiningSeconds = 1f;
        // DevA 테스트 하니스와 동일: 마우스 칸 우선 + 플레이어 인접 미개봉 상자.
        private const float ChestInteractReach = 1.75f;
        private const float CollapseSeconds = 1.5f;
        private const float FadeOutSeconds = 1.75f;
        private const float FadeInSeconds = 1.75f;
        private const float BossKnockbackTravelSpeed = 12f;
        private const float BossKnockbackMinimumSeconds = .15f;
        private const float BossKnockbackMaximumSeconds = .6f;
        private const float BossKnockbackArcVelocityRatio = .7f;
        private const float AttackFeedbackRadius = .85f;
        private const float AttackFeedbackOriginHeight = .65f;
        private const float AttackFeedbackArtRotationDegrees = -90f;
        private const float SurfaceCameraOffsetRatio = .5f;
        private const float SurfaceCameraTransitionDepthTiles = 8f;

        [SerializeField] private GameDataCatalog catalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private Camera followCamera;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [Min(0f)][SerializeField] private float cameraFollowSharpness = 12f;
        [Min(0f)][SerializeField] private float coyoteTimeSeconds =
            PlayerMovementPhysics.DefaultCoyoteTimeSeconds;

        private float jumpVelocity;
        private float gravityAcceleration;
        private float maximumFallSpeed;
        private float jumpCutMultiplier;
        private float fallDamageThresholdTiles;
        private float fallDamagePerTile;
        private float iceShardTemperatureRelief;
        private float miningReach = DefaultMiningReach;
        private float fallPeakWorldY;
        private bool trackingFall;
        private bool fallDamageBounceAscending;

        private const float GroundProbeDistance = .08f;

        private readonly StatSheet statSheet = new StatSheet();
        private Rigidbody2D body;
        private BoxCollider2D playerCollider;
        private readonly RaycastHit2D[] groundProbeHits = new RaycastHit2D[8];
        private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];
        private Health health;
        private MeleeArcAttack attack;
        private WireSnareAbility wireSnare;
        private Vector2 movementInput;
        private Vector2 facing = Vector2.down;
        private Vector2 horizontalFacing = Vector2.right;
        private float verticalVelocity;
        private float bossKnockbackHorizontalVelocity;
        private float bossKnockbackRemainingSeconds;
        private bool grounded;
        private float coyoteTimeRemaining;
        private bool airJumpConsumed;
        private bool ropeClimbing;
        private bool ropeRegrabBlocked;
        private int ropeColumn;
        private float ropeVerticalInput;
        private bool miningActive;
        private string miningTreeId = string.Empty;
        private string miningRebarId = string.Empty;
        private string miningHempId = string.Empty;
        private string miningPlacedObjectId = string.Empty;
        private Vector3Int miningCell;
        private Vector3Int miningCompanionCell;
        private bool miningHasCompanion;
        private float miningElapsedSeconds;
        private float miningRequiredSeconds;
        private bool miningTargetVisible;
        private bool miningTargetMineable;
        private Vector3Int miningTargetCell;
        private Vector3Int miningFailureCell;
        private float miningFailureMessageUntil;
        private float baseMoveSpeed;
        private float currentMoveSpeed;
        private float attackCooldown;
        private bool lastBasicAttackHitTarget;
        private const float IceAccelerationSeconds = .65f;
        private const float IceCoastStopSeconds = 2f;
        private float oreEchoMessageUntil;
        private CombatProfileDefinition activeProfile;
        private CombatProfileDefinition lanternCarryProfile;
        private SpriteRenderer attackIndicator;
        private RuntimeCharacterSpriteAnimator characterAnimator;
        private bool visualWasGrounded = true;
        private float attackIndicatorRemaining;
        private int attackIndicatorFrameIndex;
        private float attackIndicatorFrameRemaining;
        private Vector2 attackIndicatorDirection = Vector2.right;
        private bool loggedFirstAttackInput;
        private bool loggedFirstAttackHit;
        private bool dead;
        private const int YeongnoSwallowWeakPointHits = 3;
        private static readonly Color SwallowedTint = new(.45f, .2f, .55f, .65f);
        private bool swallowedByYeongno;
        private float swallowRemainingSeconds;
        private float swallowTickRemaining;
        private float swallowTickInterval;
        private int swallowTickDamage;
        private int swallowWeakPointHits;
        private bool respawnApplied;
        private float deathSequenceElapsed;
        private bool deathPhysicsLocked;
        private bool bodySimulationBeforeDeath;
        private Vector2 initialSpawnPosition;
        private SpriteRenderer playerRenderer;
        private Transform playerVisualTransform;
        private Color aliveRendererColor;
        private Quaternion aliveRotation;
        private Image deathFadeImage;
        private GameObject deathFadeCanvas;
        private Light2D portableLanternLight;
        private Light2D personalVisionLight;
        private readonly Dictionary<string, GameObject> tearPouchVisuals =
            new Dictionary<string, GameObject>();
        private bool initialized;
        private MainGameEnvironmentState environmentState;
        private MainGameTurretRuntime placedObjectInteractions;
        private MainGameWorldDecorationRenderer worldDecorationRenderer;
        private MainGameTilePaletteController tilePalette;
        private MainGameRaidTarget raidTarget;
        private MainGameEncounterCoordinator encounterCoordinator;
        private MainGameWorldDropRuntime worldDropRuntime;
        private Nyangbingo.UI.MainGameBossSummonUiController interactionMessages;
        private Nyangbingo.UI.MainGameCraftingUiController storageUi;
        private MainGameParallaxBackground parallaxBackground;
        private TileService placementBlockerTileService;
        private CounterAuraSensor playerCounterAuraSensor;
        private readonly Vector3[] placementCellCorners = new Vector3[4];

        public bool IsInitialized => initialized;
        public string ActiveCombatProfileId => activeProfile != null ? activeProfile.Id : string.Empty;
        public bool IsUsingActiveSlotItem => runtimeServices?.ActiveSlot?.IsUsingEquippedItem == true;
        public float CurrentMoveSpeed => currentMoveSpeed * (runtimeServices?.Talismans?.MovementMultiplier ?? 1f);
        public Vector2 FacingDirection => facing;
        public Vector2 HorizontalFacingDirection => horizontalFacing;
        public bool IsGrounded => grounded;
        public bool IsClimbingRope => ropeClimbing;
        public float VerticalVelocity => verticalVelocity;
        public float MiningProgress => CalculateMiningProgress(miningElapsedSeconds, miningRequiredSeconds);
        public bool IsDead => dead;
        public bool IsSwallowedByYeongno => swallowedByYeongno;
        public event System.Action<bool> YeongnoSwallowEnded;

        public void ConfigureForScene(GameDataCatalog gameDataCatalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, Camera camera, CharacterArtCatalog artCatalog = null,
            GameplayArtCatalog gameplayArt = null)
        {
            catalog = gameDataCatalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            followCamera = camera;
            characterArtCatalog = artCatalog;
            gameplayArtCatalog = gameplayArt;
        }

        private void Start() => Initialize();

        public bool Initialize()
        {
            if (initialized) return true;
            bootstrap ??= GetComponentInParent<MainGameBootstrap>();
            runtimeServices ??= GetComponentInParent<MainGameRuntimeServices>();
            if (catalog == null) catalog = bootstrap != null ? bootstrap.GameDataCatalog : null;
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<BoxCollider2D>();
            health = GetComponent<Health>();
            attack = GetComponent<MeleeArcAttack>();
            environmentState = GetComponentInParent<MainGameEnvironmentState>();
            placedObjectInteractions = GetComponentInParent<MainGameTurretRuntime>();
            if (placedObjectInteractions != null)
                playerCounterAuraSensor = new CounterAuraSensor(
                    transform, placedObjectInteractions.ActiveCounterAuras,
                    dayNight: bootstrap?.TimeService);
            worldDecorationRenderer = GetComponentInParent<MainGameWorldDecorationRenderer>();
            tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            raidTarget = GetComponent<MainGameRaidTarget>();
            encounterCoordinator = GetComponentInParent<MainGameEncounterCoordinator>();
            interactionMessages = FindAnyObjectByType<Nyangbingo.UI.MainGameBossSummonUiController>();
            storageUi = FindAnyObjectByType<Nyangbingo.UI.MainGameCraftingUiController>();
            followCamera ??= Camera.main;
            if (followCamera != null && followCamera.orthographic)
                followCamera.orthographicSize = GameplayCameraOrthographicSize;
            parallaxBackground = followCamera != null
                ? followCamera.GetComponent<MainGameParallaxBackground>()
                : null;

            var moveSpeedDefinition = catalog != null ? catalog.FindGlobal(MoveSpeedKey) : null;
            var fallThresholdDefinition = catalog != null ? catalog.FindGlobal(FallDamageThresholdKey) : null;
            var fallDamageDefinition = catalog != null ? catalog.FindGlobal(FallDamagePerTileKey) : null;
            var iceShardReliefDefinition =
                catalog != null ? catalog.FindGlobal(IceShardTemperatureReliefKey) : null;
            var sealPenaltyStartDefinition =
                catalog != null ? catalog.FindGlobal(GlobalKeys.SealPenaltyStartDay) : null;
            var defaultProfile = catalog != null ? catalog.FindCombatProfile(BareClawId) : null;
            if (catalog == null || bootstrap == null || runtimeServices == null ||
                !runtimeServices.Initialize() || body == null || health == null || attack == null ||
                !runtimeServices.BindPlayerHealth(health) ||
                moveSpeedDefinition == null || !moveSpeedDefinition.TryGetFloat(out baseMoveSpeed) ||
                baseMoveSpeed <= 0f ||
                fallThresholdDefinition == null ||
                !fallThresholdDefinition.TryGetFloat(out fallDamageThresholdTiles) ||
                fallDamageThresholdTiles <= 0f ||
                fallDamageDefinition == null ||
                !fallDamageDefinition.TryGetFloat(out fallDamagePerTile) ||
                fallDamagePerTile <= 0f ||
                iceShardReliefDefinition == null ||
                !iceShardReliefDefinition.TryGetFloat(out iceShardTemperatureRelief) ||
                iceShardTemperatureRelief <= 0f ||
                sealPenaltyStartDefinition == null ||
                !sealPenaltyStartDefinition.TryGetInt(out var sealPenaltyStartDay) ||
                sealPenaltyStartDay <= 0 ||
                defaultProfile == null)
            {
                Debug.LogError("[Nyangbingo] MainGamePlayerController: 플레이어 이동·전투 필수 데이터가 준비되지 않았습니다.");
                return false;
            }
            runtimeServices.Talismans?.BindPlayer(transform);
            attack.SetOutgoingDamageAdjuster((health, damage) =>
                runtimeServices?.Traits?.AdjustMeleeDamage(health, damage) ?? damage);

            var physics = PlayerMovementPhysics.TryLoadFromCatalog(catalog, out var legacyPhysics)
                ? legacyPhysics
                : PlayerMovementPhysics.CreateDefault();
            jumpVelocity = physics.JumpVelocity;
            gravityAcceleration = physics.Gravity;
            maximumFallSpeed = physics.MaxFallSpeed;
            jumpCutMultiplier = physics.JumpCutMultiplier;

            miningReach = DefaultMiningReach;
            var miningReachDefinition = catalog.FindGlobal(GlobalKeys.PlayerMiningReachTiles);
            if (miningReachDefinition != null && miningReachDefinition.TryGetFloat(out var configuredReach) &&
                !float.IsNaN(configuredReach) && !float.IsInfinity(configuredReach) && configuredReach > 0f)
                miningReach = configuredReach;
            else
                Debug.LogWarning("[Nyangbingo] MainGamePlayerController: player_mining_reach_tiles missing; " +
                                 $"using default {DefaultMiningReach}.");

            ConfigurePhysicsBody(body, playerCollider);
            ApplyGeneratedWorldSpawn();
            var legacyPlayerRenderer = GetComponent<SpriteRenderer>();
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform, false);
            playerVisualTransform = visualObject.transform;
            playerRenderer = visualObject.AddComponent<SpriteRenderer>();
            if (legacyPlayerRenderer != null)
            {
                playerRenderer.sharedMaterial = legacyPlayerRenderer.sharedMaterial;
                playerRenderer.sortingLayerID = legacyPlayerRenderer.sortingLayerID;
                legacyPlayerRenderer.enabled = false;
                legacyPlayerRenderer.sprite = null;
            }
            var playerArt = characterArtCatalog != null ? characterArtCatalog.Find("player") : null;
            if (playerArt?.Sprite != null)
            {
                characterAnimator = visualObject.AddComponent<RuntimeCharacterSpriteAnimator>();
                characterAnimator.Configure(playerArt, 20);
            }
            else
                RuntimePlaceholderVisual.Configure(playerRenderer, new Color(.25f, .85f, 1f), .8f, 20);
            // Normalize the idle reference once, preserving aspect ratio and animation
            // squash/stretch. Do not scale the physics root or shared imported sprites.
            if (playerRenderer.sprite != null)
            {
                var cellHeight = bootstrap.TileService.GetCellWorldBounds(
                    bootstrap.TileService.WorldToCell(transform.position)).size.y;
                var referenceHeight = playerArt?.Sprite != null
                    ? playerArt.Sprite.bounds.size.y : playerRenderer.sprite.bounds.size.y;
                var parentHeightScale = Mathf.Abs(transform.lossyScale.y);
                if (referenceHeight > Mathf.Epsilon && parentHeightScale > Mathf.Epsilon)
                {
                    var visualScale = cellHeight * PlayerVisualHeightTiles /
                                      (referenceHeight * parentHeightScale);
                    playerVisualTransform.localScale = new Vector3(visualScale, visualScale, 1f);
                }
            }
            playerVisualTransform.localPosition = Vector3.up *
                RuntimeCharacterSpriteAnimator.CalculateGroundedVisualLocalY(
                    playerCollider, playerRenderer);
            initialSpawnPosition = transform.position;
            aliveRendererColor = playerRenderer.color;
            aliveRotation = transform.rotation;
            // Keep existing attack art size/offset independent from character art scale.
            var attackVisualRoot = new GameObject("AttackVisual").transform;
            attackVisualRoot.SetParent(transform, false);
            attackVisualRoot.localPosition = playerVisualTransform.localPosition;
            var indicatorObject = new GameObject("AttackIndicator");
            indicatorObject.transform.SetParent(attackVisualRoot, false);
            attackIndicator = indicatorObject.AddComponent<SpriteRenderer>();
            // The delivered art is rotated -90 degrees for a right-facing attack, so its
            // source Y axis becomes screen X. flipY is therefore the required screen-space
            // horizontal mirror that keeps the claw tips pointing away from the player.
            attackIndicator.flipY = true;
            var attackFrames = gameplayArtCatalog?.PlayerAttackFrames;
            if (attackFrames != null && attackFrames.Count > 0)
                RuntimePlaceholderVisual.ConfigureSprite(attackIndicator, attackFrames[0], 19);
            else
                RuntimePlaceholderVisual.Configure(attackIndicator, new Color(1f, .9f, .2f, .75f), .65f, 19);
            attackIndicator.enabled = false;

            var lanternLightObject = new GameObject("PortableLanternLight");
            lanternLightObject.transform.SetParent(transform, false);
            portableLanternLight = lanternLightObject.AddComponent<Light2D>();
            portableLanternLight.lightType = Light2D.LightType.Point;
            portableLanternLight.pointLightInnerRadius = runtimeServices.PortableLantern.RadiusTiles * .35f;
            portableLanternLight.pointLightOuterRadius = runtimeServices.PortableLantern.RadiusTiles * 1.15f;
            portableLanternLight.falloffIntensity = .45f;
            portableLanternLight.intensity = 1.15f;
            // Warm torch tone close to Terraria/Stardew lantern pools.
            portableLanternLight.color = new Color(1f, .78f, .42f, 1f);

            var visionLightObject = new GameObject("PersonalVisionLight");
            visionLightObject.transform.SetParent(transform, false);
            personalVisionLight = visionLightObject.AddComponent<Light2D>();
            personalVisionLight.lightType = Light2D.LightType.Point;
            personalVisionLight.falloffIntensity = .55f;
            personalVisionLight.intensity = .65f;
            // Tiger-eye bead's approved dokkaebi-fire aura color (#7FE3C3).
            personalVisionLight.color = new Color(127f / 255f, 227f / 255f, 195f / 255f, 1f);
            RefreshPortableLanternLight();

            worldDropRuntime = GetComponentInParent<MainGameWorldDropRuntime>();
            if (worldDropRuntime == null)
            {
                var dropObject = new GameObject("MainGameWorldDrops");
                dropObject.transform.SetParent(bootstrap.transform, false);
                worldDropRuntime = dropObject.AddComponent<MainGameWorldDropRuntime>();
            }
            var hud = FindAnyObjectByType<Nyangbingo.UI.MainGameHudController>();
            worldDropRuntime.ConfigureForRuntime(transform, runtimeServices.PlayerInventory,
                hud != null ? hud.BoundItemArtCatalog : null, bootstrap.TileService);
            raidTarget?.ConfigureTheftRuntime(runtimeServices.PlayerInventory,
                runtimeServices.EquipmentSystem, worldDropRuntime);
            if (raidTarget != null && !raidTarget.ConfigureWallPaceRuntime(
                    bootstrap, sealPenaltyStartDay))
            {
                Debug.LogError("[Nyangbingo] MainGamePlayerController: seal-pace wall damage binding failed.");
                return false;
            }
            if (!runtimeServices.BindMagpieCompanion(
                    transform, worldDropRuntime, characterArtCatalog))
            {
                Debug.LogError("[Nyangbingo] MainGamePlayerController: magpie companion runtime binding failed.");
                return false;
            }

            runtimeServices.PlayerInventory.Changed += RefreshCombatProfile;
            runtimeServices.ActiveSlot.Changed += RefreshCombatProfile;
            runtimeServices.PortableLantern.Changed += RefreshPortableLanternLight;
            runtimeServices.EquipmentSystem.Changed += RefreshEquipmentStats;
            runtimeServices.PlayerTemperature.RoomTemperatureChanged += HandleRoomTemperatureChanged;
            health.Died += HandleDied;
            runtimeServices.PlayerTemperature.ReachedMaximum += HandleTemperatureMaximum;
            runtimeServices.DeathTearPouches.Changed += RefreshTearPouchVisuals;
            GameEvents.OnDayStart += HandleArtifactContextChanged;
            GameEvents.OnNightStart += HandleArtifactContextChanged;
            wireSnare = new WireSnareAbility(attack);
            attack.KnockbackApplied += HandleAttackKnockbackApplied;
            if (GetComponent<ArtifactTunnelEdgePresenter>() == null)
                gameObject.AddComponent<ArtifactTunnelEdgePresenter>();
            RefreshEquipmentStats();
            RefreshCombatProfile();
            RefreshTearPouchVisuals();
            grounded = IsStandingOnForeground() && verticalVelocity <= 0f;
            coyoteTimeRemaining = grounded ? coyoteTimeSeconds : 0f;
            ResetFallTracking();
            initialized = activeProfile != null;
            if (initialized)
            {
                bootstrap.WorldReady += RebindForegroundPlacementBlocker;
                RebindForegroundPlacementBlocker();
                SnapCameraToPlayer();
                Debug.Log($"[Nyangbingo] MainGamePlayerController: 이동·체력·근접 공격·카메라 연결 완료 " +
                          $"(speed={currentMoveSpeed:0.##}, profile={ActiveCombatProfileId}).");
            }
            return initialized;
        }

        private void RebindForegroundPlacementBlocker()
        {
            var current = bootstrap?.TileService;
            if (ReferenceEquals(current, placementBlockerTileService)) return;
            placementBlockerTileService?.ClearForegroundPlacementBlocker(
                IsPlayerOverlappingForegroundCell);
            placementBlockerTileService = current;
            placementBlockerTileService?.SetForegroundPlacementBlocker(
                IsPlayerOverlappingForegroundCell, solidOnly: true);
        }

        private bool IsPlayerOverlappingForegroundCell(Vector3Int cell)
        {
            if (playerCollider == null || !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy)
                return false;

            var worldRenderer = bootstrap?.WorldRenderer;
            if (worldRenderer == null) return false;
            worldRenderer.GetCellWorldCorners(cell, placementCellCorners);

            var minimum = placementCellCorners[0];
            var maximum = placementCellCorners[0];
            for (var index = 1; index < placementCellCorners.Length; index++)
            {
                minimum = Vector3.Min(minimum, placementCellCorners[index]);
                maximum = Vector3.Max(maximum, placementCellCorners[index]);
            }

            return BoundsOverlapCell(playerCollider.bounds, minimum, maximum);
        }

        public static bool BoundsOverlapCell(Bounds playerBounds, Vector3 cellMinimum, Vector3 cellMaximum)
        {
            const float contactEpsilon = .001f;
            return playerBounds.min.x < cellMaximum.x - contactEpsilon &&
                   playerBounds.max.x > cellMinimum.x + contactEpsilon &&
                   playerBounds.min.y < cellMaximum.y - contactEpsilon &&
                   playerBounds.max.y > cellMinimum.y + contactEpsilon;
        }

        private void Update()
        {
            if (!initialized || runtimeServices == null || !runtimeServices.IsInitialized) return;
            RefreshPortableLanternLight();
            RefreshPlayerFireDamageMultiplier();
            if (dead)
            {
                movementInput = Vector2.zero;
                CancelMining();
                HideMiningTargetFeedback();
                TickDeathSequence(Time.deltaTime);
                return;
            }
            if (swallowedByYeongno)
            {
                TickYeongnoSwallowState(Time.deltaTime);
                return;
            }
            if (Nyangbingo.UI.MainGameCraftingUiController.BlocksPlayerMovement ||
                Nyangbingo.UI.MainGameBossSummonUiController.IsDebugShortcutHelpOpen)
            {
                movementInput = Vector2.zero;
                ropeVerticalInput = 0f;
                characterAnimator?.SetRopeClimbing(ropeClimbing, false);
                CancelMining();
                HideMiningTargetFeedback();
                characterAnimator?.SetMoving(false);
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentShortcuts.IsPressed(DevelopmentShortcut.Magpie))
            {
                var active = runtimeServices.MagpieCompanion?.ToggleEditorTestOverride() == true;
                interactionMessages?.ShowExternalMessage(
                    active ? "까치 테스트 활성화" : "까치 테스트 비활성화");
            }
#endif
            if (Input.GetKeyDown(KeyCode.Q) && runtimeServices.ActiveSlot.Toggle())
            {
                var activeItemId = runtimeServices.ActiveSlot.EquippedItemId;
                var statusMessage = !runtimeServices.ActiveSlot.IsUsingEquippedItem
                    ? "맨 발톱 활성"
                    : activeItemId == LanternId && !runtimeServices.PortableLantern.IsLit
                        ? "휴대용 등불 활성 · 연료 없음 (장비 화면에서 석탄 투입)"
                        : $"활성 장비: {catalog.ItemDisplayName(activeItemId, "장비")}";
                interactionMessages?.ShowExternalMessage(statusMessage);
            }
            UpdateAimDirection();
            runtimeServices.DeathTearPouches?.TryCollectWithin(transform.position, TearPouchPickupRadius);
            if (Input.GetKeyDown(KeyCode.E) && !MainGameTurretRuntime.BlocksCombatInput &&
                !MainGameTilePaletteController.BlocksGameplayInput)
            {
                var handled = TryInteractClosestWorldTarget(includePlacedObjects: true) ||
                      TryOpenRemoteJangdok();
                if (!handled)
                    interactionMessages?.ShowExternalMessage(
                        "가까이 있는 상호작용 대상을 찾지 못했습니다.");
                if (MainGameCraftingUiController.BlocksPlayerMovement)
                {
                    movementInput = Vector2.zero;
                    CancelMining();
                    return;
                }
            }
            movementInput = new Vector2(Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f), 0f);
            var ropeDetachedThisFrame = UpdateRopeInput();
            if (Mathf.Abs(movementInput.x) > Mathf.Epsilon)
            {
                horizontalFacing = movementInput.x < 0f ? Vector2.left : Vector2.right;
                // 공격이 끝난 뒤에도 입력이 없으면 마지막 공격 방향을 유지한다.
                // 이동 중에는 애니메이터의 공격 방향 잠금이 해제된 뒤 이동 방향을 적용한다.
                characterAnimator?.SetFacing(horizontalFacing);
            }
            if (!ropeClimbing && !ropeDetachedThisFrame &&
                (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
                 Input.GetKeyDown(KeyCode.Space)))
                TryJump();
            characterAnimator?.SetRopeClimbing(ropeClimbing,
                ropeClimbing && body != null && Mathf.Abs(body.linearVelocity.y) > .05f);
            if (!ropeClimbing && !visualWasGrounded && grounded)
                characterAnimator?.PlayLand();
            characterAnimator?.SetLocomotion(
                grounded, verticalVelocity > 0.05f, movementInput.sqrMagnitude > Mathf.Epsilon);
            visualWasGrounded = grounded;

            attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);
            wireSnare.Tick(Time.deltaTime);
            if (attackIndicatorRemaining > 0f)
            {
                attackIndicatorRemaining = Mathf.Max(0f, attackIndicatorRemaining - Time.deltaTime);
                TickAttackFeedback(Time.deltaTime);
                if (attackIndicatorRemaining <= 0f) attackIndicator.enabled = false;
            }
            var pointerOverUi = Input.IsPointerOverUi();
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            var buildingPlacementActive = MainGameTurretRuntime.BlocksCombatInput ||
                                          MainGameTilePaletteController.BlocksGameplayInput ||
                                          (tilePalette != null && tilePalette.ShouldBlockPrimaryForPlacement) ||
                                          MainGameHudController.BlocksWorldPrimaryInput;
            var primaryHeld = Input.GetMouseButton(0);
            if (!buildingPlacementActive && !pointerOverUi && primaryHeld &&
                !MainGameCraftingUiController.BlocksGameplayInput)
            {
                if (attackCooldown <= 0f)
                    TryBasicAttack();
                // 명중한 공격은 채굴보다 우선하며, 공격 사이 프레임에도 채굴이 누적되지 않는다.
                if (!IsClawMiningActive || lastBasicAttackHitTarget && attackCooldown > 0f)
                    CancelMining();
                else
                    TickMining();
            }
            else CancelMining();
            UpdateMiningTargetFeedback(!IsClawMiningActive || pointerOverUi || buildingPlacementActive ||
                                       (lastBasicAttackHitTarget && attackCooldown > 0f));
            // E interacts with world targets; right-click uses the selected item.
            if (!buildingPlacementActive && !pointerOverUi && Input.GetMouseButtonDown(1) &&
                !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                TryUseSelectedItem();
            }
            if (!buildingPlacementActive && !pointerOverUi && Input.GetKeyDown(KeyCode.F))
                TryFanAbility();
        }

        private bool TryUseSelectedItem() => TryUseSelectedIceShard() || TryUseSelectedTalisman() ||
            TryUseSelectedHealingItem() || TryPlantSelectedCatnip();

        private bool UpdateRopeInput()
        {
            ropeVerticalInput =
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (Mathf.Abs(ropeVerticalInput) < .01f) ropeRegrabBlocked = false;
            var leaving = Mathf.Abs(movementInput.x) > .01f || Input.GetKeyDown(KeyCode.Space);
            if (ropeClimbing && leaving)
            {
                var jumpOff = Input.GetKeyDown(KeyCode.Space);
                ExitRope();
                if (jumpOff)
                {
                    verticalVelocity = jumpVelocity;
                    fallDamageBounceAscending = false;
                    grounded = false;
                    BeginFallTracking(body.position.y);
                }
                return true;
            }
            if (ropeClimbing || leaving || ropeRegrabBlocked || Mathf.Abs(ropeVerticalInput) < .01f ||
                bossKnockbackRemainingSeconds > 0f || body == null || playerCollider == null)
                return false;
            var service = bootstrap?.TileService;
            if (service == null) return false;
            var column = service.WorldToCell(body.position).x;
            if (!HasRopeAtBody(column, body.position)) return false;
            ropeColumn = column;
            ropeClimbing = true;
            trackingFall = false;
            fallDamageBounceAscending = false;
            verticalVelocity = 0f;
            body.linearVelocity = Vector2.zero;
            coyoteTimeRemaining = 0f;
            airJumpConsumed = false;
            return false;
        }

        private bool HasRopeAtBody(int column, Vector2 rootPosition)
        {
            var service = bootstrap?.TileService;
            if (service == null || playerCollider == null || column < 0 || column >= service.Width)
                return false;
            var bounds = playerCollider.bounds;
            var offset = rootPosition - (Vector2)transform.position;
            var minimum = service.WorldToCell((Vector2)bounds.min + offset + Vector2.up * .02f).y;
            var maximum = service.WorldToCell((Vector2)bounds.max + offset - Vector2.up * .02f).y;
            minimum = Mathf.Max(0, minimum);
            maximum = Mathf.Min(service.Height - 1, maximum);
            for (var y = minimum; y <= maximum; y++)
            {
                var cell = new Vector3Int(column, y, 0);
                if (service.GetTile(cell).elementType != WorldTileTypes.Rope) continue;
                var center = service.GetCellCenterWorld(cell);
                if (Mathf.Abs(rootPosition.x - center.x) <= .55f) return true;
            }
            return false;
        }

        private bool TickRopeMovement(float deltaSeconds)
        {
            if (!ropeClimbing) return false;
            if (bossKnockbackRemainingSeconds > 0f || !HasRopeAtBody(ropeColumn, body.position))
            {
                ExitRope();
                return false;
            }
            var inputBlocked = MainGameCraftingUiController.BlocksPlayerMovement ||
                MainGameBossSummonUiController.IsDebugShortcutHelpOpen;
            var velocity = inputBlocked ? 0f : ropeVerticalInput * CurrentMoveSpeed;
            var nextPosition = body.position + Vector2.up * (velocity * deltaSeconds);
            if (!HasRopeAtBody(ropeColumn, nextPosition))
            {
                ExitRope();
                return false;
            }
            var service = bootstrap.TileService;
            var centerX = service.GetCellCenterWorld(new Vector3Int(ropeColumn, 0, 0)).x;
            // Use Rigidbody velocity, so aligning to a rope never teleports through a wall.
            var alignment = inputBlocked ? 0f : Mathf.Clamp(
                (centerX - body.position.x) / Mathf.Max(.001f, deltaSeconds),
                -CurrentMoveSpeed, CurrentMoveSpeed);
            grounded = false;
            trackingFall = false;
            fallPeakWorldY = body.position.y;
            verticalVelocity = velocity;
            body.linearVelocity = new Vector2(alignment, velocity);
            return true;
        }

        private void ExitRope()
        {
            if (!ropeClimbing) return;
            ropeClimbing = false;
            ropeRegrabBlocked = true;
            verticalVelocity = 0f;
            characterAnimator?.SetRopeClimbing(false, false);
            if (body != null)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
                BeginFallTracking(body.position.y);
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || dead || body == null || swallowedByYeongno) return;
            var deltaSeconds = Time.fixedDeltaTime;
            if (TickRopeMovement(deltaSeconds)) return;
            grounded = IsStandingOnForeground() && verticalVelocity <= 0f;
            coyoteTimeRemaining = PlayerMovementPhysics.TickCoyoteTime(
                grounded, coyoteTimeRemaining, coyoteTimeSeconds, deltaSeconds);
            if (grounded)
            {
                ResolveFallLanding(body.position.y);
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = 0f;
                    airJumpConsumed = false;
                }
            }
            else TrackAirborneHeight(body.position.y);
            // Fall-damage recoil is an automatic launch, not a variable-height player jump.
            // Applying the released-jump cut here would collapse a requested 0.5-tile bounce
            // to an almost invisible movement over the first two physics frames.
            var jumpHeld = fallDamageBounceAscending ||
                           bossKnockbackRemainingSeconds > 0f ||
                           IsJumpPressed();
            verticalVelocity = PlayerMovementPhysics.ApplyJumpCutWhileAscending(
                verticalVelocity, jumpHeld, jumpCutMultiplier);
            verticalVelocity = ApplyGravity(verticalVelocity, gravityAcceleration, maximumFallSpeed, deltaSeconds);
            if (fallDamageBounceAscending && verticalVelocity <= 0f)
                fallDamageBounceAscending = false;
            var horizontalInput = movementInput.x;
            if (attackCooldown > 0f && IsBowCombatProfile(activeProfile) &&
                !(runtimeServices?.ArtifactVerbs?.AllowsWalkWhileCharging(
                    runtimeServices.EquipmentSystem, BuildArtifactContext()) ?? false))
                horizontalInput = 0f;
            float horizontalVelocity;
            if (TryResolveIceSlideVelocity(horizontalInput, out var slideVelocity))
                horizontalVelocity = slideVelocity;
            else
                horizontalVelocity = CalculateHorizontalVelocity(horizontalInput, CurrentMoveSpeed);
            if (bossKnockbackRemainingSeconds > 0f)
            {
                horizontalVelocity = bossKnockbackHorizontalVelocity;
                bossKnockbackRemainingSeconds =
                    Mathf.Max(0f, bossKnockbackRemainingSeconds - deltaSeconds);
                if (bossKnockbackRemainingSeconds <= 0f)
                    bossKnockbackHorizontalVelocity = 0f;
            }
            body.linearVelocity = new Vector2(
                horizontalVelocity,
                verticalVelocity);
        }

        public bool TryApplyBossKnockback(Vector2 displacement)
        {
            if (!initialized || dead || body == null ||
                float.IsNaN(displacement.x) || float.IsInfinity(displacement.x) ||
                float.IsNaN(displacement.y) || float.IsInfinity(displacement.y) ||
                displacement.sqrMagnitude <= Mathf.Epsilon)
                return false;

            ExitRope();
            var horizontalDistance = Mathf.Abs(displacement.x);
            if (horizontalDistance > Mathf.Epsilon)
            {
                var duration = Mathf.Clamp(
                    horizontalDistance / BossKnockbackTravelSpeed,
                    BossKnockbackMinimumSeconds,
                    BossKnockbackMaximumSeconds);
                bossKnockbackHorizontalVelocity = displacement.x / duration;
                bossKnockbackRemainingSeconds = duration;
            }

            var horizontalArcVelocity = horizontalDistance > Mathf.Epsilon
                ? jumpVelocity * BossKnockbackArcVelocityRatio
                : 0f;
            var airborneVelocity = CalculateBossAirborneVelocity(
                Mathf.Max(0f, displacement.y), gravityAcceleration);
            if (airborneVelocity > Mathf.Epsilon && gravityAcceleration > Mathf.Epsilon)
            {
                var ascentSeconds = airborneVelocity / gravityAcceleration;
                bossKnockbackRemainingSeconds =
                    Mathf.Max(bossKnockbackRemainingSeconds, ascentSeconds);
            }

            verticalVelocity = Mathf.Max(
                verticalVelocity,
                Mathf.Max(horizontalArcVelocity, airborneVelocity));
            grounded = false;
            coyoteTimeRemaining = 0f;
            return true;
        }

        private void ApplyGeneratedWorldSpawn()
        {
            var session = bootstrap?.Session;
            if (session?.HasWorld != true || !session.LastResult.passedValidation) return;
            var cell = session.LastResult.spawnPoint;
            var halfExtent = ColliderFeetBelowRoot(playerCollider);
            var spawn = session.SafeSpawnResolver != null &&
                        session.SafeSpawnResolver.TryResolveSafeSurfaceSpawn(cell.x, halfExtent,
                            out var surfaceSpawn)
                ? surfaceSpawn
                : new Vector2(cell.x + .5f, cell.y + .5f);
            transform.position = spawn;
            body.position = spawn;
            Debug.Log($"[Nyangbingo] MainGamePlayerController: safe surface spawn applied " +
                      $"(generated={cell}, player={spawn}).");
        }

        private void UpdateAimDirection()
        {
            if (followCamera == null || body == null) return;
            var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
            var aim = (Vector2)mouse - body.position;
            if (aim.sqrMagnitude > Mathf.Epsilon) facing = aim.normalized;
        }

        private static bool IsJumpPressed() =>
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space);

        private void TryJump()
        {
            if (PlayerMovementPhysics.CanUseGroundJump(grounded, coyoteTimeRemaining))
            {
                fallDamageBounceAscending = false;
                verticalVelocity = jumpVelocity;
                grounded = false;
                coyoteTimeRemaining = 0f;
                airJumpConsumed = false;
                BeginFallTracking(body != null ? body.position.y : transform.position.y);
                return;
            }
            if (!statSheet.HasDoubleJump || airJumpConsumed) return;
            verticalVelocity = CalculateJumpVelocityForHeightRatio(jumpVelocity,
                statSheet.DoubleJumpHeightRatio);
            fallDamageBounceAscending = false;
            airJumpConsumed = true;
            // The v34 contract treats every jump as a new fall origin, so a late double jump
            // cushions the earlier drop even when it does not rise above the old apex.
            BeginFallTracking(body != null ? body.position.y : transform.position.y);
        }

        private bool IsStandingOnForeground()
        {
            return PlayerMovementPhysics.HasForegroundGroundSupport(playerCollider, GroundProbeDistance,
                groundProbeHits, groundContacts);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision == null) return;
            for (var index = 0; index < collision.contactCount; index++)
            {
                var normal = collision.GetContact(index).normal;
                if (normal.y > .5f && verticalVelocity <= 0f)
                {
                    var bounced = ResolveFallLanding(
                        body != null ? body.position.y : transform.position.y);
                    grounded = !bounced;
                    if (!bounced)
                    {
                        coyoteTimeRemaining = coyoteTimeSeconds;
                        verticalVelocity = 0f;
                        airJumpConsumed = false;
                    }
                }
                else if (normal.y < -.5f && verticalVelocity > 0f)
                {
                    verticalVelocity = 0f;
                    fallDamageBounceAscending = false;
                }
            }
        }

        public static float CalculateHorizontalVelocity(float input, float moveSpeed)
        {
            if (float.IsNaN(input) || float.IsInfinity(input) ||
                float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed)) return 0f;
            return Mathf.Clamp(input, -1f, 1f) * Mathf.Max(0f, moveSpeed);
        }

        public static void ConfigurePhysicsBody(Rigidbody2D targetBody, BoxCollider2D targetCollider)
        {
            if (targetBody == null) throw new System.ArgumentNullException(nameof(targetBody));
            if (targetCollider == null) throw new System.ArgumentNullException(nameof(targetCollider));

            targetBody.bodyType = RigidbodyType2D.Dynamic;
            targetBody.gravityScale = 0f;
            targetBody.freezeRotation = true;
            targetBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            targetBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            targetCollider.size = new Vector2(PlayerColliderWidthTiles, PlayerColliderHeightTiles);
            targetCollider.offset = new Vector2(0f, PlayerColliderHeightTiles * .5f - PlayerFeetBelowRoot);
            targetCollider.edgeRadius = 0f;
            targetCollider.sharedMaterial = PlayerMovementPhysics.ActorMovementMaterial;
            targetCollider.isTrigger = false;
        }

        public static float ApplyGravity(float currentVelocity, float gravity, float maxFallSpeed,
            float deltaSeconds) =>
            PlayerMovementPhysics.ApplyGravity(currentVelocity, gravity, maxFallSpeed, deltaSeconds);

        public static float CalculateJumpVelocityForHeightRatio(float baseJumpVelocity, float heightRatio) =>
            PlayerMovementPhysics.CalculateJumpVelocityForHeightRatio(baseJumpVelocity, heightRatio);

        public static float CalculateBossAirborneVelocity(float heightTiles, float gravity)
        {
            if (float.IsNaN(heightTiles) || float.IsInfinity(heightTiles) ||
                float.IsNaN(gravity) || float.IsInfinity(gravity) ||
                heightTiles <= 0f || gravity <= 0f)
                return 0f;
            return Mathf.Sqrt(2f * gravity * heightTiles);
        }

        public static float CalculateFallDamage(float fallTiles, float thresholdTiles,
            float damagePerTile)
        {
            if (float.IsNaN(fallTiles) || float.IsInfinity(fallTiles) ||
                float.IsNaN(thresholdTiles) || float.IsInfinity(thresholdTiles) ||
                float.IsNaN(damagePerTile) || float.IsInfinity(damagePerTile) ||
                fallTiles < thresholdTiles || thresholdTiles <= 0f || damagePerTile <= 0f)
                return 0f;
            return fallTiles * damagePerTile;
        }

        public static int CalculateAppliedFallDamage(float fallTiles, float thresholdTiles,
            float damagePerTile)
        {
            var rawDamage = CalculateFallDamage(fallTiles, thresholdTiles, damagePerTile);
            if (rawDamage <= 0f) return 0;
            if (rawDamage >= int.MaxValue) return int.MaxValue;
            // Health is integer-based. Resolve the design's half-HP samples with conventional
            // half-up rounding while preserving the exact floating-point formula above.
            return Mathf.Max(1, Mathf.FloorToInt(rawDamage + .5f));
        }

        private void BeginFallTracking(float worldY)
        {
            if (float.IsNaN(worldY) || float.IsInfinity(worldY)) return;
            trackingFall = true;
            fallPeakWorldY = worldY;
        }

        private void TrackAirborneHeight(float worldY)
        {
            if (float.IsNaN(worldY) || float.IsInfinity(worldY)) return;
            if (!trackingFall)
            {
                BeginFallTracking(worldY);
                return;
            }
            fallPeakWorldY = Mathf.Max(fallPeakWorldY, worldY);
        }

        private bool ResolveFallLanding(float landingWorldY)
        {
            if (!trackingFall || float.IsNaN(landingWorldY) || float.IsInfinity(landingWorldY))
                return false;
            var fallTiles = Mathf.Max(0f, fallPeakWorldY - landingWorldY);
            trackingFall = false;
            fallPeakWorldY = landingWorldY;
            var damage = CalculateAppliedFallDamage(
                fallTiles, fallDamageThresholdTiles, fallDamagePerTile);
            if (damage <= 0 || health == null || health.IsDead) return false;
            var healthBeforeDamage = health.Current;
            health.ApplyDamage(damage, DamageTag.Fall, DamageDelivery.Environmental);
            if (dead || health.IsDead || health.Current >= healthBeforeDamage) return false;

            verticalVelocity = CalculateBossAirborneVelocity(
                FallDamageBounceHeightTiles, gravityAcceleration);
            if (verticalVelocity <= Mathf.Epsilon) return false;
            fallDamageBounceAscending = true;
            grounded = false;
            coyoteTimeRemaining = 0f;
            BeginFallTracking(landingWorldY);
            if (body != null)
                body.linearVelocity = new Vector2(body.linearVelocity.x, verticalVelocity);
            return true;
        }

        private void ResetFallTracking()
        {
            ropeClimbing = false;
            ropeVerticalInput = 0f;
            ropeRegrabBlocked = false;
            characterAnimator?.SetRopeClimbing(false, false);
            trackingFall = false;
            fallPeakWorldY = body != null ? body.position.y : transform.position.y;
        }

        public static float CalculateSurfaceCameraVerticalOffset(float playerWorldY,
            float undergroundThreshold, float orthographicSize)
        {
            if (float.IsNaN(playerWorldY) || float.IsInfinity(playerWorldY) ||
                float.IsNaN(undergroundThreshold) || float.IsInfinity(undergroundThreshold) ||
                float.IsNaN(orthographicSize) || float.IsInfinity(orthographicSize) ||
                orthographicSize <= 0f)
                return 0f;

            var surfaceBlend = Mathf.Clamp01(
                (playerWorldY - undergroundThreshold) / SurfaceCameraTransitionDepthTiles);
            return orthographicSize * SurfaceCameraOffsetRatio * surfaceBlend;
        }

        public void SnapCameraToPlayer()
        {
            followCamera ??= Camera.main;
            if (followCamera == null) return;
            if (followCamera.orthographic)
                followCamera.orthographicSize = GameplayCameraOrthographicSize;
            followCamera.transform.position = ResolveCameraTargetPosition();
        }

        public bool ResetTransientStateAfterSaveRestore()
        {
            // MainGameSaveCoordinator performs its editor round-trip validation before this
            // component's normal Start/Initialize pass. There is no transient motion or death
            // state to clear yet in that phase, so it is already a valid reset.
            if (!initialized) return true;
            respawnApplied = false;
            deathSequenceElapsed = 0f;
            movementInput = Vector2.zero;
            CancelAttackFeedback();
            verticalVelocity = 0f;
            fallDamageBounceAscending = false;
            bossKnockbackHorizontalVelocity = 0f;
            bossKnockbackRemainingSeconds = 0f;
            attackCooldown = 0f;
            grounded = false;
            coyoteTimeRemaining = 0f;
            airJumpConsumed = false;
            if (body != null)
            {
                body.position = transform.position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            if (playerCollider != null) playerCollider.enabled = true;
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
            transform.rotation = aliveRotation;
            characterAnimator?.ResetToIdle();
            SetDeathFadeAlpha(0f);
            SnapCameraToPlayer();
            ResetFallTracking();
            RestoreDeathPhysics();
            dead = false;
            return true;
        }

        private Vector3 ResolveCameraTargetPosition()
        {
            var current = followCamera.transform.position;
            parallaxBackground ??= followCamera.GetComponent<MainGameParallaxBackground>();
            var verticalOffset = parallaxBackground != null && followCamera.orthographic
                ? CalculateSurfaceCameraVerticalOffset(transform.position.y,
                    parallaxBackground.UndergroundThreshold, followCamera.orthographicSize)
                : 0f;
            return new Vector3(transform.position.x, transform.position.y + verticalOffset, current.z);
        }

        private void LateUpdate()
        {
            if (!initialized || followCamera == null) return;
            var current = followCamera.transform.position;
            var target = ResolveCameraTargetPosition();
            var factor = cameraFollowSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
            followCamera.transform.position = Vector3.Lerp(current, target, factor);
        }

        private bool TryBasicAttack()
        {
            if (!AllowsPlayerBasicAttack(activeProfile))
                return false;
            var isBow = BowCombatRules.IsBowProfile(activeProfile);
            if (isBow && ResolveBowProjectileSprite() == null) return false;
            if (isBow &&
                !BowCombatRules.TryConsumeAmmo(runtimeServices?.PlayerInventory))
                return false;
            var direction = isBow ? facing.normalized : SnapAttackFeedbackDirection(facing);
            if (isBow)
                lastBasicAttackHitTarget = false;
            else if (EvolvedClawCombatRules.IsSangunClaw(activeProfile))
                attack.StrikeSangunCombo(direction, activeProfile);
            else
                attack.Strike(direction);
            if (!isBow) lastBasicAttackHitTarget = attack.LastHitCount > 0;
            PlayWeaponAttack();
            ShowAttackFeedback();
            if (EvolvedFanCombatRules.IsFanAbilityWeapon(activeProfile.Id))
                ShowFanWindFeedback(activeProfile.RangeTiles);
            attackCooldown = 1f / activeProfile.AttacksPerSecond;
            // 첫 스윙은 항상, 이후에는 실제 명중(hits>0)일 때만 로그 — hits는 "데미지 수치"가 아니라 맞은 대상 수.
            if (!loggedFirstAttackInput || !isBow && attack.LastHitCount > 0 && !loggedFirstAttackHit)
            {
                Debug.Log($"[Nyangbingo] Player attack accepted (profile={activeProfile.Id}, hits={(isBow ? 0 : attack.LastHitCount)}).");
                loggedFirstAttackInput = true;
                loggedFirstAttackHit |= !isBow && attack.LastHitCount > 0;
            }
            return true;
        }

        private MiningWorldTargetKind ResolveMiningWorldTarget(TileService tileService, out Vector3Int cell)
        {
            cell = default;
            var hasMouse = TryGetInteractionAimWorld(out var mouseAim);
            var aim = hasMouse ? mouseAim : (Vector2)transform.position;
            var origin = (Vector2)transform.position;
            var facingDir = SnapAttackFeedbackDirection(facing);
            Vector2? mouseWorld = hasMouse ? mouseAim : (Vector2?)null;
            var bestAimDist = float.PositiveInfinity;
            var bestKind = MiningWorldTargetKind.None;
            if (worldDecorationRenderer != null &&
                worldDecorationRenderer.TryResolveTreeMiningTarget(origin, facingDir, miningReach,
                    out _, out var treeCell))
            {
                ConsiderMiningTarget(CellAimPoint(treeCell), aim, MiningWorldTargetKind.Tree, ref bestAimDist, ref bestKind);
                if (bestKind == MiningWorldTargetKind.Tree) cell = treeCell;
            }
            if (worldDecorationRenderer != null &&
                worldDecorationRenderer.TryResolveRebarMiningTarget(origin, facingDir, miningReach,
                    out _, out var rebarCell))
            {
                ConsiderMiningTarget(CellAimPoint(rebarCell), aim, MiningWorldTargetKind.Rebar, ref bestAimDist, ref bestKind);
                if (bestKind == MiningWorldTargetKind.Rebar) cell = rebarCell;
            }
            if (worldDecorationRenderer != null &&
                worldDecorationRenderer.TryResolveHempMiningTarget(origin, mouseWorld, facingDir, miningReach,
                    out _, out var hempCell))
            {
                ConsiderMiningTarget(CellAimPoint(hempCell), aim, MiningWorldTargetKind.Hemp, ref bestAimDist, ref bestKind);
                if (bestKind == MiningWorldTargetKind.Hemp) cell = hempCell;
            }
            if (environmentState != null &&
                environmentState.TryResolvePlacedObjectMiningTarget(origin, mouseWorld, miningReach,
                    out _, out var placedCell))
            {
                ConsiderMiningTarget(aim, aim, MiningWorldTargetKind.PlacedObject, ref bestAimDist, ref bestKind);
                if (bestKind == MiningWorldTargetKind.PlacedObject) cell = placedCell;
            }
            // Cursor picking also returns air cells for background interaction. Air cannot
            // compete with a harvestable decoration in the primary mining target list.
            if (TryResolveMiningCell(tileService, out var tileCell) &&
                IsMineableForegroundCell(tileService, tileCell))
            {
                ConsiderMiningTarget(CellAimPoint(tileCell), aim, MiningWorldTargetKind.Tile, ref bestAimDist, ref bestKind);
                if (bestKind == MiningWorldTargetKind.Tile) cell = tileCell;
            }
            // 커서 가까운 장식·설치물도 앞쪽 전경 벽 너머에서 채굴하지 않는다.
            if (bestKind != MiningWorldTargetKind.None && bestKind != MiningWorldTargetKind.Tile &&
                TryFindFirstForegroundOnSegment(tileService, origin, CellAimPoint(cell), out var blockingCell) &&
                blockingCell != cell &&
                IsWithinMiningReach(tileService, origin, blockingCell, miningReach * miningReach))
            {
                cell = blockingCell;
                return MiningWorldTargetKind.Tile;
            }
            return bestKind;
        }

        private bool IsClawMiningActive => runtimeServices?.ActiveSlot != null &&
                                          !runtimeServices.ActiveSlot.IsUsingEquippedItem;

        private void TickMining()
        {
            if (!IsClawMiningActive)
            {
                CancelMining();
                HideMiningTargetFeedback();
                return;
            }
            var tileService = bootstrap?.TileService;
            // 채굴은 플레이어 입력 진행이라 DayNight TimeScale이 아니라 Unity deltaTime을 쓴다.
            // (공격 쿨다운과 동일 — 시계 정지/배속과 채굴 가능 여부가 어긋나지 않게)
            var miningDelta = Time.deltaTime;
            if (tileService == null || miningDelta <= 0f)
            {
                ResetMiningProgress();
                return;
            }
            var clawTier = ResolveMiningClawTier();
            var bestKind = ResolveMiningWorldTarget(tileService, out var bestTileCell);

            switch (bestKind)
            {
                case MiningWorldTargetKind.Tree:
                    if (TryTickTreeMining(clawTier, miningDelta)) return;
                    ResetMiningProgress();
                    return;
                case MiningWorldTargetKind.Rebar:
                    if (TryTickRebarMining(clawTier, miningDelta)) return;
                    ResetMiningProgress();
                    return;
                case MiningWorldTargetKind.Hemp:
                    if (TryTickHempMining(clawTier, miningDelta)) return;
                    ResetMiningProgress();
                    return;
                case MiningWorldTargetKind.PlacedObject:
                    if (TryTickPlacedObjectMining(clawTier, miningDelta)) return;
                    ResetMiningProgress();
                    return;
                case MiningWorldTargetKind.Tile:
                    break;
                default:
                    ResetMiningProgress();
                    return;
            }

            var cell = bestTileCell;
            var tile = tileService.GetTile(cell);
            var requiredSeconds = ResolveTileMiningSeconds(catalog, tile.elementType, clawTier);
            if (!tileService.InBounds(cell) || tile.IsAir || clawTier < tile.hardness || requiredSeconds <= 0f)
            {
                if (!tile.IsAir && clawTier < tile.hardness)
                    ShowMiningFailure(cell,
                        $"채굴 도구 등급 부족 · 필요 {tile.hardness}, 현재 {clawTier}");
                else if (!tile.IsAir)
                    ShowMiningFailure(cell, "현재 장비로 채굴할 수 없는 대상입니다.");
                ResetMiningProgress();
                return;
            }

            var companionCell = ResolveWideMiningCompanionCell(cell, transform.position.y);
            var companionRequiredSeconds = -1f;
            var hasCompanion = clawTier >= 3 && TryGetMiningSeconds(companionCell, clawTier,
                out companionRequiredSeconds);
            if (hasCompanion) requiredSeconds = Mathf.Max(requiredSeconds, companionRequiredSeconds);

            if (!miningActive || !string.IsNullOrEmpty(miningTreeId) ||
                !string.IsNullOrEmpty(miningRebarId) || !string.IsNullOrEmpty(miningHempId) ||
                !string.IsNullOrEmpty(miningPlacedObjectId) ||
                miningCell != cell ||
                miningHasCompanion != hasCompanion ||
                hasCompanion && miningCompanionCell != companionCell ||
                !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = string.Empty;
                miningHempId = string.Empty;
                miningPlacedObjectId = string.Empty;
                miningCell = cell;
                miningCompanionCell = companionCell;
                miningHasCompanion = hasCompanion;
                miningElapsedSeconds = 0f;
                miningRequiredSeconds = requiredSeconds;
            }

            miningElapsedSeconds = Mathf.Min(miningRequiredSeconds,
                miningElapsedSeconds + miningDelta);
            Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, MiningProgress);
            if (miningElapsedSeconds < miningRequiredSeconds) return;

            CompleteMining(miningCell, clawTier);
            if (miningHasCompanion) CompleteMining(miningCompanionCell, clawTier);
            ResetMiningProgress();
        }

        private bool TryTickTreeMining(int clawTier, float miningDelta)
        {
            if (worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryResolveTreeMiningTarget(transform.position,
                    SnapAttackFeedbackDirection(facing), miningReach, out var treeId, out var hitCell))
                return false;
            var definition = catalog?.FindMineralTier(MainGameWorldDecorationRenderer.WoodItemId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }
            if (!miningActive || !string.IsNullOrEmpty(miningRebarId) ||
                !string.IsNullOrEmpty(miningHempId) ||
                !string.IsNullOrEmpty(miningPlacedObjectId) ||
                !string.Equals(miningTreeId, treeId, System.StringComparison.Ordinal) ||
                miningCell != hitCell || !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = treeId;
                miningRebarId = string.Empty;
                miningHempId = string.Empty;
                miningPlacedObjectId = string.Empty;
                miningCell = hitCell;
                miningElapsedSeconds = 0f;
                miningRequiredSeconds = requiredSeconds;
            }
            miningElapsedSeconds = Mathf.Min(miningRequiredSeconds, miningElapsedSeconds + miningDelta);
            Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, MiningProgress);
            if (miningElapsedSeconds < miningRequiredSeconds) return true;
            CompleteTreeMining(miningTreeId, miningCell, clawTier);
            ResetMiningProgress();
            return true;
        }

        public static bool AllowsPlayerBasicAttack(CombatProfileDefinition profile) =>
            profile != null && profile.HasBasicAttack && profile.AttacksPerSecond > 0f &&
            profile.Id != HapjukseonId;

        public static bool IsBowCombatProfile(CombatProfileDefinition profile) =>
            BowCombatRules.IsBowProfile(profile);

        public bool TryBeginYeongnoSwallow(int tickDamage, float durationSeconds, float tickInterval)
        {
            if (!initialized || dead || tickDamage <= 0 || durationSeconds <= 0f || tickInterval <= 0f)
                return false;
            if (GimmickWeaponCombatRules.IsActiveProfile(this, GimmickWeaponProgress.YeongnoToothId))
                return false;
            if (TryArtifactEscapeSwallow())
                return false;

            ExitRope();
            swallowedByYeongno = true;
            swallowTickDamage = tickDamage;
            swallowRemainingSeconds = durationSeconds;
            swallowTickInterval = tickInterval;
            swallowTickRemaining = 0f;
            swallowWeakPointHits = 0;
            movementInput = Vector2.zero;
            CancelMining();
            verticalVelocity = 0f;
            if (body != null) body.linearVelocity = Vector2.zero;
            if (playerRenderer != null) playerRenderer.color = SwallowedTint;
            interactionMessages?.ShowExternalMessage("영노에게 삼켜졌습니다! 공격으로 약점을 부수세요.");
            ApplyYeongnoSwallowTick();
            return true;
        }

        public bool TryArtifactEscapeSwallow()
        {
            if (!initialized || dead || !swallowedByYeongno ||
                runtimeServices?.ArtifactVerbs == null || runtimeServices.EquipmentSystem == null)
                return false;
            if (!runtimeServices.ArtifactVerbs.CanEscapeOnSwallow(
                    runtimeServices.EquipmentSystem, BuildArtifactContext()))
                return false;
            ReleaseYeongnoSwallow("영노의 탈 — 삼킴에서 즉시 탈출했습니다.");
            Debug.Log("[Nyangbingo] Artifact yeongno_mask consumed swallow escape.");
            return true;
        }

        private void TickYeongnoSwallowState(float deltaSeconds)
        {
            movementInput = Vector2.zero;
            CancelMining();
            HideMiningTargetFeedback();
            characterAnimator?.SetMoving(false);
            attackCooldown = Mathf.Max(0f, attackCooldown - deltaSeconds);
            TickYeongnoSwallow(deltaSeconds);

            var pointerOverUi = Input.IsPointerOverUi();
            if (!pointerOverUi && Input.GetMouseButton(0) && attackCooldown <= 0f)
                TrySwallowWeakPointStrike();
        }

        private void TickYeongnoSwallow(float deltaSeconds)
        {
            swallowRemainingSeconds = Mathf.Max(0f, swallowRemainingSeconds - deltaSeconds);
            swallowTickRemaining -= deltaSeconds;
            while (swallowRemainingSeconds > 0f && swallowTickRemaining <= 0f)
            {
                ApplyYeongnoSwallowTick();
                swallowTickRemaining += swallowTickInterval;
            }
            if (swallowRemainingSeconds <= 0f)
                ReleaseYeongnoSwallow();
        }

        private void ApplyYeongnoSwallowTick()
        {
            if (!swallowedByYeongno || health == null || health.IsDead) return;
            var before = health.Current;
            health.ApplyDamage(swallowTickDamage, DamageTag.Melee);
            if (health.Current < before)
                GameEvents.RaisePlayerDamaged();
        }

        private bool TrySwallowWeakPointStrike()
        {
            if (!swallowedByYeongno) return false;
            characterAnimator?.PlayAttack();
            ShowAttackFeedback();
            attackCooldown = activeProfile != null && activeProfile.AttacksPerSecond > 0f
                ? 1f / activeProfile.AttacksPerSecond
                : .5f;
            swallowWeakPointHits++;
            if (swallowWeakPointHits >= YeongnoSwallowWeakPointHits)
                ReleaseYeongnoSwallow("영노의 약점을 부쉈습니다!", escapedByBreakingWeakPoint: true);
            return true;
        }

        private void ReleaseYeongnoSwallow(string message = null, bool escapedByBreakingWeakPoint = false)
        {
            if (!swallowedByYeongno) return;
            swallowedByYeongno = false;
            swallowRemainingSeconds = 0f;
            swallowTickRemaining = 0f;
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
            interactionMessages?.ShowExternalMessage(
                string.IsNullOrWhiteSpace(message) ? "영노 배에서 빠져나왔습니다." : message);
            YeongnoSwallowEnded?.Invoke(escapedByBreakingWeakPoint);
        }

        private void HandleAttackKnockbackApplied(Health target, float knockbackStrength)
        {
            if (target == null || knockbackStrength <= 0f ||
                runtimeServices?.ArtifactVerbs == null || runtimeServices.EquipmentSystem == null)
                return;
            if (!runtimeServices.ArtifactVerbs.CanGrabKnockedTarget(
                    runtimeServices.EquipmentSystem, BuildArtifactContext()))
                return;
            target.GetComponent<WorldMobPhysicsBody>()?.TryApplyKnockbackGrab(
                ArtifactVerbRuntime.KnockbackGrabSeconds);
        }

        private bool TryResolveIceSlideVelocity(float horizontalInput, out float horizontalVelocity)
        {
            horizontalVelocity = 0f;
            if (!grounded || !IsStandingOnIceLake())
            {
                return false;
            }
            var allowsTurn = runtimeServices?.ArtifactVerbs?.AllowsTurnWhileSliding(
                runtimeServices.EquipmentSystem, BuildArtifactContext()) ?? false;
            if (allowsTurn)
            {
                horizontalVelocity = CalculateHorizontalVelocity(horizontalInput, CurrentMoveSpeed);
            }
            else
            {
                // Use the velocity left by physics so wall contact cannot retain hidden momentum.
                // Opposite input brakes through zero before accelerating in the new direction.
                var speed = Mathf.Max(0f, CurrentMoveSpeed);
                var targetVelocity = CalculateHorizontalVelocity(horizontalInput, speed);
                var responseSeconds = Mathf.Abs(horizontalInput) > Mathf.Epsilon
                    ? IceAccelerationSeconds
                    : IceCoastStopSeconds;
                horizontalVelocity = Mathf.MoveTowards(body.linearVelocity.x, targetVelocity,
                    speed / responseSeconds * Time.fixedDeltaTime);
            }
            return true;
        }

        private bool IsStandingOnIceLake()
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null) return false;
            var cell = tileService.WorldToCell(transform.position);
            var groundCell = cell + Vector3Int.down;
            return tileService.InBounds(groundCell) &&
                   string.Equals(tileService.GetTile(groundCell).elementType, WorldTileTypes.IceLake,
                       StringComparison.Ordinal);
        }

        private void TryPresentIronVeinEcho(Vector3Int minedCell, string minedElementType)
        {
            if (Time.time < oreEchoMessageUntil ||
                runtimeServices?.ArtifactVerbs == null || runtimeServices.EquipmentSystem == null)
                return;
            if (!runtimeServices.ArtifactVerbs.HighlightsOreVeins(
                    runtimeServices.EquipmentSystem, BuildArtifactContext()))
                return;
            if (!TryFindIronVeinDirection(bootstrap?.TileService, minedCell, minedElementType,
                    out var direction)) return;
            oreEchoMessageUntil = Time.time + ArtifactVerbRuntime.OreEchoHighlightSeconds;
            interactionMessages?.ShowExternalMessage($"무쇠 식성 — 철 광맥이 {direction}에 있습니다.");
        }

        // Called after a successful break: use the captured material, not the now-empty cell.
        public static bool TryFindIronVeinDirection(TileService tileService, Vector3Int minedCell,
            string minedElementType, out string direction)
        {
            direction = null;
            if (tileService == null || !tileService.InBounds(minedCell) ||
                !string.Equals(ResolveMiningDefinitionId(minedElementType), WorldTileTypes.IronOre,
                    StringComparison.Ordinal)) return false;
            Vector3Int? bestCell = null;
            var bestDistance = float.PositiveInfinity;
            for (var dx = -8; dx <= 8; dx++)
            {
                for (var dy = -8; dy <= 8; dy++)
                {
                    var candidate = minedCell + new Vector3Int(dx, dy, 0);
                    if (candidate == minedCell) continue;
                    if (!tileService.InBounds(candidate)) continue;
                    var tile = tileService.GetTile(candidate);
                    if (tile.IsAir || !string.Equals(tile.elementType, WorldTileTypes.IronOre,
                            StringComparison.Ordinal)) continue;
                    var distance = (candidate - minedCell).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }
            if (!bestCell.HasValue) return false;
            var delta = bestCell.Value - minedCell;
            direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? delta.x > 0 ? "동쪽" : "서쪽"
                : delta.y > 0 ? "북쪽" : "남쪽";
            return true;
        }

        private bool TryTickRebarMining(int clawTier, float miningDelta)
        {
            if (worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryResolveRebarMiningTarget(transform.position,
                    SnapAttackFeedbackDirection(facing), miningReach, out var rebarId, out var hitCell))
                return false;
            var definition = catalog?.FindMineralTier(MainGameWorldDecorationRenderer.RebarItemId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }
            if (!miningActive || !string.IsNullOrEmpty(miningTreeId) ||
                !string.IsNullOrEmpty(miningHempId) ||
                !string.IsNullOrEmpty(miningPlacedObjectId) ||
                !string.Equals(miningRebarId, rebarId, System.StringComparison.Ordinal) ||
                miningCell != hitCell || !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = rebarId;
                miningHempId = string.Empty;
                miningPlacedObjectId = string.Empty;
                miningCell = hitCell;
                miningElapsedSeconds = 0f;
                miningRequiredSeconds = requiredSeconds;
            }
            miningElapsedSeconds = Mathf.Min(miningRequiredSeconds, miningElapsedSeconds + miningDelta);
            Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, MiningProgress);
            if (miningElapsedSeconds < miningRequiredSeconds) return true;
            CompleteRebarMining(miningRebarId, miningCell, clawTier);
            ResetMiningProgress();
            return true;
        }

        private bool TryTickHempMining(int clawTier, float miningDelta)
        {
            Vector2? mouseWorld = followCamera != null
                ? followCamera.ScreenToWorldPoint(Input.mousePosition)
                : null;
            if (worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryResolveHempMiningTarget(
                    transform.position, mouseWorld, SnapAttackFeedbackDirection(facing), miningReach,
                    out var hempId, out var hitCell))
                return false;
            var definition = catalog?.FindMineralTier(MainGameWorldDecorationRenderer.HempItemId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }
            if (!miningActive || !string.IsNullOrEmpty(miningTreeId) ||
                !string.IsNullOrEmpty(miningRebarId) ||
                !string.IsNullOrEmpty(miningPlacedObjectId) ||
                !string.Equals(miningHempId, hempId, StringComparison.Ordinal) ||
                miningCell != hitCell || !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = string.Empty;
                miningHempId = hempId;
                miningPlacedObjectId = string.Empty;
                miningCell = hitCell;
                miningElapsedSeconds = 0f;
                miningRequiredSeconds = requiredSeconds;
            }
            miningElapsedSeconds = Mathf.Min(miningRequiredSeconds, miningElapsedSeconds + miningDelta);
            Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, MiningProgress);
            if (miningElapsedSeconds < miningRequiredSeconds) return true;
            CompleteHempMining(miningHempId, miningCell, clawTier);
            ResetMiningProgress();
            return true;
        }

        private bool TryTickPlacedObjectMining(int clawTier, float miningDelta)
        {
            if (placedObjectInteractions == null || environmentState == null) return false;

            Vector2? mouseWorld = followCamera != null
                ? followCamera.ScreenToWorldPoint(Input.mousePosition)
                : null;
            if (!environmentState.TryResolvePlacedObjectMiningTarget(
                    transform.position, mouseWorld, miningReach, out var record, out var hitCell))
                return false;

            var requiredSeconds = ResolvePlacedObjectMiningSeconds(clawTier);
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }

            if (!miningActive || !string.IsNullOrEmpty(miningTreeId) ||
                !string.IsNullOrEmpty(miningRebarId) ||
                !string.IsNullOrEmpty(miningHempId) ||
                !string.Equals(miningPlacedObjectId, record.objectId, StringComparison.Ordinal) ||
                miningCell != hitCell ||
                !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = string.Empty;
                miningHempId = string.Empty;
                miningPlacedObjectId = record.objectId;
                miningCell = hitCell;
                miningElapsedSeconds = 0f;
                miningRequiredSeconds = requiredSeconds;
            }

            miningElapsedSeconds = Mathf.Min(miningRequiredSeconds, miningElapsedSeconds + miningDelta);
            Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, MiningProgress);
            if (miningElapsedSeconds < miningRequiredSeconds) return true;

            var toRecover = record;
            ResetMiningProgress();
            placedObjectInteractions.TryRecoverPlacedObject(toRecover);
            return true;
        }

        public static float ResolvePlacedObjectMiningSeconds(int clawTier)
        {
            if (clawTier < 1) return -1f;
            return PlacedObjectBareClawMiningSeconds /
                   Mathf.Pow(2f, Mathf.Clamp(clawTier - 1, 0, 2));
        }

        private enum MiningWorldTargetKind
        {
            None = 0,
            Tree,
            Rebar,
            Hemp,
            PlacedObject,
            Tile
        }

        private enum WorldInteractKind
        {
            None = 0,
            Catnip,
            Chest,
            PlacedObject
        }

        /// <summary>상호작용/채굴 조준용 마우스 월드 좌표. 카메라가 없으면 false.</summary>
        public bool TryGetInteractionAimWorld(out Vector2 aimWorld)
        {
            aimWorld = default;
            if (followCamera == null) return false;
            var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
            if (float.IsNaN(mouse.x) || float.IsInfinity(mouse.x) ||
                float.IsNaN(mouse.y) || float.IsInfinity(mouse.y))
                return false;
            aimWorld = mouse;
            return true;
        }

        private static void ConsiderMiningTarget(Vector2 targetWorld, Vector2 aim,
            MiningWorldTargetKind kind, ref float bestAimDist, ref MiningWorldTargetKind bestKind)
        {
            var aimDist = (targetWorld - aim).sqrMagnitude;
            if (aimDist > bestAimDist) return;
            bestAimDist = aimDist;
            bestKind = kind;
        }

        private Vector2 CellAimPoint(Vector3Int cell)
        {
            var tileService = bootstrap?.TileService;
            return tileService != null
                ? (Vector2)tileService.GetCellCenterWorld(cell)
                : new Vector2(cell.x + .5f, cell.y + .5f);
        }

        /// <summary>
        /// 사거리 안 자연 상호작용과 설치물 중 마우스에 가장 가까운 대상을 E로 고른다.
        /// </summary>
        private bool TryInteractClosestWorldTarget(bool includePlacedObjects)
        {
            var origin = (Vector2)transform.position;
            var aim = TryGetInteractionAimWorld(out var mouseAim) ? mouseAim : origin;
            var bestAimDist = float.PositiveInfinity;
            var bestKind = WorldInteractKind.None;
            var bestChestCell = default(Vector3Int);

            if (worldDecorationRenderer != null &&
                worldDecorationRenderer.TryFindCatnipInRange(origin, CatnipHarvestRadius, aim, out var catnipPos))
            {
                var aimDist = (catnipPos - aim).sqrMagnitude;
                if (aimDist < bestAimDist)
                {
                    bestAimDist = aimDist;
                    bestKind = WorldInteractKind.Catnip;
                }
            }

            var session = bootstrap?.Session;
            if (session != null && session.HasWorld &&
                TryFindChestClosestToAim(session, origin, aim, ChestInteractReach * ChestInteractReach,
                    out var chestCell, out var chestAimDist) &&
                chestAimDist < bestAimDist)
            {
                bestAimDist = chestAimDist;
                bestKind = WorldInteractKind.Chest;
                bestChestCell = chestCell;
            }

            if (includePlacedObjects && environmentState != null &&
                environmentState.TryGetNearestPlacedObject(origin, MainGameTurretRuntime.InteractionRange, aim,
                    out var placed))
            {
                var placedAimDist = (placed.position - aim).sqrMagnitude;
                if (placedAimDist < bestAimDist)
                    bestKind = WorldInteractKind.PlacedObject;
            }

            switch (bestKind)
            {
                case WorldInteractKind.Catnip:
                    return TryHarvestNearbyCatnip();
                case WorldInteractKind.Chest:
                    return TryOpenChestAt(bestChestCell);
                case WorldInteractKind.PlacedObject:
                    return placedObjectInteractions?.TryInteractNearestPlacedObject() == true;
                default:
                    return false;
            }
        }

        private bool TryInteractPlacedObjectAtPointer()
        {
            if (placedObjectInteractions == null ||
                !TryGetInteractionAimWorld(out var aimWorld)) return false;
            return placedObjectInteractions.TryInteractPlacedObjectClosestToAim(aimWorld);
        }

        /// <summary>
        /// 사거리 안 마우스 칸까지의 첫 전경 장애물을 선택한다.
        /// 가로막는 전경이 없으면 마우스 칸(배경 포함)을 그대로 선택한다.
        /// 커서가 사거리 밖일 때는 조준 방향의 첫 고체로 폴백한다.
        /// </summary>
        private bool TryResolveMiningCell(TileService tileService, out Vector3Int cell)
        {
            cell = default;
            if (tileService == null) return false;
            var origin = (Vector2)transform.position;
            Vector2? mouseWorld = followCamera != null
                ? followCamera.ScreenToWorldPoint(Input.mousePosition)
                : null;
            var direction = facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector2.down;
            return TryPickMiningCell(tileService, origin, mouseWorld, direction, miningReach, out cell);
        }

        /// <summary>
        /// 마우스까지의 선분에서 첫 전경을 선택하며, 장애물이 없으면 커서 칸을 선택한다.
        /// 커서가 없거나 사거리 밖일 때는 조준 방향의 첫 전경을 선택한다.
        /// </summary>
        public static bool TryPickMiningCell(TileService tileService, Vector2 playerOrigin,
            Vector2? mouseWorld, Vector2 facing, float miningReach, out Vector3Int cell)
        {
            cell = default;
            if (tileService == null || miningReach <= 0f ||
                float.IsNaN(miningReach) || float.IsInfinity(miningReach) ||
                float.IsNaN(playerOrigin.x) || float.IsInfinity(playerOrigin.x) ||
                float.IsNaN(playerOrigin.y) || float.IsInfinity(playerOrigin.y))
                return false;

            var direction = SnapAttackFeedbackDirection(facing);
            // MeleeArcAttack and the player's Rigidbody2D both use playerOrigin. The visual claw
            // has a separate hand-height offset and must never raise the authoritative mining ray.
            var attackOrigin = playerOrigin;
            var reachSq = miningReach * miningReach;

            if (mouseWorld.HasValue &&
                !float.IsNaN(mouseWorld.Value.x) && !float.IsInfinity(mouseWorld.Value.x) &&
                !float.IsNaN(mouseWorld.Value.y) && !float.IsInfinity(mouseWorld.Value.y))
            {
                var cursorCell = tileService.WorldToCell(mouseWorld.Value);
                if (tileService.InBounds(cursorCell) &&
                    IsWithinMiningReach(tileService, attackOrigin, cursorCell, reachSq))
                {
                    cell = TryFindFirstForegroundOnSegment(tileService, attackOrigin, mouseWorld.Value,
                        out var firstSolid) ? firstSolid : cursorCell;
                    return true;
                }
            }

            return TryFindFirstForegroundOnSegment(tileService, attackOrigin,
                       attackOrigin + direction * miningReach, out cell) &&
                   IsWithinMiningReach(tileService, attackOrigin, cell, reachSq);
        }

        private static bool TryFindFirstForegroundOnSegment(TileService tileService,
            Vector2 origin, Vector2 end, out Vector3Int cell)
        {
            cell = default;
            var current = tileService.WorldToCell(origin);
            var endCell = tileService.WorldToCell(end);
            var delta = end - origin;
            var bounds = tileService.GetCellWorldBounds(current);
            if (bounds.size.x <= 0f || bounds.size.y <= 0f) return false;
            var stepX = delta.x > 0f ? 1 : delta.x < 0f ? -1 : 0;
            var stepY = delta.y > 0f ? 1 : delta.y < 0f ? -1 : 0;
            var nextX = stepX == 0 ? float.PositiveInfinity
                : ((stepX > 0 ? bounds.max.x : bounds.min.x) - origin.x) / delta.x;
            var nextY = stepY == 0 ? float.PositiveInfinity
                : ((stepY > 0 ? bounds.max.y : bounds.min.y) - origin.y) / delta.y;
            var strideX = stepX == 0 ? float.PositiveInfinity : bounds.size.x / Mathf.Abs(delta.x);
            var strideY = stepY == 0 ? float.PositiveInfinity : bounds.size.y / Mathf.Abs(delta.y);
            // 격자 경계를 따라 검사해 고정 간격 샘플링이 놓치는 짧은 대각선 교차도 찾는다.
            var maxSteps = Mathf.Abs(endCell.x - current.x) + Mathf.Abs(endCell.y - current.y) + 1;
            for (var index = 0; index < maxSteps; index++)
            {
                if (IsMineableForegroundCell(tileService, current))
                {
                    cell = current;
                    return true;
                }
                if (current == endCell || Mathf.Min(nextX, nextY) > 1f) break;
                // 모서리만 스치는 옆 칸은 건너뛰고 실제로 진입하는 대각선 칸으로 이동한다.
                if (nextX == nextY)
                {
                    current.x += stepX;
                    current.y += stepY;
                    nextX += strideX;
                    nextY += strideY;
                }
                else if (nextX < nextY)
                {
                    current.x += stepX;
                    nextX += strideX;
                }
                else
                {
                    current.y += stepY;
                    nextY += strideY;
                }
            }
            return false;
        }

        private static bool IsMineableForegroundCell(TileService tileService, Vector3Int cell)
        {
            if (!tileService.InBounds(cell)) return false;
            return !tileService.GetTile(cell).IsAir;
        }

        private static bool IsWithinMiningReach(
            TileService tileService, Vector2 playerOrigin, Vector3Int cell, float reachSq)
        {
            var closest = (Vector2)tileService.GetCellWorldBounds(cell).ClosestPoint(playerOrigin);
            return (closest - playerOrigin).sqrMagnitude <= reachSq;
        }

        private bool TryGetMiningSeconds(Vector3Int cell, int clawTier, out float requiredSeconds)
        {
            requiredSeconds = -1f;
            var tileService = bootstrap?.TileService;
            if (tileService == null || !tileService.InBounds(cell)) return false;
            var tile = tileService.GetTile(cell);
            if (tile.IsAir || clawTier < tile.hardness) return false;
            requiredSeconds = ResolveTileMiningSeconds(catalog, tile.elementType, clawTier);
            return requiredSeconds > 0f;
        }

        private void CompleteMining(Vector3Int cell, int clawTier)
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null) return;
            var minedTile = tileService.GetTile(cell);
            var minedElementType = minedTile.elementType;
            // 파괴 전에 출처를 보존한다. 직접 설치한 타일은 재채굴로 수량이 늘어나지 않는다.
            var canCritical = minedTile.isNaturalTerrain;
            string itemId;
            int amount;
            using (ItemAcquisition.CaptureRequests())
                if (!tileService.TryBreakForeground(cell, clawTier, out itemId, out amount)) return;

            TryPresentIronVeinEcho(cell, minedElementType);

            var totalAmount = amount;
            var item = string.IsNullOrEmpty(itemId) ? null : catalog?.FindItem(itemId);
            var baseCriticalChance = 0f;
            var criticalDefinition = clawTier == 2 ? catalog?.FindGlobal(IronClawMiningCriticalKey) : null;
            if (criticalDefinition != null && criticalDefinition.TryGetFloat(out var configuredChance))
                baseCriticalChance = configuredChance;
            var criticalChance = CalculateMiningCriticalChance(
                baseCriticalChance + (runtimeServices?.Traits?.MiningCriticalBonus ?? 0f),
                statSheet.MiningCriticalChance);
            var critical = canCritical && item != null && amount > 0 &&
                           UnityEngine.Random.value < criticalChance;
            if (critical)
            {
                totalAmount += amount;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }

            if (item != null && totalAmount > 0)
            {
                WorldItemDropRequest.Request(item, totalAmount,
                    tileService.ResolveForegroundMiningDropWorldPosition(cell), cell);
                Nyangbingo.Core.GameEvents.RaiseMiningResult(cell, item.DisplayName, totalAmount, critical);
            }
        }

        private void CompleteTreeMining(string treeId, Vector3Int hitCell, int clawTier)
        {
            var item = catalog?.FindItem(MainGameWorldDecorationRenderer.WoodItemId);
            if (item == null || worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryHarvestTree(treeId, out _, out var dropPosition))
                return;
            var amount = 1;
            var criticalDefinition = clawTier == 2 ? catalog?.FindGlobal(IronClawMiningCriticalKey) : null;
            var baseCriticalChance = 0f;
            if (criticalDefinition != null && criticalDefinition.TryGetFloat(out var configuredChance))
                baseCriticalChance = configuredChance;
            var critical = UnityEngine.Random.value <
                           CalculateMiningCriticalChance(
                               baseCriticalChance + (runtimeServices?.Traits?.MiningCriticalBonus ?? 0f),
                               statSheet.MiningCriticalChance);
            if (critical)
            {
                amount++;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }
            WorldItemDropRequest.Request(item, amount, dropPosition);
            Nyangbingo.Core.GameEvents.RaiseMiningResult(
                hitCell, item.DisplayName, amount, critical);
        }

        private void CompleteRebarMining(string rebarId, Vector3Int hitCell, int clawTier)
        {
            var item = catalog?.FindItem(MainGameWorldDecorationRenderer.RebarItemId);
            if (item == null || worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryHarvestRebar(rebarId, out var dropPosition))
                return;
            var amount = 1;
            var criticalDefinition = clawTier == 2 ? catalog?.FindGlobal(IronClawMiningCriticalKey) : null;
            var baseCriticalChance = 0f;
            if (criticalDefinition != null && criticalDefinition.TryGetFloat(out var configuredChance))
                baseCriticalChance = configuredChance;
            var critical = UnityEngine.Random.value <
                           CalculateMiningCriticalChance(
                               baseCriticalChance + (runtimeServices?.Traits?.MiningCriticalBonus ?? 0f),
                               statSheet.MiningCriticalChance);
            if (critical)
            {
                amount++;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }
            WorldItemDropRequest.Request(item, amount, dropPosition);
            Nyangbingo.Core.GameEvents.RaiseMiningResult(
                hitCell, item.DisplayName, amount, critical);
        }

        private void CompleteHempMining(string hempId, Vector3Int hitCell, int clawTier)
        {
            var item = catalog?.FindItem(MainGameWorldDecorationRenderer.HempItemId);
            if (item == null || worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryHarvestHemp(hempId, out var dropPosition))
                return;
            var amount = 1;
            var criticalDefinition = clawTier == 2 ? catalog?.FindGlobal(IronClawMiningCriticalKey) : null;
            var baseCriticalChance = 0f;
            if (criticalDefinition != null && criticalDefinition.TryGetFloat(out var configuredChance))
                baseCriticalChance = configuredChance;
            var critical = UnityEngine.Random.value <
                           CalculateMiningCriticalChance(
                               baseCriticalChance + (runtimeServices?.Traits?.MiningCriticalBonus ?? 0f),
                               statSheet.MiningCriticalChance);
            if (critical)
            {
                amount++;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }
            WorldItemDropRequest.Request(item, amount, dropPosition);
            Nyangbingo.Core.GameEvents.RaiseMiningResult(
                hitCell, item.DisplayName, amount, critical);
        }

        private void CancelMining()
        {
            ResetMiningProgress();
        }

        private void ResetMiningProgress()
        {
            if (miningActive)
                Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, 0f);
            miningActive = false;
            miningTreeId = string.Empty;
            miningRebarId = string.Empty;
            miningHempId = string.Empty;
            miningPlacedObjectId = string.Empty;
            miningHasCompanion = false;
            miningElapsedSeconds = 0f;
            miningRequiredSeconds = 0f;
        }

        private void UpdateMiningTargetFeedback(bool blocked)
        {
            var tileService = bootstrap?.TileService;
            if (!IsClawMiningActive || blocked || tileService == null)
            {
                HideMiningTargetFeedback();
                return;
            }

            var kind = ResolveMiningWorldTarget(tileService, out var cell);
            if (kind == MiningWorldTargetKind.None)
            {
                HideMiningTargetFeedback();
                return;
            }
            var clawTier = ResolveMiningClawTier();
            var tile = tileService.GetTile(cell);
            var mineable = kind == MiningWorldTargetKind.Tile
                ? !tile.IsAir && clawTier >= tile.hardness &&
                  ResolveTileMiningSeconds(catalog, tile.elementType, clawTier) > 0f
                : ResolvePlacedObjectMiningSeconds(clawTier) > 0f;
            if (miningTargetVisible && miningTargetCell == cell &&
                miningTargetMineable == mineable)
                return;
            miningTargetVisible = true;
            miningTargetCell = cell;
            miningTargetMineable = mineable;
            GameEvents.RaiseMiningTargetChanged(cell, true, mineable);
        }

        private void HideMiningTargetFeedback()
        {
            if (!miningTargetVisible) return;
            miningTargetVisible = false;
            GameEvents.RaiseMiningTargetChanged(miningTargetCell, false, false);
        }

        private void ShowMiningFailure(Vector3Int cell, string message)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                miningFailureCell == cell && Time.unscaledTime < miningFailureMessageUntil)
                return;
            miningFailureCell = cell;
            miningFailureMessageUntil = Time.unscaledTime + 1.5f;
            interactionMessages?.ShowExternalMessage(message);
        }

        public static string ResolveMiningDefinitionId(string elementType) => elementType switch
        {
            WorldTileTypes.StoneMid => WorldTileTypes.Stone,
            WorldTileTypes.StoneDeep => WorldTileTypes.Stone,
            WorldTileTypes.RuinWall => WorldTileTypes.Stone,
            WorldTileTypes.IceLake => WorldTileTypes.IceShard,
            _ => elementType
        };

        public static float ResolveTileMiningSeconds(
            GameDataCatalog dataCatalog, string elementType, int clawTier)
        {
            // Rope is a new installed traversal item; it has no mineral or crafting recipe.
            if (elementType == WorldTileTypes.Rope)
                return clawTier < 1 ? -1f : PlacedObjectBareClawMiningSeconds /
                    Mathf.Pow(2f, Mathf.Clamp(clawTier - 1, 0, 2));
            if (string.Equals(elementType, "insul_wall", System.StringComparison.Ordinal))
            {
                if (clawTier < 1) return -1f;
                // The approved seal-balance calculation fixes 25 T1 walls at 75 seconds.
                // Preserve the standard claw progression where each tier halves mining time.
                return InsulationWallBareClawMiningSeconds /
                       Mathf.Pow(2f, Mathf.Clamp(clawTier - 1, 0, 2));
            }

            var definition = dataCatalog?.FindMineralTier(ResolveMiningDefinitionId(elementType));
            return definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
        }

        public static Vector3Int ResolveWideMiningCompanionCell(Vector3Int primaryCell, float playerWorldY)
        {
            if (float.IsNaN(playerWorldY) || float.IsInfinity(playerWorldY)) return primaryCell + Vector3Int.up;
            return primaryCell.y + .5f < playerWorldY
                ? primaryCell + Vector3Int.down
                : primaryCell + Vector3Int.up;
        }

        public static float CalculateMiningProgress(float elapsedSeconds, float requiredSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) ||
                float.IsNaN(requiredSeconds) || float.IsInfinity(requiredSeconds) || requiredSeconds <= 0f)
                return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / requiredSeconds);
        }

        private int ResolveMiningClawTier()
        {
            var inventory = runtimeServices?.PlayerInventory;
            if (inventory?.Has(IceSteelClawId, 1) == true) return 3;
            if (inventory?.Has(IronClawId, 1) == true) return 2;
            return 1;
        }

        public static float CalculateMiningCriticalChance(float clawChance, float equipmentChance)
        {
            if (float.IsNaN(clawChance) || float.IsInfinity(clawChance)) clawChance = 0f;
            if (float.IsNaN(equipmentChance) || float.IsInfinity(equipmentChance)) equipmentChance = 0f;
            return Mathf.Clamp(clawChance + equipmentChance, 0f, .25f);
        }

        private void TryFanAbility()
        {
            if (activeProfile == null ||
                !EvolvedFanCombatRules.IsFanAbilityWeapon(activeProfile.Id)) return;
            if (activeProfile.Id == SeolpungseonId)
                attack.ConfigureFrostSlow(SeolpungseonFrostSlowFraction, SeolpungseonFrostSlowDurationSeconds);
            else
                attack.ConfigureFrostSlow(0f, 0f);
            var knockback = EvolvedFanCombatRules.ResolveAbilityKnockback(activeProfile);
            if (wireSnare.TryUse(facing, ResolveFanAbilityDamage(activeProfile.Id), knockback))
            {
                if (activeProfile.Id == SeolpungseonId)
                    attack.ConfigureFrostSlow(0f, 0f);
                PlayWeaponAttack();
                ShowAttackFeedback();
                ShowFanWindFeedback(EvolvedFanCombatRules.ResolveAbilityRange(activeProfile));
            }
        }

        public static int ResolveFanAbilityDamage(string combatProfileId) =>
            combatProfileId == CheolseonId || combatProfileId == SeolpungseonId ||
            combatProfileId == FanItemIds.SeongeFan ||
            combatProfileId == FanItemIds.IceRootWhipfan ||
            combatProfileId == FanItemIds.ColdWaveFan
                ? WireSnareAbility.CheolseonDamage
                : WireSnareAbility.HapjukseonDamage;

        private void ShowAttackFeedback()
        {
            if (BowCombatRules.IsBowProfile(activeProfile))
            {
                ShowBowProjectile();
                return;
            }
            if (gameplayArtCatalog?.FindWeaponAttackFrames(activeProfile?.Id).Count > 0)
            {
                attackIndicatorRemaining = 0f;
                if (attackIndicator != null) attackIndicator.enabled = false;
                return;
            }
            if (attackIndicator == null) return;
            attackIndicatorDirection = SnapAttackFeedbackDirection(facing);
            var attackAngle = CalculateAttackFeedbackRotationDegrees(attackIndicatorDirection);
            attackIndicator.transform.localRotation = Quaternion.Euler(0f, 0f, attackAngle);
            attackIndicator.enabled = true;
            attackIndicatorFrameRemaining = .1f;
            var frames = gameplayArtCatalog?.PlayerAttackFrames;
            attackIndicatorFrameIndex = frames != null && frames.Count > 0
                ? frames.Count - 1
                : 0;
            if (frames != null && frames.Count > 0)
                attackIndicator.sprite = frames[attackIndicatorFrameIndex];
            PositionAttackFeedback();
            attackIndicatorRemaining = frames != null && frames.Count > 0
                ? Mathf.Max(.12f, frames.Count * .1f)
                : .12f;
        }

        private void PlayWeaponAttack()
        {
            var frames = gameplayArtCatalog?.FindWeaponAttackFrames(activeProfile?.Id);
            var direction = BowCombatRules.IsBowProfile(activeProfile)
                ? facing.normalized : SnapAttackFeedbackDirection(facing);
            characterAnimator?.PlayWeaponAttack(frames, ResolveWeaponAttackDuration(frames?.Count ?? 0));
            characterAnimator?.HoldAttackFacing(direction);
        }

        private float ResolveWeaponAttackDuration(int frameCount)
        {
            var duration = frameCount * .1f;
            return activeProfile != null && !EvolvedFanCombatRules.IsFanAbilityWeapon(activeProfile.Id) &&
                   activeProfile.AttacksPerSecond > 0f
                ? Mathf.Min(duration, 1f / activeProfile.AttacksPerSecond) : duration;
        }

        private void ShowFanWindFeedback(float attackRange)
        {
            var frames = gameplayArtCatalog?.FanWindFrames;
            if (frames == null || frames.Count == 0 || playerRenderer == null ||
                float.IsNaN(attackRange) || float.IsInfinity(attackRange) || attackRange <= 0f) return;
            var directionX = Mathf.Abs(facing.x) > Mathf.Epsilon ? Mathf.Sign(facing.x) : horizontalFacing.x;
            StartCoroutine(AnimateFanWind(frames, directionX, attackRange));
        }

        private System.Collections.IEnumerator AnimateFanWind(
            IReadOnlyList<Sprite> frames, float directionX, float range)
        {
            // 부채를 펼치는 몸체 프레임 뒤에 바람이 나온다. 이펙트는 추가 타격을 만들지 않는다.
            yield return new WaitForSeconds(.2f);
            if (dead || swallowedByYeongno || playerRenderer == null) yield break;
            var wind = new GameObject("FanAttackWind");
            var renderer = wind.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = playerRenderer.sharedMaterial;
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + 1;
            renderer.flipX = directionX < 0f;
            var bottomY = playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
            var rangeEndX = transform.position.x + directionX * range;
            // 기존 +0.02에서 원본 1픽셀(1/16타일) 아래로 내려 바닥과 맞춘다.
            var effectY = bottomY - 1f / 16f;
            var frameWait = new WaitForSeconds(.15f);
            foreach (var frame in frames)
            {
                if (dead || swallowedByYeongno) break;
                renderer.sprite = frame;
                if (frame != null)
                {
                    // trim된 프레임의 전방 끝을 기준으로 맞춰 좌우 모두 사거리 끝에 닿게 한다.
                    var frontExtent = frame.bounds.max.x;
                    wind.transform.position = new Vector3(
                        rangeEndX - directionX * frontExtent, effectY, transform.position.z);
                }
                yield return frameWait;
            }
            Destroy(wind);
        }

        private Sprite ResolveBowProjectileSprite() => activeProfile?.Id == BowCombatRules.StrawSlingId
            ? gameplayArtCatalog?.SlingStoneProjectile : gameplayArtCatalog?.ArrowProjectile;

        // 탄약은 입력 승인 시 한 번만 소비하고 피해는 비행 중 충돌 시점에만 발생한다.
        private void ShowBowProjectile()
        {
            if (attackIndicator != null) attackIndicator.enabled = false;
            attackIndicatorRemaining = 0f;
            var sprite = ResolveBowProjectileSprite();
            if (sprite == null || playerRenderer == null) return;
            var direction = facing.normalized;
            Vector2? aimWorld = TryGetInteractionAimWorld(out var mouseAim) ? mouseAim : null;
            var frameCount = gameplayArtCatalog.FindWeaponAttackFrames(activeProfile.Id).Count;
            var releaseFrame = activeProfile.Id == BowCombatRules.IceRootBowId ? 6 :
                activeProfile.Id == BowCombatRules.StrawSlingId ? 4 : 3;
            var delay = frameCount > 0
                ? ResolveWeaponAttackDuration(frameCount) / frameCount * releaseFrame : 0f;
            StartCoroutine(AnimateBowProjectile(sprite, direction, Mathf.Max(.1f, activeProfile.RangeTiles),
                activeProfile.Id == BowCombatRules.StrawSlingId, delay, attack.CaptureProjectileHitContext(), aimWorld));
        }

        private System.Collections.IEnumerator AnimateBowProjectile(
            Sprite sprite, Vector2 direction, float range, bool stone, float delay,
            MeleeArcAttack.ProjectileHitContext shot, Vector2? aimWorld)
        {
            // 활을 당긴 뒤 발사한다. 발사 전에는 피해를 주지 않는다.
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (dead || swallowedByYeongno || playerRenderer == null) yield break;
            var start = (Vector2)transform.position + Vector2.up * AttackFeedbackOriginHeight;
            var aimedDirection = aimWorld.HasValue ? aimWorld.Value - start : direction;
            if (aimedDirection.sqrMagnitude > Mathf.Epsilon) direction = aimedDirection.normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon) direction = horizontalFacing;
            var projectile = new GameObject("PlayerProjectile");
            // 플레이어 이동을 상속하지 않는 물리 루트. 타이틀 전환 시 씬과 함께 정리된다.
            projectile.transform.position = start;
            var projectileBody = projectile.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Dynamic;
            projectileBody.gravityScale = .85f;
            projectileBody.mass = 1f;
            projectileBody.linearDamping = 0f;
            projectileBody.angularDamping = 0f;
            projectileBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            projectileBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            projectileBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            const float hitRadius = .1f;
            var projectileCollider = projectile.AddComponent<CircleCollider2D>();
            projectileCollider.radius = hitRadius;
            projectileCollider.isTrigger = true;
            var visual = new GameObject("Art");
            visual.transform.SetParent(projectile.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = playerRenderer.sharedMaterial;
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + 1;
            // 원본 화살은 왼쪽을 향한다.
            visual.transform.localRotation = Quaternion.Euler(0f, 0f,
                stone ? 0f : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
            visual.transform.localPosition = -(visual.transform.localRotation * sprite.bounds.center);
            const float speed = 14f;
            projectileBody.AddForce(direction * (speed * projectileBody.mass), ForceMode2D.Impulse);
            var travelled = 0f;
            var elapsed = 0f;
            var maximumLifetime = range / speed + 1f;
            var previous = start;
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(shot.TargetLayers);
            var hits = new List<RaycastHit2D>();
            var waitForPhysics = new WaitForFixedUpdate();
            while (!dead && !swallowedByYeongno && travelled < range && !shot.Finished &&
                   projectileBody != null && elapsed < maximumLifetime)
            {
                yield return waitForPhysics;
                if (dead || swallowedByYeongno || projectileBody == null) break;
                elapsed += Time.fixedDeltaTime;
                if (!stone && projectileBody.linearVelocity.sqrMagnitude > Mathf.Epsilon)
                {
                    var velocity = projectileBody.linearVelocity;
                    visual.transform.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 180f);
                    visual.transform.localPosition = -(visual.transform.localRotation * sprite.bounds.center);
                }
                var displacement = projectileBody.position - previous;
                var distance = displacement.magnitude;
                if (distance <= Mathf.Epsilon) continue;
                var stepDirection = displacement / distance;
                var step = Mathf.Min(distance, range - travelled);
                Physics2D.SyncTransforms();
                hits.Clear();
                // AddForce로 이동한 실제 물리 구간 전체를 검사해 작은 몬스터 관통 누락을 막는다.
                Physics2D.CircleCast(previous, hitRadius, stepDirection, filter, hits, step);
                hits.Sort((a, b) => a.distance.CompareTo(b.distance));
                var blocked = false;
                foreach (var hit in hits)
                {
                    var collider = hit.collider;
                    if (collider == null || collider == projectileCollider) continue;
                    if (!collider.isTrigger &&
                        (collider is TilemapCollider2D || collider is CompositeCollider2D))
                    {
                        step = hit.distance;
                        blocked = true;
                        break;
                    }
                    if (shot.TryHit(collider, stepDirection))
                    {
                        loggedFirstAttackHit = true;
                        CancelMining();
                        if (shot.Finished)
                        {
                            step = hit.distance;
                            break;
                        }
                    }
                }
                travelled += step;
                if (blocked || shot.Finished || travelled >= range)
                {
                    projectileBody.position = previous + stepDirection * step;
                    break;
                }
                previous = projectileBody.position;
            }
            if (projectileBody != null) projectileBody.simulated = false;
            if (projectile != null) Destroy(projectile);
        }

        private void TickAttackFeedback(float deltaTime)
        {
            var frames = gameplayArtCatalog?.PlayerAttackFrames;
            if (attackIndicator == null || frames == null || frames.Count <= 1) return;
            attackIndicatorFrameRemaining -= Mathf.Max(0f, deltaTime);
            while (attackIndicatorFrameRemaining <= 0f && attackIndicatorFrameIndex > 0)
            {
                attackIndicatorFrameIndex--;
                attackIndicator.sprite = frames[attackIndicatorFrameIndex];
                PositionAttackFeedback();
                attackIndicatorFrameRemaining += .1f;
            }
        }

        private void PositionAttackFeedback()
        {
            if (attackIndicator == null || attackIndicator.sprite == null) return;
            var referenceSpriteCenter = (Vector2)attackIndicator.sprite.bounds.center;
            var renderedSpriteCenter = referenceSpriteCenter;
            if (attackIndicator.flipX) renderedSpriteCenter.x = -renderedSpriteCenter.x;
            if (attackIndicator.flipY) renderedSpriteCenter.y = -renderedSpriteCenter.y;
            attackIndicator.transform.localPosition = CalculateAttackFeedbackLocalPosition(
                attackIndicatorDirection, referenceSpriteCenter, renderedSpriteCenter);
        }

        public static Vector2 SnapAttackFeedbackDirection(Vector2 direction)
        {
            if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) ||
                float.IsNaN(direction.y) || float.IsInfinity(direction.y) ||
                direction.sqrMagnitude <= Mathf.Epsilon)
                return Vector2.right;
            var angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 45f) * 45f;
            var radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        public static Vector2 CalculateAttackFeedbackLocalPosition(
            Vector2 direction, Vector2 spriteCenter)
        {
            return CalculateAttackFeedbackLocalPosition(direction, spriteCenter, spriteCenter);
        }

        public static Vector2 CalculateAttackFeedbackLocalPosition(
            Vector2 direction, Vector2 referenceSpriteCenter, Vector2 renderedSpriteCenter)
        {
            var snappedDirection = SnapAttackFeedbackDirection(direction);
            var angle = CalculateAttackFeedbackRotationDegrees(snappedDirection);
            var rotatedSpriteCenter =
                (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)renderedSpriteCenter);
            var desiredVisualCenter = Vector2.up * AttackFeedbackOriginHeight +
                                      snappedDirection *
                                      (AttackFeedbackRadius + referenceSpriteCenter.x);
            return desiredVisualCenter - rotatedSpriteCenter;
        }

        public static float CalculateAttackFeedbackRotationDegrees(Vector2 direction)
        {
            var snappedDirection = SnapAttackFeedbackDirection(direction);
            return Mathf.Atan2(snappedDirection.y, snappedDirection.x) * Mathf.Rad2Deg +
                   AttackFeedbackArtRotationDegrees;
        }

        private void CancelAttackFeedback()
        {
            attackIndicatorRemaining = 0f;
            attackIndicatorFrameRemaining = 0f;
            attackIndicatorFrameIndex = 0;
            if (attackIndicator != null) attackIndicator.enabled = false;
        }

        private void RefreshEquipmentStats()
        {
            if (runtimeServices?.EquipmentSystem == null || health == null) return;
            statSheet.Recalculate(runtimeServices.EquipmentSystem,
                runtimeServices.PlayerTemperature?.CurrentRoomTemperature ?? 0,
                runtimeServices.EquipmentColdPenalty);
            currentMoveSpeed = baseMoveSpeed * statSheet.MovementMultiplier +
                               (runtimeServices?.ArtifactVerbs?.ResolveDaySurfaceMoveBonus(
                                   runtimeServices.EquipmentSystem, BuildArtifactContext()) ?? 0f);
            health.SetDefense(statSheet.Defense);
            RefreshPlayerFireDamageMultiplier();
            RefreshPlayerVisionLight();
        }

        private void HandleRoomTemperatureChanged(int _) => RefreshEquipmentStats();

        private void HandleArtifactContextChanged() => RefreshEquipmentStats();

        private void RefreshPlayerFireDamageMultiplier()
        {
            if (health == null) return;
            var auraMultiplier = playerCounterAuraSensor?.FireDamageMultiplier ?? 1f;
            var artifactFire = runtimeServices?.ArtifactVerbs?.ResolveFireDamageModifier(
                runtimeServices.EquipmentSystem, BuildArtifactContext()) ?? 0f;
            health.SetFireDamageMultiplier(CalculateFireDamageMultiplier(
                statSheet.FireDamageModifier + artifactFire, auraMultiplier));
        }

        private ArtifactActivationContext BuildArtifactContext() =>
            ArtifactActivationContextFactory.Build(
                bootstrap?.TileService, transform.position, bootstrap?.TimeService);

        private bool TryOpenRemoteJangdok()
        {
            if (runtimeServices?.ArtifactVerbs == null || runtimeServices.EquipmentSystem == null ||
                storageUi == null || environmentState == null)
                return false;
            if (!runtimeServices.ArtifactVerbs.AllowsRemoteJangdok(
                    runtimeServices.EquipmentSystem, BuildArtifactContext()))
                return false;
            return storageUi.TryOpenRemoteJangdok(environmentState);
        }

        public static float CalculateFireDamageMultiplier(
            float equipmentModifier, float auraMultiplier)
        {
            if (float.IsNaN(equipmentModifier) || float.IsInfinity(equipmentModifier))
                equipmentModifier = 0f;
            if (float.IsNaN(auraMultiplier) || float.IsInfinity(auraMultiplier))
                auraMultiplier = 1f;
            return Mathf.Max(0f, 1f + equipmentModifier) *
                   Mathf.Max(0f, auraMultiplier);
        }

        private void RefreshCombatProfile()
        {
            if (!IsClawMiningActive)
            {
                CancelMining();
                HideMiningTargetFeedback();
            }
            var inventory = runtimeServices?.PlayerInventory;
            var clawProfileId = inventory != null && inventory.Count(IceSteelClawId) > 0
                ? IceSteelClawId
                : inventory != null && inventory.Count(IronClawId) > 0
                    ? IronClawId
                    : BareClawId;
            var profileId = runtimeServices?.ActiveSlot?.ResolveCombatProfileId(clawProfileId) ?? clawProfileId;
            CombatProfileDefinition profile;
            if (profileId == LanternId)
            {
                profile = lanternCarryProfile ??= CombatProfileDefinition.CreateRuntime(
                    LanternId, "U0", false, 0, 0f, 0f, 0f, 1.5f, 90f, false, false);
            }
            else if (GimmickWeaponCombatRules.IsGimmickWeaponId(profileId) && catalog != null)
            {
                var baseProfile = catalog.FindCombatProfile(clawProfileId);
                profile = baseProfile != null
                    ? GimmickWeaponCombatRules.CreateScaledProfile(profileId, baseProfile, catalog)
                    : catalog.FindCombatProfile(profileId);
            }
            else
            {
                profile = catalog != null ? catalog.FindCombatProfile(profileId) : null;
            }
            if (profile == null || !attack.ConfigureForRuntime(transform, ~0, profile)) return;
            var slowFraction = 0f;
            var slowDefinition = profileId == IceSteelClawId ? catalog?.FindGlobal(IceSteelClawSlowKey) : null;
            if (slowDefinition != null) slowDefinition.TryGetFloat(out slowFraction);
            attack.ConfigureFrostSlow(slowFraction, IceSteelClawSlowDurationSeconds);
            activeProfile = profile;
            // Equipment swaps and inventory refreshes must preserve recovery from the last attack.
            // The newly selected profile supplies the cooldown only after its next accepted attack.
        }

        private void HandleDied()
        {
            BeginDeath("hp");
        }

        private void HandleTemperatureMaximum()
        {
            BeginDeath("heatstroke");
        }

        private void BeginDeath(string cause)
        {
            if (dead || !initialized) return;
            dead = true;
            bossKnockbackHorizontalVelocity = 0f;
            bossKnockbackRemainingSeconds = 0f;
            respawnApplied = false;
            deathSequenceElapsed = 0f;
            movementInput = Vector2.zero;
            CancelAttackFeedback();
            verticalVelocity = 0f;
            fallDamageBounceAscending = false;
            grounded = false;
            coyoteTimeRemaining = 0f;
            airJumpConsumed = false;
            ResetFallTracking();
            LockDeathPhysics();
            characterAnimator?.SetMoving(false);
            characterAnimator?.PlayDeath();
            var dropped = runtimeServices?.DeathTearPouches?.DropTwentyPercent(transform.position) ?? 0;
            if (playerRenderer != null) playerRenderer.color = new Color(.35f, .35f, .4f);
            if (playerCollider != null) playerCollider.enabled = false;
            EnsureDeathFade();
            SetDeathFadeAlpha(0f);
            Nyangbingo.Core.GameEvents.RaisePlayerDied();
            Debug.Log($"[Nyangbingo] MainGamePlayerController: 사망 시퀀스 시작 " +
                      $"(cause={cause}, tearDrop={dropped}, duration={CollapseSeconds + FadeOutSeconds + FadeInSeconds:0.##}s).");
        }

        private void TickDeathSequence(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            deathSequenceElapsed += deltaSeconds;
            if (deathSequenceElapsed <= CollapseSeconds)
                return;

            var fadeOutElapsed = deathSequenceElapsed - CollapseSeconds;
            if (fadeOutElapsed < FadeOutSeconds)
            {
                SetDeathFadeAlpha(Mathf.Clamp01(fadeOutElapsed / FadeOutSeconds));
                return;
            }

            if (!respawnApplied) ApplyRespawn();
            var fadeInElapsed = fadeOutElapsed - FadeOutSeconds;
            SetDeathFadeAlpha(1f - Mathf.Clamp01(fadeInElapsed / FadeInSeconds));
            if (fadeInElapsed < FadeInSeconds) return;

            if (health != null) health.RestoreCurrent(health.MaxHealth);
            var temperature = runtimeServices?.PlayerTemperature;
            if (temperature != null) temperature.Restore(temperature.StartingTemperature);
            if (playerCollider != null) playerCollider.enabled = true;
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
            transform.rotation = aliveRotation;
            RestoreDeathPhysics();
            dead = false;
            SetDeathFadeAlpha(0f);
            Debug.Log("[Nyangbingo] MainGamePlayerController: 보금자리 리스폰 완료(HP 전량, 체온 시작값 복원).");
        }

        private void ApplyRespawn()
        {
            respawnApplied = true;
            transform.rotation = aliveRotation;
            var preferredRespawnPosition = initialSpawnPosition;
            var nestPosition = default(Vector2);
            var hasNest = environmentState != null &&
                environmentState.TryGetNearestPlacedObjectPosition(NestBedId, transform.position, out nestPosition);
            if (hasNest)
                preferredRespawnPosition = nestPosition;
            var respawnPosition = hasNest
                ? ResolveNestRespawn(preferredRespawnPosition)
                : ResolveSafeSurfaceRespawn(preferredRespawnPosition);
            transform.position = respawnPosition;
            if (body != null) body.position = respawnPosition;
            verticalVelocity = 0f;
            fallDamageBounceAscending = false;
            bossKnockbackHorizontalVelocity = 0f;
            bossKnockbackRemainingSeconds = 0f;
            grounded = false;
            coyoteTimeRemaining = 0f;
            airJumpConsumed = false;
            ResetFallTracking();
            transform.rotation = aliveRotation;
            characterAnimator?.ResetToIdle();
            if (health != null) health.RestoreCurrent(health.MaxHealth);
            var temperature = runtimeServices?.PlayerTemperature;
            if (temperature != null) temperature.Restore(temperature.StartingTemperature);
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
        }

        private void LockDeathPhysics()
        {
            if (body == null) return;
            if (!deathPhysicsLocked)
            {
                bodySimulationBeforeDeath = body.simulated;
                deathPhysicsLocked = true;
            }
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        private void RestoreDeathPhysics()
        {
            if (!deathPhysicsLocked) return;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = bodySimulationBeforeDeath;
            }
            deathPhysicsLocked = false;
        }

        private Vector2 ResolveNestRespawn(Vector2 nestPosition)
        {
            var tiles = bootstrap?.TileService;
            if (tiles == null || playerCollider == null) return initialSpawnPosition;
            // 사망 중 콜라이더가 비활성화돼 bounds가 비므로 원래 크기·오프셋으로 검사한다.
            var scale = transform.lossyScale;
            var size = Vector2.Scale(playerCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            var offset = Vector2.Scale(playerCollider.offset, new Vector2(scale.x, scale.y));
            var cell = tiles.WorldToCell(nestPosition);
            var nestBounds = tiles.GetCellWorldBounds(cell);
            var preferred = new Vector2(nestPosition.x, nestBounds.min.y + size.y * .5f - offset.y + .02f);
            Physics2D.SyncTransforms();
            var best = initialSpawnPosition;
            var bestDistance = float.PositiveInfinity;
            // 보금자리 주변 3칸만 검색한다. 아래 동굴/다른 열까지 이어지는 전역 검색을 하지 않는다.
            for (var x = -3; x <= 3; x++)
            for (var y = -2; y <= 2; y++)
            {
                var candidate = preferred + new Vector2(x * nestBounds.size.x, y * nestBounds.size.y);
                var center = candidate + offset;
                var blocked = false;
                foreach (var hit in Physics2D.OverlapBoxAll(center, size - Vector2.one * .02f, 0f))
                    if (IsRespawnObstacle(hit)) { blocked = true; break; }
                if (blocked) continue;
                var supported = false;
                foreach (var hit in Physics2D.BoxCastAll(center, size - Vector2.one * .02f, 0f,
                             Vector2.down, .08f))
                    if (IsRespawnObstacle(hit.collider) && hit.normal.y > .5f)
                    { supported = true; break; }
                if (!supported) continue;
                var distance = (candidate - preferred).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            if (!float.IsPositiveInfinity(bestDistance))
            {
                Debug.Log($"[Nyangbingo] MainGamePlayerController: nest respawn (nest={nestPosition}, player={best}).");
                return best;
            }
            Debug.LogWarning("[Nyangbingo] MainGamePlayerController: no safe standing space near nest; using initial spawn.");
            return initialSpawnPosition;
        }

        private bool IsRespawnObstacle(Collider2D collider) =>
            collider != null && collider != playerCollider && !collider.isTrigger &&
            (collider.attachedRigidbody == null || collider.attachedRigidbody.bodyType == RigidbodyType2D.Static);

        private Vector2 ResolveSafeSurfaceRespawn(Vector2 preferredPosition)
        {
            var session = bootstrap?.Session;
            var resolver = session?.SafeSpawnResolver;
            var halfExtent = ColliderFeetBelowRoot(playerCollider);
            var preferredCellX = Mathf.FloorToInt(preferredPosition.x);

            if (resolver != null &&
                resolver.TryResolveSafeSurfaceSpawn(preferredCellX, halfExtent, out var safeSpawn))
            {
                Debug.Log($"[Nyangbingo] MainGamePlayerController: death respawn resolved to safe surface " +
                          $"(preferred={preferredPosition}, player={safeSpawn}).");
                return safeSpawn;
            }

            var generatedSpawn = session != null
                ? session.LastResult.spawnPoint
                : default(Vector2Int);
            if (resolver != null && generatedSpawn.x != preferredCellX &&
                resolver.TryResolveSafeSurfaceSpawn(generatedSpawn.x, halfExtent, out safeSpawn))
            {
                Debug.LogWarning($"[Nyangbingo] MainGamePlayerController: preferred death respawn column was unsafe; " +
                                 $"using generated safe surface spawn (preferred={preferredPosition}, " +
                                 $"generated={generatedSpawn}, player={safeSpawn}).");
                return safeSpawn;
            }

            Debug.LogError($"[Nyangbingo] MainGamePlayerController: failed to resolve a safe surface death respawn; " +
                           $"falling back to initial spawn ({initialSpawnPosition}).");
            return initialSpawnPosition;
        }

        private void EnsureDeathFade()
        {
            if (deathFadeImage != null) return;
            deathFadeCanvas = new GameObject("RuntimeDeathInkFade");
            var canvas = deathFadeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var imageObject = new GameObject("InkFallback");
            imageObject.transform.SetParent(deathFadeCanvas.transform, false);
            deathFadeImage = imageObject.AddComponent<Image>();
            deathFadeImage.raycastTarget = false;
            var rect = deathFadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void SetDeathFadeAlpha(float alpha)
        {
            if (deathFadeImage == null) return;
            deathFadeImage.color = new Color(.035f, .025f, .035f, Mathf.Clamp01(alpha));
            deathFadeImage.enabled = alpha > 0f;
        }

        private void RefreshTearPouchVisuals()
        {
            foreach (var visual in tearPouchVisuals.Values)
                if (visual != null) Destroy(visual);
            tearPouchVisuals.Clear();
            var runtime = runtimeServices?.DeathTearPouches;
            if (runtime == null) return;
            foreach (var record in runtime.Active)
            {
                var visual = new GameObject($"TearPouch_{record.pouchId}");
                visual.transform.position = record.position;
                visual.transform.localScale = Vector3.one * .45f;
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = gameplayArtCatalog?.DeathTearPouch;
                renderer.color = Color.white;
                renderer.sortingOrder = 16;
                renderer.enabled = renderer.sprite != null;
                var labelObject = new GameObject("Amount");
                labelObject.transform.SetParent(visual.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, .55f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = $"×{record.amount}";
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .12f;
                label.fontSize = 32;
                label.color = Color.white;
                label.GetComponent<MeshRenderer>().sortingOrder = 17;
                tearPouchVisuals.Add(record.pouchId, visual);
            }
        }

        private bool TryOpenNearbyChest()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.HasWorld) return false;
            if (!TryResolveNearbyChestCell(session, out var cell)) return false;
            return TryOpenChestAt(cell);
        }

        private bool TryOpenChestAt(Vector3Int cell)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.HasWorld) return false;
            if (!session.TryOpenChestAt(cell, out var chestId, out var definition)) return false;
            storageUi ??= FindAnyObjectByType<Nyangbingo.UI.MainGameCraftingUiController>();
            if (storageUi == null || !storageUi.TryOpenChest(session.ChestProgress, chestId))
                return false;
            worldDecorationRenderer?.MarkChestOpened(chestId);
            Debug.Log($"[Nyangbingo] Product chest loot interface opened: {chestId}, region={definition.Id}.");
            return true;
        }

        private bool TryHarvestNearbyCatnip()
        {
            if (worldDecorationRenderer == null || runtimeServices?.PlayerInventory == null)
                return false;
            var origin = (Vector2)transform.position;
            var aim = TryGetInteractionAimWorld(out var mouseAim) ? mouseAim : origin;
            if (!worldDecorationRenderer.TryHarvestCatnip(
                    origin, CatnipHarvestRadius, aim, runtimeServices.PlayerInventory, out var harvested))
                return false;
            interactionMessages?.ShowExternalMessage($"캣닢 채집 ×{harvested} · 2일 뒤 재생");
            return true;
        }

        private bool TryUseSelectedHealingItem()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            var itemId = tilePalette?.SelectedItemId;
            if (!PlayerHealthRecoveryService.IsSupportedHealingItemId(itemId))
                return false;

            var recovery = runtimeServices.PlayerHealthRecovery;
            if (recovery != null && recovery.TryUseHealingItem(itemId, out var restoredHealth, tilePalette.SelectedSlotIndex))
            {
                var name = catalog?.ItemDisplayName(itemId, "회복 아이템") ?? "회복 아이템";
                interactionMessages?.ShowExternalMessage($"{name} 사용 · HP +{restoredHealth}");
                return true;
            }

            // 캣닢은 HP가 가득일 때 심기로 넘긴다.
            if (itemId == PlayerHealthRecoveryService.CatnipItemId)
                return false;

            interactionMessages?.ShowExternalMessage("HP가 가득 찼거나 회복 아이템을 사용할 수 없습니다.");
            return true;
        }

        private bool TryPlantSelectedCatnip()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette?.SelectedItemId != PlayerHealthRecoveryService.CatnipItemId ||
                worldDecorationRenderer == null || runtimeServices?.PlayerInventory == null)
                return false;

            var origin = (Vector2)transform.position;
            var aim = TryGetInteractionAimWorld(out var mouseAim) ? mouseAim : origin;
            // 수확 가능한 캣닢이 있으면 심기보다 E 상호작용(채집)을 우선한다.
            if (worldDecorationRenderer.TryFindCatnipInRange(origin, CatnipHarvestRadius, aim, out _))
                return false;
            if (!worldDecorationRenderer.TryPlantCatnip(
                    origin, CatnipHarvestRadius, aim, runtimeServices.PlayerInventory, out var message))
                return false;

            interactionMessages?.ShowExternalMessage(message);
            return true;
        }

        private bool TryUseSelectedTalisman()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            var itemId = tilePalette?.SelectedItemId;
            if (!TalismanRuntime.IsConsumableId(itemId)) return false;
            var talismans = runtimeServices?.Talismans;
            var message = string.Empty;
            if (talismans != null && talismans.TryUse(itemId, out message, tilePalette.SelectedSlotIndex))
            {
                interactionMessages?.ShowExternalMessage(message);
                return true;
            }
            interactionMessages?.ShowExternalMessage(string.IsNullOrEmpty(message)
                ? "현재 이 부적을 사용할 수 없습니다."
                : message);
            return true;
        }

        private bool TryUseSelectedIceShard()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette == null || tilePalette.SelectedItemId != IceShardItemId)
                return false;

            return TryUseIceShardFromInventory(tilePalette.SelectedSlotIndex);
        }

        public bool TryUseIceShardFromInventory(int sourceSlot)
        {
            var temperature = runtimeServices?.PlayerTemperature;
            var inventory = runtimeServices?.PlayerInventory;
            if (temperature == null || inventory == null ||
                temperature.Current <= temperature.Minimum)
            {
                interactionMessages?.ShowExternalMessage(
                    "체온이 이미 최저치라 얼음 조각을 사용할 수 없습니다.");
                return true;
            }

            var original = sourceSlot >= 0 && sourceSlot < inventory.Capacity ? inventory.Slots[sourceSlot] : default;
            if (sourceSlot < 0 || !inventory.TryRemove(IceShardItemId, 1, sourceSlot))
            {
                interactionMessages?.ShowExternalMessage("얼음 조각이 없습니다.");
                return true;
            }

            if (temperature.TryCoolImmediately(
                    iceShardTemperatureRelief, out var reducedTemperature))
            {
                interactionMessages?.ShowExternalMessage(
                    $"얼음 조각 사용 · 체온 -{reducedTemperature:0.#}");
                return true;
            }

            inventory.TryRefundOneToSlot(sourceSlot, original);
            interactionMessages?.ShowExternalMessage(
                "현재는 얼음 조각을 사용할 수 없습니다.");
            return true;
        }

        /// <summary>
        /// 단열 문 개폐는 <see cref="MainGameTurretRuntime.TryInteractNearestPlacedObject"/> →
        /// EnvironmentState 프레임 오버레이 경로를 쓴다. TileService.TryToggleNearestDoor(회전 스프라이트)는
        /// 개폐 모션을 가로채므로 플레이어 상호작용에서는 호출하지 않는다.
        /// </summary>
        private bool TryToggleNearbyDoor()
        {
            if (environmentState == null ||
                !environmentState.TryGetNearestPlacedObject(
                    transform.position, ChestInteractReach, out var record) ||
                !string.Equals(record.definitionId, MainGameEnvironmentState.DoorDefinitionId,
                    StringComparison.Ordinal))
                return false;
            if (!environmentState.TryToggleInsulationDoor(record.objectId, out var isOpen))
                return false;
            interactionMessages?.ShowExternalMessage(
                isOpen ? "단열 문을 열었습니다." : "단열 문을 닫았습니다.");
            return true;
        }

        /// <summary>
        /// 플레이어 사거리 안 상자 중 조준점(마우스)에 가장 가까운 칸.
        /// </summary>
        private bool TryResolveNearbyChestCell(WorldSessionController session, out Vector3Int cell)
        {
            cell = default;
            var origin = (Vector2)transform.position;
            var aim = TryGetInteractionAimWorld(out var mouseAim) ? mouseAim : origin;
            return TryFindChestClosestToAim(session, origin, aim, ChestInteractReach * ChestInteractReach,
                out cell, out _);
        }

        private bool TryFindChestClosestToAim(
            WorldSessionController session, Vector2 origin, Vector2 aim, float reachSq,
            out Vector3Int cell, out float aimDistSq)
        {
            cell = default;
            aimDistSq = float.PositiveInfinity;
            var chests = session?.LastResult.chests;
            if (chests == null || chests.Count == 0) return false;

            var tileService = bootstrap?.TileService;
            var found = false;
            for (var i = 0; i < chests.Count; i++)
            {
                var chest = chests[i];
                var chestCell = new Vector3Int(chest.position.x, chest.position.y, 0);
                if (!session.TryPeekChestAt(chestCell)) continue;
                var center = tileService != null
                    ? (Vector2)tileService.GetCellCenterWorld(chestCell)
                    : new Vector2(chestCell.x + .5f, chestCell.y + .5f);
                if ((center - origin).sqrMagnitude > reachSq) continue;
                var toAim = (center - aim).sqrMagnitude;
                if (toAim >= aimDistSq) continue;
                aimDistSq = toAim;
                cell = chestCell;
                found = true;
            }
            return found;
        }

        private static bool TryFindNearestChestCell(
            WorldSessionController session, TileService tileService, Vector2 origin,
            float reachSq, out Vector3Int cell)
        {
            cell = default;
            var chests = session.LastResult.chests;
            if (chests == null || chests.Count == 0) return false;

            var bestDist = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < chests.Count; i++)
            {
                var chest = chests[i];
                var chestCell = new Vector3Int(chest.position.x, chest.position.y, 0);
                var center = tileService != null
                    ? (Vector2)tileService.GetCellCenterWorld(chestCell)
                    : new Vector2(chestCell.x + .5f, chestCell.y + .5f);
                var distSq = (center - origin).sqrMagnitude;
                if (distSq > reachSq || distSq >= bestDist) continue;
                bestDist = distSq;
                cell = chestCell;
                found = true;
            }
            return found;
        }

        private static bool IsChestCellInReach(
            TileService tileService, Vector2 origin, Vector3Int cell, float reachSq)
        {
            var center = tileService != null
                ? (Vector2)tileService.GetCellCenterWorld(cell)
                : new Vector2(cell.x + .5f, cell.y + .5f);
            return (center - origin).sqrMagnitude <= reachSq;
        }

        private static string FormatChestRewardSummary(ChestDefinition definition, int worldSeed, string chestId)
        {
            if (definition == null) return "보상 없음";
            var parts = new List<string>();
            foreach (var reward in definition.Rewards)
            {
                if (reward.item == null || reward.amount <= 0) continue;
                var name = string.IsNullOrEmpty(reward.item.DisplayName) ? reward.item.Id : reward.item.DisplayName;
                parts.Add($"{name}×{reward.amount}");
            }
            var equipment = ChestRewardSelector.SelectEquipment(worldSeed, chestId, definition);
            if (equipment != null)
                parts.Add(equipment.Id);
            return parts.Count == 0 ? definition.Id : string.Join(", ", parts);
        }

        private void RefreshPortableLanternLight()
        {
            if (portableLanternLight == null) return;
            var lantern = runtimeServices?.PortableLantern;
            var isLit = lantern?.IsLit == true;
            var lanternRadius = isLit ? lantern.RadiusTiles : 0f;
            portableLanternLight.pointLightInnerRadius =
                CalculatePersonalVisionRadius(lanternRadius * .35f,
                    statSheet.VisionRadiusBonus * .35f);
            portableLanternLight.pointLightOuterRadius =
                CalculatePersonalVisionRadius(lanternRadius * 1.15f,
                    statSheet.VisionRadiusBonus);
            portableLanternLight.enabled = isLit;
        }

        private void RefreshPlayerVisionLight()
        {
            var artifactVision = runtimeServices?.ArtifactVerbs?.ResolveDeepVisionBonusTiles(
                runtimeServices.EquipmentSystem, BuildArtifactContext()) ?? 0f;
            var bonus = CalculatePersonalVisionRadius(0f, statSheet.VisionRadiusBonus + artifactVision);
            if (personalVisionLight != null)
            {
                personalVisionLight.pointLightInnerRadius = bonus * .35f;
                personalVisionLight.pointLightOuterRadius = bonus;
                personalVisionLight.enabled = bonus > 0f;
            }
            RefreshPortableLanternLight();
        }

        public static float CalculatePersonalVisionRadius(float baseRadius, float equipmentBonus)
        {
            if (float.IsNaN(baseRadius) || float.IsInfinity(baseRadius)) baseRadius = 0f;
            if (float.IsNaN(equipmentBonus) || float.IsInfinity(equipmentBonus)) equipmentBonus = 0f;
            return Mathf.Max(0f, baseRadius) + Mathf.Max(0f, equipmentBonus);
        }

        private void OnDestroy()
        {
            HideMiningTargetFeedback();
            if (bootstrap != null) bootstrap.WorldReady -= RebindForegroundPlacementBlocker;
            placementBlockerTileService?.ClearForegroundPlacementBlocker(
                IsPlayerOverlappingForegroundCell);
            placementBlockerTileService = null;
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshCombatProfile;
            if (runtimeServices?.ActiveSlot != null)
                runtimeServices.ActiveSlot.Changed -= RefreshCombatProfile;
            if (runtimeServices?.PortableLantern != null)
                runtimeServices.PortableLantern.Changed -= RefreshPortableLanternLight;
            if (runtimeServices?.EquipmentSystem != null)
                runtimeServices.EquipmentSystem.Changed -= RefreshEquipmentStats;
            if (health != null) health.Died -= HandleDied;
            if (runtimeServices?.PlayerTemperature != null)
            {
                runtimeServices.PlayerTemperature.RoomTemperatureChanged -= HandleRoomTemperatureChanged;
                runtimeServices.PlayerTemperature.ReachedMaximum -= HandleTemperatureMaximum;
            }
            if (runtimeServices?.DeathTearPouches != null)
                runtimeServices.DeathTearPouches.Changed -= RefreshTearPouchVisuals;
            GameEvents.OnDayStart -= HandleArtifactContextChanged;
            GameEvents.OnNightStart -= HandleArtifactContextChanged;
            foreach (var visual in tearPouchVisuals.Values)
                if (visual != null) Destroy(visual);
            tearPouchVisuals.Clear();
            if (deathFadeCanvas != null) Destroy(deathFadeCanvas);
            if (lanternCarryProfile != null) Destroy(lanternCarryProfile);
        }
    }

    internal static class RuntimePlaceholderVisual
    {
        private static Sprite sprite;

        public static void Configure(SpriteRenderer renderer, Color color, float size, int sortingOrder)
        {
            if (renderer == null) return;
            if (sprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "NyangbingoRuntimePlaceholderTexture",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
                sprite.name = "NyangbingoRuntimePlaceholderSprite";
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localScale = new Vector3(size, size, 1f);
        }

        public static void ConfigureSprite(SpriteRenderer renderer, Sprite sourceSprite, int sortingOrder)
        {
            if (renderer == null || sourceSprite == null) return;
            renderer.sprite = sourceSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localScale = Vector3.one;
        }
    }

    internal sealed class RuntimeDamageFlash : MonoBehaviour
    {
        private Health health;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float remaining;

        private void Awake()
        {
            health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (health != null) health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(Nyangbingo.Core.DamageTag tag, int amount)
        {
            if (spriteRenderer == null || amount <= 0) return;
            spriteRenderer.color = Color.white;
            remaining = .1f;
        }

        public void SetBaseColor(Color color)
        {
            baseColor = color;
            if (remaining <= 0f && spriteRenderer != null) spriteRenderer.color = baseColor;
        }

        private void Update()
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (spriteRenderer == null) return;
            spriteRenderer.color = remaining > 0f ? Color.white : baseColor;
        }
    }
}
