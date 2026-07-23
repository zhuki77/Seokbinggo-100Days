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
        private const string LanternItemId = "lantern";
        private const string FuelItemId = "coal";
        private const string IceFuelItemId = "ice_shard";
        private const float RetargetSeconds = .2f;
        private const float FireSeconds = 1f;
        private const float AttackRange = 8f;
        private const int AttackDamage = 4;
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
        private readonly List<CounterAura> activeCounterAuras = new List<CounterAura>();
        private readonly List<SpriteRenderer> projectileRenderers = new List<SpriteRenderer>();
        private HomingProjectilePool projectilePool;
        private MainGameBossSummonUiController craftingStationUi;
        private MainGameCraftingUiController productCraftingUi;
        private BossManager bossManager;
        private GameObject placementPreview;
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
        public IReadOnlyList<CounterAura> ActiveCounterAuras => activeCounterAuras;
        public bool IsPlacementPreviewActive => placementPreview != null;
        public bool IsPlacementPreviewValid => IsPlacementPreviewActive && placementValid;
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

        public void BindInteractionStatus(Text statusText) => interactionStatusText = statusText;

        private void Start()
        {
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
#if UNITY_EDITOR
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
            if (runtimeServices.NapService?.IsNapping == true)
            {
                ShowMessage("냥잠 중에는 제작할 수 없습니다.");
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
            placementPreviewRenderer = placementPreview.AddComponent<SpriteRenderer>();
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
                ShowMessage("붉은 미리보기 위치에는 설치할 수 없습니다.");
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
                else if (string.Equals(record.definitionId, LanternItemId, StringComparison.Ordinal))
                {
                    if (!TryRegisterPlacedLantern(record.objectId, out var lantern)) return false;
                    if (hasFuelRecord && !TryRestoreLanternFuel(save, record.objectId, lantern)) return false;
                }
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
            placementPosition = new Vector2(Mathf.Floor(placementPosition.x) + .5f,
                Mathf.Floor(placementPosition.y) + .5f);
            placementPreview.transform.position = placementPosition;
            placementValid = environmentState.CanPlaceAt(placementPosition) &&
                             GetInventoryCount(placementDefinitionId) > 0;
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
            var runtimeRegistered = definitionId == TurretItemId
                ? placed && TryRegisterPlacedTurret(record.objectId, 0, out _)
                : definitionId == LanternItemId
                    ? placed && TryRegisterPlacedLantern(record.objectId, out _)
                : definitionId != JangdokStorageRuntime.DefinitionId ||
                  placed && runtimeServices.JangdokStorage.TryRegister(record.objectId);
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
            if (record.definitionId == "nest_bed")
            {
                playerController.TryToggleNapFromInteraction();
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
            if (record.definitionId == LanternItemId && lanterns.TryGetValue(record.objectId, out var lantern))
            {
                TryRefuelLantern(lantern);
                return true;
            }
            if (record.definitionId == CoolingSourceRuntime.IceJarId)
            {
                TryRefuelIceJar(record);
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
            ShowMessage($"{ItemName(record.definitionId)} · Shift+E로 회수");
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
            if (!runtimeServices.PlayerInventory.TryRemove(FuelItemId, 1))
            {
                ShowMessage($"등불 · 연료 {entry.FuelRemaining:0}초 · 석탄 없음");
                return;
            }
            if (!entry.AddFuel(FuelSecondsPerUnit))
            {
                runtimeServices.PlayerInventory.TryAdd(FuelItemId, 1);
                ShowMessage("등불에 연료를 넣지 못했습니다.");
                return;
            }
            ShowMessage($"등불 · 석탄 1개 투입 · 연료 {entry.FuelRemaining:0}초");
            Debug.Log($"[Nyangbingo] Installed lantern refueled: id={entry.ObjectId}, " +
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
            ShowMessage($"얼음 조각 1개 투입 · 냉각 상한 {capPercent:0}% · 연료 {remaining:0}초");
            BuildStateChanged?.Invoke();
        }

        public bool TryRecoverNearestPlacedObject()
        {
            if (!TryGetNearestPlacedObject(out var record)) return false;
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
                return;
            }
            if (TryGetNearestPlacedObject(out var record))
            {
                var craftingStation = MainGameBossSummonUiController.StationForDefinitionId(record.definitionId);
                if (craftingStation != CraftingStation.None)
                    interactionStatusText.text = "E  ·  ⇧E";
                else if (record.definitionId == TurretItemId && turrets.TryGetValue(record.objectId, out var turret))
                    interactionStatusText.text = $"{turret.Controller.FuelRemaining:0}  ·  E  ·  ⇧E";
                else if (record.definitionId == LanternItemId && lanterns.TryGetValue(record.objectId, out var lantern))
                    interactionStatusText.text = $"{lantern.FuelRemaining:0}  ·  E  ·  ⇧E";
                else if (record.definitionId == JangdokStorageRuntime.DefinitionId)
                    interactionStatusText.text = "40  ·  E  ·  ⇧E";
                else if (environmentState.TryGetCoolingStatus(record.objectId, out var remaining,
                             out var capPercent, out var active))
                {
                    var lifetime = float.IsPositiveInfinity(remaining) ? "∞" : $"{remaining:0}";
                    interactionStatusText.text = $"{capPercent:0}%  ·  {(active ? lifetime : "0")}  ·  E  ·  ⇧E";
                }
                else
                    interactionStatusText.text = "E  ·  ⇧E";
                return;
            }
            interactionStatusText.text = IsPlacementPreviewActive
                ? $"{(placementValid ? "✓  ·  LMB" : "✕")}  ·  ESC/RMB"
                : string.Empty;
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

        private bool TryRegisterPlacedLantern(string objectId, out LanternEntry entry)
        {
            entry = null;
            if (lanterns.ContainsKey(objectId) || !environmentState.TryGetVisual(objectId, out var visual))
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
                RuntimeRoot = runtimeRoot,
                Light = light,
                Aura = aura
            };
            entry.RefreshActiveState();
            lanterns.Add(objectId, entry);
            activeCounterAuras.Add(aura);
            return true;
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
            activeCounterAuras.Clear();
        }

        private void HandleInventoryChanged() => BuildStateChanged?.Invoke();

#if UNITY_EDITOR
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
