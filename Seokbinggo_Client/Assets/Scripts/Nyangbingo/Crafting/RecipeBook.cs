using System;
using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.Crafting
{
    public sealed class RecipeBook
    {
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        public event Action Changed;

        public bool IsUnlocked(RecipeDefinition recipe) => recipe != null && unlocked.Contains(recipe.Id);
        public void Unlock(string recipeId)
        {
            if (!string.IsNullOrWhiteSpace(recipeId) && unlocked.Add(recipeId)) Changed?.Invoke();
        }

        public List<string> Export()
        {
            var result = new List<string>(unlocked);
            result.Sort(StringComparer.Ordinal);
            return result;
        }
        public void Import(IEnumerable<string> recipeIds)
        {
            var previous = new HashSet<string>(unlocked, StringComparer.Ordinal);
            unlocked.Clear();
            if (recipeIds != null)
                foreach (var id in recipeIds)
                    if (!string.IsNullOrWhiteSpace(id)) unlocked.Add(id);
            if (!previous.SetEquals(unlocked)) Changed?.Invoke();
        }
    }

    /// <summary>
    /// 제작대 근접 필터와 별개로 유지되는 진행도 잠금 규칙.
    /// v34에서는 강철이 최초 처치 뒤 얼음 제단 봉헌만 영구 해금된다.
    /// </summary>
    public static class RecipeUnlockPolicy
    {
        public const string GangcheoriUnlockRecipeId = "ice_altar_offering";

        public static bool IsUnlocked(RecipeDefinition recipe, RecipeBook book)
        {
            if (recipe == null) return false;
            return !string.Equals(recipe.Id, GangcheoriUnlockRecipeId, StringComparison.Ordinal) ||
                   book != null && book.IsUnlocked(recipe);
        }
    }
}
