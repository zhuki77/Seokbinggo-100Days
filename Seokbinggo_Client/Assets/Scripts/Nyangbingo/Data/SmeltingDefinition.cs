using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Smelting Recipe")]
    public sealed class SmeltingDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private SmeltingStationKind stationKind;
        [SerializeField] private ItemAmount input;
        [SerializeField] private ItemAmount fuel;
        [SerializeField] private ItemAmount output;
        [Min(1f)][SerializeField] private float durationSeconds = 20f;
        public string Id => id;
        public SmeltingStationKind StationKind => stationKind;
        public ItemAmount Input => input;
        public ItemAmount Fuel => fuel;
        public ItemAmount Output => output;
        public float DurationSeconds => durationSeconds;
        public static SmeltingDefinition CreateRuntime(string recipeId, ItemAmount source, ItemAmount fuelItem,
            ItemAmount result, float seconds, SmeltingStationKind station = SmeltingStationKind.Furnace)
        {
            var definition = CreateInstance<SmeltingDefinition>();
            definition.id = recipeId; definition.stationKind = station; definition.input = source;
            definition.fuel = fuelItem; definition.output = result;
            definition.durationSeconds = seconds; return definition;
        }
    }
}
