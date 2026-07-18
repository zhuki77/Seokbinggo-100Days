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
        [Min(1)][SerializeField] private int batchCapacity = 1;
        [TextArea][SerializeField] private string note;

        public string Id => id;
        public SmeltingStationKind StationKind => stationKind;
        public ItemAmount Input => input;
        public ItemAmount Fuel => fuel;
        public ItemAmount Output => output;
        public float DurationSeconds => durationSeconds;
        public int BatchCapacity => batchCapacity;
        public string Note => note;

        public static SmeltingDefinition CreateRuntime(string recipeId, ItemAmount source, ItemAmount fuelItem,
            ItemAmount result, float seconds, SmeltingStationKind station = SmeltingStationKind.Furnace,
            int capacity = 1, string description = null)
        {
            var definition = CreateInstance<SmeltingDefinition>();
            definition.id = recipeId; definition.stationKind = station; definition.input = source;
            definition.fuel = fuelItem; definition.output = result;
            definition.durationSeconds = seconds; definition.batchCapacity = capacity;
            definition.note = description ?? string.Empty; return definition;
        }
    }
}
