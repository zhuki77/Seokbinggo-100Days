using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float CollisionSkin = .001f;
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
        [Header("Side-scroller movement")]
        [Min(.1f)][SerializeField] private float jumpVelocity = 7f;
        [Min(.1f)][SerializeField] private float gravityAcceleration = 20f;
        [Min(.1f)][SerializeField] private float maximumFallSpeed = 14f;

        private readonly StatSheet statSheet = new StatSheet();
        private Rigidbody2D body;
        private CircleCollider2D playerCollider;
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
        private readonly Dictionary<string, GameObject> tearPouchVisuals =
            new Dictionary<string, GameObject>();
        private bool initialized;
        private MainGameEnvironmentState environmentState;
        private MainGameTurretRuntime placedObjectInteractions;
        private MainGameWorldDecorationRenderer worldDecorationRenderer;
        private MainGameRaidTarget raidTarget;
        private MainGameEncounterCoordinator encounterCoordinator;
        private Nyangbingo.UI.MainGameBossSummonUiController interactionMessages;

        public bool IsInitialized => initialized;
        public string ActiveCombatProfileId => activeProfile != null ? activeProfile.Id : string.Empty;
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

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            playerCollider.radius = .38f;
            playerCollider.isTrigger = false;
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

            runtimeServices.PlayerInventory.Changed += RefreshCombatProfile;
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
            grounded = HasGroundBelow(body.position);
            initialized = activeProfile != null;
            if (initialized)
                Debug.Log($"[Nyangbingo] MainGamePlayerController: 이동·체력·근접 공격·카메라 연결 완료 " +
                          $"(speed={currentMoveSpeed:0.##}, profile={ActiveCombatProfileId}).");
            return initialized;
        }

        private void Update()
        {
            if (!initialized) return;
            if (dead)
            {
                movementInput = Vector2.zero;
                CancelMining();
                TickDeathSequence(Time.deltaTime);
                return;
            }
            if (Nyangbingo.UI.MainGameCraftingUiController.BlocksGameplayInput)
            {
                movementInput = Vector2.zero;
                CancelMining();
                characterAnimator?.SetMoving(false);
                return;
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
            var buildingPlacementActive = MainGameTurretRuntime.BlocksCombatInput;
            var primaryHeld = Input.GetMouseButton(0);
            if (!buildingPlacementActive && !pointerOverUi && primaryHeld)
            {
                if (attackCooldown <= 0f)
                {
                    TryBasicAttack();
                    miningAllowedByLastSwing = activeProfile?.HitsWalls == true && attack.LastHitCount == 0;
                }
                if (miningAllowedByLastSwing) TickMining();
                else ResetMiningProgress();
            }
            else CancelMining();
            if (!buildingPlacementActive && !pointerOverUi && Input.GetMouseButtonDown(1))
                TryFanAbility();
        }

        private void FixedUpdate()
        {
            if (!initialized || dead || body == null) return;
            var deltaSeconds = Time.fixedDeltaTime;
            verticalVelocity = ApplyGravity(verticalVelocity, gravityAcceleration, maximumFallSpeed, deltaSeconds);
            var displacement = new Vector2(
                CalculateHorizontalVelocity(movementInput.x, currentMoveSpeed) * deltaSeconds,
                verticalVelocity * deltaSeconds);
            MoveWithTileCollision(displacement);
        }

        private void ApplyGeneratedWorldSpawn()
        {
            var session = bootstrap?.Session;
            if (session?.HasWorld != true || !session.LastResult.passedValidation) return;
            var cell = session.LastResult.spawnPoint;
            var spawn = TryFindSafeSurfaceSpawn(session.TileService, cell.x, out var surfaceSpawn)
                ? surfaceSpawn
                : new Vector2(cell.x + .5f, cell.y + .5f);
            transform.position = spawn;
            body.position = spawn;
            Debug.Log($"[Nyangbingo] MainGamePlayerController: safe surface spawn applied " +
                      $"(generated={cell}, player={spawn}).");
        }

        private bool TryFindSafeSurfaceSpawn(TileService tileService, int centerX, out Vector2 spawn)
        {
            spawn = default;
            if (tileService == null || tileService.Width <= 0 || tileService.Height <= 2) return false;
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
            centerX = Mathf.Clamp(centerX, 0, tileService.Width - 1);
            var minimumSurfaceY = EstimateSurfaceBandFloor(tileService);

            for (var distance = 0; distance < tileService.Width; distance++)
            {
                if (TryColumn(centerX + distance, out spawn)) return true;
                if (distance > 0 && TryColumn(centerX - distance, out spawn)) return true;
            }
            return false;

            bool TryColumn(int x, out Vector2 candidate)
            {
                candidate = default;
                if (x < 0 || x >= tileService.Width) return false;
                for (var y = tileService.Height - 2; y >= minimumSurfaceY; y--)
                {
                    var groundCell = new Vector3Int(x, y, 0);
                    var ground = tileService.GetTile(groundCell);
                    if (ground.IsAir || !ground.isNaturalTerrain) continue;
                    var feetCell = new Vector3Int(x, y + 1, 0);
                    if (!tileService.GetTile(feetCell).IsAir) continue;
                    if (y + 2 < tileService.Height &&
                        !tileService.GetTile(new Vector3Int(x, y + 2, 0)).IsAir) continue;
                    candidate = new Vector2(x + .5f, y + 1f + halfExtent + CollisionSkin);
                    return true;
                }
                return false;
            }
        }

        private static int EstimateSurfaceBandFloor(TileService tileService)
        {
            var heightCounts = new int[tileService.Height];
            var columnsWithNaturalTerrain = 0;
            for (var x = 0; x < tileService.Width; x++)
            {
                for (var y = tileService.Height - 2; y >= 0; y--)
                {
                    var tile = tileService.GetTile(new Vector3Int(x, y, 0));
                    if (tile.IsAir || !tile.isNaturalTerrain) continue;
                    heightCounts[y]++;
                    columnsWithNaturalTerrain++;
                    break;
                }
            }

            if (columnsWithNaturalTerrain <= 0) return tileService.Height / 2;
            var medianIndex = (columnsWithNaturalTerrain - 1) / 2;
            var accumulated = 0;
            var medianHeight = tileService.Height / 2;
            for (var y = 0; y < heightCounts.Length; y++)
            {
                accumulated += heightCounts[y];
                if (accumulated <= medianIndex) continue;
                medianHeight = y;
                break;
            }

            var terrainTolerance = Mathf.Max(4, tileService.Height / 20);
            return Mathf.Max(0, medianHeight - terrainTolerance);
        }

        private void UpdateAimDirection()
        {
            if (followCamera == null || body == null) return;
            var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
            var aim = (Vector2)mouse - body.position;
            if (aim.sqrMagnitude > Mathf.Epsilon) facing = aim.normalized;
        }

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

        private void MoveWithTileCollision(Vector2 displacement)
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null)
            {
                body.MovePosition(body.position + displacement);
                grounded = false;
                return;
            }

            var position = body.position;
            position.x = ResolveHorizontal(position, displacement.x);
            position.y = ResolveVertical(position, displacement.y);
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
            position.x = Mathf.Clamp(position.x, halfExtent, tileService.Width - halfExtent);
            position.y = Mathf.Clamp(position.y, halfExtent, tileService.Height - halfExtent);
            body.MovePosition(position);

            grounded = verticalVelocity <= 0f && HasGroundBelow(position);
            if (grounded)
            {
                verticalVelocity = 0f;
                airJumpConsumed = false;
            }
        }

        private float ResolveHorizontal(Vector2 position, float displacement)
        {
            if (Mathf.Abs(displacement) <= Mathf.Epsilon) return position.x;
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
            var targetX = position.x + displacement;
            var minY = Mathf.FloorToInt(position.y - halfExtent + CollisionSkin);
            var maxY = Mathf.FloorToInt(position.y + halfExtent - CollisionSkin);
            var direction = displacement > 0f ? 1 : -1;
            var startCell = Mathf.FloorToInt(position.x + direction * halfExtent);
            var endCell = Mathf.FloorToInt(targetX + direction * halfExtent);

            for (var x = startCell; direction > 0 ? x <= endCell : x >= endCell; x += direction)
            {
                var blocked = false;
                for (var y = minY; y <= maxY && !blocked; y++) blocked = IsSolidCell(x, y);
                if (!blocked) continue;
                return direction > 0
                    ? x - halfExtent - CollisionSkin
                    : x + 1f + halfExtent + CollisionSkin;
            }
            return targetX;
        }

        private float ResolveVertical(Vector2 position, float displacement)
        {
            if (Mathf.Abs(displacement) <= Mathf.Epsilon) return position.y;
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
            var targetY = position.y + displacement;
            var minX = Mathf.FloorToInt(position.x - halfExtent + CollisionSkin);
            var maxX = Mathf.FloorToInt(position.x + halfExtent - CollisionSkin);
            var direction = displacement > 0f ? 1 : -1;
            var startCell = Mathf.FloorToInt(position.y + direction * halfExtent);
            var endCell = Mathf.FloorToInt(targetY + direction * halfExtent);

            for (var y = startCell; direction > 0 ? y <= endCell : y >= endCell; y += direction)
            {
                var blocked = false;
                for (var x = minX; x <= maxX && !blocked; x++) blocked = IsSolidCell(x, y);
                if (!blocked) continue;
                verticalVelocity = 0f;
                return direction > 0
                    ? y - halfExtent - CollisionSkin
                    : y + 1f + halfExtent + CollisionSkin;
            }
            return targetY;
        }

        private bool HasGroundBelow(Vector2 position)
        {
            var halfExtent = playerCollider != null ? playerCollider.radius : .38f;
            var y = Mathf.FloorToInt(position.y - halfExtent - CollisionSkin * 2f);
            var minX = Mathf.FloorToInt(position.x - halfExtent + CollisionSkin);
            var maxX = Mathf.FloorToInt(position.x + halfExtent - CollisionSkin);
            for (var x = minX; x <= maxX; x++)
                if (IsSolidCell(x, y)) return true;
            return false;
        }

        private bool IsSolidCell(int x, int y)
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null) return false;
            var cell = new Vector3Int(x, y, 0);
            return !tileService.InBounds(cell) || !tileService.GetTile(cell).IsAir;
        }

        public static float CalculateHorizontalVelocity(float input, float moveSpeed)
        {
            if (float.IsNaN(input) || float.IsInfinity(input) ||
                float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed)) return 0f;
            return Mathf.Clamp(input, -1f, 1f) * Mathf.Max(0f, moveSpeed);
        }

        public static float ApplyGravity(float currentVelocity, float gravity, float maxFallSpeed,
            float deltaSeconds)
        {
            if (float.IsNaN(currentVelocity) || float.IsInfinity(currentVelocity)) currentVelocity = 0f;
            if (float.IsNaN(gravity) || float.IsInfinity(gravity)) gravity = 0f;
            if (float.IsNaN(maxFallSpeed) || float.IsInfinity(maxFallSpeed)) maxFallSpeed = 0f;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) deltaSeconds = 0f;
            return Mathf.Max(-Mathf.Max(0f, maxFallSpeed),
                currentVelocity - Mathf.Max(0f, gravity) * Mathf.Max(0f, deltaSeconds));
        }

        public static float CalculateJumpVelocityForHeightRatio(float baseJumpVelocity, float heightRatio)
        {
            if (float.IsNaN(baseJumpVelocity) || float.IsInfinity(baseJumpVelocity) ||
                float.IsNaN(heightRatio) || float.IsInfinity(heightRatio)) return 0f;
            return Mathf.Max(0f, baseJumpVelocity) * Mathf.Sqrt(Mathf.Max(0f, heightRatio));
        }

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

        private void TryBasicAttack()
        {
            if (activeProfile == null || !activeProfile.HasBasicAttack || activeProfile.AttacksPerSecond <= 0f) return;
            attack.Strike(facing);
            characterAnimator?.PlayAttack();
            ShowAttackFeedback();
            attackCooldown = 1f / activeProfile.AttacksPerSecond;
            if (!loggedFirstAttackInput || attack.LastHitCount > 0 && !loggedFirstAttackHit)
            {
                Debug.Log($"[Nyangbingo] Player attack accepted (profile={activeProfile.Id}, hits={attack.LastHitCount}).");
                loggedFirstAttackInput = true;
                loggedFirstAttackHit |= attack.LastHitCount > 0;
            }
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
        /// 마우스 아래 칸이 사거리 안이면 그 칸을 우선하고, 아니면 조준 방향 × 사거리 칸을 쓴다.
        /// (조준만으로 짧은 레이를 쓰면 벽 앞 공기만 계속 선택되는 경우가 있다.)
        /// </summary>
        private bool TryResolveMiningCell(TileService tileService, out Vector3Int cell)
        {
            cell = default;
            var origin = (Vector2)transform.position;
            if (followCamera != null)
            {
                var mouse = followCamera.ScreenToWorldPoint(Input.mousePosition);
                var mouseCell = new Vector3Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y), 0);
                var mouseCenter = new Vector2(mouseCell.x + .5f, mouseCell.y + .5f);
                if (tileService.InBounds(mouseCell) &&
                    (mouseCenter - origin).sqrMagnitude <= MiningReach * MiningReach)
                {
                    cell = mouseCell;
                    return true;
                }
            }

            var direction = facing.sqrMagnitude > Mathf.Epsilon ? facing.normalized : Vector2.down;
            var target = origin + direction * MiningReach;
            cell = new Vector3Int(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.y), 0);
            return tileService.InBounds(cell);
        }

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
            if (tileService == null ||
                !tileService.TryBreakForeground(cell, clawTier, out var itemId, out var amount)) return;

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
                ItemAcquisition.Request(item, amount);
                totalAmount += amount;
                Nyangbingo.Core.GameEvents.RaiseMiningCritical();
            }

            if (item != null && totalAmount > 0)
                Nyangbingo.Core.GameEvents.RaiseMiningResult(cell, item.DisplayName, totalAmount, critical);
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
            var profileId = inventory != null && inventory.Count(CheolseonId) > 0
                ? CheolseonId
                : inventory != null && inventory.Count(HapjukseonId) > 0
                    ? HapjukseonId
                    : inventory != null && inventory.Count(IceSteelClawId) > 0
                        ? IceSteelClawId
                        : inventory != null && inventory.Count(IronClawId) > 0
                            ? IronClawId
                            : BareClawId;
            var profile = catalog != null ? catalog.FindCombatProfile(profileId) : null;
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
            {
                var collapse = Mathf.Clamp01(deathSequenceElapsed / CollapseSeconds);
                transform.rotation = Quaternion.Lerp(aliveRotation, aliveRotation * Quaternion.Euler(0f, 0f, -90f), collapse);
                return;
            }

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
            grounded = HasGroundBelow(respawnPosition);
            airJumpConsumed = false;
            transform.rotation = aliveRotation;
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
            if (session == null) return false;
            var currentCell = Vector3Int.FloorToInt(transform.position);
            var targetCell = Vector3Int.FloorToInt((Vector2)transform.position + facing.normalized);
            if (!session.TryOpenChestAt(targetCell, out var chestId, out var definition) &&
                (targetCell == currentCell || !session.TryOpenChestAt(currentCell, out chestId, out definition)))
                return false;
            interactionMessages?.ShowExternalMessage($"상자 개봉: {definition.Id}");
            worldDecorationRenderer?.MarkChestOpened(chestId);
            Debug.Log($"[Nyangbingo] Product chest interaction completed: {chestId}, region={definition.Id}.");
            return true;
        }

        private void HandleDamaged(Nyangbingo.Core.DamageTag tag, int amount)
        {
            if (amount > 0) runtimeServices?.NapService?.Wake(NapWakeReason.PlayerDamaged);
        }

        private void HandleWallDamaged(float amount)
        {
            if (amount > 0f) runtimeServices?.NapService?.Wake(NapWakeReason.WallDamaged);
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshCombatProfile;
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

        private void Update()
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (remaining <= 0f && spriteRenderer != null) spriteRenderer.color = baseColor;
        }
    }
}
