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

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```
