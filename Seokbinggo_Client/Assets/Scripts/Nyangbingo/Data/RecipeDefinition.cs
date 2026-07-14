using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [Serializable]
    public struct ItemAmount { public ItemDefinition item; [Min(1)] public int amount; }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private CraftingStation station;
        [SerializeField] private ItemAmount[] ingredients;
        [SerializeField] private ItemAmount output;
        public string Id => id;
        public CraftingStation Station => station;
        public ItemAmount[] Ingredients => ingredients;
        public ItemAmount Output => output;

        public static RecipeDefinition CreateRuntime(string recipeId, CraftingStation requiredStation, ItemAmount[] required, ItemAmount result)
        {
            var recipe = CreateInstance<RecipeDefinition>();
            recipe.id = recipeId; recipe.station = requiredStation; recipe.ingredients = required; recipe.output = result;
            return recipe;
        }
    }
}
