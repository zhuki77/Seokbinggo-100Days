using Nyangbingo.Data;
using Nyangbingo.Core;

namespace Nyangbingo.Crafting
{
    public sealed class CraftingProcess : Nyangbingo.Core.IGameSecondsTickable
    {
        private readonly CraftingService crafting;
        private RecipeDefinition active;
        private float remaining;
        public bool IsCrafting => active != null;
        public RecipeDefinition Active => active;
        public float RemainingSeconds => remaining;
        public CraftingProcess(CraftingService crafting) { this.crafting = crafting; }
        public bool TryStart(RecipeDefinition recipe, Nyangbingo.Core.CraftingStation station, RecipeBook book = null,
            float durationMultiplier = 1f)
        {
            if (IsCrafting || recipe == null || recipe.DurationSeconds <= 0f || float.IsNaN(recipe.DurationSeconds) ||
                float.IsInfinity(recipe.DurationSeconds) || !crafting.CanCraft(recipe, station, book)) return false;
            foreach (var ingredient in recipe.Ingredients) crafting.Inventory.TryRemove(ingredient.item.Id, ingredient.amount);
            var multiplier = durationMultiplier <= 0f || float.IsNaN(durationMultiplier) ||
                             float.IsInfinity(durationMultiplier)
                ? 1f
                : durationMultiplier;
            active = recipe;
            remaining = recipe.DurationSeconds * multiplier;
            return true;
        }
        public bool Tick(float gameSeconds)
        {
            if (!IsCrafting || gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return false;
            remaining = System.Math.Max(0f, remaining - gameSeconds);
            if (remaining > 0f || !crafting.Inventory.TryAdd(active.Output.item.Id, active.Output.amount)) return false;
            var completedRecipe = active;
            active = null;
            Nyangbingo.Core.GameEvents.RaiseCraftingCompleted(completedRecipe);
            return true;
        }

        void Nyangbingo.Core.IGameSecondsTickable.Tick(float deltaGameSeconds) => Tick(deltaGameSeconds);

        public bool RestoreState(RecipeDefinition activeRecipe, float remainingSeconds)
        {
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds) || remainingSeconds < 0f) return false;
            if (activeRecipe == null)
            {
                if (remainingSeconds > 0f) return false;
                active = null;
                remaining = 0f;
                return true;
            }

            if (activeRecipe.DurationSeconds <= 0f || float.IsNaN(activeRecipe.DurationSeconds) ||
                float.IsInfinity(activeRecipe.DurationSeconds) || remainingSeconds > activeRecipe.DurationSeconds ||
                activeRecipe.Output.item == null || activeRecipe.Output.amount <= 0) return false;
            active = activeRecipe;
            remaining = remainingSeconds;
            return true;
        }
    }
}

namespace Nyangbingo.Crafting
{
    [System.Serializable]
    public sealed class StationJobRecord
    {
        public string recipeId;
        public bool smelting;
        public float duration;
        public float remaining;
        public System.Collections.Generic.List<Nyangbingo.Inventory.InventorySlot> materials =
            new System.Collections.Generic.List<Nyangbingo.Inventory.InventorySlot>();
    }

    [System.Serializable]
    public sealed class StationQueueRecord
    {
        public string objectId;
        public Nyangbingo.Core.CraftingStation station;
        public UnityEngine.Vector2 position;
        public System.Collections.Generic.List<StationJobRecord> jobs =
            new System.Collections.Generic.List<StationJobRecord>();
        public System.Collections.Generic.List<Nyangbingo.Inventory.InventorySlot> returns =
            new System.Collections.Generic.List<Nyangbingo.Inventory.InventorySlot>();
    }

    /// <summary>Each placed station owns one active job and up to four prepaid waiting jobs.</summary>
    public sealed class StationProductionService : Nyangbingo.Core.IGameSecondsTickable
    {
        public const int WaitingCapacity = 4;
        public const string HandStationId = "hand-crafting";
        private readonly GameDataCatalog catalog;
        private readonly Nyangbingo.Inventory.Inventory inventory;
        private readonly System.Func<string, bool> exists;
        private readonly System.Func<UnityEngine.Vector2, bool> temperatureOk;
        private System.Collections.Generic.List<StationQueueRecord> stations =
            new System.Collections.Generic.List<StationQueueRecord>();
        public StationProductionService(GameDataCatalog data, Nyangbingo.Inventory.Inventory items,
            System.Func<string, bool> stationExists, System.Func<UnityEngine.Vector2, bool> canSmelt)
        { catalog = data; inventory = items; exists = stationExists; temperatureOk = canSmelt; }

        public StationQueueRecord Get(string id, Nyangbingo.Core.CraftingStation station,
            UnityEngine.Vector2 position)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var state = stations.Find(s => s.objectId == id);
            if (state != null) return state.station == station ? state : null;
            state = new StationQueueRecord { objectId = id, station = station, position = position };
            stations.Add(state);
            return state;
        }

        public bool CanEnqueue(StationQueueRecord state) => state != null &&
            stations.Contains(state) && state.jobs.Count < WaitingCapacity + 1 &&
            (state.objectId == HandStationId || exists(state.objectId));

        public bool TryEnqueue(StationQueueRecord state, RecipeDefinition recipe, float multiplier = 1f)
        {
            if (!CanEnqueue(state) || recipe == null || recipe.Station != state.station ||
                !new CraftingService(inventory).CanCraft(recipe, state.station) ||
                !Finite(multiplier) || multiplier <= 0f) return false;
            return Reserve(state, recipe.Id, false, recipe.DurationSeconds * multiplier, recipe.Ingredients);
        }

        public bool TryEnqueue(StationQueueRecord state, SmeltingDefinition recipe)
        {
            if (!CanEnqueue(state) || recipe == null || !CanSmelt(state) ||
                (recipe.StationKind == SmeltingStationKind.Foundry
                    ? state.station != Nyangbingo.Core.CraftingStation.Foundry
                    : state.station != Nyangbingo.Core.CraftingStation.Furnace) ||
                recipe.Output.item == null || recipe.Output.amount <= 0) return false;
            return Reserve(state, recipe.Id, true, recipe.DurationSeconds, new[] { recipe.Input, recipe.Fuel });
        }

        private bool Reserve(StationQueueRecord state, string recipeId, bool smelting, float duration,
            ItemAmount[] ingredients)
        {
            if (!Finite(duration) || duration < 0f || ingredients == null) return false;
            var next = inventory.Export();
            var reserved = new System.Collections.Generic.List<Nyangbingo.Inventory.InventorySlot>();
            foreach (var ingredient in ingredients)
            {
                if (ingredient.item == null || ingredient.amount <= 0) return false;
                var needed = ingredient.amount;
                for (var i = 0; i < next.Count && needed > 0; i++)
                {
                    var slot = next[i];
                    if (slot.itemId != ingredient.item.Id) continue;
                    var take = System.Math.Min(needed, slot.amount);
                    var consumed = slot;
                    consumed.amount = take;
                    consumed.storageMeltRemainder = slot.storageMeltRemainder * take / slot.amount;
                    reserved.Add(consumed);
                    slot.storageMeltRemainder -= consumed.storageMeltRemainder;
                    slot.amount -= take;
                    next[i] = slot.amount == 0 ? default : slot;
                    needed -= take;
                }
                if (needed != 0) return false;
            }
            var job = new StationJobRecord { recipeId = recipeId, smelting = smelting,
                duration = duration, remaining = duration, materials = reserved };
            // Commit the job before inventory change listeners refresh the UI.
            state.jobs.Add(job);
            if (inventory.TryImport(next)) return true;
            state.jobs.Remove(job);
            return false;
        }

        public bool Cancel(StationQueueRecord state, int index)
        {
            if (state == null || !stations.Contains(state) || index < 0 || index >= state.jobs.Count) return false;
            var job = state.jobs[index];
            state.jobs.RemoveAt(index);
            state.returns.AddRange(job.materials);
            Collect(state);
            return true;
        }

        public int Collect(StationQueueRecord state)
        {
            if (state == null) return 0;
            var collected = 0;
            // Remove before dispatching Changed to avoid a reentrant double collection.
            for (var i = 0; i < state.returns.Count;)
            {
                var item = state.returns[i];
                state.returns.RemoveAt(i);
                if (inventory.TryAddWithStorageState(item.itemId, item.amount, item.hasStorageCondition,
                        item.EffectiveStorageCondition, item.storageMeltRemainder)) collected++;
                else { state.returns.Insert(i, item); i++; }
            }
            return collected;
        }

        public void Tick(float seconds)
        {
            if (!Finite(seconds) || seconds <= 0f) return;
            foreach (var state in stations.ToArray())
            {
                if (state.objectId != HandStationId && !exists(state.objectId))
                {
                    // Destroyed stations refund their prepaid jobs; retained returns are saveable.
                    while (state.jobs.Count > 0) Cancel(state, 0);
                    Collect(state);
                    if (state.returns.Count == 0) stations.Remove(state);
                    continue;
                }
                var elapsed = seconds;
                while (state.jobs.Count > 0)
                {
                    var job = state.jobs[0];
                    if (job.smelting && !CanSmelt(state)) break;
                    if (elapsed < job.remaining) { job.remaining -= elapsed; break; }
                    elapsed -= job.remaining;
                    job.remaining = 0f;
                    var recipe = job.smelting ? null : catalog.FindRecipe(job.recipeId);
                    var output = job.smelting ? catalog.FindSmelting(job.recipeId).Output : recipe.Output;
                    state.jobs.RemoveAt(0);
                    state.returns.Add(new Nyangbingo.Inventory.InventorySlot
                        { itemId = output.item.Id, amount = output.amount });
                    // Normal crafting keeps automatic delivery; smelting retains explicit collection.
                    if (!job.smelting) Collect(state);
                    if (recipe != null) Nyangbingo.Core.GameEvents.RaiseCraftingCompleted(recipe);
                    if (elapsed <= 0f) break;
                }
            }
        }

        public bool CanSmelt(StationQueueRecord state) => state != null && temperatureOk(state.position);

        public StationQueueRecord FindNearestActive(UnityEngine.Vector2 position,
            float maximumDistance = float.PositiveInfinity, bool includeHandCrafting = true)
        {
            StationQueueRecord nearest = null;
            var distance = maximumDistance * maximumDistance;
            foreach (var state in stations)
            {
                if (state.jobs.Count == 0 || state.objectId != HandStationId && !exists(state.objectId)) continue;
                if (!includeHandCrafting && state.objectId == HandStationId) continue;
                var candidate = state.objectId == HandStationId ? 0f : (state.position - position).sqrMagnitude;
                if (candidate > distance) continue;
                nearest = state;
                distance = candidate;
            }
            return nearest;
        }

        public int PendingCount(string itemId)
        {
            long count = 0;
            foreach (var station in stations)
                foreach (var slot in station.returns)
                    if (slot.itemId == itemId) count += slot.amount;
            return (int)System.Math.Min(int.MaxValue, count);
        }

        public System.Collections.Generic.List<StationQueueRecord> Export()
        {
            var result = new System.Collections.Generic.List<StationQueueRecord>();
            foreach (var state in stations)
                if (state.jobs.Count > 0 || state.returns.Count > 0)
                    result.Add(Clone(state));
            return result;
        }

        public bool Restore(System.Collections.Generic.List<StationQueueRecord> records)
        {
            var next = new System.Collections.Generic.List<StationQueueRecord>();
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var s in records ?? new System.Collections.Generic.List<StationQueueRecord>())
            {
                if (s == null || string.IsNullOrEmpty(s.objectId) || !ids.Add(s.objectId) ||
                    !Finite(s.position.x) || !Finite(s.position.y) || s.jobs == null || s.returns == null ||
                    s.jobs.Count > WaitingCapacity + 1) return false;
                foreach (var job in s.jobs)
                {
                    if (job == null || !Finite(job.duration) || !Finite(job.remaining) ||
                        job.duration < 0f || job.remaining < 0f || job.remaining > job.duration ||
                        job.materials == null) return false;
                    if (job.smelting)
                    {
                        var r = catalog.FindSmelting(job.recipeId);
                        if (r == null || r.Output.item == null || r.Output.amount <= 0 ||
                            (r.StationKind == SmeltingStationKind.Foundry
                                ? s.station != Nyangbingo.Core.CraftingStation.Foundry
                                : s.station != Nyangbingo.Core.CraftingStation.Furnace)) return false;
                    }
                    else
                    {
                        var r = catalog.FindRecipe(job.recipeId);
                        if (r == null || r.Station != s.station || r.Output.item == null || r.Output.amount <= 0)
                            return false;
                    }
                    foreach (var slot in job.materials) if (!ValidSlot(slot)) return false;
                }
                foreach (var slot in s.returns) if (!ValidSlot(slot)) return false;
                next.Add(Clone(s));
            }
            stations = next;
            return true;
        }

        public string OutputName(StationJobRecord job) => job.smelting
            ? catalog.FindSmelting(job.recipeId)?.Output.item?.DisplayName
            : catalog.FindRecipe(job.recipeId)?.Output.item?.DisplayName;

        // Old saves had no placed-station identity. Bind their existing work to the first
        // opened matching station without consuming its already-paid materials a second time.
        public void AdoptLegacy(StationQueueRecord state, CraftingProcess crafting, SmeltingStation smelting)
        {
            if (state == null || state.jobs.Count != 0) return;
            if (crafting?.Active != null && crafting.Active.Station == state.station)
            {
                var recipe = crafting.Active;
                state.jobs.Add(LegacyJob(recipe.Id, false, recipe.DurationSeconds,
                    crafting.RemainingSeconds, recipe.Ingredients));
                crafting.RestoreState(null, 0f);
            }
            if (smelting == null || smelting.Queue.Count + (smelting.Active == null ? 0 : 1) +
                    state.jobs.Count > WaitingCapacity + 1) return;
            if (smelting.Active != null)
                state.jobs.Add(LegacyJob(smelting.Active.Id, true, smelting.Active.DurationSeconds,
                    smelting.RemainingSeconds, new[] { smelting.Active.Input, smelting.Active.Fuel }));
            foreach (var r in smelting.Queue)
                state.jobs.Add(LegacyJob(r.Id, true, r.DurationSeconds, r.DurationSeconds,
                    new[] { r.Input, r.Fuel }));
            foreach (var output in smelting.Completed)
                state.returns.Add(new Nyangbingo.Inventory.InventorySlot
                    { itemId = output.item.Id, amount = output.amount });
            smelting.RestoreState(null, 0f, new SmeltingDefinition[0], new ItemAmount[0]);
        }

        private static StationJobRecord LegacyJob(string id, bool smelting, float duration,
            float remaining, ItemAmount[] ingredients)
        {
            var job = new StationJobRecord { recipeId = id, smelting = smelting,
                duration = System.Math.Max(duration, remaining), remaining = remaining };
            foreach (var i in ingredients)
                job.materials.Add(new Nyangbingo.Inventory.InventorySlot { itemId = i.item.Id, amount = i.amount });
            return job;
        }

        private bool ValidSlot(Nyangbingo.Inventory.InventorySlot slot) =>
            catalog.FindItem(slot.itemId)?.IsInventoryItem == true && slot.amount > 0 &&
            Finite(slot.EffectiveStorageCondition) && slot.EffectiveStorageCondition >= 0f &&
            slot.EffectiveStorageCondition <= 1f && Finite(slot.storageMeltRemainder) &&
            slot.storageMeltRemainder >= 0f && slot.storageMeltRemainder < 1f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static StationQueueRecord Clone(StationQueueRecord state) =>
            UnityEngine.JsonUtility.FromJson<StationQueueRecord>(UnityEngine.JsonUtility.ToJson(state));
    }
}
