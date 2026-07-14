using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;

namespace Nyangbingo.Crafting
{
    public sealed class CraftingService
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        public CraftingService(Nyangbingo.Inventory.Inventory inventory) { this.inventory = inventory; }

        public bool CanCraft(RecipeDefinition recipe, CraftingStation station, RecipeBook recipeBook = null)
        {
            if (recipe == null || recipe.Station != station || recipe.Output.item == null || (recipeBook != null && !recipeBook.IsUnlocked(recipe))) return false;
            foreach (var ingredient in recipe.Ingredients)
                if (ingredient.item == null || !inventory.Has(ingredient.item.Id, ingredient.amount)) return false;
            return true;
        }

        public bool TryCraft(RecipeDefinition recipe, CraftingStation station, RecipeBook recipeBook = null)
        {
            if (!CanCraft(recipe, station, recipeBook)) return false;
            foreach (var ingredient in recipe.Ingredients) inventory.TryRemove(ingredient.item.Id, ingredient.amount);
            if (inventory.TryAdd(recipe.Output.item.Id, recipe.Output.amount)) return true;
            foreach (var ingredient in recipe.Ingredients) inventory.TryAdd(ingredient.item.Id, ingredient.amount);
            return false;
        }
    }
}
