using Nyangbingo.Data;

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
        public bool TryStart(RecipeDefinition recipe, Nyangbingo.Core.CraftingStation station, RecipeBook book = null)
        {
            if (IsCrafting || recipe == null || recipe.DurationSeconds <= 0f || float.IsNaN(recipe.DurationSeconds) ||
                float.IsInfinity(recipe.DurationSeconds) || !crafting.CanCraft(recipe, station, book)) return false;
            foreach (var ingredient in recipe.Ingredients) crafting.Inventory.TryRemove(ingredient.item.Id, ingredient.amount);
            active = recipe; remaining = recipe.DurationSeconds; return true;
        }
        public bool Tick(float gameSeconds)
        {
            if (!IsCrafting || gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return false;
            remaining = System.Math.Max(0f, remaining - gameSeconds);
            if (remaining > 0f || !crafting.Inventory.TryAdd(active.Output.item.Id, active.Output.amount)) return false;
            active = null; return true;
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
