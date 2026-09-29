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
│  ├─ Fonts/                     # TMP 폰트 에셋 (Pretendard SDF), 포함 글자 목록
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
| `UI/UI_ResultScreen`, `UI/UI_RaceHud`, `UI/EventSystem` | 결과 화면, 인게임 HUD, XR 레이 UI 입력(`XRUIInputModule`). 아래 [UI](#ui--결과-화면-ui_resultscreen) 절 |

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

## 레이스 흐름 (`RaceManager`)

씬에 1개. 인스펙터에서 **`Player`에 플레이어 카트의 `RaceProgress`를 지정**합니다(참가자가 1명뿐이면 자동). 씬의 모든 `RaceProgress`를 참가자로 모읍니다.

```
Waiting ──(Start Delay 1초)──▶ Countdown 3,2,1 ──▶ Racing (GO) ──플레이어 완주──▶ Finished
            모든 카트 SetControlEnabled(false)        SetControlEnabled(true)
```

| API | 설명 |
| --- | --- |
| `State`, `StateChanged` | 현재 상태 |
| `RaceTime` | GO부터 흐른 시간(초). 일시정지 중엔 멈춤 |
| `GetCurrentLapTime(RaceProgress)` | 진행 중인 랩의 시간 (HUD용) |
| `GetResult(RaceProgress)` / `GetResults()` | 기록(`RaceResult`: 랩 타임 목록, 전체 기록, 완주 순서, 순위). `GetResults()`는 순위순 정렬 (결과 화면용) |
| `Pause()` / `Resume()` / `TogglePause()`, `IsPaused`, `PauseChanged` | `Time.timeScale = 0`. Countdown, Racing 중에만 가능 |
| `Restart()` / `ExitToMenu()` | `SceneLoader`로 현재 씬 다시 로드 / `MainMenu` 로드 |

- 기록은 게임 시간(`Time.timeAsDouble`) 기준이라 **일시정지 시간은 빠집니다.** 1랩 기록은 GO부터 재므로 결승선까지 달려오는 거리가 포함됩니다.
- **실시간 순위**: 카운트다운 시작 시와 그 뒤 **0.2초마다**(`Rank Update Interval`) 모든 참가자를 정렬해서 `RaceProgress.SetRank()`로 넣습니다(FR-RACE-07). 정렬 기준은 아래 순서입니다.
  1. 완주한 카트가 앞 (완주 순서대로, 이후로는 고정)
  2. **출발선을 넘은 카트가 앞.** 막 넘은 카트와 아직 못 넘은 카트는 둘 다 지나온 체크포인트가 0이라, 거리부터 비교하면 뒤 카트가 1위로 나옵니다.
  3. 지나온 체크포인트 수(랩 포함 누적)가 많은 카트가 앞
  4. 다음 체크포인트까지 직선거리가 짧은 카트가 앞
- AI의 아이템 확률(#5 P2)이나 HUD는 `IRaceParticipant.Rank`만 읽으면 됩니다.
- 플레이어가 완주하면 `Finished` → `OnRaceFinished`. AI는 그 뒤에도 계속 달리고, 완주하면 `OnParticipantFinished`가 발생합니다.
- 일시정지 입력(컨트롤러 메뉴 버튼)과 메뉴 UI는 #12에서 이 API를 호출합니다.

## UI — 결과 화면 (`UI_ResultScreen`)

`Scripts/UI/` (`VRKart.UI`), `Prefabs/UI/`

- 트랙 씬에 `Prefabs/UI/UI_ResultScreen.prefab`을 1개 놓습니다. `RaceManager`는 비워두면 자동으로 찾습니다.
- 플레이어가 완주(`OnRaceFinished`)하면 **메인 카메라 수평 정면 1.5m, 눈높이 -0.15m**에 뜹니다. AI가 나중에 완주하면(`OnParticipantFinished`) 표가 바로 갱신됩니다.
- 화면은 **카메라 루트(`Camera.main.transform.root`)의 자식**으로 붙습니다. 그래서 카트와 함께 움직이고, 고개를 돌려도 따라오지 않습니다(head-locked 아님). → **#2 약속**: XR Origin을 카트의 자식으로 두고, 카트는 씬 **최상위**에 둡니다(다른 부모 오브젝트 아래에 넣으면 그 부모에 붙음).
- 크기: 월드 스페이스 Canvas 1200 × 720px × 0.001 = **1.2m × 0.72m** (1.5m에서 가로 약 44°). 글자는 38px 이상(약 3.8cm)이라 [DEVICE.md](DEVICE.md)의 기준(약 3cm)을 넘습니다.
- 문구는 한글입니다(`완주!`, `순위 / 선수 / 기록`, `내 기록`, `1랩 … 최고`, `다시 시작`, `메뉴`, 미완주는 `주행 중`, 플레이어 이름은 `나`).
- XR 레이 클릭 조건: Canvas에 `TrackedDeviceGraphicRaycaster`가 있고, **씬에 `EventSystem` + `XRUIInputModule`** 이 있어야 합니다. 메뉴, 일시정지 UI도 공통입니다. 마우스 클릭도 됩니다.
- 시간 표시 유틸: `TimeFormat.Format(초)` → `1:23.456`. HUD(#13)도 같이 씁니다.

## UI — 인게임 HUD (`UI_RaceHud`)

- 트랙 씬에 `Prefabs/UI/UI_RaceHud.prefab`을 1개 놓습니다. `RaceManager`는 비워두면 자동으로 찾습니다. 레이캐스터가 없어서 레이 클릭을 막지 않습니다.
- **카운트다운이 시작될 때** 플레이어 눈 위치에 배치되고(`PlayerSpace`, 결과 화면과 같은 방식), **플레이어가 완주하면 숨깁니다.** 결과 화면 버튼을 가리지 않게 하려는 것입니다.
- `RaceHud` — 대시보드: 눈에서 **0.85m, 20° 아래**, 시선에 수직([DEVICE.md](DEVICE.md) 2-1의 대시보드 자리). 720 × 170px(가로 약 46°)입니다.
  - 칸: 랩 `1/3`, 순위 `1위 /4`, 속도(km/h, `IKart.CurrentSpeed`), 시간(`RaceTime`, 0.1초 단위), 아이템 칸
  - 값이 바뀔 때만 텍스트를 갱신합니다(매 프레임 문자열 생성 X → GC 부담 없음).
  - 순위는 `RaceProgress.Rank`입니다(RaceManager가 0.2초마다 갱신하는 실시간 순위).
  - **아이템 칸은 비어 있습니다.** #5 `ItemHolder`가 나오면 `Dashboard/ItemSlot/Icon` 이미지에 연결합니다.
- `RaceMessages` — 가운데 메시지: 정면 **2m**, ±15° 안. `Pretendard-SemiBold SDF - Outline` 머티리얼(외곽선)이라 밝은 하늘 위에서도 읽힙니다.
  - 카운트다운 `3 · 2 · 1 · 출발!`(크게 떴다 작아짐), 랩 완료 시 `N랩  0:00.000` 2.5초, 마지막 랩 진입 시 `마지막 랩!`, 역주행 중 `역주행!`(빨강)
  - 게임 시간 기준이라 일시정지하면 메시지도 멈춥니다.
- ⚠️ **#2 조종석과 위치를 맞춰야 합니다.** 대시보드 자리(0.85m, 20° 아래)는 핸들·손과 겹칠 수 있습니다. 조종석이 나오면 `Content/Dashboard`의 위치·각도를 조정합니다(프리팹에서 바로 수정 가능).

### 한글 폰트 (TextMeshPro)

- **TMP 기본 폰트 = `Assets/_Project/Fonts/Pretendard-SemiBold SDF.asset`** (TMP Settings에서 지정). 새로 만드는 TMP 텍스트는 자동으로 이 폰트를 씁니다. 영문 대체 폰트(fallback)는 LiberationSans입니다.
- 한글 11,172자를 다 넣으면 에셋이 수십 MB가 되므로, **UI에 쓰는 글자만 넣은 정적(Static) 아틀라스**입니다. 실행 중에 에셋이 바뀌지 않아 git에 변경이 생기지 않습니다.
- 들어 있는 글자는 `Assets/_Project/Fonts/Pretendard_Characters.txt`입니다. ASCII 전체, 한글 100자, 결과·메뉴·설정·HUD에 쓸 단어가 들어 있습니다.
- ⚠️ **파일에 없는 한글을 쓰면 □로 나옵니다.** 새 문구를 쓸 때는:
  1. `Pretendard_Characters.txt`에 그 글자(단어)를 추가
  2. `Window > TextMeshPro > Font Asset Creator` — Source Font `Pretendard-SemiBold`, Sampling Point Size **Custom 48**, Padding **6**, Packing Optimum, Atlas **1024 x 1024**, Character Set **Characters from File** → 위 txt, Render Mode **SDFAA**
  3. **Generate Font Atlas → Save** 를 누르고 기존 `Pretendard-SemiBold SDF.asset`에 덮어쓰기 (GUID가 유지돼 프리팹 연결이 그대로)
- 원본 폰트: `Assets/ThirdParty/Fonts/Pretendard/Pretendard-SemiBold.otf` (+ `OFL.txt`). 원본은 수정하지 않습니다.
- 테스트 씬: `Scenes/Sandbox/Seunghee_UI.unity` — 작은 사각 코스(체크포인트 6개)에서 플레이어·AI 테스트 카트가 2랩을 돕니다. HUD가 보이다가 약 25초 뒤 결과 화면이 뜹니다.

**씬 전환 (`SceneLoader`)**: `Load(이름)`, `LoadMainMenu()`, `ReloadCurrent()`. 씬은 **Build Profiles의 Scene List에 등록돼 있어야** 로드됩니다(없으면 에러 대신 경고). 이름 상수는 `SceneLoader.MainMenu`, `TrackMain`, `TrackTest`입니다.

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```
