using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// 아티팩트 효과 SO 베이스. 새 아티팩트 = VerbId 1줄 + 자식 SO 1개 + 채널 등록.
    /// </summary>
    public abstract class ArtifactEffect : ScriptableObject
    {
        public abstract ArtifactVerbId Id { get; }

        /// <summary>효과 적용. 구체 구현은 후속 작업(P5는 스키마·조회만).</summary>
        public abstract void Apply(object target);
    }
}
