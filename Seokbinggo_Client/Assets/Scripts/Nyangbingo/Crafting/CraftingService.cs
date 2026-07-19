using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;

namespace Nyangbingo.Crafting
{
    public sealed class CraftingService
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        public Nyangbingo.Inventory.Inventory Inventory => inventory;
        public CraftingService(Nyangbingo.Inventory.Inventory inventory) { this.inventory = inventory; }

        public bool CanCraft(RecipeDefinition recipe, CraftingStation station, RecipeBook recipeBook = null)
        {
            if (recipe == null || recipe.Station != station || recipe.Output.item == null || recipe.Output.amount <= 0 ||
                recipe.Ingredients == null || recipe.DurationSeconds < 0f || float.IsNaN(recipe.DurationSeconds) ||
                float.IsInfinity(recipe.DurationSeconds) || (recipeBook != null && !recipeBook.IsUnlocked(recipe))) return false;

            var requiredByItem = new Dictionary<string, int>();
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.item == null || ingredient.amount <= 0) return false;
                requiredByItem.TryGetValue(ingredient.item.Id, out var required);
                if (required > int.MaxValue - ingredient.amount) return false;
                requiredByItem[ingredient.item.Id] = required + ingredient.amount;
            }

            foreach (var required in requiredByItem)
                if (!inventory.Has(required.Key, required.Value)) return false;
            return true;
        }

        public bool TryCraft(RecipeDefinition recipe, CraftingStation station, RecipeBook recipeBook = null)
        {
            if (!CanCraft(recipe, station, recipeBook)) return false;
            foreach (var ingredient in recipe.Ingredients) inventory.TryRemove(ingredient.item.Id, ingredient.amount);
            if (inventory.TryAdd(recipe.Output.item.Id, recipe.Output.amount))
            {
                GameEvents.RaiseCraftingCompleted(recipe);
                return true;
            }
            foreach (var ingredient in recipe.Ingredients) inventory.TryAdd(ingredient.item.Id, ingredient.amount);
            return false;
        }
    }
}
