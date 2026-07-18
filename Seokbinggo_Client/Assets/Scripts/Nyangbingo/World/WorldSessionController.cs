using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 개발 A의 월드 세션 오케스트레이터. 새 게임 시작, 저장 캡처, 로드 복원, 상자 개봉을 한 곳에서
    /// 관리한다. 타일 diff/상자 검증·저장 규칙 자체는 이미 Dev B가 <see cref="Nyangbingo.Save.WorldSaveAdapter"/>
    /// (Save/SaveGame.cs)로 구현·검증해 두었으므로 여기서 다시 구현하지 않고 그대로 소비한다 —
    /// 이 클래스의 역할은 "같은 시드로 월드를 결정론적으로 재생성 → TileService/SealSystem을 새로 구성 →
    /// Dev B 어댑터로 diff/상자 상태를 적용" 순서를 조립하는 접착 계층이다.
    ///
    /// 복원 순서(DEV_B_TO_DEV_A_HANDOFF.md §11.6, Development Part A 범위):
    ///  1) 저장된 seed로 월드를 깨끗하게 재생성한다.
    ///  2) TileService.RestoreTileChanges로 타일 변경 이력을 그대로 재생해 타일맵을 복원한다.
    ///  3) WorldSaveAdapter.RestoreChests로 20개 상자의 열림 상태를 복원한다
    ///     (이무기 제단은 파괴 불가 + 시드로만 결정되는 타일이라 재생성만으로 이미 원상 복구됨).
    ///  4) 타일맵 렌더러를 갱신하고, SealSystem.InvalidateAll()로 밀폐 캐시를 초기화한다.
    /// </summary>
    public sealed class WorldSessionController : IDisposable
    {
        private readonly WorldGenerationConfig config;
        private readonly TilemapRenderer renderer;
        private readonly GameDataCatalog catalog;

        private MapGenerator generator;
        private TileService tileService;
        private SealSystem sealSystem;
        private ChestProgress chestProgress = new ChestProgress();
        private int seed;
        private bool disposed;

        public TileService TileService => tileService;
        public SealSystem SealSystem => sealSystem;
        public ChestProgress ChestProgress => chestProgress;
        public MapGenerator Generator => generator;
        public int Seed => seed;
        public WorldGenerationResult LastResult { get; private set; }
        public bool HasWorld => tileService != null;

        public WorldSessionController(WorldGenerationConfig config, TilemapRenderer renderer, GameDataCatalog catalog)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            this.catalog = catalog; // 카탈로그 없이도(상자 보상 미연결) 채굴/밀폐 테스트는 가능해야 한다.
        }

        /// <summary>결정론적 4패스 생성을 처음부터 실행해 새 게임을 시작한다.</summary>
        public WorldGenerationResult StartNewWorld(int requestedSeed)
        {
            generator = new MapGenerator(config);
            var result = generator.GenerateDetailed(requestedSeed);
            seed = result.acceptedSeed; // 리롤이 있었을 수 있으므로 항상 확정 시드를 세이브 기준으로 삼는다.
            LastResult = result;

            renderer.RenderWorld(result.tiles);
            RebuildLiveSystems(result.tiles);
            chestProgress = new ChestProgress();
            return result;
        }

        /// <summary>현재 라이브 상태(타일 diff + 상자 개봉 여부)를 save에 캡처한다.</summary>
        public bool CaptureSnapshot(SaveGame save)
        {
            if (save == null || !HasWorld) return false;
            save.seed = seed;
            return Nyangbingo.Save.WorldSaveAdapter.CaptureWorld(save, tileService.GetTileChangeRecords(),
                Array.Empty<PlacedObjectRecord>(), generator, chestProgress);
        }

        /// <summary>
        /// save.seed로 월드를 결정론적으로 재생성한 뒤, 저장된 타일 diff와 상자 상태를 그대로 재생해
        /// 로드 이전의 최종 모습으로 복원한다. 검증에 실패하면 false를 반환하고 이전 라이브 상태는
        /// 그대로 유지한다(§11.6: "복원 실패를 무시하고 일부 상태로 게임을 시작하지 않는다").
        /// </summary>
        public bool LoadSnapshot(SaveGame save)
        {
            if (save == null) return false;
            save.NormalizeAfterLoad();
            if (!Nyangbingo.Save.WorldSaveAdapter.ValidateWorldRecords(save)) return false;

            var loadedGenerator = new MapGenerator(config);
            var result = loadedGenerator.GenerateDetailed(save.seed);
            if (result.acceptedSeed != save.seed)
            {
                // 저장된 시드가 더 이상 같은 검증 통과 맵을 재현하지 못한다 — 룰/시드가 바뀐 손상된 세이브다.
                Debug.LogError($"[Nyangbingo] WorldSessionController: seed {save.seed} 재생성 결과가 저장 시점과 달라 로드를 중단합니다.");
                return false;
            }

            // 1) 저장된 타일 diff를 방금 재생성한 배열 위에 재생한다. RestoreTileChanges가 result.tiles를
            // 직접 변형하므로(TileService는 배열을 복사하지 않고 참조만 들고 있다), 아래 RenderWorld가
            // 그릴 시점에는 이미 diff가 반영된 "최종" 상태가 담겨 있다.
            var loadedTileService = new TileService(result.tiles, renderer, catalog, result.acceptedSeed);
            if (!loadedTileService.RestoreTileChanges(save.tileChanges))
            {
                Debug.LogError("[Nyangbingo] WorldSessionController: 타일 변경 이력 재생에 실패해 로드를 중단합니다.");
                return false;
            }

            // 2) 이무기 제단은 파괴 불가 + 시드로만 결정되는 타일이라 재생성만으로 이미 원상 복구돼 있다.
            // 상자는 사용자 상호작용 결과(열림 여부)가 시드로 재현되지 않으므로 별도 복원이 필요하다.
            var loadedChestProgress = new ChestProgress();
            if (!Nyangbingo.Save.WorldSaveAdapter.RestoreChests(save, loadedGenerator, loadedChestProgress))
            {
                Debug.LogError("[Nyangbingo] WorldSessionController: 상자 상태 복원에 실패해 로드를 중단합니다.");
                return false;
            }

            // 여기까지 전부 성공해야 라이브 상태를 교체한다 — 부분 복원으로 게임을 계속하지 않는다.
            generator = loadedGenerator;
            tileService = loadedTileService;
            chestProgress = loadedChestProgress;
            seed = result.acceptedSeed;
            LastResult = result;

            // 3) 타일맵 렌더러 갱신 — diff가 이미 반영된 배열을 한 번에 SetTilesBlock으로 그린다.
            renderer.RenderWorld(result.tiles);

            // 4) SealSystem은 옛 TileService(옛 배열)를 참조하던 이벤트 구독을 갖고 있으므로 새로 만들고,
            // InvalidateAll()로 리전 캐시를 초기화해 다음 조회부터 새 배열 기준으로 다시 계산되게 한다.
            sealSystem?.Dispose();
            sealSystem = new SealSystem(tileService, catalog != null ? catalog.SealWhitelist : null);
            sealSystem.InvalidateAll();
            return true;
        }

        /// <summary>
        /// 우클릭 등으로 특정 셀을 상자로 취급해 열어본다. 성공하면 chestId를 돌려주고, 지역(Ruins/Upper/
        /// Middle/Deep)에 해당하는 chests.csv 보상 풀(§7)에서 아이템/액세서리를 ChestProgress.TryOpen 경로로
        /// 지급한다. 카탈로그가 없거나 해당 지역 ChestDefinition을 못 찾으면 안전하게 실패로 처리한다.
        /// </summary>
        public bool TryOpenChestAt(Vector3Int cell, out string chestId, out ChestDefinition definition)
        {
            chestId = null;
            definition = null;
            if (!HasWorld || catalog == null) return false;
            if (!generator.TryGetChestIdAt(new Vector2Int(cell.x, cell.y), out var foundChestId)) return false;
            if (chestProgress.IsOpened(foundChestId)) return false;

            var region = generator.GetChestRegion(foundChestId);
            var found = catalog.FindChest(RegionCatalogId(region));
            if (found == null || !chestProgress.TryOpen(foundChestId, found, seed)) return false;

            chestId = foundChestId;
            definition = found;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sealSystem?.Dispose();
        }

        private void RebuildLiveSystems(TileData[,] tiles)
        {
            sealSystem?.Dispose();
            tileService = new TileService(tiles, renderer, catalog, seed);
            sealSystem = new SealSystem(tileService, catalog != null ? catalog.SealWhitelist : null);
        }

        private static string RegionCatalogId(ChestRegion region) => region switch
        {
            ChestRegion.Ruins => "ruins_chest",
            ChestRegion.Upper => "upper_chest",
            ChestRegion.Middle => "middle_chest",
            _ => "deep_chest"
        };
    }
}
