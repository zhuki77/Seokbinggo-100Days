# v24.1 ID 정렬 보고서

기준일: 2026-07-17  
공식 기준: `nyangbingo-kit-data-v24.1`

- `items.csv`: 85행, SHA-256 `777293FA4E1FA7544B51AC39356911E8E1117102CEBEE5C076B8539FB0F0FC27`
- `id-migration.csv`: 27행, SHA-256 `BB1B9BD352DA58F61367419E8E59D081ADFF0ED286743BB9CD6BBE6F670704D3`
- 비교 기준선: 이 브랜치 작업 전 Git `HEAD`의 Unity CSV
- 현재 자동 대조: 공식 CSV 17종 / Unity CSV 10종 / 매핑 8쌍

## 요약

| 구분 | 수량 | 설명 |
|---|---:|---|
| 변경 ID | 27 | 공식 v24.1 rename 26건 + 기존 `folding_fan → hapjukseon` 1건 |
| 폐기 ID | 1 | `fox_rain_charm`, 보유 수량당 `yokai_tear:3` 환급 |
| 실제 신규 ID | 37 | 기존 ID의 단순 개명이 아닌 신규 마스터 ID |
| 현재 Unity `items.csv` 누락 | 43 | 신규 37개 + 장비 정의에만 존재하는 액세서리 6개 |

`gangcheori`는 도메인에 따라 분리한다. 일반 요괴·도감·스폰 기록은 `gangcheol`, 보스 전투·강제 조우 기록은 `gangcheol_boss`를 사용한다.

## 변경 ID

### item

| 기존 ID | 신규 ID |
|---|---|
| `club_fragment` | `club_shard` |
| `foundry` | `blast_furnace` |
| `goblin_fire_essence` | `dokkaebi_fire_essence` |
| `hemp` | `hemp_stalk` |
| `ice_steel_claws` | `icesteel_claw` |
| `ice_steel_ingot` | `icesteel_ingot` |
| `ice_steel_ore` | `icesteel_ore` |
| `iron_claws` | `iron_claw` |
| `iron_furnace_heart` | `iron_forge_core` |
| `nest` | `nest_bed` |
| `reverse_scale` | `gangcheol_scale` |
| `shadow_fragment` | `shadow_shard` |
| `wrestling_belt` | `ssireum_satba` |
| `yokai_tears` | `yokai_tear` |
| `bell_charm` | `bell_norigae` |
| `ice_heart_charm` | `ice_heart_norigae` |
| `lucky_pouch` | `bokjumeoni` |
| `wind_ribbon` | `wind_daenggi` |
| `tiger_eye_orb` | `tiger_eye_bead` |
| `goblin_hat` | `dokkaebi_gamtu` |

기존 선행 마이그레이션:

| 기존 ID | 신규 ID |
|---|---|
| `folding_fan` | `hapjukseon` |

### yokai

| 기존 ID | 신규 ID |
|---|---|
| `club_goblin` | `club` |
| `yagwanggwi` | `yakwang` |
| `gangcheori` | `gangcheol` |

### boss

| 기존 ID | 신규 ID |
|---|---|
| `goblin_chief` | `king_dokkaebi` |
| `gangcheori` | `gangcheol_boss` |

### smelting

| 기존 ID | 신규 ID |
|---|---|
| `smelt_ice_steel` | `smelt_icesteel` |

## 폐기 ID

| 기존 ID | 처리 |
|---|---|
| `fox_rain_charm` | 모든 참조·쿨다운·상태 제거, 인벤토리 보유 1개당 `yokai_tear` 3개 환급, 초과분은 pending acquisition으로 보존 |

## 실제 신규 ID 37개

### equipment 9

`straw_helm`, `straw_armor`, `straw_boots`, `iron_helm`, `iron_armor`, `iron_boots`, `icesteel_helm`, `icesteel_armor`, `icesteel_boots`

### tool 1

`bare_claw`

### weapon 3

`cheolseon`, `dokkaebi_club`, `frostclaw_gauntlet`

### placeable 24

`clay_plaster`, `cold_device`, `cold_wave_core`, `dokkaebi_fire_tower`, `door`, `frost_lantern`, `ice_core`, `ice_crystal_cooler`, `ice_jar`, `insul_wall`, `iron_bell_rope`, `iron_insul_wall`, `iron_sieve`, `jangdok`, `minhwa_scroll`, `munpungji`, `onggi_pot`, `roof`, `saekdong_cushion`, `saekdong_lantern`, `singijeon_cart`, `straw_insul`, `water_jar`, `wind_chime`

## 현재 Unity 마스터 누락 43개

위 신규 ID 37개 외에 다음 액세서리 6개가 `equipment.csv`와 Equipment SO에는 존재하지만 `items.csv`에는 없다.

`bell_norigae`, `wind_daenggi`, `tiger_eye_bead`, `ice_heart_norigae`, `bokjumeoni`, `dokkaebi_gamtu`

공식 `items.csv`가 유일한 아이템 ID 마스터이므로 후속 통합 시 이 6개도 Unity `items.csv` 및 Item SO에 추가해야 한다.

## 자동 대조에서 확인된 별도 데이터 차이

- `ice_anvil.station_id`: 공식 `blast_furnace`, Unity `Furnace`
- `yakwang.dmg_taken_condition`: 공식 `steal_only`, Unity `None`
- 미연동 요괴 필드: `spawn_track`, `dawn_flee`, `sig_condition`
- 상자 CSV는 행 수만 4/4이며 공식 풀·보너스 자원용 custom adapter가 아직 필요하다.

