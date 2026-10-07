using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Goal")]
    public sealed class GoalDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private int order;
        [SerializeField] private string phase;
        [SerializeField] private string track;
        [SerializeField] private string displayName;
        [SerializeField] private string iconRef;
        [SerializeField] private string completeState;
        [SerializeField] private string completeParam;
        [SerializeField] private string guideTarget;
        [SerializeField] private string hintMessageId;
        [SerializeField] private string mvpScope;
        [SerializeField] private string note;

        public string Id => id;
        public int Order => order;
        public string Phase => phase;
        public string Track => track;
        public string DisplayName => displayName;
        public string IconRef => iconRef;
        public string CompleteState => completeState;
        public string CompleteParam => completeParam;
        public string GuideTarget => guideTarget;
        public string HintMessageId => hintMessageId;
        public string MvpScope => mvpScope;
        public string Note => note;
    }
}
