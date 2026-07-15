using System;
using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.Crafting
{
    public sealed class RecipeBook
    {
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        public bool IsUnlocked(RecipeDefinition recipe) => recipe != null && unlocked.Contains(recipe.Id);
        public void Unlock(string recipeId) { if (!string.IsNullOrWhiteSpace(recipeId)) unlocked.Add(recipeId); }
        public List<string> Export()
        {
            var result = new List<string>(unlocked);
            result.Sort(StringComparer.Ordinal);
            return result;
        }
        public void Import(IEnumerable<string> recipeIds)
        {
            unlocked.Clear();
            if (recipeIds == null) return;
            foreach (var id in recipeIds) Unlock(id);
        }
    }
}
