using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [Serializable]
    public struct ItemAmount { public ItemDefinition item; [Min(1)] public int amount; }

    public enum RecipeType
    {
        Unspecified,
        Armor,
        Claw,
        ColdSource,
        Cooling,
        Deco,
        Device,
        Evo,
        Insulation,
        Module,
        Placeable,
        Station,
        Summon,
        Turret,
        Util,
        Wall,
        Weapon
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private CraftingStation station;
        [SerializeField] private ItemAmount[] ingredients;
        [SerializeField] private ItemAmount output;
        [Min(0f)][SerializeField] private float durationSeconds;
        [SerializeField] private RecipeType type;
        [SerializeField] private ItemMvpScope mvpScope;
        [TextArea][SerializeField] private string note;

        public string Id => id;
        public CraftingStation Station => station;
        public ItemAmount[] Ingredients => ingredients;
        public ItemAmount Output => output;
        public float DurationSeconds => durationSeconds;
        public RecipeType Type => type;
        public ItemMvpScope MvpScope => mvpScope;
        public string Note => note;

        public static RecipeDefinition CreateRuntime(string recipeId, CraftingStation requiredStation,
            ItemAmount[] required, ItemAmount result, float seconds = 0f,
            RecipeType recipeType = RecipeType.Unspecified,
            ItemMvpScope scope = ItemMvpScope.Unspecified, string description = null)
        {
            var recipe = CreateInstance<RecipeDefinition>();
            recipe.id = recipeId;
            recipe.station = requiredStation;
            recipe.ingredients = required;
            recipe.output = result;
            recipe.durationSeconds = seconds;
            recipe.type = recipeType;
            recipe.mvpScope = scope;
            recipe.note = description ?? string.Empty;
            return recipe;
        }
    }
}
