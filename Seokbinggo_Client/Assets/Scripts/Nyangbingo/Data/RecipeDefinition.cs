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
        [Min(0f)][SerializeField] private float durationSeconds;
        public string Id => id;
        public CraftingStation Station => station;
        public ItemAmount[] Ingredients => ingredients;
        public ItemAmount Output => output;
        public float DurationSeconds => durationSeconds;

        public static RecipeDefinition CreateRuntime(string recipeId, CraftingStation requiredStation, ItemAmount[] required, ItemAmount result, float seconds = 0f)
        {
            var recipe = CreateInstance<RecipeDefinition>();
            recipe.id = recipeId; recipe.station = requiredStation; recipe.ingredients = required; recipe.output = result; recipe.durationSeconds = seconds;
            return recipe;
        }
    }
}
