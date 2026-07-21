using System;
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
    ///
    /// A-12(v26 냉기원 상한 연동 계약): 활성 냉기원 중 최고 온도 상한(%)을 제공해야 한다 — 확정값은
    /// 물단지 25 / 얼음 항아리 50 / 얼음 저장고 100 / 빙정 냉각로 100, 여러 개가 가동 중이면 최고값,
    /// 냉기원이 하나도 없으면 0이다. <see cref="CoolingCapPercent"/>는 C# 8 기본 인터페이스 구현으로
    /// 0을 반환하므로, 이 구현체를 이미 이 계약대로 채워 둔 <c>MainGameEnvironmentState</c>(개발 B) 등
    /// 기존 구현체는 이 멤버를 추가해도 컴파일이 깨지지 않는다. SealSystem은 <see cref="IsColdSourceActive"/>
    /// 단일 boolean 대신 이 값을 직접 온도 산식(min(cap, 밀폐율 기반 %))에 사용한다.
    /// </summary>
    public interface ICoolingSourceProvider
    {
        bool IsColdSourceActive { get; }

        /// <summary>0~100 스케일. Provider가 이 멤버를 구현하지 않았다면(레거시) 0으로 간주 — "연결 안 됨=100%"로
        /// 잘못 해석되는 사고를 막기 위해 기본값을 100이 아니라 0으로 둔다(A-12 요구사항 그대로).</summary>
        float CoolingCapPercent => 0f;
    }

    /// <summary>
    /// A-16/A-19: 밀폐된 석빙고 코어 구역의 배경 도포율. SealPercent/TemperaturePercent와 별개이며,
    /// 개발 B는 물단지·얼음 항아리 지속시간 +25%에만 사용한다(얼음 저장고·빙정 냉각로·온도 상한은 변경 금지).
    /// </summary>
    public interface IWallpaperCoverageSource
    {
        float GetCoveragePercent(Vector3Int sealCoreCell);
        bool IsCoverageComplete(Vector3Int sealCoreCell);
        event Action WallpaperCoverageChanged;
    }

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

    /// <summary>
    /// A-22: 새 게임·데모 세이브·손상 위치 교정용 공용 안전 지표면 스폰 계약.
    /// 정상 저장 플레이어 위치는 호출측이 검증 실패일 때만 이 API로 대체한다.
    /// </summary>
    public interface IWorldSafeSpawnResolver
    {
        /// <summary>현재 전경 기준으로 액터가 서 있을 수 있는지(발밑 고체·몸/머리 공기·낙하 구멍 아님).</summary>
        bool IsSafeStandingPosition(Vector2 worldPosition, float actorHalfExtent);

        /// <summary>
        /// preferredCellX 근처에서 결정론적으로 안전 지표면 스폰을 찾는다.
        /// 동일 seed·월드·preferredCellX·actorHalfExtent → 동일 결과.
        /// </summary>
        bool TryResolveSafeSurfaceSpawn(int preferredCellX, float actorHalfExtent, out Vector2 worldPosition);
    }

    /// <summary>A-25: 한 셀의 배경/벽지 상태(전경과 독립).</summary>
    public readonly struct BackgroundCellState
    {
        public readonly string CurrentBackgroundId;
        public readonly string NaturalBackgroundId;
        public readonly bool HasWallpaper;
        public readonly bool HasNaturalBackground;

        public BackgroundCellState(string currentBackgroundId, string naturalBackgroundId,
            bool hasWallpaper, bool hasNaturalBackground)
        {
            CurrentBackgroundId = currentBackgroundId ?? string.Empty;
            NaturalBackgroundId = naturalBackgroundId ?? string.Empty;
            HasWallpaper = hasWallpaper;
            HasNaturalBackground = hasNaturalBackground;
        }
    }

    /// <summary>
    /// A-23/A-25: 벽지·배경 전용 배치 계약. Collider·밀폐율에 영향 없음.
    /// 인벤토리 소비는 TileService 오버로드(consumeFrom)로 처리하며, 성공 시 1개·실패 시 0개.
    /// </summary>
    public interface IBackgroundPlacementService
    {
        bool CanPlaceWallpaper(Vector3Int cell);
        bool TryPlaceWallpaper(Vector3Int cell);
        bool TryRemoveWallpaper(Vector3Int cell);
        BackgroundCellState GetBackgroundState(Vector3Int cell);
    }

    /// <summary>A-26: 반경·밀폐 창 오버레이 형상.</summary>
    public enum WorldRangeShape
    {
        Circle = 0,
        AxisAlignedRect = 1
    }

    /// <summary>
    /// A-26: 표시 전용 범위. Circle: Radius=타일 반경.
    /// Rect: Radius=halfExtentX, SecondaryRadius=halfExtentY(0이면 Radius와 동일 — SealSystem rx/ry).
    /// </summary>
    public readonly struct WorldRangeOverlay
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float SecondaryRadius;
        public readonly WorldRangeShape Shape;

        public WorldRangeOverlay(Vector2 center, float radius, WorldRangeShape shape)
            : this(center, radius, 0f, shape) { }

        public WorldRangeOverlay(Vector2 center, float radius, float secondaryRadius, WorldRangeShape shape)
        {
            Center = center;
            Radius = radius;
            SecondaryRadius = secondaryRadius;
            Shape = shape;
        }
    }

    /// <summary>
    /// A-26: 입력(R/팔레트)을 읽지 않는 표시 전용 오버레이. Collider·밀폐·저장에 부작용 없음.
    /// </summary>
    public interface IWorldRangeOverlayRenderer
    {
        void SetVisible(bool visible);
        void Render(IReadOnlyList<WorldRangeOverlay> overlays);
        void Clear();
    }
}
