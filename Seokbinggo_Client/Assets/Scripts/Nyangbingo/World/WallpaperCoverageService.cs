using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// A-16/A-19: 밀폐된 코어 구역의 배경 도포율.
    /// SealSystem 코어 창 리전을 재사용하며, 전경·배경 타일 변경/코어 변경/로드 시에만 재계산한다.
    /// 도포율은 SealPercent·TemperaturePercent를 변경하지 않는다.
    /// </summary>
    public sealed class WallpaperCoverageService : IWallpaperCoverageSource, IDisposable
    {
        private TileService tileService;
        private readonly SealSystem sealSystem;

        private Vector3Int? cachedCore;
        private float cachedPercent = -1f;
        private bool cachedComplete;
        private bool cacheValid;

        public event Action WallpaperCoverageChanged;

        public WallpaperCoverageService(TileService tileService, SealSystem sealSystem)
        {
            this.tileService = tileService ?? throw new ArgumentNullException(nameof(tileService));
            this.sealSystem = sealSystem ?? throw new ArgumentNullException(nameof(sealSystem));

            GameEvents.OnTileBroken += HandleTileChanged;
            GameEvents.OnTilePlaced += HandleTileChanged;
            GameEvents.OnNightStart += Invalidate;
            sealSystem.WatchPointSealChanged += HandleSealChanged;
        }

        public void Dispose()
        {
            GameEvents.OnTileBroken -= HandleTileChanged;
            GameEvents.OnTilePlaced -= HandleTileChanged;
            GameEvents.OnNightStart -= Invalidate;
            sealSystem.WatchPointSealChanged -= HandleSealChanged;
        }

        /// <summary>로드 후 새 TileService로 참조만 교체한다(이벤트 구독 유지).</summary>
        public void Rebind(TileService newTileService)
        {
            tileService = newTileService ?? throw new ArgumentNullException(nameof(newTileService));
            Invalidate();
        }

        /// <summary>타일/코어/밤 시작 등 결과가 달라질 수 있는 시점에만 캐시를 비운다.</summary>
        public void Invalidate()
        {
            var hadCache = cacheValid;
            cacheValid = false;
            cachedPercent = -1f;
            if (hadCache) WallpaperCoverageChanged?.Invoke();
        }

        public float GetCoveragePercent(Vector3Int sealCoreCell)
        {
            EnsureCache(sealCoreCell);
            return cachedPercent;
        }

        public bool IsCoverageComplete(Vector3Int sealCoreCell)
        {
            EnsureCache(sealCoreCell);
            return cachedComplete;
        }

        private void EnsureCache(Vector3Int sealCoreCell)
        {
            if (cacheValid && cachedCore.HasValue && cachedCore.Value == sealCoreCell) return;

            var previousComplete = cacheValid && cachedComplete;
            Compute(sealCoreCell, out cachedPercent, out cachedComplete);
            cachedCore = sealCoreCell;
            cacheValid = true;

            if (previousComplete != cachedComplete) WallpaperCoverageChanged?.Invoke();
        }

        private void Compute(Vector3Int sealCoreCell, out float percent, out bool complete)
        {
            percent = 0f;
            complete = false;

            // 코어가 없거나 요청 코어와 다르면 효과 비활성(A-19).
            if (!sealSystem.HasSealCoreCell || sealSystem.SealCoreCell != sealCoreCell)
                return;

            if (!sealSystem.TryGetCoreRegionSnapshot(out var isSealed, out var leakFaces, out var interior))
                return;

            // 미밀폐면 완료 효과 비활성. 도포율 자체는 0으로 둔다.
            if (!isSealed || leakFaces != 0 || interior == null || interior.Count == 0)
                return;

            var covered = 0;
            var total = 0;
            foreach (var cell in interior)
            {
                if (!tileService.InBounds(cell)) continue;
                total++;
                if (tileService.GetTile(cell).CountsAsWallpaperCovered) covered++;
            }

            if (total == 0) return;

            percent = 100f * covered / total;
            // 정확히 100%에서만 완료(99%는 효과 없음).
            complete = covered == total;
        }

        private void HandleTileChanged(Vector3Int _) => Invalidate();

        private void HandleSealChanged(Vector3Int _, bool __) => Invalidate();
    }
}
