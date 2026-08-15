using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// B-UI-v71 B-1: 플레이어 위치 실온(℃). A측 정식 산식 전까지 SealSystem·폭염 단계 기반 임시 역산.
    /// RoomTempService.Resolve(playerCell) 계약 형태를 유지한다.
    /// </summary>
    public sealed class RoomTempService
    {
        private readonly GameDataCatalog catalog;
        private readonly SealSystem sealSystem;
        private readonly DayNightService timeService;
        private readonly MainGameEnvironmentState environmentState;

        public RoomTempService(GameDataCatalog catalog, SealSystem sealSystem, DayNightService timeService,
            MainGameEnvironmentState environmentState = null)
        {
            this.catalog = catalog;
            this.sealSystem = sealSystem;
            this.timeService = timeService;
            this.environmentState = environmentState;
        }

        public int ColdEnterCelsius => ReadThreshold("room_temp_cold_enter", -5);
        public int FrozenEnterCelsius => ReadThreshold("room_temp_frozen_enter", -10);

        public int Resolve(Vector3 worldPosition)
        {
            if (sealSystem == null) return 0;
            if (!sealSystem.IsInsideSealedArea(worldPosition))
                return ResolveAmbientCelsius();
            var capped = Mathf.Clamp(sealSystem.TemperaturePercent, 0f, 100f);
            var insulation = environmentState != null
                ? environmentState.ResolveTemperatureRecoveryMultiplier(worldPosition, sealSystem)
                : 1f;
            var effective = capped * Mathf.Clamp(insulation, .5f, 1.5f);
            return Mathf.RoundToInt(Mathf.Lerp(FrozenEnterCelsius - 5f, 0f, effective / 100f));
        }

        private int ResolveAmbientCelsius()
        {
            var stage = timeService?.CurrentDayCurve?.HeatStage ?? 1;
            stage = PlayerTemperatureState.CalculateEffectiveHeatStage(stage,
                environmentState?.HeatStageReduction ?? 0);
            return Mathf.Clamp(2 * stage - 2, -4, 6);
        }

        private int ReadThreshold(string key, int fallback)
        {
            var definition = catalog?.FindGlobal(key);
            if (definition != null && definition.TryGetInt(out var value)) return value;
            return fallback;
        }
    }
}
