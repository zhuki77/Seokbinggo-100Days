using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>낮/밤 중 현재 어느 쪽인지 — GameEvents 알림과 별개로, 조회 편의를 위한 상태값.</summary>
    public enum DayNightState { Day, Night }

    /// <summary>
    /// 실시간 낮/밤 사이클 매니저. DEV_B_TO_DEV_A_HANDOFF.md §5(시간 계약)와 §13 1단계("시간과 이벤트
    /// 연결")가 개발 A에게 요구하는 실제 TimeManager 구현체다. 다른 프로그래머/기획자는 아래 두 방식으로
    /// 이 위에 시스템을 얹을 수 있다:
    ///
    ///  1) 정적 이벤트 구독 — <see cref="Nyangbingo.Core.GameEvents.OnDayStart"/>/OnNightStart/OnDawnWarning
    ///     (예: ForcedBossEncounterBinding, BaekjungTimeBinding, DawnAutoSave가 이미 이 방식으로 연결됨).
    ///  2) 인스턴스 참조 — <see cref="IGameSecondsSource"/>(YokaiBrain.SetGameSecondsSource),
    ///     <see cref="ISaveableTimeSource"/>(BossManager.ConfigureForRuntime, DawnAutoSave)로 그대로 넘기면 된다.
    ///
    /// 규칙(§5.3 그대로 준수):
    ///  - GameSeconds는 세션 전체에서 단조 증가하는 누적값 — 밤낮이 바뀌어도 리셋되지 않는다.
    ///  - timeScale &lt;= 0(정지)이면 GameSeconds/TimeOfDay 둘 다 전혀 증가하지 않는다.
    ///  - Day 시작(자정→새벽)마다 Dawn(인스턴스 이벤트)과 GameEvents.OnDayStart(전역 알림)를 각각 정확히 한 번만 호출한다.
    ///  - 밤 시작 시 OnNightStart, 새벽 몇 초 전에 OnDawnWarning을 각각 정확히 한 번만 호출한다.
    ///  - timeScale을 아주 높게 올려 여러 날을 한 프레임에 건너뛰어도(빠른 테스트용) 경계마다 순서대로
    ///    이벤트가 정확히 한 번씩 발행된다(큰 deltaTime을 한 번에 처리하지 않고 경계 단위로 나눠 진행).
    /// </summary>
    public sealed class DayNightService : MonoBehaviour, IGameSecondsSource, ISaveableTimeSource
    {
        [Header("Official day-curve.csv catalog")]
        [SerializeField] private GameDataCatalog gameDataCatalog;

        [Header("낮/밤 길이 (기획 정본: 낮 900초 / 밤 540초)")]
        [Min(1f)][SerializeField] private float dayDurationSeconds = 900f;
        [Min(1f)][SerializeField] private float nightDurationSeconds = 540f;

        [Header("테스트용 배속 — 1: 실시간, 0: 완전 정지, 인스펙터에서 즉시 조절 가능")]
        [Min(0f)][SerializeField] private float timeScale = 1f;

        [Header("새벽 경고 — 밤이 끝나기 몇 초 전에 OnDawnWarning을 한 번 울릴지 (기획 정본: 새벽 180초/3분 전)")]
        [Min(0f)][SerializeField] private float dawnWarningLeadSeconds = 180f;

        [Header("시작 상태")]
        [Min(1)][SerializeField] private int startDay = 1;
        [SerializeField] private bool startAtNight;

        [Header("HUD용 — 생존 목표 D-day 표시 기준 (기획 정본: '백일폭염' 세계관, 100일 — 5 UI UX v15 QA-E / 1 개요 기준으로 확정)")]
        [Min(1)][SerializeField] private int survivalDayLimit = 100;

        private float gameSeconds;
        private float timeOfDayGameSeconds;
        private int day;
        private bool isNight;
        private bool dawnWarningFired;
        private GlobalSettings globalSettings;

        /// <summary>새벽(밤→낮) 전환이 확정된 순간 정확히 한 번 호출된다. ITimeSource 계약.</summary>
        public event Action Dawn;

        public float GameSeconds => gameSeconds;
        public int Day => day;
        public bool IsNight => isNight;
        public float TimeOfDayGameSeconds => timeOfDayGameSeconds;
        public DayNightState State => isNight ? DayNightState.Night : DayNightState.Day;
        public float DayDurationSeconds => dayDurationSeconds;
        public float NightDurationSeconds => nightDurationSeconds;
        public float CycleLengthSeconds => dayDurationSeconds + nightDurationSeconds;
        public int SurvivalDayLimit => survivalDayLimit;
        public int MvpContentDayLimit { get; private set; } = 30;
        public DayCurveDefinition CurrentDayCurve => gameDataCatalog != null
            ? gameDataCatalog.FindDayCurve(day) : null;
        public GlobalSettings OfficialGlobals => globalSettings;

        /// <summary>D-100 HUD 표시용 — 생존 목표일까지 남은 날짜(도달/초과 시 0).</summary>
        public int DaysRemaining => Mathf.Max(0, survivalDayLimit - day);

        /// <summary>다음 경계(밤 시작 또는 새벽)까지 남은 게임 시간(초).</summary>
        public float SecondsUntilNextTransition =>
            isNight ? CycleLengthSeconds - timeOfDayGameSeconds : dayDurationSeconds - timeOfDayGameSeconds;

        /// <summary>테스트/디버그 하네스가 배속을 즉시 조절할 수 있게 공개 접근자를 둔다. 음수는 0으로 클램프.</summary>
        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        public bool ConfigureDayCurve(GameDataCatalog catalog) => ConfigureOfficialData(catalog);

        public bool ConfigureOfficialData(GameDataCatalog catalog)
        {
            gameDataCatalog = catalog;
            if (!ApplyOfficialGlobals() || CurrentDayCurve == null) return false;
            if (gameSeconds <= 0f && day <= 1)
            {
                isNight = startAtNight;
                timeOfDayGameSeconds = isNight ? dayDurationSeconds : 0f;
                dawnWarningFired = false;
            }
            return true;
        }

        private void Awake()
        {
            ApplyOfficialGlobals();
            day = Mathf.Max(1, startDay);
            isNight = startAtNight;
            // timeOfDayGameSeconds는 하루 전체(낮+밤) 누적 축이라 밤은 dayDurationSeconds 지점부터 시작한다.
            // 여기서 무조건 0으로 시작하면 startAtNight == true일 때 nightElapsed가 -dayDurationSeconds가 되어
            // 첫 밤이 (dayDurationSeconds + nightDurationSeconds)만큼 길어지는 버그가 있었다(A-02).
            timeOfDayGameSeconds = isNight ? dayDurationSeconds : 0f;
            dawnWarningFired = false;
        }

        private bool ApplyOfficialGlobals()
        {
            globalSettings = gameDataCatalog != null ? new GlobalSettings(gameDataCatalog.Globals) : null;
            if (globalSettings == null || !globalSettings.IsValid ||
                !globalSettings.TryGetFloat(GlobalKeys.DayLengthSeconds, out var officialDayLength) ||
                !globalSettings.TryGetFloat(GlobalKeys.NightLengthSeconds, out var officialNightLength) ||
                !globalSettings.TryGetInt(GlobalKeys.MvpDays, out var officialMvpDays) ||
                !globalSettings.TryGetInt(GlobalKeys.TotalDays, out var officialTotalDays) ||
                !globalSettings.TryGetBool(GlobalKeys.StartAtNight, out var officialStartAtNight) ||
                officialDayLength <= 0f || officialNightLength <= 0f || officialMvpDays <= 0 ||
                officialTotalDays < officialMvpDays)
                return false;

            dayDurationSeconds = officialDayLength;
            nightDurationSeconds = officialNightLength;
            MvpContentDayLimit = officialMvpDays;
            survivalDayLimit = officialTotalDays;
            startAtNight = officialStartAtNight;
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime * timeScale);
        }

        /// <summary>
        /// rawDeltaSeconds(실시간 초 × timeScale, 혹은 테스트/디버그 도구가 직접 넘기는 임의의 게임 초)만큼
        /// 시간을 흘린다. Update()가 매 프레임 호출하지만, "밤 건너뛰기" 같은 디버그 명령이나 단위 테스트가
        /// 직접 호출해도 안전하도록 public으로 노출한다. 경계(밤 시작/새벽 경고/새벽)를 하나씩 순서대로
        /// 통과시키므로, 아주 큰 delta(배속 테스트로 여러 날을 한 번에 건너뛰는 경우)에도 각 이벤트가
        /// 정확히 한 번씩, 올바른 순서로 발행된다.
        /// </summary>
        public void Tick(float rawDeltaSeconds)
        {
            // §5.3: 음수/NaN/Infinity 거부 — timeScale이 0(정지)이면 rawDeltaSeconds도 항상 0이 되어 여기서 걸러진다.
            if (rawDeltaSeconds <= 0f || float.IsNaN(rawDeltaSeconds) || float.IsInfinity(rawDeltaSeconds)) return;

            gameSeconds += rawDeltaSeconds;
            var remaining = rawDeltaSeconds;

            while (remaining > 0f)
            {
                if (!isNight)
                {
                    var toNight = dayDurationSeconds - timeOfDayGameSeconds;
                    if (remaining < toNight)
                    {
                        timeOfDayGameSeconds += remaining;
                        remaining = 0f;
                        continue;
                    }

                    timeOfDayGameSeconds = dayDurationSeconds;
                    remaining -= toNight;
                    isNight = true;
                    dawnWarningFired = false;
                    GameEvents.RaiseNightStart();
                    continue;
                }

                var nightElapsed = timeOfDayGameSeconds - dayDurationSeconds;
                var warnAt = Mathf.Clamp(nightDurationSeconds - Mathf.Max(0f, dawnWarningLeadSeconds), 0f, nightDurationSeconds);

                if (!dawnWarningFired)
                {
                    var toWarn = warnAt - nightElapsed;
                    if (toWarn > 0f)
                    {
                        if (remaining < toWarn)
                        {
                            timeOfDayGameSeconds += remaining;
                            remaining = 0f;
                            continue;
                        }
                        timeOfDayGameSeconds = dayDurationSeconds + warnAt;
                        remaining -= toWarn;
                    }
                    dawnWarningFired = true;
                    GameEvents.RaiseDawnWarning();
                    continue;
                }

                var toDawn = nightDurationSeconds - nightElapsed;
                if (remaining < toDawn)
                {
                    timeOfDayGameSeconds += remaining;
                    remaining = 0f;
                    continue;
                }

                remaining -= toDawn;
                timeOfDayGameSeconds = 0f;
                isNight = false;
                day++;
                Dawn?.Invoke();
                GameEvents.RaiseDayStart();
            }
        }

        /// <summary>
        /// 세이브 로드 전용 원자적 복원. §11.6: "이벤트를 임의로 중복 발행하지 말고 상태를 원자적으로 복원한다" —
        /// 그래서 GameEvents/Dawn을 전혀 호출하지 않고 내부 상태만 맞춘다. 복원된 시점이 새벽 경고 지점을
        /// 이미 지난 상태라면 dawnWarningFired도 true로 맞춰, 로드 직후 Tick()이 중복으로 경고를
        /// 다시 울리지 않게 한다.
        /// </summary>
        public bool RestoreTimeState(int restoredDay, float restoredTimeOfDayGameSeconds, bool restoredIsNight)
        {
            if (restoredDay < 1 || restoredTimeOfDayGameSeconds < 0f ||
                float.IsNaN(restoredTimeOfDayGameSeconds) || float.IsInfinity(restoredTimeOfDayGameSeconds) ||
                restoredTimeOfDayGameSeconds >= CycleLengthSeconds)
                return false;

            var expectedNight = restoredTimeOfDayGameSeconds >= dayDurationSeconds;
            if (expectedNight != restoredIsNight) return false;

            day = restoredDay;
            timeOfDayGameSeconds = restoredTimeOfDayGameSeconds;
            isNight = restoredIsNight;

            var warnAt = Mathf.Clamp(nightDurationSeconds - Mathf.Max(0f, dawnWarningLeadSeconds), 0f, nightDurationSeconds);
            dawnWarningFired = isNight && (timeOfDayGameSeconds - dayDurationSeconds) >= warnAt;
            return true;
        }
    }
}
