# v46 이어서 작업 — Agent 인수인계 (완료 스냅샷)

> 갱신: 2026-08-08 · `Seokbinggo_Client/`  
> 기획 정본: 문서 지도 v46 → 개발 명세서 ⑤ (모듈 20종, A/B 분담 폐기)

## 이 라운드에서 완료

### P0 CSV
- items 160 · bosses 10 · accessories 26 · crafting-tree 90 · equipment 24 · drops 17 · player-combat 18 · mineral-tiers 15 · globals 114 · night-waves 15 · modules 11
- `ProjectSettings/NyangbingoDataImportManifest.txt` 재생성

### P1 연동
- FrostSpread ↔ TileService (pending / lazy reveal / bedrock unseal)
- InsulationPanels → EnvironmentState 체온 회복
- SeokbinggoUpgrade 세이브·모듈 동기화·Smithy UI 잠금
- EvolutionCraft / GimmickWeaponProgress / ArtifactVerbCatalog
- accessories→equipment 임포트, BossKind 10종, CraftingStation.Smithy

### P2
- `Nyangbingo/Run v46 Regression Tests` 메뉴 추가
- BuildGate·DataMenu·V24 validator를 v46 행수에 맞춤
- WaveNight `CurrentWave`에서 108 기본값 제거 (globals만)

## Unity에서 한 번만

1. `Nyangbingo/Reimport v34 Data Bundle`
2. `Nyangbingo/Run v46 Regression Tests`
3. `Nyangbingo/Validate Product Data Freshness`

하드코딩 금지 유지: 파도 임계·방어 min1·터렛 상한·낮 밝기 곡선 = globals.
