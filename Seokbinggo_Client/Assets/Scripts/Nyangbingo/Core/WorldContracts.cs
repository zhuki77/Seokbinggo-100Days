using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Core
{
    // Development A implements these contracts; Development B only consumes them.
    public interface IGameSecondsSource { float GameSeconds { get; } }
    public interface ITimeSource { int Day { get; } bool IsNight { get; } event System.Action Dawn; }
    public interface ISaveableTimeSource : ITimeSource
    {
        float TimeOfDayGameSeconds { get; }
        bool RestoreTimeState(int day, float timeOfDayGameSeconds, bool isNight);
    }
    public interface ISealSource { float SealPercent { get; } bool IsInsideSealedArea(Vector2 position); }
    public interface ITileDiffSource { int Seed { get; } IReadOnlyList<string> ExportTileDiff(); }
}
