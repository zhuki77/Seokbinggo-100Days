using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    /// <summary>VerbId → ArtifactEffect O(1) 조회 채널.</summary>
    [CreateAssetMenu(menuName = "Nyangbingo/Artifact Effect Channel")]
    public sealed class ArtifactEffectChannel : ScriptableObject
    {
        [SerializeField] private List<ArtifactEffect> effects = new List<ArtifactEffect>();

        private Dictionary<ArtifactVerbId, ArtifactEffect> map;

        private void OnEnable() => RebuildMap();

        public void RebuildMap()
        {
            map = new Dictionary<ArtifactVerbId, ArtifactEffect>();
            if (effects == null) return;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    Debug.LogError("[Nyangbingo] ArtifactEffectChannel: 빈 효과가 있습니다.");
                    continue;
                }

                if (effect.Id == ArtifactVerbId.None)
                {
                    Debug.LogError("[Nyangbingo] ArtifactEffectChannel: VerbId.None은 등록할 수 없습니다.");
                    continue;
                }

                if (map.ContainsKey(effect.Id))
                {
                    Debug.LogError($"[Nyangbingo] ArtifactEffectChannel: VerbId 중복 {effect.Id}");
                    continue;
                }

                map[effect.Id] = effect;
            }
        }

        public bool TryExecute(ArtifactVerbId id, object target)
        {
            if (id == ArtifactVerbId.None) return false;
            if (map == null) RebuildMap();
            if (map == null || !map.TryGetValue(id, out var effect) || effect == null)
            {
                Debug.LogWarning($"[Nyangbingo] ArtifactEffectChannel: 미등록 VerbId {id}");
                return false;
            }

            effect.Apply(target);
            return true;
        }
    }
}
