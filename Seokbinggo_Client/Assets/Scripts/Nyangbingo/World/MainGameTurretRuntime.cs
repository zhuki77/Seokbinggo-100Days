using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.Yokai;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(-60)]
    public sealed class MainGameTurretRuntime : MonoBehaviour, IGameSecondsTickable
    {
        private sealed class TurretEntry
        {
            public string ObjectId;
            public Transform Origin;
            public TurretController Controller;
            public Action<Health, int> FireHandler;
        }

        private const string TurretItemId = "dokkaebi_fire_tower";
        private const string FuelItemId = "coal";
        private const float RetargetSeconds = .2f;
        private const float FireSeconds = 1f;
        private const float AttackRange = 8f;
        private const int AttackDamage = 4;
        private const float FuelSecondsPerUnit = 270f;
        private const float ProjectileSpeed = 6f;
        private const float ProjectileHitDistance = .1f;
        private const float InteractionRange = 2.5f;

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameEnvironmentState environmentState;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private BuildingArtCatalog buildingArtCatalog;
        [SerializeField] private MainGamePlayerController playerController;
        [SerializeField] private Text interactionStatusText;

        private readonly Dictionary<string, TurretEntry> turrets =
            new Dictionary<string, TurretEntry>(StringComparer.Ordinal);
        private readonly List<SpriteRenderer> projectileRenderers = new List<SpriteRenderer>();
        private HomingProjectilePool projectilePool;
        private MainGameBossSummonUiController craftingStationUi;
        private GameObject placementPreview;
        private SpriteRenderer placementPreviewRenderer;
        private LineRenderer placementRangeRenderer;
        private Material placementRangeMaterial;
        private Vector2 placementPosition;
        private bool placementValid;
        private bool registered;

        public bool HasSceneBindings => gameDataCatalog != null && runtimeServices != null &&
                                        environmentState != null && gameplayArtCatalog != null &&
                                        buildingArtCatalog != null && playerController != null &&
                                        interactionStatusText != null;
        public int ActiveTurretCount => turrets.Count;
        public bool IsPlacementPreviewActive => placementPreview != null;
        public bool IsPlacementPreviewValid => IsPlacementPreviewActive && placementValid;
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
            registered = runtimeServices != null && runtimeServices.Register(this);
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed += HandleInventoryChanged;
            if (!registered)
                Debug.LogError("[Nyangbingo] MainGameTurretRuntime: central game-seconds tick registration failed.");
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (IsPlacementPreviewActive) ConfirmPlacementPreview();
                else if (TurretItemCount > 0) BeginPlacementPreview();
                else TryStartCraftingFromUi();
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) TryRecoverNearestTurret();
                else TryRefuelNearestTurret();
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
            UpdatePlacementPreview();
            RefreshInteractionStatus();
        }

        public void Tick(float deltaGameSeconds)
        {
            foreach (var entry in turrets.Values) entry.Controller.Tick(deltaGameSeconds);
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

        public bool BeginPlacementPreview()
        {
            if (IsPlacementPreviewActive) return true;
            if (TurretItemCount <= 0)
            {
                ShowMessage("설치할 도깨비불 등탑이 없습니다.");
                return false;
            }

            placementPreview = new GameObject("DokkaebiFireTowerPlacementPreview");
            placementPreviewRenderer = placementPreview.AddComponent<SpriteRenderer>();
            placementPreviewRenderer.sortingOrder = 30;
            placementPreviewRenderer.sprite = buildingArtCatalog?.Find(TurretItemId)?.Sprite;
            if (placementPreviewRenderer.sprite == null)
                RuntimePlaceholderVisual.Configure(placementPreviewRenderer, Color.white, 1f, 30);

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
            BuildStateChanged?.Invoke();
        }

        public bool CaptureProgress(SaveGame save)
        {
            if (save == null) return false;
            save.turretFuel.Clear();
            foreach (var entry in turrets.Values.OrderBy(value => value.ObjectId, StringComparer.Ordinal))
                if (!WorldSaveAdapter.CaptureTurretFuel(save, entry.ObjectId, entry.Controller)) return false;
            return true;
        }

        public bool RestoreProgress(SaveGame save)
        {
            if (save == null) return false;
            CancelPlacementPreview();
            ClearTurrets();
            projectilePool = new HomingProjectilePool(4);
            foreach (var record in environmentState.ExportPlacedObjects())
            {
                if (!string.Equals(record.definitionId, TurretItemId, StringComparison.Ordinal)) continue;
                if (!TryRegisterPlacedTurret(record.objectId, 0, out var entry)) return false;
                var hasFuelRecord = save.turretFuel.Any(value =>
                    string.Equals(value.objectId, record.objectId, StringComparison.Ordinal));
                if (hasFuelRecord && !WorldSaveAdapter.RestoreTurretFuel(save, record.objectId, entry.Controller))
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
            var direction = playerController.HorizontalFacingDirection;
            placementPosition = (Vector2)playerController.transform.position + direction * 2f;
            placementPosition = new Vector2(Mathf.Floor(placementPosition.x) + .5f,
                Mathf.Floor(placementPosition.y) + .5f);
            placementPreview.transform.position = placementPosition;
            placementValid = environmentState.CanPlaceAt(placementPosition) && TurretItemCount > 0;
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
            var record = new PlacedObjectRecord
            {
                objectId = $"{TurretItemId}_{Guid.NewGuid():N}",
                definitionId = TurretItemId,
                position = position,
                rotationDegrees = 0f
            };
            if (!runtimeServices.PlayerInventory.TryRemove(TurretItemId, 1)) return false;
            if (!environmentState.TryPlace(record, barrierActive: false) ||
                !TryRegisterPlacedTurret(record.objectId, 0, out _))
            {
                environmentState.TryRemove(record.objectId);
                runtimeServices.PlayerInventory.TryAdd(TurretItemId, 1);
                ShowMessage("해당 위치에는 등탑을 설치할 수 없습니다.");
                return false;
            }
            ShowMessage("도깨비불 등탑 설치 완료 · 석탄을 넣어 가동하세요.");
            Debug.Log($"[Nyangbingo] Turret placed: id={record.objectId}, position={record.position}, fuel=0.");
            BuildStateChanged?.Invoke();
            return true;
        }

        private void TryRefuelNearestTurret()
        {
            var entry = FindNearestTurret();
            if (entry == null)
            {
                ShowMessage("연료를 넣을 등탑 가까이에서 F를 누르세요.");
                return;
            }
            if (!runtimeServices.PlayerInventory.TryRemove(FuelItemId, 1))
            {
                ShowMessage("등탑 연료로 사용할 석탄이 없습니다.");
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

        private void TryRecoverNearestTurret()
        {
            var entry = FindNearestTurret();
            if (entry == null)
            {
                ShowMessage("회수할 등탑 가까이에서 Shift+F를 누르세요.");
                return;
            }
            if (!runtimeServices.PlayerInventory.TryAdd(TurretItemId, 1))
            {
                ShowMessage("인벤토리 공간이 없어 등탑을 회수할 수 없습니다.");
                return;
            }
            if (!environmentState.TryRemove(entry.ObjectId))
            {
                runtimeServices.PlayerInventory.TryRemove(TurretItemId, 1);
                ShowMessage("등탑 회수에 실패했습니다.");
                return;
            }
            entry.Controller.Fired -= entry.FireHandler;
            turrets.Remove(entry.ObjectId);
            ShowMessage("도깨비불 등탑 회수 완료 · 남은 연료는 반환되지 않습니다.");
            Debug.Log($"[Nyangbingo] Turret recovered: id={entry.ObjectId}, " +
                      $"discardedFuel={entry.Controller.FuelRemaining:0.0}.");
            BuildStateChanged?.Invoke();
        }

        private TurretEntry FindNearestTurret()
        {
            if (playerController == null) return null;
            TurretEntry nearest = null;
            var bestDistance = InteractionRange * InteractionRange;
            foreach (var entry in turrets.Values)
            {
                if (entry.Origin == null) continue;
                var distance = ((Vector2)entry.Origin.position - (Vector2)playerController.transform.position)
                    .sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                nearest = entry;
            }
            return nearest;
        }

        private void RefreshInteractionStatus()
        {
            if (interactionStatusText == null) return;
            var entry = FindNearestTurret();
            if (entry != null)
            {
                interactionStatusText.text = $"도깨비불 등탑 · 연료 {entry.Controller.FuelRemaining:0}초" +
                                             "  |  F 석탄 투입  |  Shift+F 회수";
                return;
            }
            interactionStatusText.text = IsPlacementPreviewActive
                ? $"설치 미리보기 · {(placementValid ? "설치 가능" : "설치 불가")} · T로 확정"
                : TurretItemCount > 0 ? "V · 건축 패널  |  T · 등탑 설치 미리보기" : string.Empty;
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
            ClearTurrets();
            if (registered) runtimeServices?.Unregister(this);
            registered = false;
        }
    }
}
