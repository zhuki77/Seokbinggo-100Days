using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Guide Message")]
    public sealed class GuideMessageDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private int tier;
        [SerializeField] private string triggerState;
        [SerializeField] private string text;
        [SerializeField] private string variables;
        [SerializeField] private string clearWhen;
        [SerializeField] private string repeat;
        [SerializeField] private string source;
        [SerializeField] private string note;

        public string Id => id;
        public int Tier => tier;
        // Owner's 2026-10-14 ordering. Tier remains the UI severity group.
        public int Priority => id switch
        {
            "hypo_danger" => 1,
            "burn_danger" => 2,
            "boss_active" => 3,
            "boss_tonight" => 4,
            "invasion_active" => 5,
            "invasion_tonight" => 6,
            "baekjung_tonight" => 7,
            "cold_bed" => 8,
            "door_open" => 9,
            "placed_block_not_seal" => 10,
            "core_unsealed" => 11,
            "seal_success" => 12,
            "heat_cleared" => 13,
            "storage_ok" => 14,
            "reward_crafting" => 15,
            "dawn_kept" => 16,
            "dawn_lost" => 17,
            "base_damaged" => 18,
            "seal_boundary_dig" => 19,
            "storage_below" => 20,
            _ => 100 + tier
        };
        public string TriggerState => triggerState;
        public string Text => text;
        public string Variables => variables;
        public string ClearWhen => clearWhen;
        public string Repeat => repeat;
        public string Source => source;
        public string Note => note;
    }
}
