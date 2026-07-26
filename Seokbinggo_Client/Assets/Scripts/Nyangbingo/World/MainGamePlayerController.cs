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

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    [RequireComponent(typeof(MeleeArcAttack), typeof(SpriteRenderer))]
    public sealed class MainGamePlayerController : MonoBehaviour
    {
        public const float GameplayCameraOrthographicSize = 8f;
        public const float FallDamageBounceHeightTiles = .5f;

        private const string MoveSpeedKey = "player_move_speed";
        private const string BareClawId = "bare_claw";
        private const string HapjukseonId = "hapjukseon";
        private const string CheolseonId = "cheolseon";
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
        // 공격 사거리(bare_claw rangeTiles=1.5)와 맞춰, facing*짧은 거리만 보면 조준 타일 앞 공기 칸만
        // 찍혀 채굴이 조용히 실패하던 문제를 피한다.
        private const float MiningReach = 1.5f;
        public const float InsulationWallBareClawMiningSeconds = 3f;
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

        private float jumpVelocity;
        private float gravityAcceleration;
        private float maximumFallSpeed;
        private float jumpCutMultiplier;
        private float fallDamageThresholdTiles;
        private float fallDamagePerTile;
        private float iceShardTemperatureRelief;
        private float fallPeakWorldY;
        private bool trackingFall;
        private bool fallDamageBounceAscending;

        private const float GroundProbeDistance = .08f;

        private readonly StatSheet statSheet = new StatSheet();
        private Rigidbody2D body;
        private CircleCollider2D playerCollider;
        private readonly RaycastHit2D[] groundProbeHits = new RaycastHit2D[8];
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
        private bool airJumpConsumed;
        private bool miningActive;
        private string miningTreeId = string.Empty;
        private string miningRebarId = string.Empty;
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
        private CombatProfileDefinition activeProfile;
        private CombatProfileDefinition lanternCarryProfile;
        private SpriteRenderer attackIndicator;
        private RuntimeCharacterSpriteAnimator characterAnimator;
        private float attackIndicatorRemaining;
        private int attackIndicatorFrameIndex;
        private float attackIndicatorFrameRemaining;
        private Vector2 attackIndicatorDirection = Vector2.right;
        private bool loggedFirstAttackInput;
        private bool loggedFirstAttackHit;
        private bool dead;
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
        public float CurrentMoveSpeed => currentMoveSpeed;
        public Vector2 FacingDirection => facing;
        public Vector2 HorizontalFacingDirection => horizontalFacing;
        public bool IsGrounded => grounded;
        public float VerticalVelocity => verticalVelocity;
        public float MiningProgress => CalculateMiningProgress(miningElapsedSeconds, miningRequiredSeconds);
        public bool IsDead => dead;

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
            playerCollider = GetComponent<CircleCollider2D>();
            health = GetComponent<Health>();
            attack = GetComponent<MeleeArcAttack>();
            environmentState = GetComponentInParent<MainGameEnvironmentState>();
            placedObjectInteractions = GetComponentInParent<MainGameTurretRuntime>();
            if (placedObjectInteractions != null)
                playerCounterAuraSensor = new CounterAuraSensor(
                    transform, placedObjectInteractions.ActiveCounterAuras);
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

            var physics = PlayerMovementPhysics.TryLoadFromCatalog(catalog, out var legacyPhysics)
                ? legacyPhysics
                : PlayerMovementPhysics.CreateDefault();
            jumpVelocity = physics.JumpVelocity;
            gravityAcceleration = physics.Gravity;
            maximumFallSpeed = physics.MaxFallSpeed;
            jumpCutMultiplier = physics.JumpCutMultiplier;

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
            playerVisualTransform.localPosition = Vector3.up *
                RuntimeCharacterSpriteAnimator.CalculateGroundedVisualLocalY(
                    playerCollider, playerRenderer);
            initialSpawnPosition = transform.position;
            aliveRendererColor = playerRenderer.color;
            aliveRotation = transform.rotation;
            var indicatorObject = new GameObject("AttackIndicator");
            indicatorObject.transform.SetParent(playerVisualTransform, false);
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
            health.Died += HandleDied;
            runtimeServices.PlayerTemperature.ReachedMaximum += HandleTemperatureMaximum;
            runtimeServices.DeathTearPouches.Changed += RefreshTearPouchVisuals;
            wireSnare = new WireSnareAbility(attack);
            RefreshEquipmentStats();
            RefreshCombatProfile();
            RefreshTearPouchVisuals();
            grounded = IsStandingOnForeground();
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
                IsPlayerOverlappingForegroundCell);
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
            if (!initialized) return;
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
            if (Nyangbingo.UI.MainGameCraftingUiController.BlocksGameplayInput ||
                Nyangbingo.UI.MainGameBossSummonUiController.IsDebugShortcutHelpOpen)
            {
                movementInput = Vector2.zero;
                CancelMining();
                HideMiningTargetFeedback();
                characterAnimator?.SetMoving(false);
                return;
            }
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.M) &&
                (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
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
                        : $"활성 장비: {catalog.FindItem(activeItemId)?.DisplayName}";
                interactionMessages?.ShowExternalMessage(statusMessage);
            }
            UpdateAimDirection();
            runtimeServices.DeathTearPouches.TryCollectWithin(transform.position, TearPouchPickupRadius);
            if (Input.GetKeyDown(KeyCode.E))
            {
                var recover = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                var handled = recover
                    ? placedObjectInteractions?.TryRecoverNearestPlacedObject() == true
                    : TryUseSelectedIceShard() ||
                      TryUseSelectedCatnip() ||
                      TryHarvestNearbyCatnip() ||
                      TryHarvestNearbyHemp() ||
                      TryOpenNearbyChest() ||
                      TryToggleNearbyDoor() ||
                      placedObjectInteractions?.TryInteractNearestPlacedObject() == true;
                if (!handled)
                    interactionMessages?.ShowExternalMessage(recover
                        ? "가까이 있는 회수 가능한 설치물이 없습니다."
                        : "가까이 있는 상호작용 대상을 찾지 못했습니다.");
            }
            movementInput = new Vector2(Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f), 0f);
            if (Mathf.Abs(movementInput.x) > Mathf.Epsilon)
                horizontalFacing = movementInput.x < 0f ? Vector2.left : Vector2.right;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.Space))
                TryJump();
            characterAnimator?.SetFacing(horizontalFacing);
            characterAnimator?.SetMoving(movementInput.sqrMagnitude > Mathf.Epsilon);

            attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);
            wireSnare.Tick(Time.deltaTime);
            if (attackIndicatorRemaining > 0f)
            {
                attackIndicatorRemaining = Mathf.Max(0f, attackIndicatorRemaining - Time.deltaTime);
                TickAttackFeedback(Time.deltaTime);
                if (attackIndicatorRemaining <= 0f) attackIndicator.enabled = false;
            }
            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var buildingPlacementActive = MainGameTurretRuntime.BlocksCombatInput ||
                                          MainGameTilePaletteController.BlocksGameplayInput ||
                                          MainGameHudController.BlocksWorldPrimaryInput;
            UpdateMiningTargetFeedback(pointerOverUi || buildingPlacementActive);
            var primaryHeld = Input.GetMouseButton(0);
            if (!buildingPlacementActive && !pointerOverUi && primaryHeld)
            {
                if (attackCooldown <= 0f)
                    TryBasicAttack();
                // 좌클릭 공격이 요괴에게 명중해도 같은 방향의 채굴 진행은 끊지 않는다.
                // 공격 쿨다운과 채굴 시간은 서로 독립적으로 누적된다.
                TickMining();
            }
            else CancelMining();
            // 우클릭은 부채 액티브 전용이다. 상자와 설치물의 제품 상호작용은 E로 통합한다.
            if (!buildingPlacementActive && !pointerOverUi && Input.GetMouseButtonDown(1))
                TryFanAbility();
        }

        private void FixedUpdate()
        {
            if (!initialized || dead || body == null) return;
            var deltaSeconds = Time.fixedDeltaTime;
            grounded = IsStandingOnForeground();
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
            var horizontalVelocity = CalculateHorizontalVelocity(movementInput.x, currentMoveSpeed);
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
            return true;
        }

        private void ApplyGeneratedWorldSpawn()
        {
            var session = bootstrap?.Session;
            if (session?.HasWorld != true || !session.LastResult.passedValidation) return;
            var cell = session.LastResult.spawnPoint;
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
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
            if (grounded)
            {
                fallDamageBounceAscending = false;
                verticalVelocity = jumpVelocity;
                grounded = false;
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
            if (playerCollider == null || !playerCollider.enabled) return false;
            var hitCount = playerCollider.Cast(Vector2.down, ContactFilter2D.noFilter,
                groundProbeHits, GroundProbeDistance);
            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = groundProbeHits[index].collider;
                if (hitCollider is TilemapCollider2D || hitCollider is CompositeCollider2D)
                    return true;
            }
            return false;
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

        public static void ConfigurePhysicsBody(Rigidbody2D targetBody, CircleCollider2D targetCollider)
        {
            if (targetBody == null) throw new System.ArgumentNullException(nameof(targetBody));
            if (targetCollider == null) throw new System.ArgumentNullException(nameof(targetCollider));

            targetBody.bodyType = RigidbodyType2D.Dynamic;
            targetBody.gravityScale = 0f;
            targetBody.freezeRotation = true;
            targetBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            targetBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            targetCollider.radius = .38f;
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
            BeginFallTracking(landingWorldY);
            if (body != null)
                body.linearVelocity = new Vector2(body.linearVelocity.x, verticalVelocity);
            return true;
        }

        private void ResetFallTracking()
        {
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
            attack.Strike(SnapAttackFeedbackDirection(facing));
            characterAnimator?.PlayAttack();
            ShowAttackFeedback();
            attackCooldown = 1f / activeProfile.AttacksPerSecond;
            // 첫 스윙은 항상, 이후에는 실제 명중(hits>0)일 때만 로그 — hits는 "데미지 수치"가 아니라 맞은 대상 수.
            if (!loggedFirstAttackInput || attack.LastHitCount > 0 && !loggedFirstAttackHit)
            {
                Debug.Log($"[Nyangbingo] Player attack accepted (profile={activeProfile.Id}, hits={attack.LastHitCount}).");
                loggedFirstAttackInput = true;
                loggedFirstAttackHit |= attack.LastHitCount > 0;
            }
            return true;
        }

        private void TickMining()
        {
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
            if (TryTickTreeMining(clawTier, miningDelta)) return;
            if (TryTickRebarMining(clawTier, miningDelta)) return;
            if (!TryResolveMiningCell(tileService, out var cell))
            {
                ResetMiningProgress();
                return;
            }
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
                !string.IsNullOrEmpty(miningRebarId) || miningCell != cell ||
                miningHasCompanion != hasCompanion ||
                hasCompanion && miningCompanionCell != companionCell ||
                !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = string.Empty;
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
                    SnapAttackFeedbackDirection(facing), MiningReach, out var treeId, out var hitCell))
                return false;
            var definition = catalog?.FindMineralTier(MainGameWorldDecorationRenderer.WoodItemId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }
            if (!miningActive || !string.IsNullOrEmpty(miningRebarId) ||
                !string.Equals(miningTreeId, treeId, System.StringComparison.Ordinal) ||
                miningCell != hitCell || !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = treeId;
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

        private bool TryTickRebarMining(int clawTier, float miningDelta)
        {
            if (worldDecorationRenderer == null ||
                !worldDecorationRenderer.TryResolveRebarMiningTarget(transform.position,
                    SnapAttackFeedbackDirection(facing), MiningReach, out var rebarId, out var hitCell))
                return false;
            var definition = catalog?.FindMineralTier(MainGameWorldDecorationRenderer.RebarItemId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return true;
            }
            if (!miningActive || !string.IsNullOrEmpty(miningTreeId) ||
                !string.Equals(miningRebarId, rebarId, System.StringComparison.Ordinal) ||
                miningCell != hitCell || !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
                miningTreeId = string.Empty;
                miningRebarId = rebarId;
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

        /// <summary>
        /// 마우스 아래 칸이 사거리 안이면 그 칸(공기면 발밑·인접 고체)을 우선하고,
        /// 아니면 조준 방향 × 사거리 칸을 같은 규칙으로 쓴다.
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
            return TryPickMiningCell(tileService, origin, mouseWorld, direction, MiningReach, out cell);
        }

        /// <summary>
        /// 지표 채굴 UX — 마우스가 공기 칸(플레이어 발 높이)을 가리키면 바로 아래·인접 전경 고체로 보정한다.
        /// </summary>
        public static bool TryPickMiningCell(TileService tileService, Vector2 playerOrigin,
            Vector2? mouseWorld, Vector2 facing, float miningReach, out Vector3Int cell)
        {
            cell = default;
            if (tileService == null || miningReach <= 0f ||
                float.IsNaN(playerOrigin.x) || float.IsInfinity(playerOrigin.x) ||
                float.IsNaN(playerOrigin.y) || float.IsInfinity(playerOrigin.y))
                return false;

            var direction = SnapAttackFeedbackDirection(facing);
            // MeleeArcAttack and the player's Rigidbody2D both use playerOrigin. The visual claw
            // has a separate hand-height offset and must never raise the authoritative mining ray.
            var attackOrigin = playerOrigin;
            var reachSq = miningReach * miningReach;

            // Cursor intent wins over geometric ray order. Near a tile corner, a diagonal ray
            // enters the horizontal or vertical neighbor a fraction earlier and used to select
            // that nearer cell even though the cursor was visibly over the diagonal tile.
            if (mouseWorld.HasValue)
            {
                var cursorCell = tileService.WorldToCell(mouseWorld.Value);
                if (IsMineableForegroundCell(tileService, cursorCell) &&
                    IsWithinMiningReach(tileService, attackOrigin, cursorCell, reachSq))
                {
                    cell = cursorCell;
                    return true;
                }
            }

            // Preserve deterministic eight-direction targeting when the cursor is over air.
            // This checks the intended adjacent octant before the fallback ray, preventing the
            // player's support/side tile from winning merely because its boundary is closer.
            var originCell = tileService.WorldToCell(attackOrigin);
            var directionalCell = originCell + new Vector3Int(
                Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y), 0);
            if (directionalCell != originCell &&
                IsMineableForegroundCell(tileService, directionalCell) &&
                IsWithinMiningReach(tileService, attackOrigin, directionalCell, reachSq))
            {
                cell = directionalCell;
                return true;
            }

            // For gaps wider than one cell, keep the existing first-solid fallback along the
            // snapped claw direction.
            var steps = Mathf.Max(1, Mathf.CeilToInt(miningReach * 8f));
            var previousCell = new Vector3Int(int.MinValue, int.MinValue, 0);
            for (var step = 0; step <= steps; step++)
            {
                var distance = miningReach * step / steps;
                var sample = attackOrigin + direction * distance;
                var candidate = tileService.WorldToCell(sample);
                if (candidate == previousCell) continue;
                previousCell = candidate;
                if (!IsMineableForegroundCell(tileService, candidate) ||
                    !IsWithinMiningReach(tileService, attackOrigin, candidate, reachSq))
                    continue;
                cell = candidate;
                return true;
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
            string itemId;
            int amount;
            using (ItemAcquisition.CaptureRequests())
                if (!tileService.TryBreakForeground(cell, clawTier, out itemId, out amount)) return;

            var totalAmount = amount;
            var item = string.IsNullOrEmpty(itemId) ? null : catalog?.FindItem(itemId);
            var baseCriticalChance = 0f;
            var criticalDefinition = clawTier == 2 ? catalog?.FindGlobal(IronClawMiningCriticalKey) : null;
            if (criticalDefinition != null && criticalDefinition.TryGetFloat(out var configuredChance))
                baseCriticalChance = configuredChance;
            var criticalChance = CalculateMiningCriticalChance(baseCriticalChance,
                statSheet.MiningCriticalChance);
            var critical = item != null && amount > 0 && UnityEngine.Random.value < criticalChance;
            if (critical)
            {
                totalAmount += amount;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }

            if (item != null && totalAmount > 0)
            {
                WorldItemDropRequest.Request(item, totalAmount,
                    tileService.GetCellCenterWorld(cell));
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
                           CalculateMiningCriticalChance(baseCriticalChance, statSheet.MiningCriticalChance);
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
                           CalculateMiningCriticalChance(baseCriticalChance, statSheet.MiningCriticalChance);
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
            miningHasCompanion = false;
            miningElapsedSeconds = 0f;
            miningRequiredSeconds = 0f;
        }

        private void UpdateMiningTargetFeedback(bool blocked)
        {
            var tileService = bootstrap?.TileService;
            if (blocked || tileService == null || !TryResolveMiningCell(tileService, out var cell))
            {
                HideMiningTargetFeedback();
                return;
            }

            var tile = tileService.GetTile(cell);
            var clawTier = ResolveMiningClawTier();
            var mineable = !tile.IsAir && clawTier >= tile.hardness &&
                           ResolveTileMiningSeconds(catalog, tile.elementType, clawTier) > 0f;
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
                (activeProfile.Id != HapjukseonId && activeProfile.Id != CheolseonId)) return;
            if (wireSnare.TryUse(facing, ResolveFanAbilityDamage(activeProfile.Id))) ShowAttackFeedback();
        }

        public static int ResolveFanAbilityDamage(string combatProfileId) =>
            combatProfileId == CheolseonId
                ? WireSnareAbility.CheolseonDamage
                : WireSnareAbility.HapjukseonDamage;

        private void ShowAttackFeedback()
        {
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
            statSheet.Recalculate(runtimeServices.EquipmentSystem);
            currentMoveSpeed = baseMoveSpeed * statSheet.MovementMultiplier;
            health.SetDefense(statSheet.Defense);
            RefreshPlayerFireDamageMultiplier();
            RefreshPlayerVisionLight();
        }

        private void RefreshPlayerFireDamageMultiplier()
        {
            if (health == null) return;
            var auraMultiplier = playerCounterAuraSensor?.FireDamageMultiplier ?? 1f;
            health.SetFireDamageMultiplier(CalculateFireDamageMultiplier(
                statSheet.FireDamageModifier, auraMultiplier));
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
            var inventory = runtimeServices?.PlayerInventory;
            var clawProfileId = inventory != null && inventory.Count(IceSteelClawId) > 0
                ? IceSteelClawId
                : inventory != null && inventory.Count(IronClawId) > 0
                    ? IronClawId
                    : BareClawId;
            var profileId = runtimeServices?.ActiveSlot?.ResolveCombatProfileId(clawProfileId) ?? clawProfileId;
            var profile = profileId == LanternId
                ? lanternCarryProfile ??= CombatProfileDefinition.CreateRuntime(
                    LanternId, "U0", false, 0, 0f, 0f, 0f, 1.5f, 90f, false, false)
                : catalog != null ? catalog.FindCombatProfile(profileId) : null;
            if (profile == null || !attack.ConfigureForRuntime(transform, ~0, profile)) return;
            var slowFraction = 0f;
            var slowDefinition = profileId == IceSteelClawId ? catalog?.FindGlobal(IceSteelClawSlowKey) : null;
            if (slowDefinition != null) slowDefinition.TryGetFloat(out slowFraction);
            attack.ConfigureFrostSlow(slowFraction, IceSteelClawSlowDurationSeconds);
            activeProfile = profile;
            attackCooldown = 0f;
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
            var preferredRespawnPosition = initialSpawnPosition;
            if (environmentState != null &&
                environmentState.TryGetNearestPlacedObjectPosition(NestBedId, transform.position, out var nestPosition))
                preferredRespawnPosition = nestPosition;
            var respawnPosition = ResolveSafeSurfaceRespawn(preferredRespawnPosition);
            transform.position = respawnPosition;
            if (body != null) body.position = respawnPosition;
            verticalVelocity = 0f;
            fallDamageBounceAscending = false;
            bossKnockbackHorizontalVelocity = 0f;
            bossKnockbackRemainingSeconds = 0f;
            grounded = false;
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

        private Vector2 ResolveSafeSurfaceRespawn(Vector2 preferredPosition)
        {
            var session = bootstrap?.Session;
            var resolver = session?.SafeSpawnResolver;
            var halfExtent = playerCollider != null
                ? Mathf.Max(.05f, playerCollider.radius * Mathf.Abs(transform.lossyScale.y))
                : .38f;
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
                visual.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.Configure(renderer, new Color(.2f, .85f, 1f, .9f), .45f, 16);
                var labelObject = new GameObject("Amount");
                labelObject.transform.SetParent(visual.transform, false);
                labelObject.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
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
            if (worldDecorationRenderer == null || runtimeServices?.PlayerInventory == null ||
                !worldDecorationRenderer.TryHarvestCatnip(
                    transform.position, CatnipHarvestRadius, runtimeServices.PlayerInventory, out var harvested))
                return false;
            interactionMessages?.ShowExternalMessage($"캣닢 채집 ×{harvested} · 2일 뒤 재생");
            return true;
        }

        private bool TryUseSelectedCatnip()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette == null ||
                tilePalette.SelectedItemId != PlayerHealthRecoveryService.CatnipItemId)
                return false;

            var recovery = runtimeServices.PlayerHealthRecovery;
            if (recovery != null && recovery.TryUseCatnip(out var restoredHealth))
            {
                interactionMessages?.ShowExternalMessage($"캣닢 사용 · HP +{restoredHealth}");
                return true;
            }

            interactionMessages?.ShowExternalMessage("HP가 가득 찼거나 캣닢을 사용할 수 없습니다.");
            return true;
        }

        private bool TryUseSelectedIceShard()
        {
            tilePalette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette == null || tilePalette.SelectedItemId != IceShardItemId)
                return false;

            var temperature = runtimeServices?.PlayerTemperature;
            var inventory = runtimeServices?.PlayerInventory;
            if (temperature == null || inventory == null ||
                temperature.Current <= temperature.Minimum)
            {
                interactionMessages?.ShowExternalMessage(
                    "체온이 이미 최저치라 얼음 조각을 사용할 수 없습니다.");
                return true;
            }

            if (!inventory.TryRemove(IceShardItemId, 1))
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

            inventory.TryAdd(IceShardItemId, 1);
            interactionMessages?.ShowExternalMessage(
                "현재는 얼음 조각을 사용할 수 없습니다.");
            return true;
        }

        private bool TryToggleNearbyDoor()
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null ||
                !tileService.TryToggleNearestDoor(
                    transform.position, ChestInteractReach, out var isOpen))
                return false;
            interactionMessages?.ShowExternalMessage(
                isOpen ? "단열 문을 열었습니다." : "단열 문을 닫았습니다.");
            return true;
        }

        private bool TryHarvestNearbyHemp()
        {
            if (worldDecorationRenderer == null || runtimeServices?.PlayerInventory == null ||
                !worldDecorationRenderer.TryHarvestHemp(
                    transform.position, CatnipHarvestRadius, runtimeServices.PlayerInventory, out var harvested))
                return false;
            interactionMessages?.ShowExternalMessage($"삼줄기 채집 ×{harvested}");
            return true;
        }

        /// <summary>
        /// 마우스 아래 칸(사거리 안) → 조준 칸 → 발 칸 → 사거리 안 최근접 미개봉 상자.
        /// </summary>
        private bool TryResolveNearbyChestCell(WorldSessionController session, out Vector3Int cell)
        {
            cell = default;
            var origin = (Vector2)transform.position;
            var reachSq = ChestInteractReach * ChestInteractReach;
            var tileService = bootstrap?.TileService;

            if (followCamera != null)
            {
                var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
                var mouseCell = tileService != null
                    ? tileService.WorldToCell(mouse)
                    : new Vector3Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y), 0);
                if (IsChestCellInReach(tileService, origin, mouseCell, reachSq) &&
                    session.TryPeekChestAt(mouseCell))
                {
                    cell = mouseCell;
                    return true;
                }
            }

            var facingPosition = origin +
                (facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector2.right);
            var facingCell = tileService != null
                ? tileService.WorldToCell(facingPosition)
                : Vector3Int.FloorToInt(facingPosition);
            var currentCell = tileService != null
                ? tileService.WorldToCell(origin)
                : Vector3Int.FloorToInt(origin);
            if (IsChestCellInReach(tileService, origin, facingCell, reachSq) &&
                session.TryPeekChestAt(facingCell))
            {
                cell = facingCell;
                return true;
            }
            if (IsChestCellInReach(tileService, origin, currentCell, reachSq) &&
                session.TryPeekChestAt(currentCell))
            {
                cell = currentCell;
                return true;
            }

            return TryFindNearestChestCell(session, tileService, origin, reachSq, out cell);
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
            var bonus = CalculatePersonalVisionRadius(0f, statSheet.VisionRadiusBonus);
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
                runtimeServices.PlayerTemperature.ReachedMaximum -= HandleTemperatureMaximum;
            if (runtimeServices?.DeathTearPouches != null)
                runtimeServices.DeathTearPouches.Changed -= RefreshTearPouchVisuals;
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
