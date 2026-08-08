using System.Collections.Generic;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-55)]
    public sealed class MainGameBossSummonUiController : MonoBehaviour, IBossSummonSite
    {
        private static readonly string[] BossIds =
        {
            "king_dokkaebi", "mother_bulgasari", "imugi", "gangcheol_boss"
        };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly string[] DeliveredInventoryArtTestItemIds =
        {
            // bare_claw is an innate body weapon (maxStack 0), so it never belongs in inventory.
            "iron_claw", "icesteel_claw", "dokkaebi_club", "cheolseon", "clay", "drought_heart",
            "frostclaw_gauntlet", "iron_forge_core", "hapjukseon", "copper_ingot", "icesteel_ingot",
            "iron_ingot", "water_jar"
        };

        private const string DebugShortcutHelpText =
            "보스·소환\n" +
            "B  보스 선택  ·  C  선택 보스 소환 아이템 제작\n" +
            "F6  소환 재료 지급  ·  Shift+F6  제작대로 이동\n" +
            "Ctrl+F6  신규 아이템 아트 검증 지급\n" +
            "F7  소환 아이템 지급  ·  Shift+F7  깊은 제단 이동\n" +
            "F8  도깨비 대장  ·  Shift+F8  강철이\n" +
            "Ctrl+F8  어미 불가사리  ·  Alt+F8  이무기\n" +
            "J  일반 요괴 정리  ·  K  활성 보스 처치\n\n" +
            "제작·설치·연출\n" +
            "Ctrl+F5  선택 항목 재료 지급  ·  Shift+F5  필요 제작대로 이동\n" +
            "F10  냥잠 연출  ·  Shift+F10  채굴 파괴 연출\n" +
            "Ctrl+F10  채굴 치명타 연출\n" +
            "F11  등탑 지급  ·  Shift+F11  등탑 재료 지급\n" +
            "Ctrl+F11  등탑 연료 지급\n" +
            "F12  어둑시니 소환  ·  Shift+F12  어둑시니 테스트 키트\n" +
            "Ctrl+F12  차열 지붕 테스트 키트";
#endif

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameEnvironmentState environmentState;
        [SerializeField] private MainGameEncounterCoordinator encounterCoordinator;
        [SerializeField] private MainGameRaidTarget playerTarget;
        [SerializeField] private Text statusText;
        [Min(.1f)][SerializeField] private float deepAltarInteractionRange = 2.5f;
        [Min(.1f)][SerializeField] private float craftingStationInteractionRange = 1.5f;

        private int selectedIndex;
        private string transientMessage;
        private float transientMessageUntil;
        private bool initialized;
        private GameShellController gameShell;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameObject debugShortcutHelpRoot;
        private float debugShortcutHelpPreviousTimeScale = 1f;
        private static bool debugShortcutHelpOpen;
        private static int debugShortcutHelpEscapeConsumedFrame = -1;
        private static MainGameBossSummonUiController debugShortcutHelpInstance;
#endif

        public const KeyCode DebugShortcutHelpKey = KeyCode.F5;
        public static readonly Vector2 DebugShortcutHelpPanelSize = new Vector2(230f, 190f);
        public const int DebugShortcutHelpBodyFontSize = 6;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool IsDebugShortcutHelpOpen => debugShortcutHelpOpen;
#else
        public static bool IsDebugShortcutHelpOpen => false;
#endif

        public static bool ConsumeEscapeIfDebugHelpOpen()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugShortcutHelpEscapeConsumedFrame == Time.frameCount) return true;
            if (!debugShortcutHelpOpen || !Input.GetKeyDown(KeyCode.Escape) ||
                debugShortcutHelpInstance == null) return false;
            debugShortcutHelpEscapeConsumedFrame = Time.frameCount;
            debugShortcutHelpInstance.SetDebugShortcutHelpOpen(false);
            return true;
#else
            return false;
#endif
        }

        public BossDefinition SelectedBoss => gameDataCatalog != null
            ? gameDataCatalog.FindBoss(BossIds[selectedIndex])
            : null;
        public CraftingStation NearbyCraftingStation => initialized
            ? ResolveNearbyCraftingStation()
            : CraftingStation.None;
        public bool IsNight => bootstrap?.TimeService?.IsNight == true;
        public bool HasSceneBindings => gameDataCatalog != null && bootstrap != null && runtimeServices != null &&
                                        environmentState != null && encounterCoordinator != null &&
                                        playerTarget != null && statusText != null;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, MainGameEncounterCoordinator encounters,
            MainGameRaidTarget target, Text text, MainGameEnvironmentState environment = null)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            environmentState = environment;
            encounterCoordinator = encounters;
            playerTarget = target;
            statusText = text;
        }

        private void Start()
        {
            gameShell = FindAnyObjectByType<GameShellController>();
            if (environmentState == null) environmentState = FindAnyObjectByType<MainGameEnvironmentState>();
            if (gameDataCatalog == null || bootstrap == null || runtimeServices == null ||
                environmentState == null || encounterCoordinator == null || playerTarget == null || statusText == null ||
                !runtimeServices.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameBossSummonUiController: summon UI wiring is incomplete.");
                enabled = false;
                return;
            }
            runtimeServices.PlayerInventory.Changed += RefreshStatus;
            if (encounterCoordinator.BossManager != null)
            {
                encounterCoordinator.BossManager.BossStarted += HandleBossStateChanged;
                encounterCoordinator.BossManager.BossEnded += HandleBossStateChanged;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugShortcutHelpInstance = this;
            BuildDebugShortcutHelp();
#endif
            initialized = true;
            RefreshStatus();
            Debug.Log("[Nyangbingo] MainGame boss summon item, placed crafting station, nighttime, and deep altar interaction ready.");
        }

        private void Update()
        {
            if (!initialized) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ConsumeEscapeIfDebugHelpOpen()) return;
            if (Input.GetKeyDown(DebugShortcutHelpKey) &&
                !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) &&
                !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) &&
                !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            {
                if (debugShortcutHelpOpen || gameShell?.Screen == GameShellScreen.Gameplay)
                    SetDebugShortcutHelpOpen(!debugShortcutHelpOpen);
                return;
            }
            if (debugShortcutHelpOpen) return;
#endif
            if (Time.timeScale <= 0f) return;
            if (MainGameCraftingUiController.BlocksGameplayInput) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.B))
            {
                selectedIndex = (selectedIndex + 1) % BossIds.Length;
                transientMessage = string.Empty;
                RefreshStatus();
            }
            if (Input.GetKeyDown(KeyCode.C)) TryCraftSelectedSummonItem();
            if (Input.GetKeyDown(KeyCode.F6))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    GrantDeliveredArtItemsForEditorTest();
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    TeleportToSelectedCraftingStationForEditorTest();
                else
                    GrantSelectedSummonMaterialsForEditorTest();
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    TeleportToDeepAltarForEditorTest();
                else
                    GrantSelectedSummonItemForEditorTest();
            }
#endif
            if (!string.IsNullOrEmpty(transientMessage) && Time.unscaledTime >= transientMessageUntil)
            {
                transientMessage = string.Empty;
                RefreshStatus();
            }
        }

        public bool IsAtDeepAltar(BossDefinition definition)
        {
            if (definition == null || !definition.RequiresDeepAltar) return true;
            var generator = bootstrap?.Session?.Generator;
            if (generator == null || playerTarget == null) return false;
            var altar = generator.AltarPosition;
            var altarCenter = new Vector2(altar.x + .5f, altar.y + .5f);
            return Vector2.Distance(playerTarget.transform.position, altarCenter) <= deepAltarInteractionRange;
        }

        public bool IsPlayerNearStation(CraftingStation station) =>
            initialized && ResolveNearbyCraftingStation() == station;

        public void ShowExternalMessage(string message) => ShowMessage(message);

        public BossDefinition FindBossForSummonItem(string itemId)
        {
            if (gameDataCatalog == null || string.IsNullOrWhiteSpace(itemId)) return null;
            var bosses = gameDataCatalog.Bosses;
            for (var index = 0; index < bosses.Count; index++)
            {
                var definition = bosses[index];
                if (definition?.SummonItem != null && definition.SummonItem.Id == itemId) return definition;
            }
            return null;
        }

        public bool CanUseSummonItem(string itemId, out BossDefinition definition, out string reason)
        {
            definition = FindBossForSummonItem(itemId);
            if (definition == null)
            {
                reason = "소환 아이템이 아닙니다.";
                return false;
            }
            if (runtimeServices?.NapService?.IsNapping == true)
            {
                reason = "냥잠 중에는 소환할 수 없습니다.";
                return false;
            }
            if (encounterCoordinator?.BossManager?.IsBossActive == true)
            {
                reason = "이미 보스전이 진행 중입니다.";
                return false;
            }
            if (!IsNight)
            {
                reason = "밤에만 소환 가능";
                return false;
            }
            if (runtimeServices?.PlayerInventory == null ||
                !runtimeServices.PlayerInventory.Has(definition.SummonItem.Id, 1))
            {
                reason = $"{definition.SummonItem.DisplayName}이 필요합니다.";
                return false;
            }
            if (!IsAtDeepAltar(definition))
            {
                reason = "깊은 얼음 제단 근처에서만 소환 가능";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool TryUseSummonItem(string itemId)
        {
            if (!CanUseSummonItem(itemId, out var definition, out var reason))
            {
                ShowMessage(reason);
                return false;
            }
            if (encounterCoordinator.TryStartPlayerSummonedBoss(definition, this))
            {
                ShowMessage($"{definition.DisplayName} 소환 성공. 소환 아이템 1개를 소비했습니다.");
                Debug.Log($"[Nyangbingo] Inventory summon confirmed: {definition.Id}, " +
                          $"item={definition.SummonItem.Id}.");
                return true;
            }
            ShowMessage($"{definition.DisplayName} 소환에 실패했습니다. 아이템은 보존됩니다.");
            return false;
        }

        private void TryCraftSelectedSummonItem()
        {
            if (runtimeServices?.NapService?.IsNapping == true)
            { ShowMessage("냥잠 중에는 제작할 수 없습니다."); return; }
            var definition = SelectedBoss;
            if (definition?.SummonItem == null || definition.SummonMaterials.Length == 0)
            { ShowMessage("소환 아이템 제작 데이터가 없습니다."); return; }
            var nearbyStation = ResolveNearbyCraftingStation();
            if (nearbyStation != definition.SummonStation)
            { ShowMessage($"{StationLabel(definition.SummonStation)} 근처에서 제작해야 합니다."); return; }

            var recipe = RecipeDefinition.CreateRuntime($"runtime_{definition.SummonItem.Id}",
                definition.SummonStation, definition.SummonMaterials,
                new ItemAmount { item = definition.SummonItem, amount = 1 }, 0f,
                RecipeType.Summon, definition.MvpScope, "bosses.csv summon material contract");
            var crafted = runtimeServices.CraftingService.TryCraft(recipe, nearbyStation);
            Destroy(recipe);
            if (crafted)
            {
                ShowMessage($"제작 완료: {definition.SummonItem.DisplayName} x1");
                Debug.Log($"[Nyangbingo] Boss summon item crafted: {definition.SummonItem.Id}, " +
                          $"station={nearbyStation}.");
            }
            else ShowMessage("재료 또는 인벤토리 공간이 부족합니다.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TeleportToCraftingStationForEditorTest(CraftingStation station)
        {
            var definitionId = DefinitionIdForStation(station);
            if (environmentState == null || string.IsNullOrEmpty(definitionId) ||
                !environmentState.TryGetNearestPlacedObjectPosition(definitionId, playerTarget.transform.position,
                    out var position)) return false;
            MovePlayerForEditorTest(position);
            return true;
        }

        private void GrantSelectedSummonMaterialsForEditorTest()
        {
            var definition = SelectedBoss;
            if (definition == null || definition.SummonMaterials.Length == 0)
            { ShowMessage("F6 재료 지급 실패: 제작 데이터가 없습니다."); return; }
            var granted = new List<ItemAmount>();
            for (var index = 0; index < definition.SummonMaterials.Length; index++)
            {
                var material = definition.SummonMaterials[index];
                if (material.item != null && runtimeServices.PlayerInventory.TryAdd(material.item.Id, material.amount))
                {
                    granted.Add(material);
                    continue;
                }
                for (var rollbackIndex = granted.Count - 1; rollbackIndex >= 0; rollbackIndex--)
                    runtimeServices.PlayerInventory.TryRemove(granted[rollbackIndex].item.Id,
                        granted[rollbackIndex].amount);
                ShowMessage("F6 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                return;
            }
            ShowMessage($"F6 테스트 재료 지급: {definition.DisplayName}");
        }

        private void GrantDeliveredArtItemsForEditorTest()
        {
            var granted = 0;
            var missing = 0;
            for (var index = 0; index < DeliveredInventoryArtTestItemIds.Length; index++)
            {
                var item = gameDataCatalog.FindItem(DeliveredInventoryArtTestItemIds[index]);
                if (item == null)
                {
                    missing++;
                    continue;
                }
                if (runtimeServices.PlayerInventory.TryAdd(item.Id, 1)) granted++;
            }

            var message = $"Ctrl+F6 신규 아이템 아트 검증 지급: {granted}/{DeliveredInventoryArtTestItemIds.Length}";
            if (missing > 0) message += $" (정의 누락 {missing})";
            ShowMessage(message);
            Debug.Log($"[Nyangbingo] {message}");
        }

        private void TeleportToSelectedCraftingStationForEditorTest()
        {
            var definition = SelectedBoss;
            if (definition == null || !TeleportToCraftingStationForEditorTest(definition.SummonStation))
            { ShowMessage("선택한 제작대를 찾을 수 없습니다."); return; }
            ShowMessage($"Shift+F6: {StationLabel(definition.SummonStation)} 앞으로 이동했습니다.");
        }

        private void GrantSelectedSummonItemForEditorTest()
        {
            var definition = SelectedBoss;
            if (definition?.SummonItem == null ||
                !runtimeServices.PlayerInventory.TryAdd(definition.SummonItem.Id, 1))
            {
                ShowMessage("F7 소환 아이템 지급 실패: 인벤토리 공간을 확인하세요.");
                return;
            }
            ShowMessage($"F7 테스트 지급: {definition.SummonItem.DisplayName} x1");
        }

        private void TeleportToDeepAltarForEditorTest()
        {
            var generator = bootstrap?.Session?.Generator;
            if (generator == null) { ShowMessage("깊은 제단 좌표를 찾을 수 없습니다."); return; }
            var altar = generator.AltarPosition;
            var position = new Vector2(altar.x + .5f, altar.y + .5f);
            MovePlayerForEditorTest(position);
            ShowMessage("Shift+F7: 깊은 얼음 제단으로 이동했습니다.");
        }

        private void MovePlayerForEditorTest(Vector2 position)
        {
            var body = playerTarget.GetComponent<Rigidbody2D>();
            if (body != null) body.position = position;
            playerTarget.transform.position = position;
        }
#endif

        private CraftingStation ResolveNearbyCraftingStation()
        {
            if (environmentState == null || playerTarget == null) return CraftingStation.None;
            var playerPosition = (Vector2)playerTarget.transform.position;
            var stations = new[]
            {
                CraftingStation.Workbench, CraftingStation.Furnace,
                CraftingStation.IceAnvil, CraftingStation.Foundry, CraftingStation.Smithy
            };
            foreach (var station in stations)
            {
                var definitionId = DefinitionIdForStation(station);
                if (environmentState.HasPlacedObjectWithin(
                        definitionId, playerPosition, craftingStationInteractionRange)) return station;
            }
            return CraftingStation.None;
        }

        public static string DefinitionIdForStation(CraftingStation station)
        {
            switch (station)
            {
                case CraftingStation.Workbench: return "workbench";
                case CraftingStation.Furnace: return "furnace";
                case CraftingStation.IceAnvil: return "ice_anvil";
                case CraftingStation.Foundry: return "blast_furnace";
                case CraftingStation.Smithy: return "smithy";
                default: return string.Empty;
            }
        }

        public static CraftingStation StationForDefinitionId(string definitionId)
        {
            switch (definitionId)
            {
                case "workbench": return CraftingStation.Workbench;
                case "furnace": return CraftingStation.Furnace;
                case "ice_anvil": return CraftingStation.IceAnvil;
                case "blast_furnace": return CraftingStation.Foundry;
                case "smithy": return CraftingStation.Smithy;
                default: return CraftingStation.None;
            }
        }

        private static string StationLabel(CraftingStation station)
        {
            switch (station)
            {
                case CraftingStation.Workbench: return "작업대";
                case CraftingStation.Furnace: return "화로";
                case CraftingStation.IceAnvil: return "얼음 모루";
                case CraftingStation.Foundry: return "용광로";
                case CraftingStation.Smithy: return "대장간";
                default: return station.ToString();
            }
        }

        private void ShowMessage(string value)
        {
            if (!string.IsNullOrEmpty(value)) Debug.Log($"[Nyangbingo] {value}");
            transientMessage = string.Empty;
            transientMessageUntil = 0f;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;
            if (encounterCoordinator?.BossManager?.IsBossActive == true)
            {
                statusText.text = string.Empty;
                return;
            }
            if (!string.IsNullOrEmpty(transientMessage)) { statusText.text = transientMessage; return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            statusText.text = string.Empty;
#else
            statusText.text = string.Empty;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void BuildDebugShortcutHelp()
        {
            var canvas = statusText != null ? statusText.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;

            debugShortcutHelpRoot = new GameObject("DebugShortcutHelp", typeof(RectTransform), typeof(Image));
            debugShortcutHelpRoot.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)debugShortcutHelpRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var backdrop = debugShortcutHelpRoot.GetComponent<Image>();
            backdrop.color = new Color(.01f, .015f, .022f, .94f);
            backdrop.raycastTarget = true;

            var panel = CreateDebugHelpObject("Panel", debugShortcutHelpRoot.transform,
                DebugShortcutHelpPanelSize, Vector2.zero);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(.055f, .075f, .105f, 1f);
            panelImage.raycastTarget = true;

            var title = CreateDebugHelpText(panel.transform, "Title", 8, TextAnchor.MiddleCenter,
                new Vector2(210f, 14f), new Vector2(0f, 80.5f));
            title.text = "MainGame 테스트 단축키";

            var body = CreateDebugHelpText(panel.transform, "Shortcuts", DebugShortcutHelpBodyFontSize,
                TextAnchor.UpperLeft,
                new Vector2(205f, 155f), new Vector2(0f, -.5f));
            body.text = DebugShortcutHelpText;
            body.lineSpacing = .92f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            var footer = CreateDebugHelpText(panel.transform, "Footer", 6, TextAnchor.MiddleCenter,
                new Vector2(210f, 10f), new Vector2(0f, -86.5f));
            footer.text = "F5 · 닫기";

            debugShortcutHelpRoot.SetActive(false);
            debugShortcutHelpOpen = false;
        }

        private void SetDebugShortcutHelpOpen(bool value)
        {
            if (debugShortcutHelpRoot == null) return;
            if (value)
            {
                debugShortcutHelpPreviousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                debugShortcutHelpRoot.transform.SetAsLastSibling();
            }
            else
            {
                Time.timeScale = debugShortcutHelpPreviousTimeScale;
            }
            debugShortcutHelpOpen = value;
            debugShortcutHelpRoot.SetActive(value);
        }

        private static GameObject CreateDebugHelpObject(string name, Transform parent, Vector2 size,
            Vector2 position)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            var rect = (RectTransform)result.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return result;
        }

        private static Text CreateDebugHelpText(Transform parent, string name, int fontSize, TextAnchor anchor,
            Vector2 size, Vector2 position)
        {
            var text = CreateDebugHelpObject(name, parent, size, position).AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = new Color(.93f, .96f, 1f);
            text.raycastTarget = false;
            return text;
        }
#endif

        private void HandleBossStateChanged(BossDefinition _) => RefreshStatus();

        private void HandleBossStateChanged(BossDefinition _, bool __) => RefreshStatus();

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugShortcutHelpOpen)
            {
                Time.timeScale = debugShortcutHelpPreviousTimeScale;
                debugShortcutHelpOpen = false;
            }
            if (debugShortcutHelpInstance == this) debugShortcutHelpInstance = null;
#endif
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshStatus;
            if (encounterCoordinator?.BossManager != null)
            {
                encounterCoordinator.BossManager.BossStarted -= HandleBossStateChanged;
                encounterCoordinator.BossManager.BossEnded -= HandleBossStateChanged;
            }
        }
    }
}
