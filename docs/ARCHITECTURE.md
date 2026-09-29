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

- 1랩 약 876m, 폭 10m 도로. 카트 크기(약 1.2m x 2m) 기준으로 나란히 4~5대가 달릴 수 있는 폭입니다.
- `XR Origin (XR Rig)`, `KartSizeRef_Temp`는 Kart 프리팹(#3)이 나오기 전까지의 **임시 오브젝트**입니다. Kart가 들어오면 삭제합니다.
- 트랙 조각은 모두 `Static`이라 빌드 시 Static Batching으로 합쳐집니다 (NFR-01 드로우콜 예산).

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```
