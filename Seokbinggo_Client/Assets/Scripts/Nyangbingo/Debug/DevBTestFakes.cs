using System;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    public sealed class DevBTestTimeSource : MonoBehaviour, ITimeSource
    {
        public int Day => 1;
        public bool IsNight { get; set; } = true;
        public event Action Dawn;
        public void RaiseDawn() => Dawn?.Invoke();
    }
    public sealed class DevBTestSpawnController : MonoBehaviour, IRegularSpawnController
    {
        public bool IsRegularSpawning { get; private set; } = true;
        public void SetRegularSpawning(bool enabled) => IsRegularSpawning = enabled;
    }
}
