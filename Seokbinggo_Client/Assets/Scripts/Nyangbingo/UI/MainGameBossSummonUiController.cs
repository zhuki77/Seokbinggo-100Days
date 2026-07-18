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

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameEncounterCoordinator encounterCoordinator;
        [SerializeField] private MainGameRaidTarget playerTarget;
        [SerializeField] private Text statusText;
        [Min(.1f)][SerializeField] private float deepAltarInteractionRange = 2.5f;
        [Min(.1f)][SerializeField] private float craftingStationInteractionRange = 1.5f;

        private readonly Dictionary<CraftingStation, Transform> stationAnchors =
            new Dictionary<CraftingStation, Transform>();
        private int selectedIndex;
        private string transientMessage;
        private float transientMessageUntil;
        private bool initialized;

        public BossDefinition SelectedBoss => gameDataCatalog != null
            ? gameDataCatalog.FindBoss(BossIds[selectedIndex])
            : null;
        public bool HasSceneBindings => gameDataCatalog != null && bootstrap != null && runtimeServices != null &&
                                        encounterCoordinator != null && playerTarget != null && statusText != null;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, MainGameEncounterCoordinator encounters,
            MainGameRaidTarget target, Text text)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            encounterCoordinator = encounters;
            playerTarget = target;
            statusText = text;
        }

        private void Start()
        {
            if (gameDataCatalog == null || bootstrap == null || runtimeServices == null ||
                encounterCoordinator == null || playerTarget == null || statusText == null ||
                !runtimeServices.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameBossSummonUiController: summon UI wiring is incomplete.");
                enabled = false;
                return;
            }
            runtimeServices.PlayerInventory.Changed += RefreshStatus;
            bootstrap.WorldReady += RebuildCraftingStationAnchors;
            initialized = true;
            RebuildCraftingStationAnchors();
            RefreshStatus();
            Debug.Log("[Nyangbingo] MainGame boss summon item, nighttime, and deep altar interaction ready.");
        }

        private void Update()
        {
            if (!initialized || Time.timeScale <= 0f) return;
            if (Input.GetKeyDown(KeyCode.B))
            {
                selectedIndex = (selectedIndex + 1) % BossIds.Length;
                transientMessage = string.Empty;
                RefreshStatus();
            }
            if (Input.GetKeyDown(KeyCode.C)) TryCraftSelectedSummonItem();
            if (Input.GetKeyDown(KeyCode.G)) TrySummonSelectedBoss();
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F6))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
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

        private void TryCraftSelectedSummonItem()
        {
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

        private void TrySummonSelectedBoss()
        {
            var definition = SelectedBoss;
            if (definition == null) { ShowMessage("선택한 보스 데이터가 없습니다."); return; }
            if (encounterCoordinator.BossManager?.IsBossActive == true)
            { ShowMessage("이미 보스전이 진행 중입니다."); return; }
            if (bootstrap.TimeService?.IsNight != true)
            { ShowMessage("보스는 밤에만 소환할 수 있습니다."); return; }
            if (definition.SummonItem == null ||
                !runtimeServices.PlayerInventory.Has(definition.SummonItem.Id, 1))
            { ShowMessage($"소환 아이템이 필요합니다: {definition.SummonItem?.DisplayName ?? definition.Id}"); return; }
            if (!IsAtDeepAltar(definition))
            { ShowMessage("이무기는 깊은 얼음 제단 근처에서만 소환할 수 있습니다."); return; }

            if (encounterCoordinator.TryStartPlayerSummonedBoss(definition, this))
            {
                ShowMessage($"{definition.DisplayName} 소환 성공. 소환 아이템 1개를 소비했습니다.");
                Debug.Log($"[Nyangbingo] Player summon started: {definition.Id}, item={definition.SummonItem.Id}.");
            }
            else ShowMessage($"{definition.DisplayName} 소환에 실패했습니다. 아이템은 보존됩니다.");
        }

#if UNITY_EDITOR
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

        private void TeleportToSelectedCraftingStationForEditorTest()
        {
            var definition = SelectedBoss;
            if (definition == null || !stationAnchors.TryGetValue(definition.SummonStation, out var anchor) ||
                anchor == null)
            { ShowMessage("선택한 제작대를 찾을 수 없습니다."); return; }
            MovePlayerForEditorTest(anchor.position);
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

        private void RebuildCraftingStationAnchors()
        {
            foreach (var entry in stationAnchors)
                if (entry.Value != null) Destroy(entry.Value.gameObject);
            stationAnchors.Clear();
            if (playerTarget == null || bootstrap == null) return;
            var origin = playerTarget.transform.position;
            CreateCraftingStationAnchor(CraftingStation.Workbench, origin + new Vector3(-3f, 2f),
                new Color(.65f, .4f, .18f), "WORKBENCH");
            CreateCraftingStationAnchor(CraftingStation.Furnace, origin + new Vector3(0f, 3f),
                new Color(.95f, .3f, .1f), "FURNACE");
            CreateCraftingStationAnchor(CraftingStation.IceAnvil, origin + new Vector3(3f, 2f),
                new Color(.25f, .75f, 1f), "ICE ANVIL");
        }

        private void CreateCraftingStationAnchor(CraftingStation station, Vector3 position, Color color,
            string label)
        {
            var stationObject = new GameObject($"BossSummonStation_{station}");
            stationObject.transform.SetParent(bootstrap.transform, false);
            stationObject.transform.position = position;
            RuntimePlaceholderVisual.Configure(stationObject.AddComponent<SpriteRenderer>(), color, 1f, 12);
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(stationObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, .8f, 0f);
            labelObject.transform.localScale = Vector3.one * .12f;
            var text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 28;
            text.color = Color.white;
            var renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 13;
            stationAnchors[station] = stationObject.transform;
        }

        private CraftingStation ResolveNearbyCraftingStation()
        {
            var playerPosition = (Vector2)playerTarget.transform.position;
            foreach (var entry in stationAnchors)
                if (entry.Value != null &&
                    Vector2.Distance(playerPosition, entry.Value.position) <= craftingStationInteractionRange)
                    return entry.Key;
            return CraftingStation.None;
        }

        private static string StationLabel(CraftingStation station)
        {
            switch (station)
            {
                case CraftingStation.Workbench: return "작업대";
                case CraftingStation.Furnace: return "용광로";
                case CraftingStation.IceAnvil: return "얼음 모루";
                default: return station.ToString();
            }
        }

        private void ShowMessage(string value)
        {
            transientMessage = value;
            transientMessageUntil = Time.unscaledTime + 3f;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;
            if (!string.IsNullOrEmpty(transientMessage)) { statusText.text = transientMessage; return; }
            var definition = SelectedBoss;
            if (definition?.SummonItem == null) { statusText.text = "B 보스 선택  ·  소환 데이터 없음"; return; }
            var count = runtimeServices?.PlayerInventory?.Count(definition.SummonItem.Id) ?? 0;
            var altar = definition.RequiresDeepAltar ? "  ·  깊은 제단 필요" : string.Empty;
            var materials = BuildMaterialStatus(definition);
            statusText.text = $"B 선택  ·  C 제작  ·  G 소환  ·  {definition.DisplayName}  ·  " +
                              $"{definition.SummonItem.DisplayName} x{count}  ·  {materials}{altar}";
#if UNITY_EDITOR
            statusText.text += "  ·  F6 재료 / Shift+F6 제작대  ·  F7 아이템 / Shift+F7 제단  ·  J 요괴 정리";
#endif
        }

        private string BuildMaterialStatus(BossDefinition definition)
        {
            var parts = new List<string>();
            var materials = definition.SummonMaterials;
            for (var index = 0; index < materials.Length; index++)
            {
                var material = materials[index];
                if (material.item == null) continue;
                var owned = runtimeServices?.PlayerInventory?.Count(material.item.Id) ?? 0;
                parts.Add($"{material.item.DisplayName} {owned}/{material.amount}");
            }
            return $"{StationLabel(definition.SummonStation)} [{string.Join(", ", parts)}]";
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshStatus;
            if (bootstrap != null) bootstrap.WorldReady -= RebuildCraftingStationAnchors;
            foreach (var entry in stationAnchors)
                if (entry.Value != null) Destroy(entry.Value.gameObject);
            stationAnchors.Clear();
        }
    }
}
