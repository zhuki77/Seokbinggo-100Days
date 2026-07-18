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

    // 아래 두 계약은 방향이 반대다 — Development B(설치물/온도 시스템)가 구현하고,
    // Development A의 SealSystem이 소비한다. B파트 시스템이 아직 프로젝트에 없어도
    // SealSystem은 이 계약 없이(둘 다 null) 기존 동작(자연 지형만 인정, 냉기원 게이트 없음)을 그대로 유지한다.

    /// <summary>
    /// 결재 브리프 v2(밀폐도 사태) 화이트리스트 확장 지점. SealSystem은 TileData 그리드(<c>isNaturalTerrain</c>)
    /// 만으로는 차열벽·차열 지붕·단열 문(기획 v15 QA-F 화이트리스트) 같은 B파트 설치물을 알 수 없으므로,
    /// 경계벽으로 판정된 좌표가 "인정된 밀폐 구조물"인지 이 인터페이스로 되묻는다.
    /// Flood Fill 도중 경계 셀마다 호출되므로 구현체는 반드시 O(1)/O(log n) 조회여야 한다(무거운 연산 금지).
    /// </summary>
    public interface ISealBarrierRegistry { bool IsRecognizedBarrier(Vector3Int cell); }

    /// <summary>
    /// "4 시스템"(v17 최종) 온도% 산식의 <c>냉기원 가동</c> 조건을 SealSystem 외부(B파트 온도 시스템)에서
    /// 주입받기 위한 계약. 아이스박스 등 냉기원이 실제로 가동 중인지는 SealSystem이 알 수 없는 정보다.
    /// </summary>
    public interface ICoolingSourceProvider { bool IsColdSourceActive { get; } }

    /// <summary>
    /// 개발 A가 제공하는 중앙 game seconds Tick 계약(A-04). 제작·제련·유틸리티·AI·전투 등 매 프레임
    /// delta game seconds가 필요한 개발 B 소비자는 이 인터페이스만 구현하면 된다.
    /// </summary>
    public interface IGameSecondsTickable
    {
        void Tick(float deltaGameSeconds);
    }

    /// <summary>
    /// <see cref="IGameSecondsTickable"/> 등록/해제 계약. 개발 B는 구체 클래스(CentralTickDriver)
    /// 대신 이 인터페이스에 의존해 소비자를 연결·해제할 수 있다.
    /// </summary>
    public interface IGameSecondsTickDriver
    {
        void Register(IGameSecondsTickable tickable);
        void Unregister(IGameSecondsTickable tickable);
    }
}
