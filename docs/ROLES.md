# 역할 분담

| 작업자 | GitHub | 한 줄 요약 |
| --- | --- | --- |
| 이창민 | [@2-changmin](https://github.com/2-changmin) | **"카트를 움직이게"** — XR, 조작, 카트 물리, 아이템, 빌드 |
| 윤승희 | [@realp0tato](https://github.com/realp0tato) | **"레이스를 만들게"** — 트랙, 랩/순위, AI, UI, 사운드 |

레포 관리(설정, 브랜치 보호, 라벨)는 레포 소유자인 이창민이 맡습니다.

## 이슈 배정

| # | 이슈 | 담당 | 마일스톤 | 우선순위 |
| --- | --- | --- | --- | --- |
| 1 | Unity 프로젝트 생성 & XR 환경 구성 | 이창민 | M1 | P0 |
| 2 | VR 카트 탑승 & 핸들 조향 입력 | 이창민 | M2 | P0 |
| 3 | 카트 주행 물리 (KartController) | 이창민 | M2 | P0 |
| 4 | 드리프트 & 부스트 | 이창민 | M3 | P1 |
| 5 | 아이템 박스 & 아이템 시스템 | 이창민 | M3 | P1 |
| 6 | 멀미 저감 옵션 | 이창민 | M3 | P1 |
| 7 | 그레이박스 테스트 트랙 | 윤승희 | M1 | P0 |
| 8 | 메인 트랙 제작 | 윤승희 | M3 | P1 |
| 9 | 체크포인트 & 랩 시스템 | 윤승희 | M2 | P0 |
| 10 | 레이스 흐름 (RaceManager) | 윤승희 | M2 | P0 |
| 11 | AI 카트 & 실시간 순위 | 윤승희 | M3 | P1 |
| 12 | 메뉴 / 결과 / 일시정지 UI | 윤승희 | M3 | P1 |
| 13 | 인게임 HUD | 윤승희 | M3 | P1 |
| 14 | 사운드 & 이펙트 | 윤승희 | M4 | P2 |
| 15 | Quest 빌드 & 성능 최적화 | 이창민 | M4 | P1 |
| 16 | 통합 테스트 & 발표/시연 준비 | 공동 | M4 | P0 |

> 이슈 번호는 GitHub에 등록된 순서입니다. 새 이슈가 생기면 이 표에도 추가해 주세요.

## 담당 폴더 / 씬 (충돌 방지용)

**자기 담당 폴더는 자유롭게 수정**, 상대 담당 폴더는 수정 전에 말하고 수정합니다.

| 경로 | 담당 |
| --- | --- |
| `Assets/_Project/Scripts/XR/` | 이창민 |
| `Assets/_Project/Scripts/Kart/` | 이창민 |
| `Assets/_Project/Scripts/Items/` | 이창민 |
| `Assets/_Project/Prefabs/Kart/`, `Prefabs/Items/` | 이창민 |
| `Assets/_Project/Scripts/Race/` | 윤승희 |
| `Assets/_Project/Scripts/AI/` | 윤승희 |
| `Assets/_Project/Scripts/UI/` | 윤승희 |
| `Assets/_Project/Scripts/Audio/` | 윤승희 |
| `Assets/_Project/Prefabs/Track/`, `Prefabs/UI/` | 윤승희 |
| `Assets/_Project/Scenes/Track_Test.unity`, `Track_Main.unity` | 윤승희 |
| `Assets/_Project/Scenes/MainMenu.unity` | 윤승희 |
| `Assets/_Project/Scenes/Sandbox/Changmin_*.unity` | 이창민 (개인 실험용) |
| `Assets/_Project/Scenes/Sandbox/Seunghee_*.unity` | 윤승희 (개인 실험용) |
| `Assets/_Project/Scripts/Core/`, `Packages/`, `ProjectSettings/` | 공동 — **PR 설명에 변경 이유 필수** |

## 협업 접점 (서로 기다리지 않기)

두 사람의 작업이 만나는 지점은 [ARCHITECTURE.md](ARCHITECTURE.md)의 **인터페이스 약속**으로 먼저 정하고, 구현이 늦어지면 임시(Stub) 구현으로 진행합니다.

| 접점 | 제공 | 사용 | 내용 |
| --- | --- | --- | --- |
| `KartController` | 이창민 | 윤승희 (AI, HUD, 레이스) | AI는 `IKartInput`을 구현해 같은 카트를 조종, HUD는 속도 표시 |
| `KartController.SetControlEnabled()` | 이창민 | 윤승희 (카운트다운) | 카운트다운 동안 조작 잠금 |
| `RaceProgress` / 체크포인트 | 윤승희 | 이창민 (리스폰, 아이템 확률) | 마지막 체크포인트 위치, 현재 순위 |
| `ItemHolder.CurrentItem` | 이창민 | 윤승희 (HUD) | 보유 아이템 아이콘 표시 |
| `Kart.prefab` | 이창민 | 윤승희 (트랙 배치, AI) | 트랙 씬에는 프리팹으로만 배치 |

## 커뮤니케이션

- 작업 시작 시 이슈에 본인 Assign 확인 후 **"작업 시작합니다"** 코멘트 (선택)
- 막히거나 상대 담당 영역 수정이 필요하면 **이슈 코멘트**로 남기기 (카톡은 보조)
- PR 리뷰 요청이 오면 **24시간 내** 리뷰
- 주 1회 짧게 진행 상황 공유 (마일스톤 페이지 확인)
