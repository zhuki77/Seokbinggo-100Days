using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nyangbingo.World
{
    /// <summary>
    /// 개발 A가 제공하는 공통 game seconds Tick 공급 구조(개발 A 보완 작업 명세서 A-04).
    /// 게임 진행 시간의 단일 기준은 <see cref="DayNightService"/>(IGameSecondsSource)이며,
    /// Unity의 Time.timeScale이 아니라 GameSeconds의 프레임 간 증가량(delta)만을 등록된
    /// 소비자에게 릴레이하되, 새 소비자에는 등록 이후의 증가량만 전달한다.
    ///
    /// - 배속/일시정지는 DayNightService.TimeScale이 결정한다. GameSeconds가 그 배속 그대로
    ///   증가하므로, 이 드라이버는 매 프레임 그 증가량만 relay할 뿐 자체 배속 로직을 갖지 않는다.
    ///   TimeScale이 0(정지)이면 GameSeconds가 전혀 늘지 않아 delta가 0이 되고, 아무도 Tick되지 않는다.
    /// - 인스턴스별 마지막 시각을 보관하여 중복 등록과 생성 전 시간의 소급 전달을 방지한다.
    /// - Destroy된 MonoBehaviour 소비자는 Unity의 "가짜 null(fake null)" 비교로 감지해 자동으로 제거한다
    ///   (인터페이스 참조로는 일반적인 `== null` 비교가 통하지 않으므로 <see cref="IsAlive"/>에서 UnityEngine.Object로
    ///   캐스팅한 뒤 오버로드된 연산자로 검사한다).
    /// - 씬 재진입/재시작 시 이 컴포넌트 자체가 새로 생성되므로 소비자 목록도 함께 초기화되어
    ///   중복 구독이 남지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)] // DayNightService.Update()가 먼저 GameSeconds를 갱신한 뒤 관찰하도록 보장(LateUpdate라 필수는 아니지만 안전장치로 명시).
    public sealed class CentralTickDriver : MonoBehaviour, IGameSecondsTickDriver
    {
        [Tooltip("비워두면 같은 GameObject → 씬 전체에서 DayNightService를 자동으로 찾는다.")]
        [SerializeField] private DayNightService dayNightService;

        private IGameSecondsSource source;
        private float lastGameSeconds;

        private readonly Dictionary<IGameSecondsTickable, float> tickables = new Dictionary<IGameSecondsTickable, float>();
        private readonly List<IGameSecondsTickable> tickBuffer = new List<IGameSecondsTickable>();
        private readonly List<IGameSecondsTickable> deadBuffer = new List<IGameSecondsTickable>();

        /// <summary>지금까지 이 드라이버가 소비자들에게 전달한 delta game seconds의 총합(테스트/디버그 검증용).</summary>
        public float TotalRelayedGameSeconds { get; private set; }

        public int RegisteredCount => tickables.Count;

        /// <summary>런타임에 시간 소스를 명시적으로 지정한다(테스트 하네스처럼 DayNightService 참조를 코드로 주입하는 경우).</summary>
        public void Configure(IGameSecondsSource timeSource)
        {
            source = timeSource;
            lastGameSeconds = source?.GameSeconds ?? 0f;
            ResetConsumerClocks(lastGameSeconds);
        }

        private void Awake()
        {
            if (source == null)
            {
                if (dayNightService == null)
                    dayNightService = GetComponent<DayNightService>() ?? FindAnyObjectByType<DayNightService>();
                source = dayNightService;
            }
            lastGameSeconds = source?.GameSeconds ?? 0f;
            ResetConsumerClocks(lastGameSeconds);
        }

        /// <summary>소비자를 등록한다. 이미 등록돼 있으면(중복 호출) 아무 일도 일어나지 않는다.</summary>
        public void Register(IGameSecondsTickable tickable)
        {
            if (tickable == null || tickables.ContainsKey(tickable)) return;
            tickables.Add(tickable, source?.GameSeconds ?? lastGameSeconds);
        }

        /// <summary>소비자를 해제한다. 등록되어 있지 않았다면 아무 일도 일어나지 않는다.</summary>
        public void Unregister(IGameSecondsTickable tickable)
        {
            if (tickable == null) return;
            tickables.Remove(tickable);
        }

        private void LateUpdate()
        {
            if (source == null) return;

            var current = source.GameSeconds;
            var delta = current - lastGameSeconds;
            lastGameSeconds = current;

            // 정지(TimeScale<=0)이거나 부동소수 오차로 delta가 0/음수/NaN이면 아무도 Tick하지 않는다.
            if (float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (delta < 0f) ResetConsumerClocks(current);
            if (delta <= 0f) return;

            tickBuffer.Clear();
            deadBuffer.Clear();
            foreach (var tickable in tickables.Keys)
            {
                if (IsAlive(tickable)) tickBuffer.Add(tickable);
                else deadBuffer.Add(tickable);
            }

            foreach (var dead in deadBuffer) tickables.Remove(dead);
            if (tickBuffer.Count == 0) return;

            TotalRelayedGameSeconds += delta;

            // 콜백 도중 Register/Unregister가 재진입해도 이번 프레임 순회 목록(tickBuffer)은 바뀌지 않는다 —
            // 스냅샷을 순회하므로 "같은 프레임에 두 번 Tick" 또는 반복 중 컬렉션 변경 예외가 발생하지 않는다.
            foreach (var tickable in tickBuffer)
            {
                if (!IsAlive(tickable) || !tickables.TryGetValue(tickable, out var lastConsumerSeconds)) continue;
                // NightStart가 낮 건너뛰기 도중 AI를 등록해도 생성 전 낮 시간은 전달하지 않는다.
                var consumerDelta = current - lastConsumerSeconds;
                tickables[tickable] = current;
                if (consumerDelta > 0f && !float.IsNaN(consumerDelta) && !float.IsInfinity(consumerDelta))
                    tickable.Tick(consumerDelta);
            }
        }

        private void ResetConsumerClocks(float current)
        {
            foreach (var tickable in new List<IGameSecondsTickable>(tickables.Keys))
                tickables[tickable] = current;
        }

        private static bool IsAlive(IGameSecondsTickable tickable)
        {
            if (tickable == null) return false;
            // MonoBehaviour/ScriptableObject 등 UnityEngine.Object 구현체는 Destroy 후에도 C# 참조 자체는
            // 살아있는("가짜 null") 경우가 있어, 일반적인 참조 비교로는 파괴 여부를 알 수 없다. UnityEngine.Object로
            // 캐스팅해서 오버로드된 == 연산자로 검사해야 실제 파괴 여부가 정확히 나온다.
            if (tickable is Object unityObject) return unityObject != null;
            return true;
        }
    }
}
