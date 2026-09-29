# 아키텍처 & 프로젝트 구조

## 폴더 구조

우리가 만든 파일은 모두 `Assets/_Project/` 아래에 둡니다. 에셋 스토어/외부 패키지는 `Assets/ThirdParty/`에 둡니다.

```
Assets/
├─ _Project/
│  ├─ Scenes/
│  │  ├─ MainMenu.unity
│  │  ├─ Track_Test.unity        # 그레이박스 테스트 트랙
│  │  ├─ Track_Main.unity        # 메인 트랙
│  │  └─ Sandbox/                # 개인 실험 씬 (이름_기능.unity)
│  ├─ Scripts/
│  │  ├─ Core/                   # 공용: 인터페이스, 이벤트, 유틸
│  │  ├─ XR/                     # 핸들, 리센터, 비네팅
│  │  ├─ Kart/                   # KartController, 드리프트, 부스트, 입력
│  │  ├─ Items/                  # 아이템 박스, 아이템
│  │  ├─ Race/                   # 체크포인트, 랩, RaceManager, 순위
│  │  ├─ AI/                     # AI 입력, 웨이포인트
│  │  ├─ UI/                     # 메뉴, HUD, 결과
│  │  └─ Audio/                  # 사운드 매니저
│  ├─ Prefabs/ (Kart, Items, Track, UI)
│  ├─ Materials/  Models/  Textures/  Audio/  VFX/
│  └─ Settings/                  # URP, Input Actions 등
└─ ThirdParty/
```

## 네이밍 규칙

| 대상 | 규칙 | 예시 |
| --- | --- | --- |
| 클래스, 메서드, public 프로퍼티 | PascalCase | `KartController`, `ApplyBoost()` |
| private 필드 | `_camelCase` | `_currentSpeed` |
| 인스펙터 노출 필드 | `[SerializeField] private` + `_camelCase` | `[SerializeField] private float _maxSpeed;` |
| 인터페이스 | `I` + PascalCase | `IKartInput` |
| 네임스페이스 | `VRKart.<폴더>` | `VRKart.Kart`, `VRKart.Race` |
| 씬/프리팹/에셋 | PascalCase, 구분은 `_` | `Track_Main`, `Item_Banana.prefab` |

- `public` 필드 대신 `[SerializeField] private` 사용
- `Update()`에서 `GetComponent`/`Find` 금지 → `Awake()`에서 캐싱
- `Debug.Log`는 PR 올리기 전에 정리 (필요한 것만 남김)

## 주요 컴포넌트

```
[XR Origin] ──(자식)── [Kart]
                        ├─ KartController      ← IKartInput 에서 입력을 받아 Rigidbody 이동
                        ├─ PlayerKartInput     : IKartInput  (핸들 + 컨트롤러)
                        │   또는 AIKartInput    : IKartInput  (웨이포인트)
                        ├─ DriftBoost
                        ├─ ItemHolder
                        └─ RaceProgress        ← 체크포인트/랩 진행도

RaceManager (씬에 1개)  ── 상태: Waiting → Countdown → Racing → Finished
  ├─ 모든 RaceProgress를 모아 순위 계산
  └─ 이벤트 발행 → HUD / 결과 UI / 사운드가 구독
```

## 인터페이스 약속 (Contract)

두 사람 작업의 경계입니다. **시그니처를 바꿀 때는 PR에 `breaking` 표시 + 상대방 리뷰 필수.**
구현 전이라도 아래 형태로 먼저 `Scripts/Core/`에 만들어 두고 각자 작업합니다 (이슈 #1에서 생성).

```csharp
namespace VRKart.Core
{
    // 카트 조종 입력. 플레이어/AI 모두 이것을 구현한다. (이창민 정의, 윤승희 AI에서 구현)
    public interface IKartInput
    {
        float Throttle { get; }   // 0 ~ 1
        float Brake    { get; }   // 0 ~ 1
        float Steer    { get; }   // -1(좌) ~ 1(우)
        bool  Drift    { get; }
        bool  UseItem  { get; }   // 이번 프레임에 눌렸는지
    }

    // 카트 상태 조회/제어. (이창민 구현, 윤승희 사용)
    public interface IKart
    {
        float CurrentSpeed { get; }      // m/s
        float MaxSpeed     { get; }
        void  SetControlEnabled(bool enabled);
        void  ApplyBoost(float power, float duration);
        void  SpinOut();                 // 바나나/쉘 피격
        void  Respawn(Pose pose);
    }

    // 레이스 진행도. (윤승희 구현, 이창민 사용 — 리스폰/아이템 확률)
    public interface IRaceParticipant
    {
        int  CurrentLap  { get; }
        int  Rank        { get; }        // 1부터
        bool IsFinished  { get; }
        Pose LastCheckpointPose { get; }
    }

    public enum ItemType { None, Booster, Banana, Shell }
}
```

### 이벤트

`RaceManager`가 C# 이벤트로 발행하고 UI/사운드가 구독합니다.

| 이벤트 | 시점 |
| --- | --- |
| `OnCountdownTick(int)` | 3, 2, 1, 0(GO) |
| `OnRaceStarted()` | GO |
| `OnLapCompleted(IRaceParticipant, int lap, float lapTime)` | 랩 완료 |
| `OnParticipantFinished(IRaceParticipant, float totalTime)` | 개별 완주 |
| `OnRaceFinished()` | 플레이어 완주 (결과 화면) |

## 레이어 / 태그 규칙

`ProjectSettings/TagManager.asset`에 정의합니다. 트랙 조각은 아래 레이어로 나뉘어 있어서 카트 물리·AI·리스폰이 "지금 어디를 달리는지"를 레이어로 판별합니다.

| 레이어 (번호) | 대상 | 용도 |
| --- | --- | --- |
| `Road` (8) | 도로 노면, 점프대 | 정상 주행 구간. 최고 속도 100% |
| `Grass` (9) | 갓길, 트랙 밖 바닥 | 코스 밖 판정 → 최고 속도 감소 (FR-KART-05) |
| `Wall` (10) | 도로 양옆 벽 | 코스 이탈 방지. 충돌 시 감속/튕김 처리 |

- 카트 아래로 Raycast 해서 맞은 콜라이더의 **레이어**로 노면을 판별합니다 (태그는 쓰지 않음).
- 새 레이어가 필요하면 이 표에 먼저 추가하고 PR에 `breaking` 라벨을 붙입니다 (`ProjectSettings/` 공동 영역).
- 체크포인트, 아이템 박스 같은 **트리거**는 레이어가 아니라 각 담당 컴포넌트(`Checkpoint` 등)로 구분합니다.

## 트랙 구성 (`Track_Test.unity`)

| 오브젝트 | 설명 |
| --- | --- |
| `Track/Track_Segment_###` | 도로 조각. 프리팹 `Track_Segment`(폭 10m 도로 + 3m 갓길 + 0.5m 벽) 하나를 길이(Z 스케일)와 회전만 바꿔 이어 붙임 |
| `Props/Track_StartLine` | 체크무늬 출발선 (콜라이더 없음) |
| `Props/Track_StartGrid` | 출발 위치 `StartPos_1`~`StartPos_4`. **1번 = 플레이어, 2~4번 = AI**. 출발선 뒤쪽으로 2열 엇갈려 배치 |
| `Props/Track_JumpRamp` | 점프대 (경사 7°, 높이 약 1.1m) |
| `Environment/Ground_Grass` | 트랙 밖 넓은 바닥 (`Grass`) |
| `RaceTrack/Checkpoint_00~15` | 체크포인트 16개 (약 55m 간격). `00` = 결승선. 아래 [체크포인트 & 랩 규칙](#체크포인트--랩-규칙) |
| `KillZone` | 바닥 아래(y -20 ~ -10) 넓은 트리거. 떨어진 카트를 마지막 체크포인트로 리스폰 |

- 1랩 약 876m, 폭 10m 도로. 카트 크기(약 1.2m x 2m) 기준으로 나란히 4~5대가 달릴 수 있는 폭입니다.
- `XR Origin (XR Rig)`, `TestKart_Temp`는 Kart 프리팹(#3)이 나오기 전까지의 **임시 오브젝트**입니다. Kart가 들어오면 삭제합니다.
- 트랙 조각은 모두 `Static`이라 빌드 시 Static Batching으로 합쳐집니다 (NFR-01 드로우콜 예산).

## 체크포인트 & 랩 규칙

`Scripts/Race/` (`VRKart.Race`)

| 컴포넌트 | 붙는 곳 | 역할 |
| --- | --- | --- |
| `RaceTrack` | 씬에 1개 | 자식 `Checkpoint`를 순번대로 모음, 총 랩 수(기본 3), `DirectionAt(위치)` = 그 위치의 트랙 진행 방향 |
| `Checkpoint` | 트리거 (`Prefabs/Track/Checkpoint.prefab`) | 순번 `Index`. **`transform.forward` = 진행 방향**. `RespawnPose` = 위치 + 0.5m, 진행 방향을 보는 수평 회전 |
| `RaceProgress` | **카트 루트** (`IRaceParticipant`) | 랩/순서 추적, `LastCheckpointPose`, 역주행 감지 |
| `KillZone` | 트리거 | 들어온 카트의 `IKart.Respawn(LastCheckpointPose)` 호출 |
| `LapCounter` | (순수 C# 클래스) | 순서·랩 판정 로직만 분리. Unity 없이 검증 가능 |

**판정 규칙**

- 카트는 결승선(0번) **뒤에서** 출발합니다. 결승선을 **처음** 넘으면 레이스 시작이고 랩은 세지 않습니다. 그래서 출발 직후 표시는 `1/3` 랩입니다.
- 다음 순번 체크포인트만 인정합니다. 건너뛰기, 역방향 재통과, 한 번에 여러 콜라이더가 겹치는 중복 트리거는 모두 무시됩니다.
- 모든 체크포인트를 돈 뒤 결승선을 넘으면 랩 완료입니다. 3번째 랩 완료 = `IsFinished`이고, 이후 통과는 무시됩니다.
- 역주행은 0.2초 구간 평균 속도가 그 위치의 트랙 방향과 반대(내적 < -0.5)인 상태가 1초 이어지면 `WrongWayChanged(true)`입니다. 정방향으로 움직이면 `false`가 됩니다.

**카트 쪽 약속 (#3)**

- `RaceProgress`와 `IKart` 구현(`KartController`)은 **같은 게임오브젝트(카트 루트)**에 붙입니다. 체크포인트/킬존은 카트 콜라이더에서 `GetComponentInParent<RaceProgress>()`로 찾습니다.
- 카트에는 Rigidbody가 있어야 트리거가 발생합니다.
- 체크포인트/킬존은 `Ignore Raycast` 레이어라서, 지면 판정 Raycast에는 `Road | Grass` 레이어 마스크를 쓰면 걸리지 않습니다.

**이벤트** (`RaceProgress`, `RaceManager`(#10)가 모아서 위 표의 레이스 이벤트로 발행)

| 이벤트 | 시점 |
| --- | --- |
| `CheckpointPassed(RaceProgress, Checkpoint)` | 순서에 맞는 체크포인트 통과 (결승선 포함) |
| `LapCompleted(RaceProgress, int lap)` | 랩 완료, 완료한 랩 번호(1부터) |
| `Finished(RaceProgress)` | 마지막 랩 완료 |
| `WrongWayChanged(RaceProgress, bool)` | 역주행 시작/해제 |

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```
