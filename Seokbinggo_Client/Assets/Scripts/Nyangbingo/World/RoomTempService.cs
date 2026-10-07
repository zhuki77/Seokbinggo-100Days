using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v72 A-1: 플레이어 위치의 절대 실온(℃).
    /// 자연 실온과 냉각 범위를 덮는 코어의 냉각량을 합산한다. 밀폐 코어는 같은 실내에만 적용한다.
    /// </summary>
    public sealed class RoomTempService
    {
        private readonly GameDataCatalog catalog;
        private readonly SealSystem sealSystem;
        private readonly HeatStageService heatStage;
        private readonly MainGameEnvironmentState environmentState;
        private readonly WorldSessionController worldSession;
        private readonly InvasionService invasion;
        private readonly System.Collections.Generic.List<Vector3Int> iceCoreCells =
            new System.Collections.Generic.List<Vector3Int>();
        private readonly float warmEndStageOne;
        private readonly System.Collections.Generic.List<Vector3Int> coldDeviceCells =
            new System.Collections.Generic.List<Vector3Int>();
        private readonly bool coldDeviceCoreEquivalent;
        private readonly float coldEndStageOne;
        private readonly float bandShiftPerStage;
        private readonly float frozenDepthMinimum;
        private readonly int undergroundDepthTiles;
        private readonly int coreRangeWidth;
        private readonly int coreRangeHeight;
        private readonly int coreDeltaPlain;
        private readonly int coreDeltaInsulated;

        public RoomTempService(GameDataCatalog catalog, SealSystem sealSystem, HeatStageService stages,
            MainGameEnvironmentState environmentState = null, WorldSessionController worldSession = null,
            InvasionService invasionService = null)
        {
            this.catalog = catalog;
            this.sealSystem = sealSystem;
            heatStage = stages;
            this.environmentState = environmentState;
            this.worldSession = worldSession;
            invasion = invasionService;
            warmEndStageOne = ReadFloat(GlobalKeys.DepthNormWarmEndStageOne, .333f);
            coldEndStageOne = ReadFloat(GlobalKeys.DepthNormColdEndStageOne, .667f);
            bandShiftPerStage = ReadFloat(GlobalKeys.DepthNormShiftPerStage, .074f);
            frozenDepthMinimum = ReadFloat(GlobalKeys.DepthNormFrozenMinimum, .185f);
            undergroundDepthTiles = Mathf.Max(1, ReadThreshold(GlobalKeys.LayerT3Depth,
                WorldGenerationConfig.UndergroundDepthMinTiles));
            coreRangeWidth = Mathf.Max(1, ReadThreshold(GlobalKeys.CoreRangeWidth, 8));
            coreRangeHeight = Mathf.Max(1, ReadThreshold(GlobalKeys.CoreRangeHeight, 10));
            coreDeltaPlain = ReadThreshold(GlobalKeys.CoreDeltaPlain, -5);
            coreDeltaInsulated = ReadThreshold(GlobalKeys.CoreDeltaInsulated, -10);
            coldDeviceCoreEquivalent = catalog?.FindGlobal("cold_device_effect")?.Value == "core_equivalent";
        }

        public int ColdEnterCelsius => ReadThreshold("room_temp_cold_enter", -5);
        public int FrozenEnterCelsius => ReadThreshold("room_temp_frozen_enter", -10);

        // 용기 위치의 실제 냉각 판정과 같은 코어 목록을 사용한다. 가까운 코어 하나로 한정하지 않는다.
        public void InspectStorageEnvironment(Vector3 position, out bool sealedArea, out bool inColdRange,
            out float invasionHeat)
        {
            var cell = worldSession?.TileService != null ? worldSession.TileService.WorldToCell(position) :
                new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
            iceCoreCells.Clear(); environmentState?.CopyIceCoreCells(iceCoreCells);
            inColdRange = false;
            foreach (var core in iceCoreCells)
            {
                if (!CanReceiveCoreCooling(cell, core)) continue;
                inColdRange = true;
            }
            sealedArea = sealSystem != null && sealSystem.IsInsideSealedArea(position);
            invasionHeat = inColdRange ? invasion?.TemperatureRiseCelsius ?? 0f : 0f;
            CopyColdDevices();
            foreach (var device in coldDeviceCells)
                if (CanReceiveCoreCooling(cell, device)) inColdRange = true;
        }

        public readonly struct ShelterInspection
        {
            public readonly bool HasCore, InRange, Sealed, HasLeak;
            public readonly Vector3Int Core, Leak;
            public readonly int CoreDelta, RangeWidth, RangeHeight;
            public ShelterInspection(bool hasCore, bool inRange, bool sealedRoom, bool hasLeak,
                Vector3Int core, Vector3Int leak, int delta, int width, int height)
            {
                HasCore = hasCore; InRange = inRange; Sealed = sealedRoom; HasLeak = hasLeak;
                Core = core; Leak = leak; CoreDelta = delta; RangeWidth = width; RangeHeight = height;
            }
        }

        // Inspect the nearest core without changing the primary core or progression.
        // Use the same range and seal rules as ResolveExact; natural cold is not a built shelter.
        public ShelterInspection InspectShelter(Vector3 worldPosition)
        {
            var cell = worldSession?.TileService != null
                ? worldSession.TileService.WorldToCell(worldPosition)
                : new Vector3Int(Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y), 0);
            iceCoreCells.Clear();
            environmentState?.CopyIceCoreCells(iceCoreCells);
            if (iceCoreCells.Count == 0) return default;
            var core = iceCoreCells[0];
            foreach (var candidate in iceCoreCells)
                if ((candidate - cell).sqrMagnitude < (core - cell).sqrMagnitude) core = candidate;
            var inRange = CanReceiveCoreCooling(cell, core);
            var sealedRoom = sealSystem != null && sealSystem.IsCoreWindowSealed(core);
            var leak = default(Vector3Int);
            var hasLeak = sealSystem != null && sealSystem.TryGetCoreLeakCell(core, out leak);
            return new ShelterInspection(true, inRange, sealedRoom, hasLeak, core, leak,
                inRange ? (sealedRoom ? coreDeltaInsulated : coreDeltaPlain) : 0,
                coreRangeWidth, coreRangeHeight);
        }

        public int Resolve(Vector3 worldPosition)
            => Mathf.RoundToInt(ResolveExact(worldPosition));

        public float ResolveExact(Vector3 worldPosition)
        {
            var cell = worldSession?.TileService != null
                ? worldSession.TileService.WorldToCell(worldPosition)
                : new Vector3Int(Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y), 0);
            return ResolveExact(cell);
        }

        public int Resolve(Vector3Int cell) => Mathf.RoundToInt(ResolveExact(cell));

        public float ResolveExact(Vector3Int cell)
        {
            var stage = heatStage?.Current ?? 1;
            var temperature = (float)Natural(ResolveDepthNorm(cell), stage,
                warmEndStageOne, coldEndStageOne, bandShiftPerStage, frozenDepthMinimum);
            if (environmentState == null) return temperature;

            environmentState.CopyIceCoreCells(iceCoreCells);
            var insideCoreRange = false;
            for (var index = 0; index < iceCoreCells.Count; index++)
            {
                var core = iceCoreCells[index];
                if (!CanReceiveCoreCooling(cell, core)) continue;
                insideCoreRange = true;
                temperature += sealSystem != null && sealSystem.IsCoreWindowSealed(core)
                    ? coreDeltaInsulated
                    : coreDeltaPlain;
            }
            if (insideCoreRange && invasion != null)
                temperature += invasion.TemperatureRiseCelsius;
            CopyColdDevices();
            foreach (var device in coldDeviceCells)
            {
                if (!CanReceiveCoreCooling(cell, device)) continue;
                temperature += sealSystem != null && sealSystem.IsCoreWindowSealed(device)
                    ? coreDeltaInsulated : coreDeltaPlain;
            }
            return temperature;
        }

        private void CopyColdDevices()
        {
            coldDeviceCells.Clear();
            if (coldDeviceCoreEquivalent) environmentState?.CopyColdDeviceCells(coldDeviceCells);
        }

        private bool CanReceiveCoreCooling(Vector3Int cell, Vector3Int core)
        {
            if (!IsInsideCoreRange(cell, core, coreRangeWidth, coreRangeHeight)) return false;
            // 실내 영역은 SealSystem의 캐시를 재사용한다. 다른 방이나 벽 밖에는 밀폐 냉기가 새지 않는다.
            return sealSystem == null || !sealSystem.IsCoreWindowSealed(core) ||
                sealSystem.IsInsideCoreSealedArea(core, cell);
        }

        public static int Natural(float depthNorm, int heatStage, float warmEndStageOne = .333f,
            float coldEndStageOne = .667f, float shiftPerStage = .074f,
            float frozenMinimum = .185f)
        {
            var depth = Mathf.Clamp01(depthNorm);
            var stageOffset = Mathf.Max(0, heatStage - 1) * Mathf.Max(0f, shiftPerStage);
            var warmEnd = Mathf.Max(0f, warmEndStageOne - stageOffset);
            var coldEnd = Mathf.Max(warmEnd, coldEndStageOne - stageOffset);
            // v72 frozen_min: 단계 이동으로 빙결 시작선이 지표 쪽으로 과도하게 올라가지 않게 한다.
            coldEnd = Mathf.Max(Mathf.Clamp01(frozenMinimum), coldEnd);
            if (depth < warmEnd) return 0;
            return depth < coldEnd ? -5 : -10;
        }

        public static bool IsInsideCoreRange(Vector3Int cell, Vector3Int core, int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            var minX = core.x - width / 2;
            var minY = core.y - height / 2;
            return cell.x >= minX && cell.x < minX + width &&
                   cell.y >= minY && cell.y < minY + height;
        }

        private float ResolveDepthNorm(Vector3Int cell)
        {
            var surfaces = worldSession != null && worldSession.HasWorld
                ? worldSession.LastResult.surfaceHeights
                : null;
            if (surfaces == null || surfaces.Length == 0) return 0f;
            var x = Mathf.Clamp(cell.x, 0, surfaces.Length - 1);
            return Mathf.Clamp01((surfaces[x] - cell.y) / (float)undergroundDepthTiles);
        }

        private int ReadThreshold(string key, int fallback)
        {
            var definition = catalog?.FindGlobal(key);
            if (definition != null && definition.TryGetInt(out var value)) return value;
            return fallback;
        }

        private float ReadFloat(string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) ? value : fallback;
        }
    }
}
