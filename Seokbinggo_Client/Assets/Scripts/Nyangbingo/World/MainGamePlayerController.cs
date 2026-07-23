using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.UI;
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
        private const string NestBedId = "nest_bed";
        private const float NestInteractionRadius = 1.25f;
        private const float NapYokaiWakeRadius = 12f;
        private const float TearPouchPickupRadius = .75f;
        // 공격 사거리(bare_claw rangeTiles=1.5)와 맞춰, facing*짧은 거리만 보면 조준 타일 앞 공기 칸만
        // 찍혀 채굴이 조용히 실패하던 문제를 피한다.
        private const float MiningReach = 1.5f;
        // DevA 테스트 하니스와 동일: 마우스 칸 우선 + 플레이어 인접 미개봉 상자.
        private const float ChestInteractReach = 1.75f;
        private const float CollapseSeconds = 1.5f;
        private const float FadeOutSeconds = 1.75f;
        private const float FadeInSeconds = 1.75f;

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
        private bool grounded;
        private bool airJumpConsumed;
        private bool miningAllowedByLastSwing;
        private bool miningActive;
        private Vector3Int miningCell;
        private Vector3Int miningCompanionCell;
        private bool miningHasCompanion;
        private float miningElapsedSeconds;
        private float miningRequiredSeconds;
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
        private bool loggedFirstAttackInput;
        private bool loggedFirstAttackHit;
        private bool dead;
        private bool respawnApplied;
        private float deathSequenceElapsed;
        private Vector2 initialSpawnPosition;
        private SpriteRenderer playerRenderer;
        private Color aliveRendererColor;
        private Quaternion aliveRotation;
        private Image deathFadeImage;
        private GameObject deathFadeCanvas;
        private Light2D portableLanternLight;
        private readonly Dictionary<string, GameObject> tearPouchVisuals =
            new Dictionary<string, GameObject>();
        private bool initialized;
        private MainGameEnvironmentState environmentState;
        private MainGameTurretRuntime placedObjectInteractions;
        private MainGameWorldDecorationRenderer worldDecorationRenderer;
        private MainGameRaidTarget raidTarget;
        private MainGameEncounterCoordinator encounterCoordinator;
        private MainGameWorldDropRuntime worldDropRuntime;
        private Nyangbingo.UI.MainGameBossSummonUiController interactionMessages;

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
            worldDecorationRenderer = GetComponentInParent<MainGameWorldDecorationRenderer>();
            raidTarget = GetComponent<MainGameRaidTarget>();
            encounterCoordinator = GetComponentInParent<MainGameEncounterCoordinator>();
            interactionMessages = FindAnyObjectByType<Nyangbingo.UI.MainGameBossSummonUiController>();
            followCamera ??= Camera.main;

            var moveSpeedDefinition = catalog != null ? catalog.FindGlobal(MoveSpeedKey) : null;
            var defaultProfile = catalog != null ? catalog.FindCombatProfile(BareClawId) : null;
            if (catalog == null || bootstrap == null || runtimeServices == null ||
                !runtimeServices.Initialize() || body == null || health == null || attack == null ||
                moveSpeedDefinition == null || !moveSpeedDefinition.TryGetFloat(out baseMoveSpeed) ||
                baseMoveSpeed <= 0f || defaultProfile == null)
            {
                Debug.LogError("[Nyangbingo] MainGamePlayerController: 플레이어 이동·전투 필수 데이터가 준비되지 않았습니다.");
                return false;
            }

            if (!PlayerMovementPhysics.TryLoadFromCatalog(catalog, out var physics))
            {
                physics = PlayerMovementPhysics.CreateDefault();
                Debug.LogWarning("[Nyangbingo] MainGamePlayerController: player physics globals missing; " +
                                 "using Terraria-like defaults.");
            }
            jumpVelocity = physics.JumpVelocity;
            gravityAcceleration = physics.Gravity;
            maximumFallSpeed = physics.MaxFallSpeed;
            jumpCutMultiplier = physics.JumpCutMultiplier;

            ConfigurePhysicsBody(body, playerCollider);
            ApplyGeneratedWorldSpawn();
            playerRenderer = GetComponent<SpriteRenderer>();
            var playerArt = characterArtCatalog != null ? characterArtCatalog.Find("player") : null;
            if (playerArt?.Sprite != null)
            {
                characterAnimator = GetComponent<RuntimeCharacterSpriteAnimator>() ??
                                    gameObject.AddComponent<RuntimeCharacterSpriteAnimator>();
                characterAnimator.Configure(playerArt, 20);
            }
            else
                RuntimePlaceholderVisual.Configure(playerRenderer, new Color(.25f, .85f, 1f), .8f, 20);
            initialSpawnPosition = transform.position;
            aliveRendererColor = playerRenderer.color;
            aliveRotation = transform.rotation;
            var indicatorObject = new GameObject("AttackIndicator");
            indicatorObject.transform.SetParent(transform, false);
            attackIndicator = indicatorObject.AddComponent<SpriteRenderer>();
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

            runtimeServices.PlayerInventory.Changed += RefreshCombatProfile;
            runtimeServices.ActiveSlot.Changed += RefreshCombatProfile;
            runtimeServices.PortableLantern.Changed += RefreshPortableLanternLight;
            runtimeServices.EquipmentSystem.Changed += RefreshEquipmentStats;
            health.Died += HandleDied;
            health.Damaged += HandleDamaged;
            runtimeServices.PlayerTemperature.ReachedMaximum += HandleTemperatureMaximum;
            runtimeServices.DeathTearPouches.Changed += RefreshTearPouchVisuals;
            if (raidTarget != null) raidTarget.WallDamaged += HandleWallDamaged;
            wireSnare = new WireSnareAbility(attack);
            RefreshEquipmentStats();
            RefreshCombatProfile();
            RefreshTearPouchVisuals();
            grounded = IsStandingOnForeground();
            initialized = activeProfile != null;
            if (initialized)
                Debug.Log($"[Nyangbingo] MainGamePlayerController: 이동·체력·근접 공격·카메라 연결 완료 " +
                          $"(speed={currentMoveSpeed:0.##}, profile={ActiveCombatProfileId}).");
            return initialized;
        }

        private void Update()
        {
            if (!initialized) return;
            RefreshPortableLanternLight();
            if (dead)
            {
                movementInput = Vector2.zero;
                CancelMining();
                TickDeathSequence(Time.deltaTime);
                return;
            }
            if (Nyangbingo.UI.MainGameCraftingUiController.BlocksGameplayInput ||
                Nyangbingo.UI.MainGameBossSummonUiController.IsDebugShortcutHelpOpen)
            {
                movementInput = Vector2.zero;
                CancelMining();
                characterAnimator?.SetMoving(false);
                return;
            }
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
                    : TryOpenNearbyChest() ||
                      placedObjectInteractions?.TryInteractNearestPlacedObject() == true;
                if (!handled)
                    interactionMessages?.ShowExternalMessage(recover
                        ? "가까이 있는 회수 가능한 설치물이 없습니다."
                        : "가까이 있는 상호작용 대상을 찾지 못했습니다.");
                if (runtimeServices.NapService.IsNapping)
                {
                    movementInput = Vector2.zero;
                    CancelMining();
                    characterAnimator?.SetMoving(false);
                    return;
                }
            }
            if (runtimeServices.NapService.IsNapping)
            {
                if (encounterCoordinator != null &&
                    encounterCoordinator.HasActiveYokaiWithin(transform.position, NapYokaiWakeRadius))
                    runtimeServices.NapService.Wake(NapWakeReason.YokaiApproached);
            }
            if (runtimeServices.NapService.IsNapping)
            {
                movementInput = Vector2.zero;
                CancelMining();
                characterAnimator?.SetMoving(false);
                return;
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
            var primaryHeld = Input.GetMouseButton(0);
            if (!buildingPlacementActive && !pointerOverUi && primaryHeld)
            {
                if (attackCooldown <= 0f)
                {
                    var attacked = TryBasicAttack();
                    miningAllowedByLastSwing = !attacked || attack.LastHitCount == 0;
                }
                if (miningAllowedByLastSwing) TickMining();
                else ResetMiningProgress();
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
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
                airJumpConsumed = false;
            }
            var jumpHeld = IsJumpPressed();
            verticalVelocity = PlayerMovementPhysics.ApplyJumpCutWhileAscending(
                verticalVelocity, jumpHeld, jumpCutMultiplier);
            verticalVelocity = ApplyGravity(verticalVelocity, gravityAcceleration, maximumFallSpeed, deltaSeconds);
            body.linearVelocity = new Vector2(
                CalculateHorizontalVelocity(movementInput.x, currentMoveSpeed),
                verticalVelocity);
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
                verticalVelocity = jumpVelocity;
                grounded = false;
                airJumpConsumed = false;
                return;
            }
            if (!statSheet.HasDoubleJump || airJumpConsumed) return;
            verticalVelocity = CalculateJumpVelocityForHeightRatio(jumpVelocity,
                statSheet.DoubleJumpHeightRatio);
            airJumpConsumed = true;
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
                    grounded = true;
                    verticalVelocity = 0f;
                    airJumpConsumed = false;
                }
                else if (normal.y < -.5f && verticalVelocity > 0f)
                    verticalVelocity = 0f;
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

        private void LateUpdate()
        {
            if (!initialized || followCamera == null) return;
            var current = followCamera.transform.position;
            var target = new Vector3(transform.position.x, transform.position.y, current.z);
            var factor = cameraFollowSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-cameraFollowSharpness * Time.unscaledDeltaTime);
            followCamera.transform.position = Vector3.Lerp(current, target, factor);
        }

        private bool TryBasicAttack()
        {
            if (activeProfile == null || !activeProfile.HasBasicAttack || activeProfile.AttacksPerSecond <= 0f)
                return false;
            attack.Strike(facing);
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
            if (!TryResolveMiningCell(tileService, out var cell))
            {
                ResetMiningProgress();
                return;
            }
            var clawTier = ResolveMiningClawTier();
            var tile = tileService.GetTile(cell);
            var definitionId = ResolveMiningDefinitionId(tile.elementType);
            var definition = catalog?.FindMineralTier(definitionId);
            var requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
            if (!tileService.InBounds(cell) || tile.IsAir || clawTier < tile.hardness || requiredSeconds <= 0f)
            {
                ResetMiningProgress();
                return;
            }

            var companionCell = ResolveWideMiningCompanionCell(cell, transform.position.y);
            var companionRequiredSeconds = -1f;
            var hasCompanion = clawTier >= 3 && TryGetMiningSeconds(companionCell, clawTier,
                out companionRequiredSeconds);
            if (hasCompanion) requiredSeconds = Mathf.Max(requiredSeconds, companionRequiredSeconds);

            if (!miningActive || miningCell != cell ||
                miningHasCompanion != hasCompanion ||
                hasCompanion && miningCompanionCell != companionCell ||
                !Mathf.Approximately(miningRequiredSeconds, requiredSeconds))
            {
                ResetMiningProgress();
                miningActive = true;
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

            var reachSq = miningReach * miningReach;
            if (mouseWorld.HasValue)
            {
                var mouse = mouseWorld.Value;
                if (!float.IsNaN(mouse.x) && !float.IsInfinity(mouse.x) &&
                    !float.IsNaN(mouse.y) && !float.IsInfinity(mouse.y))
                {
                    var mouseCell = new Vector3Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y), 0);
                    if (TryPickSolidMiningCell(tileService, playerOrigin, mouseCell, reachSq, out cell))
                        return true;
                }
            }

            var direction = facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector2.down;
            var aim = playerOrigin + direction * miningReach;
            var aimCell = new Vector3Int(Mathf.FloorToInt(aim.x), Mathf.FloorToInt(aim.y), 0);
            return TryPickSolidMiningCell(tileService, playerOrigin, aimCell, reachSq, out cell);
        }

        private static bool TryPickSolidMiningCell(TileService tileService, Vector2 playerOrigin,
            Vector3Int primaryCell, float reachSq, out Vector3Int cell)
        {
            cell = default;
            foreach (var offset in MiningCellPickOffsets)
            {
                var candidate = primaryCell + offset;
                if (!IsMineableForegroundCell(tileService, candidate)) continue;
                if (!IsWithinMiningReach(playerOrigin, candidate, reachSq)) continue;
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

        private static bool IsWithinMiningReach(Vector2 playerOrigin, Vector3Int cell, float reachSq)
        {
            var center = new Vector2(cell.x + .5f, cell.y + .5f);
            return (center - playerOrigin).sqrMagnitude <= reachSq;
        }

        private static readonly Vector3Int[] MiningCellPickOffsets =
        {
            Vector3Int.zero,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right,
            new Vector3Int(-1, -1, 0),
            new Vector3Int(1, -1, 0),
            Vector3Int.up,
            new Vector3Int(-1, 1, 0),
            new Vector3Int(1, 1, 0),
        };

        private bool TryGetMiningSeconds(Vector3Int cell, int clawTier, out float requiredSeconds)
        {
            requiredSeconds = -1f;
            var tileService = bootstrap?.TileService;
            if (tileService == null || !tileService.InBounds(cell)) return false;
            var tile = tileService.GetTile(cell);
            if (tile.IsAir || clawTier < tile.hardness) return false;
            var definition = catalog?.FindMineralTier(ResolveMiningDefinitionId(tile.elementType));
            requiredSeconds = definition?.MiningSecondsForClawTier(clawTier) ?? -1f;
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
                    new Vector2(cell.x + .5f, cell.y + .5f));
                Nyangbingo.Core.GameEvents.RaiseMiningResult(cell, item.DisplayName, totalAmount, critical);
            }
        }

        private void CancelMining()
        {
            miningAllowedByLastSwing = false;
            ResetMiningProgress();
        }

        private void ResetMiningProgress()
        {
            if (miningActive)
                Nyangbingo.Core.GameEvents.RaiseMiningProgress(miningCell, 0f);
            miningActive = false;
            miningHasCompanion = false;
            miningElapsedSeconds = 0f;
            miningRequiredSeconds = 0f;
        }

        public static string ResolveMiningDefinitionId(string elementType) => elementType switch
        {
            WorldTileTypes.StoneMid => WorldTileTypes.Stone,
            WorldTileTypes.StoneDeep => WorldTileTypes.Stone,
            WorldTileTypes.RuinWall => WorldTileTypes.Stone,
            WorldTileTypes.IceLake => WorldTileTypes.IceShard,
            _ => elementType
        };

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
            if (wireSnare.TryUse(facing)) ShowAttackFeedback();
        }

        private void ShowAttackFeedback()
        {
            if (attackIndicator == null) return;
            attackIndicator.transform.localPosition = facing * .85f;
            attackIndicator.transform.right = facing;
            attackIndicator.enabled = true;
            attackIndicatorFrameIndex = 0;
            attackIndicatorFrameRemaining = .1f;
            var frames = gameplayArtCatalog?.PlayerAttackFrames;
            if (frames != null && frames.Count > 0) attackIndicator.sprite = frames[0];
            attackIndicatorRemaining = frames != null && frames.Count > 0
                ? Mathf.Max(.12f, frames.Count * .1f)
                : .12f;
        }

        private void TickAttackFeedback(float deltaTime)
        {
            var frames = gameplayArtCatalog?.PlayerAttackFrames;
            if (attackIndicator == null || frames == null || frames.Count <= 1) return;
            attackIndicatorFrameRemaining -= Mathf.Max(0f, deltaTime);
            while (attackIndicatorFrameRemaining <= 0f && attackIndicatorFrameIndex < frames.Count - 1)
            {
                attackIndicatorFrameIndex++;
                attackIndicator.sprite = frames[attackIndicatorFrameIndex];
                attackIndicatorFrameRemaining += .1f;
            }
        }

        private void RefreshEquipmentStats()
        {
            if (runtimeServices?.EquipmentSystem == null || health == null) return;
            statSheet.Recalculate(runtimeServices.EquipmentSystem);
            currentMoveSpeed = baseMoveSpeed * statSheet.MovementMultiplier;
            health.SetDefense(statSheet.Defense);
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
            respawnApplied = false;
            deathSequenceElapsed = 0f;
            movementInput = Vector2.zero;
            verticalVelocity = 0f;
            grounded = false;
            airJumpConsumed = false;
            characterAnimator?.SetMoving(false);
            characterAnimator?.PlayDeath();
            runtimeServices?.NapService?.Wake(NapWakeReason.PlayerDied);
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

            dead = false;
            if (health != null) health.RestoreCurrent(health.MaxHealth);
            var temperature = runtimeServices?.PlayerTemperature;
            if (temperature != null) temperature.Restore(temperature.StartingTemperature);
            if (playerCollider != null) playerCollider.enabled = true;
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
            transform.rotation = aliveRotation;
            SetDeathFadeAlpha(0f);
            Debug.Log("[Nyangbingo] MainGamePlayerController: 보금자리 리스폰 완료(HP 전량, 체온 시작값 복원).");
        }

        private void ApplyRespawn()
        {
            respawnApplied = true;
            var respawnPosition = initialSpawnPosition;
            if (environmentState != null &&
                environmentState.TryGetNearestPlacedObjectPosition(NestBedId, transform.position, out var nestPosition))
                respawnPosition = nestPosition;
            transform.position = respawnPosition;
            if (body != null) body.position = respawnPosition;
            verticalVelocity = 0f;
            grounded = false;
            airJumpConsumed = false;
            transform.rotation = aliveRotation;
            characterAnimator?.ResetToIdle();
            if (health != null) health.RestoreCurrent(health.MaxHealth);
            var temperature = runtimeServices?.PlayerTemperature;
            if (temperature != null) temperature.Restore(temperature.StartingTemperature);
            if (playerRenderer != null) playerRenderer.color = aliveRendererColor;
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

        public void TryToggleNapFromInteraction()
        {
            var nap = runtimeServices?.NapService;
            if (nap == null) return;
            if (nap.IsNapping)
            {
                nap.Wake(NapWakeReason.Manual);
                return;
            }
            if (runtimeServices?.CraftingProcess?.IsCrafting == true) return;

            var onNest = environmentState != null &&
                         environmentState.HasPlacedObjectWithin(
                             NestBedId, transform.position, NestInteractionRadius);
            var sealedArea = bootstrap?.SealSystem?.IsInsideSealedArea(transform.position) == true;
            nap.TryStart(onNest, sealedArea);
        }

        private bool TryOpenNearbyChest()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.HasWorld) return false;
            if (!TryResolveNearbyChestCell(session, out var cell)) return false;
            if (!session.TryOpenChestAt(cell, out var chestId, out var definition)) return false;

            var rewardSummary = FormatChestRewardSummary(definition, session.Seed, chestId);
            interactionMessages?.ShowExternalMessage($"상자 개봉: {rewardSummary}");
            worldDecorationRenderer?.MarkChestOpened(chestId);
            Debug.Log($"[Nyangbingo] Product chest interaction completed: {chestId}, region={definition.Id}, rewards={rewardSummary}.");
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

            if (followCamera != null)
            {
                var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
                var mouseCell = new Vector3Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y), 0);
                if (IsChestCellInReach(origin, mouseCell, reachSq) &&
                    session.TryPeekUnopenedChestAt(mouseCell))
                {
                    cell = mouseCell;
                    return true;
                }
            }

            var facingCell = Vector3Int.FloorToInt(origin +
                (facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector2.right));
            var currentCell = Vector3Int.FloorToInt(origin);
            if (IsChestCellInReach(origin, facingCell, reachSq) &&
                session.TryPeekUnopenedChestAt(facingCell))
            {
                cell = facingCell;
                return true;
            }
            if (IsChestCellInReach(origin, currentCell, reachSq) &&
                session.TryPeekUnopenedChestAt(currentCell))
            {
                cell = currentCell;
                return true;
            }

            return TryFindNearestUnopenedChestCell(session, origin, reachSq, out cell);
        }

        private static bool TryFindNearestUnopenedChestCell(WorldSessionController session, Vector2 origin,
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
                if (session.ChestProgress != null && session.ChestProgress.IsOpened(chest.id)) continue;
                var chestCell = new Vector3Int(chest.position.x, chest.position.y, 0);
                var center = new Vector2(chestCell.x + .5f, chestCell.y + .5f);
                var distSq = (center - origin).sqrMagnitude;
                if (distSq > reachSq || distSq >= bestDist) continue;
                bestDist = distSq;
                cell = chestCell;
                found = true;
            }
            return found;
        }

        private static bool IsChestCellInReach(Vector2 origin, Vector3Int cell, float reachSq)
        {
            var center = new Vector2(cell.x + .5f, cell.y + .5f);
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

        private void HandleDamaged(Nyangbingo.Core.DamageTag tag, int amount)
        {
            if (amount > 0) runtimeServices?.NapService?.Wake(NapWakeReason.PlayerDamaged);
        }

        private void HandleWallDamaged(float amount)
        {
            if (amount > 0f) runtimeServices?.NapService?.Wake(NapWakeReason.WallDamaged);
        }

        private void RefreshPortableLanternLight()
        {
            if (portableLanternLight != null)
                portableLanternLight.enabled = runtimeServices?.PortableLantern?.IsLit == true;
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshCombatProfile;
            if (runtimeServices?.ActiveSlot != null)
                runtimeServices.ActiveSlot.Changed -= RefreshCombatProfile;
            if (runtimeServices?.PortableLantern != null)
                runtimeServices.PortableLantern.Changed -= RefreshPortableLanternLight;
            if (runtimeServices?.EquipmentSystem != null)
                runtimeServices.EquipmentSystem.Changed -= RefreshEquipmentStats;
            if (health != null) health.Died -= HandleDied;
            if (health != null) health.Damaged -= HandleDamaged;
            if (runtimeServices?.PlayerTemperature != null)
                runtimeServices.PlayerTemperature.ReachedMaximum -= HandleTemperatureMaximum;
            if (runtimeServices?.DeathTearPouches != null)
                runtimeServices.DeathTearPouches.Changed -= RefreshTearPouchVisuals;
            if (raidTarget != null) raidTarget.WallDamaged -= HandleWallDamaged;
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
