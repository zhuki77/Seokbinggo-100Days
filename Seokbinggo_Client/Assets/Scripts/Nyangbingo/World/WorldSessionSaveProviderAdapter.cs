using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// DEV_B_TO_DEV_A_HANDOFF.md §11.4 계약(<see cref="ISaveSnapshotProvider"/>)과 B파트 새벽 자동 저장
    /// 컴포넌트(<see cref="DawnAutoSave"/>) 사이를 잇는 얇은 배선용 어댑터.
    ///
    /// <see cref="WorldSessionController"/>는 MonoBehaviour가 아니고(순수 C# <c>IDisposable</c>),
    /// <c>bool CaptureSnapshot(SaveGame save)</c>라는 다른 시그니처를 가진 일반 클래스라 DawnAutoSave의
    /// <c>ISaveSnapshotProvider snapshotProviderComponent</c> 슬롯에 직접 꽂을 수 없다. 이 컴포넌트는
    /// 그 간극만 메우는 얇은 중계자다 — 검증/캡처 로직은 전부 WorldSessionController·WorldSaveAdapter에
    /// 그대로 있고, 여기서는 새 <see cref="SaveGame"/>을 만들어 캡처를 요청하고 결과만 돌려준다.
    ///
    /// 씬 배선 순서:
    ///  1) 빈 GameObject(예: "SaveWiring")에 이 컴포넌트와 <see cref="DawnAutoSave"/>를 붙인다.
    ///  2) 세션 루트가 <see cref="WorldSessionController"/>를 생성한 뒤 <see cref="Configure"/>를 호출한다.
    ///  3) DawnAutoSave 인스펙터의 timeSourceComponent에 <see cref="DayNightService"/>,
    ///     snapshotProviderComponent에 이 컴포넌트, saveManager에 <c>SaveManager</c>를 각각 연결한다.
    ///
    /// ⚠️ 현재 캡처 범위는 월드(타일 diff·상자)뿐이다. 인벤토리·장비·제작·보스 등 세션 전체 상태를 아우르는
    /// 구성 루트(DEV_B_TO_DEV_A_HANDOFF.md §13 5단계 "전체 세이브 조립")가 아직 없으므로, 그 루트가
    /// 만들어지면 CaptureSnapshot 내부를 "전체 스냅샷 조립" 호출로 교체해야 한다.
    /// </summary>
    public sealed class WorldSessionSaveProviderAdapter : MonoBehaviour, ISaveSnapshotProvider
    {
        private WorldSessionController worldSession;

        /// <summary>
        /// 세션 루트가 WorldSessionController를 생성한 뒤 정확히 한 번 호출해 연결한다. 로드로 내부 상태가
        /// 바뀌어도(<see cref="WorldSessionController.LoadSnapshot"/>) 같은 컨트롤러 인스턴스를 재사용하므로
        /// 다시 호출할 필요는 없다. 새 컨트롤러 인스턴스를 통째로 교체하는 경우에만 재호출한다.
        /// </summary>
        public void Configure(WorldSessionController controller) => worldSession = controller;

        /// <summary>
        /// DawnAutoSave가 <c>ITimeSource.Dawn</c> 시점에 호출하는 지점(§11.4). 월드 세션이 아직 준비되지
        /// 않았으면(연결 누락, 세션 시작 전) 저장을 강행하지 않도록 null을 반환한다.
        /// </summary>
        public SaveGame CaptureSnapshot()
        {
            if (worldSession == null || !worldSession.HasWorld)
            {
                Debug.LogWarning("[Nyangbingo] WorldSessionSaveProviderAdapter: 월드 세션이 준비되지 않아 새벽 자동 저장을 건너뜁니다.");
                return null;
            }

            var save = new SaveGame();
            return worldSession.CaptureSnapshot(save) ? save : null;
        }
    }
}
