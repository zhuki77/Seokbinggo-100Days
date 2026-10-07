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
        public string TriggerState => triggerState;
        public string Text => text;
        public string Variables => variables;
        public string ClearWhen => clearWhen;
        public string Repeat => repeat;
        public string Source => source;
        public string Note => note;
    }
}
