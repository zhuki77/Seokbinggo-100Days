# ui.zip 게임 연결

2026-10-06 사용자 승인: 게임 연결 진행, 고친 칸은 녹색 check 선택.

| 원본 | 연결 |
|---|---|
| check.aseprite | GameplayArtCatalog.SealRepairCheck. 방금 고친 경계 칸 체크 |
| ui_raid_arrow.aseprite | GoalDirectionArrow. 현재 목표 대상 방향 화살표 |
| 찬바람 새는 이펙트.aseprite | SealLeakMarkerFrames 6개. 누락 경계 칸의 반복 효과, 프레임당 0.1초 |
| 찬바람 새는 마커.aseprite | SealLeakStaticMarker. 체온계/밀폐 진단을 눌렀을 때 대표 누출 칸 표시 |
| 찬바람 새는 아이콘.aseprite | SealLeakStatusIcon. 해당 진단 표시가 활성인 동안 하단 알람 줄 |
| 공격 icon.aseprite | YokaiDamageIcon. 발톱 자국 1안 선택, 기존 근접 피격 알람 0.28초 동안 하단 알람 줄 |
| 화상icon.aseprite | BurnStatusIcon. 실제 낮 화상 활성 시 하단 알람 줄의 공용 위험 아이콘 대체 |
| vault_seal.aseprite | ItemArtCatalog vault_seal. 창고 봉인 장비 아이콘 |

check2와 공격 2icon은 대체안으로 임포트·보관하지만 선택되지 않는다. 밀폐 성공 파동과 객귀 체력 프레임은 여전히 미제공이므로 대체하지 않는다.

원본 10개는 수정 없이 Assets에 복사했다. Aseprite가 누출 이펙트 프레임을 서로 다른 크기로 잘라 uGUI에 전달하므로, 해당 애니메이션만 CPU로 원본 16×16 캔버스의 RGBA PNG 6개를 추출하고 연결한다. 임의 그림 생성·보간·리사이즈 없이 투명 여백을 보존한다. Unity가 메타와 Sprite 참조를 생성한다. 원본 Aseprite도 함께 보존한다.

재연결 함수: NyangbingoTileArtIntegrator.ApplyDeliveredUiArt. 창고 봉인은 ItemArtFiles에도 등록하여 일반 아이템 아트 재연결 시 유지한다. 전용 화상·피해·누출 아트는 공용 DangerIcon을 덮어쓰지 않는다.

검증: CPU 런타임/Editor C# 컴파일, Unity MCP로 비플레이 Title 씬 확인 후 임포트·카탈로그 저장, 저장된 원본 경로·16 PPU·프레임 수 확인. 게임 실행·GPU 검증 없음.

사용자 확인: 경계 구멍의 바람 → 경계를 막았을 때 녹색 체크 → 목표 화면 밖 방향 화살표 → 창고 봉인 아이콘 → 실제 화상 불꽃 알람 → 근접 피격 발톱 알람 → 밀폐 진단 클릭 시 찬바람 아이콘/대표 칸 마커. 메뉴에서는 해당 알람 줄과 월드 안내가 숨겨져야 한다.
