using System;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Combat
{
    public sealed class UtilityService
    {
        public event Action<float> FanUsed;
        public event Action<float> AlarmPlaced;
        public event Action<float> FireBufferActivated;

        public bool TryUse(UtilityDefinition utility)
        {
            if (utility == null) return false;
            switch (utility.Kind)
            {
                case UtilityKind.FoldingFan: FanUsed?.Invoke(utility.Value); return true;
                case UtilityKind.BellRope: AlarmPlaced?.Invoke(utility.Value); return true;
                case UtilityKind.FoxRainCharm: FireBufferActivated?.Invoke(utility.Value); return true;
                default: return false;
            }
        }
    }
}
