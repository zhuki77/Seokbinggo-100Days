using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Core
{
    // Development A owns deterministic placement; Development B owns rewards and opened-state persistence.
    public interface IChestSource
    {
        IReadOnlyList<string> ChestIds { get; }
        Vector2 GetChestPosition(string chestId);
    }
}
