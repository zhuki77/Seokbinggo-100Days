using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.Yokai;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(-60)]
    public sealed class MainGameTurretRuntime : MonoBehaviour, IGameSecondsTickable
    {
        public const bool ProductHudNarrativeTextEnabled = false;
        public const string NearbyInteractionPrompt =
            "E · 상호작용    좌클릭 유지 · 회수";

        private sealed class TurretEntry
        {
            public string ObjectId;
            public Transform Origin;
            public TurretController Controller;
            public Action<Health, int> FireHandler;
        }

        private sealed class LanternEntry
        {
            public string ObjectId;
            public string DefinitionId;
            public string FuelItemId;
            public float FuelSecondsPerUnit;
            public GameObject RuntimeRoot;
            public Light2D Light;
            public CounterAura Aura;
            public float FuelRemaining;

            public void Tick(float deltaGameSeconds)
            {
                if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds) ||
                    FuelRemaining <= 0f) return;
                FuelRemaining = Mathf.Max(0f, FuelRemaining - deltaGameSeconds);
                RefreshActiveState();
            }

            public bool AddFuel(float seconds)
            {
                if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds) ||
                    seconds > float.MaxValue - FuelRemaining) return false;
                FuelRemaining += seconds;
                RefreshActiveState();
                return true;
            }

            public bool RestoreFuel(float seconds)
            {
                if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
                FuelRemaining = seconds;
                RefreshActiveState();
                return true;
            }

            public void RefreshActiveState()
            {
                var active = FuelRemaining > 0f;
                if (Light != null) Light.enabled = active;
                if (Aura != null) Aura.enabled = active;
            }
        }

        private const string TurretItemId = "dokkaebi_fire_tower";
        public const string LanternItemId = "lantern";
        public const string FrostLanternItemId = "frost_lantern";
        public const string SieveItemId = "sieve";
        public const string IronSieveItemId = "iron_sieve";
        public const string HaetaeStatueItemId = "haetae_statue";
        public const string BellRopeItemId = "bell_rope";
        public const string IronBellRopeItemId = "iron_bell_rope";
        private const string FuelItemId = "coal";
        private const string FrostLanternFuelItemId = "frost_essence";
        private const string FrostLanternFuelSecondsKey = "frost_lantern_fuel_sec";
        private const string IceFuelItemId = "ice_shard";
        private const float RetargetSeconds = .2f;
        private const float FireSeconds = 1f;
        private const float AttackRange = 8f;
        private const int AttackDamage = 10;
        private const float FuelSecondsPerUnit = 270f;
        private const float InstalledLanternRadius = 6f;
        private const string EoduksiniBloomPauseKey = "eoduksini_bloom_pause_sec";
        private const string EoduksiniRebloomCooldownKey = "eoduksini_rebloom_cd_sec";
        private const float ProjectileSpeed = 6f;
        private const float ProjectileHitDistance = .1f;
        private const float InteractionRange = 2.5f;
        private static bool anyPlacementPreviewActive;
        private static int placementPointerConsumedFrame = -1;
        private static int placementEscapeConsumedFrame = -1;

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameEnvironmentState environmentState;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private BuildingArtCatalog buildingArtCatalog;
        [SerializeField] private MainGamePlayerController playerController;
        [SerializeField] private Text interactionStatusText;

        private readonly Dictionary<string, TurretEntry> turrets =
            new Dictionary<string, TurretEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, LanternEntry> lanterns =
            new Dictionary<string, LanternEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> passiveCounterAuraRoots =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<CounterAura> activeCounterAuras = new List<CounterAura>();
        private readonly List<SpriteRenderer> projectileRenderers = new List<SpriteRenderer>();
        private HomingProjectilePool projectilePool;
        private MainGameBossSummonUiController craftingStationUi;
        private MainGameCraftingUiController productCraftingUi;
        private BossManager bossManager;
        private GameObject placementPreview;
        private Transform placementPreviewVisual;
        private SpriteRenderer placementPreviewRenderer;
        private LineRenderer placementRangeRenderer;
        private Material placementRangeMaterial;
        private Vector2 placementPosition;
        private bool placementValid;
        private bool registered;
        private Camera placementCamera;
        private string placementDefinitionId;

        public bool HasSceneBindings => gameDataCatalog != null && runtimeServices != null &&
                                        environmentState != null && gameplayArtCatalog != null &&
                                        buildingArtCatalog != null && playerController != null &&
                                        interactionStatusText != null;
        public int ActiveTurretCount => turrets.Count;
        public int ActiveDamageTurretCount
        {
            get
            {
                var count = 0;
                foreach (var pair in turrets)
                {
                    if (pair.Value?.Controller == null) continue;
                    // Turret map currently only holds damage towers (dokkaebi_fire_tower).
                    count++;
                }
                return count;
            }
        }
        public IReadOnlyList<CounterAura> ActiveCounterAuras => activeCounterAuras;
        public bool IsPlacementPreviewActive => placementPreview != null;
        public bool IsPlacementPreviewValid => IsPlacementPreviewActive && placementValid;
        public bool IsBottomInteractionPromptVisible { get; private set; }
        public static bool BlocksCombatInput => anyPlacementPreviewActive ||
                                                placementPointerConsumedFrame == Time.frameCount;
        public static bool ConsumedEscapeThisFrame => placementEscapeConsumedFrame == Time.frameCount;
        public int TurretItemCount => runtimeServices?.PlayerInventory?.Count(TurretItemId) ?? 0;
        public int CoalCount => runtimeServices?.PlayerInventory?.Count(FuelItemId) ?? 0;
        public bool IsCrafting => runtimeServices?.CraftingProcess?.IsCrafting == true;
        public RecipeDefinition TurretRecipe => gameDataCatalog?.FindRecipe(TurretItemId);
        public event Action BuildStateChanged;

        public int GetInventoryCount(string itemId) =>
            string.IsNullOrEmpty(itemId) ? 0 : runtimeServices?.PlayerInventory?.Count(itemId) ?? 0;

        public bool CanPlaceByTurretSlots(string definitionId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(definitionId) ||
                (!SeokbinggoRules.IsDamageTurret(definitionId) &&
                 !string.Equals(definitionId, TurretItemId, StringComparison.Ordinal)))
                return true;

            var stage = runtimeServices?.Seokbinggo?.Stage ?? 0;
            var damageCap = SeokbinggoRules.DefaultDamageSlotCap;
            var damageGlobal = gameDataCatalog?.FindGlobal(GlobalKeys.TurretDamageSlotCap);
            if (damageGlobal != null && damageGlobal.TryGetInt(out var configuredCap) && configuredCap >= 0)
                damageCap = configuredCap;

            var isDamage = SeokbinggoRules.IsDamageTurret(definitionId);
            if (SeokbinggoRules.CanPlaceTurret(stage, ActiveTurretCount, isDamage, ActiveDamageTurretCount,
                    damageCap))
                return true;

            if (ActiveTurretCount >= SeokbinggoRules.TurretSlotCap(stage))
            {
                reason = stage <= 0
                    ? "석빙고를 승급해야 터렛을 설치할 수 있습니다."
                    : $"터렛 슬롯 가득 ({ActiveTurretCount}/{SeokbinggoRules.TurretSlotCap(stage)}) · 석빙고 {stage}단계";
                return false;
            }

            reason = $"화력 터렛 상한 ({ActiveDamageTurretCount}/{damageCap})";
            return false;
        }

        public void ConfigureForScene(GameDataCatalog catalog, MainGameRuntimeServices services,
            MainGameEnvironmentState environment, GameplayArtCatalog gameplayArt,
            BuildingArtCatalog buildingArt, MainGamePlayerController player)
        {
            gameDataCatalog = catalog;
            runtimeServices = services;
            environmentState = environment;
            gameplayArtCatalog = gameplayArt;
            buildingArtCatalog = buildingArt;
            playerController = player;
        }

        public void BindInteractionStatus(Text statusText)
        {
            if (interactionStatusText != null && interactionStatusText != statusText)
            {
                interactionStatusText.text = string.Empty;
                interactionStatusText.gameObject.SetActive(false);
            }
            interactionStatusText = statusText;
            if (interactionStatusText != null)
                interactionStatusText.gameObject.SetActive(true);
            ConfigureBottomInteractionStatus();
        }

        private void Start()
        {
            ConfigureBottomInteractionStatus();
            projectilePool = new HomingProjectilePool(4);
            craftingStationUi = FindAnyObjectByType<MainGameBossSummonUiController>();
            productCraftingUi = FindAnyObjectByType<MainGameCraftingUiController>();
            bossManager = FindAnyObjectByType<BossManager>();
            registered = runtimeServices != null && runtimeServices.Register(this);
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed += HandleInventoryChanged;
            if (!registered)
                Debug.LogError("[Nyangbingo] MainGameTurretRuntime: central game-seconds tick registration failed.");
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (MainGameCraftingUiController.BlocksGameplayInput)
            {
                RefreshInteractionStatus();
                return;
            }
            if (IsPlacementPreviewActive)
            {
                UpdatePlacementPreview();
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    placementEscapeConsumedFrame = Time.frameCount;
                    CancelPlacementPreview();
                    return;
                }
                var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!pointerOverUi && Input.GetMouseButtonDown(0))
                {
                    placementPointerConsumedFrame = Time.frameCount;
                    ConfirmPlacementPreview();
                    return;
                }
                if (!pointerOverUi && Input.GetMouseButtonDown(1))
                {
                    placementPointerConsumedFrame = Time.frameCount;
                    CancelPlacementPreview();
                    return;
                }
                RefreshInteractionStatus();
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    GrantFuelForEditorTest();
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    GrantCraftingMaterialsForEditorTest();
                else GrantTurretItemForEditorTest();
            }
#endif
            RefreshInteractionStatus();
        }

        public void Tick(float deltaGameSeconds)
        {
            foreach (var entry in turrets.Values) entry.Controller.Tick(deltaGameSeconds);
            foreach (var entry in lanterns.Values) entry.Tick(deltaGameSeconds);
            projectilePool?.Tick(deltaGameSeconds);
            RefreshProjectileVisuals();
        }

        public bool TryStartCraftingFromUi()
        {
            if (!HasSceneBindings || !runtimeServices.Initialize() || !environmentState.Initialize())
            {
                ShowMessage("등탑 시스템 배선이 준비되지 않았습니다.");
                return false;
            }
            var recipe = TurretRecipe;
            if (recipe == null)
            {
                ShowMessage("도깨비불 등탑 제작법을 찾을 수 없습니다.");
                return false;
            }
            if (craftingStationUi == null)
                craftingStationUi = FindAnyObjectByType<MainGameBossSummonUiController>();
            if (craftingStationUi == null || !craftingStationUi.IsPlayerNearStation(recipe.Station))
            {
                ShowMessage("작업대 근처에서 제작할 수 있습니다.");
                return false;
            }
            if (!runtimeServices.CraftingProcess.TryStart(recipe, recipe.Station))
            {
                ShowMessage("등탑 재료가 부족하거나 다른 제작이 진행 중입니다.");
                return false;
            }
            ShowMessage($"도깨비불 등탑 제작 시작: {recipe.DurationSeconds:0}초");
            Debug.Log($"[Nyangbingo] Turret crafting started: {recipe.Id}, duration={recipe.DurationSeconds:0.##}.");
            BuildStateChanged?.Invoke();
            return true;
        }

        public bool BeginPlacementPreview() => BeginPlacementPreview(TurretItemId);

        public bool BeginPlacementPreview(string definitionId)
        {
            if (IsPlacementPreviewActive) return true;
            var item = gameDataCatalog?.FindItem(definitionId);
            if (item == null || item.MvpScope == ItemMvpScope.B || GetInventoryCount(definitionId) <= 0)
            {
                ShowMessage("설치할 완성품이 없습니다.");
                return false;
            }

            placementDefinitionId = definitionId;
            placementPreview = new GameObject($"{definitionId}PlacementPreview");
            placementCamera = Camera.main;
            anyPlacementPreviewActive = true;
            var visualObject = new GameObject("Art");
            visualObject.transform.SetParent(placementPreview.transform, false);
            placementPreviewVisual = visualObject.transform;
            placementPreviewRenderer = visualObject.AddComponent<SpriteRenderer>();
            placementPreviewRenderer.sortingOrder = 30;
            placementPreviewRenderer.sprite = buildingArtCatalog?.Find(definitionId)?.Sprite;
            if (placementPreviewRenderer.sprite == null)
                RuntimePlaceholderVisual.Configure(placementPreviewRenderer, Color.white, 1f, 30);

            if (definitionId == TurretItemId)
            {
                placementRangeRenderer = placementPreview.AddComponent<LineRenderer>();
                placementRangeRenderer.useWorldSpace = false;
                placementRangeRenderer.loop = true;
                placementRangeRenderer.positionCount = 64;
                placementRangeRenderer.startWidth = .08f;
                placementRangeRenderer.endWidth = .08f;
                placementRangeRenderer.sortingOrder = 29;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    placementRangeMaterial = new Material(shader);
                    placementRangeRenderer.sharedMaterial = placementRangeMaterial;
                }
                for (var index = 0; index < placementRangeRenderer.positionCount; index++)
                {
                    var angle = index * Mathf.PI * 2f / placementRangeRenderer.positionCount;
                    placementRangeRenderer.SetPosition(index,
                        new Vector3(Mathf.Cos(angle) * AttackRange, Mathf.Sin(angle) * AttackRange, 0f));
                }
            }
            UpdatePlacementPreview();
            BuildStateChanged?.Invoke();
            return true;
        }

        public bool ConfirmPlacementPreview()
        {
            if (!IsPlacementPreviewActive || !placementValid)
            {
                var withinReach = playerController != null &&
                                  MainGameTilePaletteController.IsWithinPlacementReach(
                                      playerController.transform.position, placementPosition,
                                      MainGameTilePaletteController.PlacementReachTiles);
                ShowMessage(withinReach
                    ? "붉은 미리보기 위치에는 설치할 수 없습니다."
                    : "설치 거리가 너무 멉니다.");
                return false;
            }
            var placed = TryPlaceTurretAt(placementPosition);
            if (placed) CancelPlacementPreview();
            return placed;
        }

        public void CancelPlacementPreview()
        {
            if (placementPreview != null) Destroy(placementPreview);
            if (placementRangeMaterial != null) Destroy(placementRangeMaterial);
            placementPreview = null;
            placementPreviewVisual = null;
            placementPreviewRenderer = null;
            placementRangeRenderer = null;
            placementRangeMaterial = null;
            placementValid = false;
            placementCamera = null;
            placementDefinitionId = null;
            anyPlacementPreviewActive = false;
            BuildStateChanged?.Invoke();
        }

        public bool CaptureProgress(SaveGame save)
        {
            if (save == null) return false;
            save.turretFuel.Clear();
            foreach (var entry in turrets.Values.OrderBy(value => value.ObjectId, StringComparer.Ordinal))
                if (!WorldSaveAdapter.CaptureTurretFuel(save, entry.ObjectId, entry.Controller)) return false;
            foreach (var entry in lanterns.Values.OrderBy(value => value.ObjectId, StringComparer.Ordinal))
                save.turretFuel.Add(new TurretFuelRecord
                {
                    objectId = entry.ObjectId,
                    remainingGameSeconds = entry.FuelRemaining,
                    storesGameSeconds = true
                });
            return true;
        }

        public bool RestoreProgress(SaveGame save)
        {
            if (save == null) return false;
            CancelPlacementPreview();
            ClearTurrets();
            ClearLanterns();
            projectilePool = new HomingProjectilePool(4);
            foreach (var record in environmentState.ExportPlacedObjects())
            {
                var hasFuelRecord = save.turretFuel.Any(value =>
                    string.Equals(value.objectId, record.objectId, StringComparison.Ordinal));
                if (string.Equals(record.definitionId, TurretItemId, StringComparison.Ordinal))
                {
                    if (!TryRegisterPlacedTurret(record.objectId, 0, out var entry)) return false;
                    if (hasFuelRecord && !WorldSaveAdapter.RestoreTurretFuel(save, record.objectId, entry.Controller))
                        return false;
                }
                else if (IsInstalledLanternDefinition(record.definitionId))
                {
                    if (!TryRegisterPlacedLantern(
                            record.objectId, record.definitionId, out var lantern))
                        return false;
                    if (hasFuelRecord && !TryRestoreLanternFuel(save, record.objectId, lantern)) return false;
                }
                else if (TryGetPassiveCounterAuraConfiguration(
                             record.definitionId, out _, out _, out _, out _, out _) &&
                         !TryRegisterPlacedCounterAura(record.objectId, record.definitionId))
                    return false;
            }
            var restoredFuel = string.Join(", ", turrets.Values
                .OrderBy(value => value.ObjectId, StringComparer.Ordinal)
                .Select(value => $"{value.ObjectId}:{value.Controller.FuelRemaining:0.0}"));
            Debug.Log($"[Nyangbingo] Turret placement/fuel restore completed: count={turrets.Count}, " +
                      $"fuel=[{restoredFuel}].");
            BuildStateChanged?.Invoke();
            return true;
        }

        private void UpdatePlacementPreview()
        {
            if (!IsPlacementPreviewActive || playerController == null) return;
            if (placementCamera != null)
                placementPosition = placementCamera.ScreenToWorldPoint(Input.mousePosition);
            else
                placementPosition = (Vector2)playerController.transform.position +
                                    playerController.HorizontalFacingDirection * 2f;
            var tileService = environmentState?.TileService;
            var placementCell = tileService != null
                ? tileService.WorldToCell(placementPosition)
                : new Vector3Int(Mathf.FloorToInt(placementPosition.x),
                    Mathf.FloorToInt(placementPosition.y), 0);
            placementPosition = tileService != null
                ? (Vector2)tileService.GetCellCenterWorld(placementCell)
                : new Vector2(placementCell.x + .5f, placementCell.y + .5f);
            placementPreview.transform.position = placementPosition;
            if (placementPreviewVisual != null)
                placementPreviewVisual.localPosition = Vector3.zero;
            tileService?.AlignSpriteBoundsToCellBase(
                placementPreviewRenderer, placementCell);
            placementValid = MainGameTilePaletteController.IsWithinPlacementReach(
                                 playerController.transform.position, placementPosition,
                                 MainGameTilePaletteController.PlacementReachTiles) &&
                             environmentState.CanPlaceDefinitionAt(
                                 placementDefinitionId, placementPosition) &&
                             GetInventoryCount(placementDefinitionId) > 0 &&
                             CanPlaceByTurretSlots(placementDefinitionId, out _);
            var color = placementValid ? new Color(.35f, 1f, .75f, .65f) : new Color(1f, .25f, .25f, .65f);
            if (placementPreviewRenderer != null) placementPreviewRenderer.color = color;
            if (placementRangeRenderer != null)
            {
                placementRangeRenderer.startColor = color;
                placementRangeRenderer.endColor = color;
            }
        }

        private bool TryPlaceTurretAt(Vector2 position)
        {
            var definitionId = placementDefinitionId;
            var item = gameDataCatalog?.FindItem(definitionId);
            if (item == null || item.MvpScope == ItemMvpScope.B) return false;
            if (!CanPlaceByTurretSlots(definitionId, out var slotReason))
            {
                ShowMessage(slotReason);
                return false;
            }
            var record = new PlacedObjectRecord
            {
                objectId = $"{definitionId}_{Guid.NewGuid():N}",
                definitionId = definitionId,
                position = position,
                rotationDegrees = 0f
            };
            if (!runtimeServices.PlayerInventory.TryRemove(definitionId, 1)) return false;
            // MainGameEnvironmentState applies the authoritative seal whitelist internally.
            // Passing the placement through as a barrier candidate lets insul_wall/door/roof seal,
            // while lanterns, storage and other non-whitelisted placeables remain non-sealing.
            var placed = environmentState.TryPlace(record, barrierActive: true);
            var runtimeRegistered = placed && TryRegisterPlacedObjectRuntime(record);
            if (!placed || !runtimeRegistered)
            {
                environmentState.TryRemove(record.objectId);
                runtimeServices.PlayerInventory.TryAdd(definitionId, 1);
                ShowMessage("해당 위치에는 설치할 수 없습니다.");
                return false;
            }
            ShowMessage(CoolingSourceRuntime.IsCoolingDefinition(definitionId)
                ? $"{item.DisplayName} 설치 완료 · 가까이에서 E로 상태를 확인하세요."
                : $"{item.DisplayName} 설치 완료");
            Debug.Log($"[Nyangbingo] Product placeable installed: id={record.objectId}, " +
                      $"definition={definitionId}, position={record.position}.");
            BuildStateChanged?.Invoke();
            return true;
        }

        public bool TryInteractNearestPlacedObject()
        {
            if (!TryGetNearestPlacedObject(out var record)) return false;
            var craftingStation = MainGameBossSummonUiController.StationForDefinitionId(record.definitionId);
            if (craftingStation != CraftingStation.None)
            {
                if (productCraftingUi == null)
                    productCraftingUi = FindAnyObjectByType<MainGameCraftingUiController>();
                if (productCraftingUi != null && productCraftingUi.TryOpenForStation(craftingStation)) return true;
                ShowMessage($"{ItemName(record.definitionId)} 제작 화면을 열 수 없습니다.");
                return true;
            }
            if (record.definitionId == JangdokStorageRuntime.DefinitionId)
            {
                if (productCraftingUi == null)
                    productCraftingUi = FindAnyObjectByType<MainGameCraftingUiController>();
                if (productCraftingUi != null && productCraftingUi.TryOpenJangdok(record.objectId)) return true;
                ShowMessage("장독 창고 화면을 열 수 없습니다.");
                return true;
            }
            if (record.definitionId == TurretItemId && turrets.TryGetValue(record.objectId, out var turret))
            {
                TryRefuelTurret(turret);
                return true;
            }
            if (IsInstalledLanternDefinition(record.definitionId) &&
                lanterns.TryGetValue(record.objectId, out var lantern))
            {
                TryRefuelLantern(lantern);
                return true;
            }
            if (record.definitionId == CoolingSourceRuntime.IceJarId)
            {
                TryRefuelIceJar(record);
                return true;
            }
            if (string.Equals(record.definitionId, SeokbinggoRules.IceCoreDefinitionId, StringComparison.Ordinal))
            {
                TryUpgradeSeokbinggo(record.objectId);
                return true;
            }
            if (string.Equals(record.definitionId, MainGameEnvironmentState.DoorDefinitionId,
                    StringComparison.Ordinal))
            {
                if (environmentState != null &&
                    environmentState.TryToggleInsulationDoor(record.objectId, out var nowOpen))
                {
                    ShowMessage(nowOpen
                        ? "단열 문 개방 · 밀폐 보정 일시 정지"
                        : "단열 문 닫힘 · 밀폐 인정");
                    BuildStateChanged?.Invoke();
                    return true;
                }
                ShowMessage("단열 문을 조작할 수 없습니다.");
                return true;
            }
            if (environmentState.TryGetCoolingStatus(record.objectId, out var remaining,
                    out var capPercent, out var active))
            {
                var itemName = ItemName(record.definitionId);
                var lifetime = float.IsPositiveInfinity(remaining) ? "영구" : $"{remaining:0}초";
                ShowMessage($"{itemName} · 냉각 상한 {capPercent:0}% · " +
                            $"{(active ? $"가동 중 ({lifetime})" : "정지")}");
                return true;
            }
            ShowMessage($"{ItemName(record.definitionId)} · 좌클릭 유지로 회수");
            return true;
        }

        private void TryRefuelTurret(TurretEntry entry)
        {
            if (!runtimeServices.PlayerInventory.TryRemove(FuelItemId, 1))
            {
                ShowMessage($"도깨비불 등탑 · 연료 {entry.Controller.FuelRemaining:0}초 · 석탄 없음");
                return;
            }
            if (!entry.Controller.AddFuel(1))
            {
                runtimeServices.PlayerInventory.TryAdd(FuelItemId, 1);
                ShowMessage("등탑에 연료를 넣지 못했습니다.");
                return;
            }
            ShowMessage($"석탄 1개 투입 · 등탑 연료 {entry.Controller.FuelRemaining:0}초");
            Debug.Log($"[Nyangbingo] Turret refueled: id={entry.ObjectId}, " +
                      $"fuel={entry.Controller.FuelRemaining:0.0}.");
            BuildStateChanged?.Invoke();
        }

        private void TryRefuelLantern(LanternEntry entry)
        {
            var fuelName = ItemName(entry.FuelItemId);
            if (!runtimeServices.PlayerInventory.TryRemove(entry.FuelItemId, 1))
            {
                ShowMessage($"{ItemName(entry.DefinitionId)} · 연료 {entry.FuelRemaining:0}초 · {fuelName} 없음");
                return;
            }
            if (!entry.AddFuel(entry.FuelSecondsPerUnit))
            {
                runtimeServices.PlayerInventory.TryAdd(entry.FuelItemId, 1);
                ShowMessage($"{ItemName(entry.DefinitionId)}에 연료를 넣지 못했습니다.");
                return;
            }
            ShowMessage($"{ItemName(entry.DefinitionId)} · {fuelName} 1개 투입 · 연료 {entry.FuelRemaining:0}초");
            Debug.Log($"[Nyangbingo] Installed lantern refueled: id={entry.ObjectId}, " +
                      $"definition={entry.DefinitionId}, fuelItem={entry.FuelItemId}, " +
                      $"fuel={entry.FuelRemaining:0.0}.");
            BuildStateChanged?.Invoke();
        }

        private void TryRefuelIceJar(PlacedObjectRecord record)
        {
            if (!environmentState.TryGetCoolingStatus(record.objectId, out var remaining,
                    out var capPercent, out var active)) return;
            if (!runtimeServices.PlayerInventory.TryRemove(IceFuelItemId, 1))
            {
                ShowMessage($"얼음 항아리 · 냉각 상한 {capPercent:0}% · " +
                            $"{(active ? $"연료 {remaining:0}초" : "연료 없음")} · 얼음 조각 없음");
                return;
            }
            if (!environmentState.TryAddIceJarFuel(record.objectId))
            {
                runtimeServices.PlayerInventory.TryAdd(IceFuelItemId, 1);
                ShowMessage("얼음 항아리에 연료를 넣지 못했습니다.");
                return;
            }
            environmentState.TryGetCoolingStatus(record.objectId, out remaining, out capPercent, out _);
            ShowMessage($"얼음 항아리 · 얼음 조각 1개 투입 · 연료 {remaining:0}초 · 상한 {capPercent:0}%");
            BuildStateChanged?.Invoke();
        }

        private void TryUpgradeSeokbinggo(string iceCoreObjectId)
        {
            var service = runtimeServices?.Seokbinggo;
            if (service == null)
            {
                ShowMessage("석빙고 승급 서비스를 찾을 수 없습니다.");
                return;
            }

            if (service.TryUpgrade(out var message))
            {
                ShowMessage(message);
                BuildStateChanged?.Invoke();
                return;
            }

            if (service.IsMaxStage)
            {
                if (!string.IsNullOrEmpty(iceCoreObjectId) && environmentState != null &&
                    environmentState.TryGetCoolingStatus(iceCoreObjectId, out var remaining, out var capPercent,
                        out var active))
                {
                    var lifetime = float.IsPositiveInfinity(remaining) ? "영구" : $"{remaining:0}초";
                    ShowMessage($"석빙고 {service.Stage}단계(최고) · 냉각 상한 {capPercent:0}% · " +
                                $"{(active ? $"가동 중 ({lifetime})" : "정지")}");
                    return;
                }

                ShowMessage($"석빙고 {service.Stage}단계(최고) · 터렛 슬롯 {service.TurretSlotCap}");
                return;
            }

            ShowMessage(string.IsNullOrEmpty(message)
                ? $"석빙고 {service.Stage}단계 · 다음 승급 재료가 부족합니다."
                : message);
        }

        public bool TryRecoverNearestPlacedObject()
        {
            if (!TryGetNearestPlacedObject(out var record)) return false;
            return TryRecoverPlacedObject(record);
        }

        public bool TryRecoverPlacedObject(PlacedObjectRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.objectId) ||
                string.IsNullOrWhiteSpace(record.definitionId) ||
                environmentState == null || runtimeServices?.PlayerInventory == null)
                return false;
            if (record.definitionId == JangdokStorageRuntime.DefinitionId &&
                !runtimeServices.JangdokStorage.CanRecover(record.objectId))
            {
                ShowMessage("장독 창고가 비어 있어야 회수할 수 있습니다.");
                return true;
            }
            var item = gameDataCatalog?.FindItem(record.definitionId);
            if (item == null || !runtimeServices.PlayerInventory.TryAdd(record.definitionId, 1))
            {
                ShowMessage("인벤토리 공간이 없어 설치물을 회수할 수 없습니다.");
                return true;
            }
            if (!environmentState.TryRemove(record.objectId))
            {
                runtimeServices.PlayerInventory.TryRemove(record.definitionId, 1);
                ShowMessage("설치물 회수에 실패했습니다.");
                return true;
            }
            if (record.definitionId == JangdokStorageRuntime.DefinitionId)
                runtimeServices.JangdokStorage.TryRemoveEmpty(record.objectId);
            if (turrets.TryGetValue(record.objectId, out var entry))
            {
                entry.Controller.Fired -= entry.FireHandler;
                turrets.Remove(record.objectId);
            }
            RemoveLantern(record.objectId);
            RemovePassiveCounterAura(record.objectId);
            ShowMessage($"{item.DisplayName} 회수 완료 · 남은 연료는 반환되지 않습니다.");
            Debug.Log($"[Nyangbingo] Product placeable recovered: id={record.objectId}, " +
                      $"definition={record.definitionId}.");
            BuildStateChanged?.Invoke();
            return true;
        }

        private bool TryGetNearestPlacedObject(out PlacedObjectRecord record)
        {
            record = default;
            return playerController != null && environmentState != null &&
                   environmentState.TryGetNearestPlacedObject(playerController.transform.position,
                       InteractionRange, out record);
        }

        private string ItemName(string definitionId) =>
            gameDataCatalog?.FindItem(definitionId)?.DisplayName ?? definitionId;

        private void RefreshInteractionStatus()
        {
            if (interactionStatusText == null) return;
            if (bossManager?.IsBossActive == true)
            {
                interactionStatusText.text = string.Empty;
                IsBottomInteractionPromptVisible = false;
                return;
            }
            if (TryGetNearestPlacedObject(out _))
            {
                interactionStatusText.text = NearbyInteractionPrompt;
                IsBottomInteractionPromptVisible = true;
                return;
            }
            interactionStatusText.text = IsPlacementPreviewActive
                ? $"{(placementValid ? "LMB · 설치" : "설치 불가")}    ESC/RMB · 취소"
                : string.Empty;
            IsBottomInteractionPromptVisible = !string.IsNullOrEmpty(interactionStatusText.text);
        }

        private void ConfigureBottomInteractionStatus()
        {
            if (interactionStatusText == null) return;
            var rect = interactionStatusText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, MainGameTilePaletteController.BottomStatusBaseY);
            rect.sizeDelta = new Vector2(MainGameTilePaletteController.PaletteLogicalWidth,
                MainGameTilePaletteController.BottomStatusLineHeight);
            rect.localScale = Vector3.one;
            interactionStatusText.fontSize = 9;
            interactionStatusText.alignment = TextAnchor.MiddleCenter;
            interactionStatusText.raycastTarget = false;
        }

        private bool TryRegisterPlacedTurret(string objectId, int initialFuelUnits, out TurretEntry entry)
        {
            entry = null;
            if (turrets.ContainsKey(objectId) || !environmentState.TryGetVisual(objectId, out var visual)) return false;
            var controller = new TurretController(visual.transform, FindHostileTargets,
                RetargetSeconds, FireSeconds, AttackRange, AttackDamage, FuelSecondsPerUnit);
            var created = new TurretEntry { ObjectId = objectId, Origin = visual.transform, Controller = controller };
            created.FireHandler = (target, damage) => LaunchProjectile(created, target, damage);
            controller.Fired += created.FireHandler;
            if (initialFuelUnits > 0 && !controller.AddFuel(initialFuelUnits))
            {
                controller.Fired -= created.FireHandler;
                return false;
            }
            turrets.Add(objectId, created);
            entry = created;
            return true;
        }

        private bool TryRegisterPlacedLantern(
            string objectId, string definitionId, out LanternEntry entry)
        {
            entry = null;
            if (!IsInstalledLanternDefinition(definitionId) ||
                lanterns.ContainsKey(objectId) ||
                !environmentState.TryGetVisual(objectId, out var visual))
                return false;

            var runtimeRoot = new GameObject("InstalledLanternRuntime");
            runtimeRoot.transform.SetParent(visual.transform, false);
            var light = runtimeRoot.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerRadius = InstalledLanternRadius * .35f;
            light.pointLightOuterRadius = InstalledLanternRadius * 1.15f;
            light.falloffIntensity = .45f;
            light.intensity = 1.15f;
            light.color = new Color(1f, .78f, .42f, 1f);

            var aura = runtimeRoot.AddComponent<CounterAura>();
            var eoduksini = gameDataCatalog?.FindYokai("eoduksini");
            aura.ConfigureForRuntime(CounterAuraKind.Lantern, InstalledLanternRadius,
                eoduksini != null ? eoduksini.DamageTakenMultiplier : 2f,
                FindGlobalFloat(EoduksiniBloomPauseKey, 6f),
                FindGlobalFloat(EoduksiniRebloomCooldownKey, 12f));

            entry = new LanternEntry
            {
                ObjectId = objectId,
                DefinitionId = definitionId,
                FuelItemId = FuelItemForInstalledLantern(definitionId),
                FuelSecondsPerUnit = definitionId == FrostLanternItemId
                    ? FindGlobalFloat(FrostLanternFuelSecondsKey, FuelSecondsPerUnit)
                    : FuelSecondsPerUnit,
                RuntimeRoot = runtimeRoot,
                Light = light,
                Aura = aura
            };
            entry.RefreshActiveState();
            lanterns.Add(objectId, entry);
            activeCounterAuras.Add(aura);
            return true;
        }

        private bool TryRegisterPlacedObjectRuntime(PlacedObjectRecord record)
        {
            if (record.definitionId == TurretItemId)
                return TryRegisterPlacedTurret(record.objectId, 0, out _);
            if (IsInstalledLanternDefinition(record.definitionId))
                return TryRegisterPlacedLantern(
                    record.objectId, record.definitionId, out _);
            if (TryGetPassiveCounterAuraConfiguration(
                    record.definitionId, out _, out _, out _, out _, out _))
                return TryRegisterPlacedCounterAura(record.objectId, record.definitionId);
            return record.definitionId != JangdokStorageRuntime.DefinitionId ||
                   runtimeServices.JangdokStorage.TryRegister(record.objectId);
        }

        public static bool IsInstalledLanternDefinition(string definitionId) =>
            definitionId == LanternItemId || definitionId == FrostLanternItemId;

        public static string FuelItemForInstalledLantern(string definitionId) =>
            definitionId == FrostLanternItemId ? FrostLanternFuelItemId : FuelItemId;

        private bool TryRegisterPlacedCounterAura(string objectId, string definitionId)
        {
            if (passiveCounterAuraRoots.ContainsKey(objectId) ||
                !TryGetPassiveCounterAuraConfiguration(
                    definitionId, out var kind, out var radius, out var effect,
                    out var duration, out var cooldown) ||
                !environmentState.TryGetVisual(objectId, out var visual))
                return false;

            var runtimeRoot = new GameObject("InstalledCounterAuraRuntime");
            runtimeRoot.transform.SetParent(visual.transform, false);
            var aura = runtimeRoot.AddComponent<CounterAura>();
            aura.ConfigureForRuntime(kind, radius, effect, duration, cooldown);
            passiveCounterAuraRoots.Add(objectId, runtimeRoot);
            activeCounterAuras.Add(aura);
            return true;
        }

        public static bool TryGetPassiveCounterAuraConfiguration(
            string definitionId, out CounterAuraKind kind, out float radius,
            out float effect, out float duration, out float cooldown)
        {
            kind = default;
            radius = effect = duration = cooldown = 0f;
            switch (definitionId)
            {
                case SieveItemId:
                case IronSieveItemId:
                    kind = CounterAuraKind.Sieve;
                    radius = 4f;
                    effect = 1.5f;
                    duration = 12f;
                    cooldown = 30f;
                    return true;
                case HaetaeStatueItemId:
                    kind = CounterAuraKind.Haetae;
                    radius = 8f;
                    effect = .5f;
                    return true;
                case BellRopeItemId:
                case IronBellRopeItemId:
                    kind = CounterAuraKind.BellRope;
                    radius = 10f;
                    cooldown = 4f;
                    return true;
                default:
                    return false;
            }
        }

        private float FindGlobalFloat(string key, float fallback)
        {
            var definition = gameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) && value >= 0f
                ? value
                : fallback;
        }

        private static bool TryRestoreLanternFuel(SaveGame save, string objectId, LanternEntry entry)
        {
            var matches = save.turretFuel.Where(record =>
                string.Equals(record.objectId, objectId, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) return false;
            var record = matches[0];
            var seconds = record.storesGameSeconds
                ? record.remainingGameSeconds
                : record.fuel * FuelSecondsPerUnit;
            return entry.RestoreFuel(seconds);
        }

        private void RemoveLantern(string objectId)
        {
            if (!lanterns.TryGetValue(objectId, out var entry)) return;
            lanterns.Remove(objectId);
            activeCounterAuras.Remove(entry.Aura);
            if (entry.RuntimeRoot != null) Destroy(entry.RuntimeRoot);
        }

        private void RemovePassiveCounterAura(string objectId)
        {
            if (!passiveCounterAuraRoots.TryGetValue(objectId, out var runtimeRoot)) return;
            passiveCounterAuraRoots.Remove(objectId);
            if (runtimeRoot != null)
            {
                var aura = runtimeRoot.GetComponent<CounterAura>();
                if (aura != null) activeCounterAuras.Remove(aura);
                Destroy(runtimeRoot);
            }
        }

        private IReadOnlyList<Health> FindHostileTargets()
        {
            var allHealth = FindObjectsByType<Health>();
            var hostile = new List<Health>(allHealth.Length);
            foreach (var health in allHealth)
            {
                if (health == null || health.IsDead) continue;
                if (health.GetComponent<YokaiBrain>() != null || health.GetComponent<BossCombatController>() != null)
                    hostile.Add(health);
            }
            return hostile;
        }

        private void LaunchProjectile(TurretEntry turret, Health target, int damage)
        {
            if (turret?.Origin == null || target == null || projectilePool == null) return;
            var origin = (Vector2)turret.Origin.position + Vector2.up * .5f;
            projectilePool.Spawn(origin, target, damage, DamageTag.Fire, ProjectileSpeed, ProjectileHitDistance);
        }

        private void RefreshProjectileVisuals()
        {
            if (projectilePool == null) return;
            var projectiles = projectilePool.Projectiles;
            while (projectileRenderers.Count < projectiles.Count)
                projectileRenderers.Add(CreateProjectileRenderer(projectileRenderers.Count));
            for (var index = 0; index < projectileRenderers.Count; index++)
            {
                var renderer = projectileRenderers[index];
                var active = index < projectiles.Count && projectiles[index].IsActive;
                renderer.enabled = active;
                if (active) renderer.transform.position = projectiles[index].Position;
            }
        }

        private SpriteRenderer CreateProjectileRenderer(int index)
        {
            var visual = new GameObject($"BlueProjectile_{index:00}");
            visual.transform.SetParent(transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 25;
            var frames = gameplayArtCatalog?.BlueProjectileFrames;
            if (frames != null && frames.Count > 0)
            {
                renderer.sprite = frames[0];
                visual.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(frames, .08f);
            }
            renderer.enabled = false;
            return renderer;
        }

        private void ClearTurrets()
        {
            foreach (var entry in turrets.Values) entry.Controller.Fired -= entry.FireHandler;
            turrets.Clear();
        }

        private void ClearLanterns()
        {
            foreach (var entry in lanterns.Values)
                if (entry.RuntimeRoot != null) Destroy(entry.RuntimeRoot);
            lanterns.Clear();
            foreach (var runtimeRoot in passiveCounterAuraRoots.Values)
                if (runtimeRoot != null) Destroy(runtimeRoot);
            passiveCounterAuraRoots.Clear();
            activeCounterAuras.Clear();
        }

        private void HandleInventoryChanged() => BuildStateChanged?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void GrantTurretItemForEditorTest()
        {
            if (runtimeServices?.PlayerInventory != null &&
                runtimeServices.PlayerInventory.TryAdd(TurretItemId, 1))
                ShowMessage("F11 테스트 지급: 도깨비불 등탑 x1");
            else ShowMessage("F11 등탑 지급 실패: 인벤토리 공간을 확인하세요.");
        }

        private void GrantCraftingMaterialsForEditorTest()
        {
            var recipe = TurretRecipe;
            if (recipe?.Ingredients == null) return;
            foreach (var ingredient in recipe.Ingredients)
                if (ingredient.item != null)
                    runtimeServices.PlayerInventory.TryAdd(ingredient.item.Id, ingredient.amount);
            ShowMessage("Shift+F11 테스트 지급: 등탑 제작 재료");
        }

        private void GrantFuelForEditorTest()
        {
            if (runtimeServices?.PlayerInventory != null &&
                runtimeServices.PlayerInventory.TryAdd(FuelItemId, 1))
                ShowMessage("Ctrl+F11 테스트 지급: 석탄 x1");
            else ShowMessage("Ctrl+F11 석탄 지급 실패: 인벤토리 공간을 확인하세요.");
        }
#endif

        private void ShowMessage(string message)
        {
            if (craftingStationUi == null)
                craftingStationUi = FindAnyObjectByType<MainGameBossSummonUiController>();
            craftingStationUi?.ShowExternalMessage(message);
            Debug.Log("[Nyangbingo] " + message);
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= HandleInventoryChanged;
            CancelPlacementPreview();
            anyPlacementPreviewActive = false;
            ClearTurrets();
            ClearLanterns();
            if (registered) runtimeServices?.Unregister(this);
            registered = false;
        }
    }
}
