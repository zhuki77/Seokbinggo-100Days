using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v86 S1. 실제 서비스 상태 11종을 읽고 이벤트성 휴식/새벽 확인 이력을 보존한다.</summary>
    public sealed class MainGameGoalTracker : IDisposable
    {
        private readonly GameDataCatalog catalog;
        private readonly MainGameRuntimeServices services;
        private readonly MainGameBootstrap bootstrap;
        private readonly MainGameEnvironmentState environment;
        private readonly HashSet<string> bosses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> shownRewardCraftingIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> pendingRewardCraftingIds = new List<string>();
        private List<PlacedObjectRecord> placed = new List<PlacedObjectRecord>();
        private bool evaluating, suspended;
        public bool DemoComplete { get; private set; }
        public bool StorageSuccess => Progress.IsComplete(DemoAchievementRules.StorageGoalId);
        private float? sleepTemperature;
        private int confirmedKeptIce;
        private string targetKey;
        private Vector3Int? targetCell;
        private readonly List<Vector3Int> targetCandidates = new List<Vector3Int>();
        private Vector2 targetOrigin;
        private MainGameWorldDecorationRenderer decorations;
        public GoalProgress Progress { get; }
        public GuidePriorityQueue GuideQueue { get; } = new GuidePriorityQueue();
        public int PendingDawnDay { get; private set; }
        public int PendingDawnKeptIce { get; private set; }
        public int RestoreVersion { get; private set; }
        private readonly List<StorageDawnRecord> pendingDawns = new List<StorageDawnRecord>();
        public IReadOnlyList<StorageContainerDawnRecord> PendingDawnContainers => pendingDawns.Count > 0
            ? pendingDawns[0].containers : Array.Empty<StorageContainerDawnRecord>();
        public bool IsSuspended => suspended;

        public MainGameGoalTracker(GameDataCatalog data, MainGameRuntimeServices runtime,
            MainGameBootstrap mainBootstrap, MainGameEnvironmentState state)
        {
            catalog = data; services = runtime; bootstrap = mainBootstrap; environment = state;
            Progress = new GoalProgress(data.Goals);
            Progress.Completed += HandleGoalCompleted;
            services.PlayerInventory.Changed += Evaluate;
            services.JangdokStorage.Changed += Evaluate;
            services.Bed.Slept += HandleSlept;
            services.StorageTemperature.DailyProcessed += HandleDailyProcessed;
            GameEvents.OnPlacedObjectBuilt += HandlePlaced;
            GameEvents.OnTilePlaced += HandleTile;
            GameEvents.OnTileBroken += HandleTile;
            GameEvents.OnSealChanged += Evaluate;
            GameEvents.OnBossDefeated += HandleBoss;
            GameEvents.OnWorldItemPickedUp += HandleRewardPickedUp;
            bootstrap.WorldReady += InvalidateTarget;
        }

        public void SetSuspended(bool value) => suspended = value;
        public IReadOnlyList<RecipeDefinition> RewardRecipes(string itemId) => catalog.Recipes
            .Where(recipe => recipe != null && recipe.Output.item != null && recipe.Ingredients != null &&
                recipe.Ingredients.Any(ingredient => ingredient.item != null && ingredient.item.Id == itemId))
            .OrderBy(recipe => recipe.Station).ThenBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray();

        private void HandleRewardPickedUp(ItemDefinition item, int amount, Vector2 position)
        {
            if (suspended || item == null || amount <= 0 || shownRewardCraftingIds.Contains(item.Id) ||
                pendingRewardCraftingIds.Contains(item.Id)) return;
            var reward = catalog.Bosses.Any(boss => boss != null && boss.GuaranteedDrops.Any(drop => drop.item?.Id == item.Id)) ||
                catalog.Yokai.Any(yokai => yokai != null && yokai.Drops.Any(drop => drop.item?.Id == item.Id));
            if (reward && RewardRecipes(item.Id).Count > 0) pendingRewardCraftingIds.Add(item.Id);
        }

        // UI가 실제로 노출한 3초만 소모한다. 위험·메뉴·결과 화면에서는 대기한다.
        public void RefreshRewardCraftingGuide()
        {
            const string messageId = "reward_crafting";
            var entry = GuideQueue.Entries.FirstOrDefault(value => value.Definition.Id == messageId);
            if (pendingRewardCraftingIds.Count == 0) return;
            var id = pendingRewardCraftingIds[0];
            if (entry != null && entry.Context == id)
            {
                if (entry.Remaining > 0f) return;
                shownRewardCraftingIds.Add(id);
                pendingRewardCraftingIds.RemoveAt(0);
                return;
            }
            var definition = catalog.FindGuideMessage(messageId);
            var recipes = RewardRecipes(id);
            if (definition == null || recipes.Count == 0) return;
            var names = recipes.Select(recipe => catalog.ItemDisplayName(recipe.Output.item.Id)).Distinct().ToArray();
            var text = definition.Text.Replace("{item}", catalog.ItemDisplayName(id, "보상 재료"))
                .Replace("{recipes}", string.Join(" · ", names.Take(3)))
                .Replace("{n}", names.Length.ToString());
            GuideQueue.Fire(definition, text);
            entry = GuideQueue.Entries.FirstOrDefault(value => value.Definition.Id == messageId);
            if (entry != null) entry.Context = id;
        }
        public void Evaluate() => Evaluate(true);
        private void Evaluate(bool notify)
        {
            if (suspended || evaluating || environment == null || !environment.IsInitialized ||
                bootstrap.Session?.HasWorld != true) return;
            evaluating = true;
            try
            {
                placed = environment.ExportPlacedObjects();
                Progress.Evaluate(IsTrue, notify);
                DemoComplete |= DemoAchievementRules.MeetsCompletion(
                    bosses.Contains(DemoAchievementRules.DemoGateId(catalog)),
                    CoreModuleCount, DemoAchievementRules.RequiredCoreModuleCount(catalog));
            }
            finally { evaluating = false; }
        }

        private bool IsTrue(GoalDefinition goal) => goal != null && IsTrue(goal.CompleteState, goal.CompleteParam);

        public bool IsTrue(string state, string parameter)
        {
            if (!evaluating && environment != null) placed = environment.ExportPlacedObjects();
            var arg = parameter ?? string.Empty;
            var parts = arg.Split(':');
            switch (state)
            {
                case "station_installed": case "module_installed":
                    return HasInstalledDefinition(arg);
                case "item_count_ge":
                    return parts.Length == 2 && Number(parts[1], out var count) && services.PlayerInventory.Count(parts[0]) >= count;
                case "core_sealed":
                    return arg == "true" && placed.Any(p => p.definitionId == "ice_core" &&
                        bootstrap.SealSystem.IsCoreWindowSealed(bootstrap.TileService.WorldToCell(p.position)));
                case "storage_contains":
                    return parts.Length == 3 && Number(parts[2], out var required) && placed.Any(p =>
                        p.definitionId == parts[0] && services.JangdokStorage.TryGet(p.objectId, out var container) &&
                        container.Count(parts[1]) >= required);
                case "storage_condition_met":
                    return placed.Any(p => services.JangdokStorage.TryGet(p.objectId, out var storage) &&
                        storage.Count(arg) > 0 && services.StorageTemperature.TryGetStatus(p.objectId, out var temperature, out _) &&
                        !services.StorageTemperature.IsAtRisk(arg, temperature, p.objectId));
                case "slept":
                    return sleepTemperature.HasValue && parts.Length == 2 && parts[0] == "room_temp_ge" &&
                        catalog.FindGlobal(parts[1]) is { } sleepRequirement && sleepRequirement.TryGetFloat(out var minimum) &&
                        sleepTemperature.Value >= minimum;
                case "dawn_storage_result":
                    return parts.Length == 2 && parts[0] == "kept_ge" && Number(parts[1], out var kept) && confirmedKeptIce >= kept;
                case "item_owned":
                    return services.PlayerInventory.Count(arg) > 0 || services.EquipmentCollection.Contains(arg);
                case "boss_defeated": return bosses.Contains(arg);
                case "core_modules_complete":
                    return catalog.FindGlobal(arg) is { } value && value.TryGetInt(out var total) && total > 0 &&
                        CoreModuleCount >= total;
                default: return false;
            }
        }

        public IReadOnlyList<string> InstalledCoreModuleIds
        {
            get
            {
                if (!evaluating && environment != null) placed = environment.ExportPlacedObjects();
                return DemoAchievementRules.CoreModules(catalog).Where(m => HasInstalledDefinition(m.Item.Id))
                    .Select(m => m.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            }
        }
        public int CoreModuleCount => InstalledCoreModuleIds.Count;
        private bool HasInstalledDefinition(string id)
        {
            if (placed.Any(p => p.definitionId == id)) return true;
            var tiles = bootstrap.TileService;
            return tiles != null && tiles.GetTileChangeRecords().Any(change => change.placed &&
                TileIdAlias.ToCanonical(tiles.GetTile(new Vector3Int(change.x, change.y, change.z)).elementType) == id);
        }

        public string CurrentHint
        {
            get
            {
                var text = catalog.FindGuideMessage(Progress.Current?.HintMessageId)?.Text ?? string.Empty;
                var frozen = services.StorageTemperature.FrozenMaximum.ToString("0.#", CultureInfo.InvariantCulture);
                var requirement = catalog.FindGlobal("win_modules")?.Value ?? string.Empty;
                var temperature = placed.Where(p => services.JangdokStorage.TryGet(p.objectId, out var s) && s.Count("ice_shard") > 0)
                    .Select(p => services.RoomTemperature.ResolveExact(p.position)).DefaultIfEmpty(float.NaN).Min();
                return text.Replace("{n}", CoreModuleCount.ToString()).Replace("{total}", requirement)
                    .Replace("{req}", frozen).Replace("{temp}", float.IsNaN(temperature) ? "—" : temperature.ToString("0.#", CultureInfo.InvariantCulture));
            }
        }

        public void AcknowledgeDawn(int day)
        {
            if (suspended || day != PendingDawnDay || day < 1) return;
            confirmedKeptIce = Math.Max(confirmedKeptIce, PendingDawnKeptIce);
            if (pendingDawns.Count > 0) pendingDawns.RemoveAt(0);
            RefreshPendingDawn();
            Evaluate();
        }

        public GoalProgressRecord Capture()
        {
            Evaluate();
            var record = Progress.Capture();
            record.pendingDawnDay = PendingDawnDay; record.pendingDawnKeptIce = PendingDawnKeptIce;
            record.pendingStorageDawns = pendingDawns.Select(result => result.Copy()).ToList();
            record.guideDay = GuideQueue.Day;
            record.consumedGuideIds = GuideQueue.CaptureConsumed();
            record.shownRewardCraftingIds = shownRewardCraftingIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            record.pendingRewardCraftingIds = pendingRewardCraftingIds.ToList();
            return record;
        }

        public void Restore(SaveGame save, bool reevaluate)
        {
            Progress.Restore(save.goalProgress);
            shownRewardCraftingIds.Clear();
            pendingRewardCraftingIds.Clear();
            foreach (var id in save.goalProgress.shownRewardCraftingIds ?? new List<string>())
                if (catalog.FindItem(id) != null) shownRewardCraftingIds.Add(id);
            foreach (var id in save.goalProgress.pendingRewardCraftingIds ?? new List<string>())
                if (catalog.FindItem(id) != null && !shownRewardCraftingIds.Contains(id) &&
                    !pendingRewardCraftingIds.Contains(id)) pendingRewardCraftingIds.Add(id);
            DemoComplete = save.demoComplete;
            var currentDay = bootstrap.TimeService.Day;
            GuideQueue.RestoreConsumed(currentDay, save.goalProgress.guideDay == currentDay
                ? save.goalProgress.consumedGuideIds : null);
            RestoreVersion++;
            // Events cannot be inferred from a placed bed or from ice present after loading.
            sleepTemperature = null;
            confirmedKeptIce = 0;
            bosses.Clear();
            foreach (var record in save.bossRecords)
                if (record.count > 0) bosses.Add(record.bossId);
            pendingDawns.Clear();
            if (save.goalProgress.pendingStorageDawns != null)
                pendingDawns.AddRange(save.goalProgress.pendingStorageDawns.Select(result => result.Copy()));
            if (pendingDawns.Count == 0 && save.goalProgress.pendingDawnDay > 0 && save.goalProgress.pendingDawnKeptIce > 0)
                pendingDawns.Add(new StorageDawnRecord
                { day = save.goalProgress.pendingDawnDay, keptIce = save.goalProgress.pendingDawnKeptIce });
            RefreshPendingDawn();
            services.StorageTemperature.ResetConditionBaseline();
            InvalidateTarget();
            if (reevaluate)
            {
                var wasSuspended = suspended;
                suspended = false;
                try { Evaluate(false); }
                finally { suspended = wasSuspended; }
            }
        }

        private void HandleSlept(float temperature)
        {
            if (suspended) return;
            sleepTemperature = temperature;
            Evaluate();
        }
        private void HandleDailyProcessed(int day, StorageDailyResult result)
        {
            if (suspended) return;
            if (result.KeptIceItems > 0 || result.Containers?.Count > 0)
            {
                // 여러 새벽을 넘겨도 아직 읽지 않은 결과를 덮어쓰지 않는다.
                pendingDawns.RemoveAll(entry => entry.day == day);
                pendingDawns.Add(new StorageDawnRecord
                {
                    day = day, keptIce = result.KeptIceItems,
                    containers = result.Containers?.Select(container => container.Copy()).ToList() ?? new List<StorageContainerDawnRecord>()
                });
                RefreshPendingDawn();
            }
            Evaluate();
        }
        private void RefreshPendingDawn()
        {
            PendingDawnDay = pendingDawns.Count > 0 ? pendingDawns[0].day : 0;
            PendingDawnKeptIce = pendingDawns.Count > 0 ? pendingDawns[0].keptIce : 0;
        }
        private void HandleBoss(BossDefinition boss) { if (!suspended && boss != null) bosses.Add(boss.Id); Evaluate(); }
        private void HandlePlaced(string _) => Evaluate();
        private void HandleGoalCompleted(GoalDefinition _) => GameEvents.RaiseGoalBadgeCompleted();
        private void HandleTile(Vector3Int cell)
        {
            if (targetKey != null)
            {
                var id = targetKey.StartsWith("nearest:", StringComparison.Ordinal) ? targetKey.Substring(8) : WorldTileTypes.IceLake;
                targetCandidates.Remove(cell);
                if (TileIdAlias.ToCanonical(bootstrap.TileService.GetTile(cell).elementType) == id) targetCandidates.Add(cell);
                targetCell = null;
            }
            Evaluate();
        }
        private void InvalidateTarget() { targetKey = null; targetCell = null; targetCandidates.Clear(); }
        private static bool Number(string raw, out int value) => int.TryParse(raw, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value) && value > 0;

        private string ResolveGuideKey()
        {
            var goal = Progress.Current;
            if (goal == null) return null;
            string itemId;
            var amount = 1;
            switch (goal.CompleteState)
            {
                case "station_installed":
                case "module_installed":
                case "item_owned":
                    itemId = goal.CompleteParam;
                    break;
                case "item_count_ge":
                    var parts = (goal.CompleteParam ?? string.Empty).Split(':');
                    if (parts.Length != 2 || !Number(parts[1], out amount)) return goal.GuideTarget;
                    itemId = parts[0];
                    break;
                default:
                    return goal.GuideTarget;
            }
            // A crafted installation already in the bag needs placing, not another resource trip.
            if (services.PlayerInventory.Count(itemId) >= amount) return null;
            return ProductionGoalGuide.Resolve(catalog, services.PlayerInventory, itemId, amount,
                stationId => environment.ExportPlacedObjects().Any(p => p.definitionId == stationId));
        }

        public Vector2? ResolveTarget(Vector2 origin)
        {
            var key = ResolveGuideKey();
            if (string.IsNullOrEmpty(key) || key == "none" || environment == null) return null;
            if (key == "core") return environment.TryGetNearestPlacedObjectPosition("ice_core", origin, out var core) ? core : null;
            var parts = key.Split(':');
            if (parts.Length != 2) return null;
            if (parts[0] == "station")
                return environment.TryGetNearestPlacedObjectPosition(parts[1], origin, out var station) ? station : null;
            if (parts[0] == "summon")
                return environment.TryGetNearestPlacedObjectPosition("ice_altar", origin, out var altar) ? altar :
                    bootstrap.Session?.HasWorld == true ? (Vector2)bootstrap.Session.LastResult.altarPosition + Vector2.one * .5f : null;
            if (parts[0] == "arena" && parts[1] == "imugi")
                return bootstrap.Session?.SurfaceIceLakeBounds.width > 0
                    ? bootstrap.Session.SurfaceIceLakeArrival : null;
            var id = parts[0] == "arena" && parts[1] == "imugi" ? WorldTileTypes.IceLake :
                parts[0] == "nearest" ? parts[1] : null;
            var tiles = bootstrap.TileService;
            if (id == null || tiles == null) return null;
            if (id == MainGameWorldDecorationRenderer.WoodItemId ||
                id == MainGameWorldDecorationRenderer.HempItemId ||
                id == MainGameWorldDecorationRenderer.RebarItemId)
            {
                decorations ??= UnityEngine.Object.FindAnyObjectByType<MainGameWorldDecorationRenderer>();
                return decorations != null && decorations.TryFindNearestMaterial(id, origin, out var material)
                    ? material : null;
            }
            if (targetKey != key)
            {
                targetKey = key; targetCell = null; targetCandidates.Clear();
                // Resolve once per goal or target removal, never scan the map every HUD frame.
                for (var x = 0; x < tiles.Width; x++)
                    for (var y = 0; y < tiles.Height; y++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (TileIdAlias.ToCanonical(tiles.GetTile(cell).elementType) != id) continue;
                        targetCandidates.Add(cell);
                    }
            }
            if (!targetCell.HasValue || (origin - targetOrigin).sqrMagnitude >= 4f)
            {
                var distance = float.PositiveInfinity;
                targetOrigin = origin;
                foreach (var cell in targetCandidates)
                {
                    var delta = ((Vector2)tiles.GetCellCenterWorld(cell) - origin).sqrMagnitude;
                    if (delta >= distance) continue;
                    distance = delta; targetCell = cell;
                }
            }
            return targetCell.HasValue ? (Vector2)tiles.GetCellCenterWorld(targetCell.Value) : null;
        }

        public void Dispose()
        {
            Progress.Completed -= HandleGoalCompleted;
            services.PlayerInventory.Changed -= Evaluate; services.JangdokStorage.Changed -= Evaluate;
            services.Bed.Slept -= HandleSlept; services.StorageTemperature.DailyProcessed -= HandleDailyProcessed;
            GameEvents.OnPlacedObjectBuilt -= HandlePlaced; GameEvents.OnTilePlaced -= HandleTile;
            GameEvents.OnTileBroken -= HandleTile; GameEvents.OnSealChanged -= Evaluate;
            GameEvents.OnBossDefeated -= HandleBoss; bootstrap.WorldReady -= InvalidateTarget;
            GameEvents.OnWorldItemPickedUp -= HandleRewardPickedUp;
        }
    }
    /// <summary>Derive the next collection or production step from the current recipe data.</summary>
    public static class ProductionGoalGuide
    {
        public static string Resolve(GameDataCatalog catalog, Nyangbingo.Inventory.Inventory inventory,
            string itemId, int amount, Func<string, bool> stationExists)
        {
            if (catalog == null || inventory == null || amount <= 0 || string.IsNullOrEmpty(itemId)) return null;
            return ResolveItem(catalog, inventory, itemId, amount, stationExists,
                new HashSet<string>(StringComparer.Ordinal));
        }

        private static string ResolveItem(GameDataCatalog catalog, Nyangbingo.Inventory.Inventory inventory,
            string itemId, int required, Func<string, bool> stationExists, HashSet<string> visiting)
        {
            var shortage = required - inventory.Count(itemId);
            if (shortage <= 0) return null;
            if (!visiting.Add(itemId)) return null;
            try
            {
                // Smelting has a separate fuel cost; it must not be skipped by generic recipes.
                var smelt = catalog.Smelting.FirstOrDefault(r => r != null && r.Output.item?.Id == itemId);
                if (smelt != null && smelt.Output.amount > 0)
                {
                    var batches = 1L + (shortage - 1L) / smelt.Output.amount;
                    var key = MissingMaterial(catalog, inventory, new[] { smelt.Input, smelt.Fuel },
                        batches, stationExists, visiting);
                    return key ?? ResolveStation(catalog, inventory,
                        smelt.StationKind == SmeltingStationKind.Foundry ? CraftingStation.Foundry :
                        CraftingStation.Furnace, stationExists, visiting);
                }
                var recipe = catalog.Recipes.FirstOrDefault(r => r != null &&
                    r.Output.item?.Id == itemId && r.MvpScope != ItemMvpScope.B);
                if (recipe == null || recipe.Output.amount <= 0) return "nearest:" + itemId;
                var cycles = 1L + (shortage - 1L) / recipe.Output.amount;
                var missing = MissingMaterial(catalog, inventory, recipe.Ingredients,
                    cycles, stationExists, visiting);
                return missing ?? ResolveStation(catalog, inventory, recipe.Station, stationExists, visiting);
            }
            finally { visiting.Remove(itemId); }
        }

        private static string MissingMaterial(GameDataCatalog catalog, Nyangbingo.Inventory.Inventory inventory,
            IEnumerable<ItemAmount> ingredients, long batches, Func<string, bool> stationExists,
            HashSet<string> visiting)
        {
            foreach (var group in (ingredients ?? Array.Empty<ItemAmount>())
                         .Where(i => i.item != null && i.amount > 0).GroupBy(i => i.item.Id))
            {
                var required = (int)Math.Min(int.MaxValue, group.Sum(i => (long)i.amount) * batches);
                if (inventory.Count(group.Key) >= required) continue;
                return ResolveItem(catalog, inventory, group.Key, required, stationExists, visiting) ?? "none";
            }
            return null;
        }

        private static string ResolveStation(GameDataCatalog catalog, Nyangbingo.Inventory.Inventory inventory,
            CraftingStation station, Func<string, bool> stationExists, HashSet<string> visiting)
        {
            var id = station switch
            {
                CraftingStation.Workbench => "workbench", CraftingStation.Furnace => "furnace",
                CraftingStation.Foundry => "blast_furnace", CraftingStation.IceAnvil => "ice_anvil",
                CraftingStation.Smithy => "smithy", _ => null
            };
            // Hand crafting and an unplaced station in the bag have no world arrow destination.
            if (id == null || inventory.Count(id) > 0) return null;
            return stationExists == null || stationExists(id) ? "station:" + id :
                ResolveItem(catalog, inventory, id, 1, stationExists, visiting);
        }
    }

}
