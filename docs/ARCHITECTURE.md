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
│  │  ├─ Track/                  # 트랙 중심선(TrackLayout), 도로 메시 생성, 에디터 버튼
│  │  ├─ AI/                     # AI 입력, 웨이포인트
│  │  ├─ UI/                     # 메뉴, HUD, 결과
│  │  └─ Audio/                  # 음악, 효과음, 엔진음, 충돌음·진동, 볼륨
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

## XR — 조종석 (`Kart_Player`)

`Scripts/XR/` (`VRKart.XR`), `Scripts/Kart/PlayerKartInput.cs`, `Prefabs/Kart/`

- `Kart.prefab` = 플레이어/AI 공용 카트(몸체 + Rigidbody + `KartController`, 아래 Kart 절). `Kart_Player.prefab` = 그 **변형(Variant)** 으로 조종석(`Cockpit`)과 `XR Origin (XR Rig)`, `PlayerKartInput`, `ViewRecenter`를 더한 것. AI 카트는 `Kart.prefab`을 씁니다.
- XR Origin은 카트 루트 → **`ViewPivot`**(좌석 눈 위치, `HorizonLock`) 아래에 있고, 카트 루트는 씬 **최상위**에 둡니다(UI `PlayerSpace` 약속 — `Camera.main.transform.root`는 그대로 카트 루트). XR Rig의 `Locomotion`(이동·회전·텔레포트)과 `CharacterController`는 꺼 둡니다. 카트가 움직이고, 캡슐 콜라이더가 카트 물리와 겹치기 때문입니다.
- `SteeringWheel` (`XRBaseInteractable`, Select Mode Multiple): Grip으로 한 손/두 손 잡기. 잡은 손이 핸들 축(`transform.forward`) 둘레로 돈 각도(두 손이면 평균)만큼 돌고 ±90°에서 멈춥니다. 놓으면 360°/초로 중앙 복귀. `Normalized` = -1(좌) ~ 1(우).
- `PlayerKartInput : IKartInput`: 핸들을 잡고 있으면 핸들 각도, 아니면 왼손 스틱(데드존 0.15)으로 조향. 버튼은 [DEVICE.md](DEVICE.md) 2-4 표대로 코드에서 바인딩합니다(`PauseMenu`와 같은 방식). `UseItem`은 `WasPressedThisFrame`이라 `Update`에서 읽어야 합니다.
- `ViewRecenter`: 시작 3프레임 뒤, 그리고 **왼손 Y**를 누를 때 머리를 `Cockpit/SeatEye`(눈 위치, forward = 카트 정면)로 옮깁니다. UI는 뜰 때의 머리 위치 기준이라 리센터해도 다시 배치되지 않습니다.
- 차량 외형·실내(핸들 모양, 계기판, 대시보드)는 아래 [차량 비주얼 규칙](#차량-비주얼-규칙-40)을 따릅니다.
- **PC 운전 모드** (에디터·PC 빌드만, `#if UNITY_EDITOR || UNITY_STANDALONE`): `Race/Testing/DesktopDriveController`가 실행 시 자동으로 생기고, 헤드셋이 없으면(`XRSettings.isDeviceActive == false`) `Core/DesktopDriveMode.Active`를 켜고 **XR Interaction Simulator를 끕니다**(R = 기기 리셋, Shift = 왼손 전환 등 키가 겹쳐서). `PlayerKartInput`은 이 모드일 때만 키보드를 함께 읽습니다: T 가속, Shift 브레이크(멈추면 0), R 후진(= 계속 브레이크), J/L 조향, Space 드리프트, E 아이템. P 일시정지, F1 모드 전환. 키 표는 README.
- 테스트 씬: `Scenes/Sandbox/Changmin_Steering.unity`. XR Interaction Simulator에서 `]`(오른손 선택) → `G`(잡기) + `Q`/`E`(위아래 이동)로 핸들 회전, `Shift+2` = 왼손 Y(리센터).

### 멀미 저감 (`ComfortSettings`, #6)

- **`ComfortSettings`** (static, `VRKart.XR`): `VignetteEnabled`(기본 켬), `VignetteIntensity`(0~1, 기본 0.6), `HorizonLock`(기본 켬), `ShowPresetLabel`(기본 끔), `event Changed`, `Save()`(PlayerPrefs `settings.vignette.enabled`, `settings.vignette.intensity`, `settings.horizonLock`, `settings.comfort.showLabel`). 설정 UI(#12)는 setter만 부르고 화면을 닫을 때 `Save()`를 한 번 부릅니다.
- **프리셋** (멀미 평가 #46): `ApplyPreset(ComfortPreset.Off | On)` — Off = 비네팅·수평 유지 모두 끔, On = 모두 켬. `CurrentPreset`은 둘 중 하나라도 켜져 있으면 On.
- **`ComfortVignette`** (`Kart_Player` 루트): 메인 카메라 자식 `ComfortVignette` 반구(XRI 샘플 `VR/TunnelingVignette` 셰이더, `Materials/XR/Comfort_Vignette.mat`)의 조리개를 조절합니다. **직진 속도**(최고 속도의 15%부터)와 **회전 속도**(20~90°/s) 중 큰 쪽 × 강도 × 0.45만큼 닫힘. 0.3초에 닫히고 0.6초에 걷힘, 일시정지 중에는 걷힘.
- **`HorizonLock`** (`ViewPivot`): 켜져 있으면 카트의 앞뒤·좌우 기울기를 상쇄하고 방향(요)만 따라갑니다 → 경사·점프대에서 지평선이 기울지 않음. 조종석은 카트와 함께 기울어 보입니다. `ViewRecenter`는 `ViewPivot`의 위쪽을 기준으로 맞춥니다.
- **`ComfortPresetToggle`** (`Kart_Player` 루트): **왼손 X 1.5초**(PC 운전 모드 `F2` 1.5초)로 프리셋 켬 ↔ 끔 + 저장. 전환 후 3초 동안 시야 위쪽에 `COMFORT ON/OFF` 표시(메인 카메라 자식 `ComfortLabel`). `ShowPresetLabel`이 켜져 있으면 계속 표시.
- ⚠️ 비네팅이 강하면 대시보드 양옆 HUD 화면을 덮을 수 있습니다. 기본 강도는 실기기에서 확정합니다(#15, #46).

## 차량 비주얼 규칙 (#40)

조종석은 **닫힌 승용차 실내**(그란투리스모 실내 시점 구도), AI 차량은 **레이스 리버리를 입힌 투어링카**(GRID 구도)입니다. 참고 게임의 모델·로고는 쓰지 않고, Kenney Car Kit(CC0) + 직접 만든 메시로 만들었습니다. 결정 배경과 체크리스트는 이슈 #40에 있습니다.

**꼭 지킬 것** (Claude로 수정할 때도 먼저 읽기)

1. **비주얼과 물리는 분리.** 차 모델·실내·번호·브레이크등은 콜라이더 없는 자식입니다. `Kart.prefab` 루트의 `Rigidbody`, `BoxCollider`(1.2 x 0.3 x 2m), `CapsuleCollider`, `KartController` 값은 비주얼 작업에서 바꾸지 않습니다.
2. 로우폴리 + 단색/팔레트 텍스처(Kenney 스타일). 실시간 반사(거울 카메라), 고해상도 텍스처는 Quest 성능 때문에 쓰지 않습니다(룸미러·사이드미러는 반사 없는 금속 머티리얼).
3. 외부 모델은 `ThirdParty/Kenney/CarKit/` 원본 그대로. 색·번호는 `_Project`의 머티리얼/프리팹에서 바꿉니다.
4. `Cockpit/SeatEye`(0, 1.05, -0.35), `ViewRecenter`, `SteeringWheel`의 **잡기 콜라이더·회전 로직·`_visual` 연결은 그대로.** 핸들은 `SteeringWheel/Visual` 아래 **모양만**(링 `Rim` + `Spoke` + `BottomSpoke` + `Hub`).
5. 실내 부품은 **핸들 위쪽 틈 → 계기판**(눈 기준 약 13~24° 아래, 가운데)과 **앞유리 시야(수평 ~ 12° 아래)** 를 가리지 않습니다. A필러는 6cm 이하.
6. 실내는 `Prefabs/Kart/Cockpit_Interior.prefab` 하나로, `Kart_Player/Cockpit` 아래에 붙어 있습니다. 실내 수정은 이 프리팹에서 합니다(`Kart_Player` 충돌 방지).
7. HUD 대시보드(`DashLeft`/`DashRight`)는 실내의 `CockpitHudAnchors` 자리(`HudAnchor_Left`/`Right`)에 붙습니다. 화면 위치·크기를 바꾸려면 **앵커만** 옮기거나 스케일을 바꿉니다.
8. 계기판·바퀴·브레이크등 스크립트는 **읽기만** 합니다(`IKart.CurrentSpeed`, 같은 카트의 `IKartInput.Steer`/`Brake`). 입력·물리에 쓰지 않습니다.
9. AI 차량은 `Kart.prefab`의 **Variant**(`Kart_AI_*.prefab`)입니다. 박스 차체·좌석·실린더 바퀴(`Body`, `Seat`, `Wheels`)는 꺼 두고, 자식 `Car`(Car Kit 모델, **1.15배**)를 보여 줍니다.
10. 리버리 = 머티리얼(팔레트에서 차체 색 칸만 바꾼 텍스처) + 레이스 번호(TextMeshPro). 모델 메시는 수정하지 않습니다.
11. 씬의 AI 1~3에는 `RaceProgress`를 **씬에서** 추가합니다(프리팹에 넣지 않음). `AIKartInput`은 프리팹에 들어 있고 값(속도·라인)은 씬에서 덮어씁니다.
12. 드리프트 이펙트(#4)·아이템(#5)을 카트에 붙일 때는 `Car`와 `Cockpit_Interior`를 건드리지 말고 **형제 오브젝트**로 추가합니다.

**구성**

| 대상 | 내용 |
| --- | --- |
| `Kart_Player` | `Body`·`Wheels` 꺼짐(`Seat`는 유지). `Car` = 세단 외형 **그림자 전용**(`ShadowsOnly` — 안에서 보면 창문 면이 시야를 가려서) + 보이는 `Hood`(파랑 보닛). `Cockpit/Cockpit_Interior`. 핸들 모양 교체 |
| `Cockpit_Interior` | 대시보드(돌출 메시), 계기판 `GaugeCluster`(속도계 0~120km/h, 회전계 0~8천rpm·7천부터 레드존, 디지털 속도, 단수), 송풍구, 대시 장식 라인, HUD 화면 앵커 2개, 센터 콘솔·기어 레버, 도어 패널·창틀·팔걸이, A/B/C필러, 지붕, 룸미러, 사이드미러, 바닥·페달. 메시는 `Models/Cockpit/Cockpit_Meshes.asset`(핸들 링, 원판, 계기 테두리, 대시보드, 보닛) |
| `Kart_AI_Sedan_Red` | `sedan-sports`, 빨강, **7** — 씬의 `AI 1` |
| `Kart_AI_Hatch_Yellow` | `hatchback-sports`, 노랑, **22** — 씬의 `AI 2` |
| `Kart_AI_Sedan_Green` | `sedan-sports`, 초록, **31** — 씬의 `AI 3` |

- 플레이어 리버리는 파랑(`Car_Livery_Player_Blue`, 보닛 `Car_Paint_Player_Blue`)입니다.
- `CarVisual` (`Scripts/Kart/`, `Car`에 붙음): 바퀴 피벗(`wheel-*_pivot`)을 속도만큼 굴리고 앞바퀴를 조향 × 25° 꺾음, 브레이크 입력 > 0.1이면 브레이크등(`BrakeLight` 쿼드, 원래 미등 위치)을 밝은 빨강으로. `MaterialPropertyBlock`(`_BaseColor`)이라 머티리얼을 복제하지 않습니다.
- **빛나 보이는 머티리얼은 URP Unlit**(`Cockpit_Needle`, `Cockpit_GaugeMark`, `Cockpit_Redline`, `Car_BrakeLight`). URP Lit의 Emission은 에디터가 저장할 때 `_EMISSION` 키워드를 빼버리는 일이 반복돼서 쓰지 않습니다(Quest에는 Bloom도 없어서 차이 없음).
- `CockpitGauges` (`Scripts/UI/`, `Cockpit_Interior`에 붙음): 바늘은 로컬 Z로 260° 시계 방향. 회전수·단수는 변속기가 없어서 속도 구간(최고 속도의 18/34/52/70/86/100%)으로 흉내 냅니다. 후진 `R`, 정지 `N`.
- **새 AI 리버리 추가**: `Textures/Cars/Car_Livery_*.png`(원본 `colormap.png`에서 차체 칸 (6,1)·(3,1) 색만 바꾼 512px 팔레트, Point 필터) + `Materials/Cars/Car_Livery_*.mat` → `Kart_AI_*` Variant를 복제해서 `Car` 렌더러 머티리얼과 `RaceNumber/Number` 텍스트(도어 2 + 지붕 1)를 바꿉니다.
- ⚠️ 차 모델(1.15배)은 콜라이더(길이 2m)보다 앞뒤로 약 0.45m 깁니다. 부딪힐 때 겹쳐 보이면 콜라이더를 차 크기에 맞출지 #3/#15에서 결정합니다(물리 변경이라 이창민 담당).

## Kart — 주행 (`KartController`)

`Scripts/Kart/` (`VRKart.Kart`), `Prefabs/Kart/Kart.prefab`, 튜닝 값 `Prefabs/Kart/KartStats_Default.asset`

- `KartController : IKart`는 카트 루트에 붙고, **같은 게임오브젝트의 `IKartInput`** (`PlayerKartInput` 또는 `AIKartInput`)을 `Awake`에서 찾아 `FixedUpdate`마다 읽습니다. AI 카트는 `Kart.prefab` 루트에 `AIKartInput`만 붙이면 됩니다.
- 트랙 씬에서는 카트 루트에 **`RaceProgress`를 추가**합니다(프리팹에는 없음 — `RaceTrack`이 없는 씬에서 에러가 나기 때문).
- 구조: Rigidbody(150kg, 회전 잠금) + 지면에서 0.15m 떠 있는 콜라이더(바닥 캡슐 + 몸통 박스, 마찰 0). **높이는 앞/뒤 두 레이의 스프링 서스펜션**이 유지해서, 점프대 입구·작은 턱에 걸리지 않고 착지 충격도 흡수합니다.
- 회전은 물리에 맡기지 않습니다: 요 = 조향, 기울기 = 지면 법선(부드럽게 따라감) → **뒤집히지 않습니다.** 지면 위에서는 속도를 진행 방향으로 덮어써서 옆·경사 미끄러짐이 없습니다.
- 벽·카트에 막히면 실제로 움직인 만큼으로 속도가 줄어듭니다(정면 충돌 ≈ 0).
- 노면: 레이가 맞은 콜라이더가 `Grass` 레이어면 최고 속도 × `OffRoadSpeedFactor`(0.5).
- `SetControlEnabled(false)`: 속도 0으로 잡아 두고 밀리지 않음(카운트다운). `Respawn(pose)`: 위치·회전 적용, 속도·각속도 0.
- `ApplyBoost(power, duration)`: **power = 최고 속도에 더하는 비율**(0.3 → +30%). 겹치면 큰 값. `SpinOut()`: 속도 × 0.3, 1.2초 조작 불가. 멀미(NFR-03) 때문에 카트를 빙글 돌리지 않습니다.
- 추가 조회용: `IsGrounded`, `IsOffRoad`, `IsBoosting`, `IsSpinningOut`, `IsControlEnabled`, `IsDrifting`, `DriftDirection`, `Stats` (HUD·사운드·이펙트용). 이벤트 `SpunOut()`: 바나나·쉘 피격 (소리·진동용)
- 이벤트 `BoostStarted(power, duration)`: **모든 부스트**(미니 터보, 대시 패드, 아이템)가 걸릴 때마다. 부스트 불꽃·소리는 여기에 붙이면 됩니다.
- 기본 튜닝: 최고 20 m/s(72 km/h), 가속 10 m/s², 후진 최고 6 m/s, 회전 110°/s(저속) → 60°/s(최고 속도).
- 테스트 씬 `Changmin_Steering`: 도로 바닥, 잔디(`Grass_Patch`), `Track_JumpRamp`, 벽, 경사, `DashPad`(z 22). PC 운전 모드(헤드셋 없을 때 자동) `T` 가속 · `J`/`L` 조향 · `Space` 드리프트, F1 → 시뮬레이터 `]` → `G`+`Q`/`E`(핸들).

### 드리프트 & 미니 터보 (`DriftBoost`, #4)

- `Kart.prefab` 루트에 붙어 있습니다(플레이어·AI 공통, AI는 `Drift = false`라 드리프트하지 않음). 판정·충전·이벤트는 `DriftBoost`, 회전·미끄러짐 물리는 `KartController`(`StartDrift`/`StopDrift`)가 맡습니다.
- **시작**: 드리프트 버튼(A / PC `Space`) + 조향 `|Steer| ≥ 0.3` + 속도 8 m/s 이상 + 지면 위. 그때 조향 방향으로 **방향 고정**.
- **드리프트 중**: 고정된 방향으로만 돕니다. 안쪽으로 꺾으면 회전 × 1.4, 바깥쪽으로 꺾으면 × 0.6. 진행 방향이 차 앞보다 **바깥으로 12°** 미끄러집니다(멀미 때문에 작게, `DriftSlipAngle`로 조절. 0이면 미끄러짐 없음).
- **충전**: 지면 위에서 1초 → 1단, 2.2초 → 2단(공중에서는 충전 안 됨).
- **해제**(버튼을 뗌): 1단 = +20% 0.7초, 2단 = +30% 1.3초 부스트. 0단이면 부스트 없음. 속도가 4 m/s 아래로 떨어지거나 스핀아웃·조작 잠금·리스폰이면 부스트 없이 끝납니다.
- **이벤트** (`DriftBoost`)

| 이벤트 | 시점 |
| --- | --- |
| `DriftStarted(int direction)` | 드리프트 시작. 1 = 오른쪽, -1 = 왼쪽 |
| `DriftEnded()` | 드리프트 끝 (부스트 여부와 상관없이) |
| `BoostLevelChanged(int level)` | 0 → 1 → 2, 끝나면 0 (불꽃 색) |
| `BoostFired(int level, float duration)` | 미니 터보 발동 |

- **이펙트 위치**: `Kart.prefab/DriftFX/RearLeft`, `RearRight` (빈 오브젝트, 뒷바퀴 바닥). [차량 비주얼 규칙](#차량-비주얼-규칙-40) 12대로 `Car`·실내와 형제입니다. AI 차 모델은 1.15배라 위치가 다르면 Variant에서 옮기면 됩니다.
- 튜닝 값은 `KartStats`의 "드리프트" 항목.

### 대시 패드 (`Prefabs/Items/DashPad.prefab`, #4)

- `Scripts/Items/DashPad.cs` (`VRKart.Items`): 트리거에 들어온 카트(`GetComponentInParent<IKart>()`)에 `ApplyBoost(0.35, 1.2)` → 최고 속도 +35% 1.2초. 카트 콜라이더가 2개라 같은 카트는 0.5초 안에 다시 부스트하지 않습니다.
- 크기: **가로 4m × 길이 3m** (도로 폭 10m에 한 줄 2개까지), 트리거 높이 1m. 루트·자식 모두 **`Ignore Raycast`** 레이어라 카트 지면 레이에 걸리지 않고, 보이는 판(주황 + 노란 화살표 2개, URP Unlit)은 콜라이더가 없습니다.
- 배치: 프리팹의 **+Z(화살표 방향) = 진행 방향**으로 도로 면(y = 도로 높이)에 놓습니다.
- 소리·이펙트는 카트 쪽 `KartController.BoostStarted`로 받으면 대시 패드·미니 터보·아이템을 한 곳에서 처리할 수 있습니다.

## Items — 아이템 (#5)

`Scripts/Items/` (`VRKart.Items`), `Prefabs/Items/`

| 구성 | 내용 |
| --- | --- |
| `ItemHolder` | **카트 루트**(`Kart.prefab`, 플레이어·AI 공통). 아이템 1개 보유. `CurrentItem`, `ItemChanged(ItemType)`(획득·사용, 사용하면 `None`), `ItemUsed(ItemType)`, `TryGive(item)`(들고 있으면 false). 같은 카트의 `IKartInput.UseItem`(플레이어 B / PC `E`)이 눌리면 사용. **조작 잠금(카운트다운)·스핀아웃 중에는 쓰지 않습니다.** |
| `ItemBox.prefab` | 트리거 1.8m 정육면체(높이 0.9m 중심), 보이는 상자 0.8m가 돎. 지나간 카트가 **빈손이면** 아이템 1개 → 상자가 사라졌다가 **3초 뒤** 다시 생김. 이미 들고 있으면 무시(상자도 그대로). `PickedUp(ItemHolder, ItemType)` 이벤트 |
| 부스터 | `ApplyBoost(0.4, 1.5)` — 최고 속도 +40% 1.5초 |
| `Banana.prefab` | 카트 1.6m 뒤 바닥에 놓임. 밟은 카트 `SpinOut` 후 사라짐. 놓은 카트는 1초 면제. 60초 뒤 자동 삭제 |
| `Shell.prefab` | 카트 1.8m 앞에서 **35 m/s 직진**, 지면 0.3m 위를 따라감. **`Wall` 레이어에서 1회 반사**, 두 번째 벽이면 사라짐. 맞은 카트 `SpinOut`. 쏜 카트 0.4초 면제, 6초 뒤 삭제. 트랙 밖으로 나가면 떨어짐 |
| `ItemRoll` | 뽑기 확률(FR-ITEM-04). `IRaceParticipant.Rank`와 참가자 수(`RaceManager.GetResults().Count`, 획득할 때만 셈)로 1등 ↔ 꼴찌 사이를 보간. 레이스 밖이면 중간 값 |

**확률** (1등 → 꼴찌로 선형 보간)

| | 부스터 | 쉘 | 바나나 |
| --- | --- | --- | --- |
| 1등 | 15% | 40% | 45% |
| 중간 | 38% | 34% | 27% |
| 꼴찌 | 60% | 30% | 10% |

- 아이템 박스·바나나·쉘은 모두 **`Ignore Raycast`** 레이어 → 카트 지면 레이에 안 걸림. 보이는 부분은 콜라이더 없음.
- 카트 콜라이더가 2개라 트리거가 같은 스텝에 두 번 들어옵니다. 바나나·쉘은 첫 번째만 처리합니다(`SpunOut` 1회).
- **배치(#8)**: 아이템 박스는 도로 폭 10m에 **2.5m 간격으로 한 줄 3~4개**. 프리팹 원점 = 바닥.
- **HUD(#13)**: `RaceManager.Player.GetComponent<ItemHolder>()` → `CurrentItem` + `ItemChanged`.
- **AI(#11, P2)**: AI 카트에도 `ItemHolder`가 있어 박스를 지나면 아이템을 받습니다. `AIKartInput.UseItem`을 `true`로 한 프레임 주면 사용합니다(지금은 항상 `false`라 들고만 있음).
- **사운드(#14)**: `ItemBox.PickedUp`(획득), `ItemHolder.ItemUsed`(사용), `KartController.SpunOut`(피격), `KartController.BoostStarted`(부스터).

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
| `UI/UI_ResultScreen`, `UI/UI_RaceHud`, `UI/UI_PauseMenu`, `UI/EventSystem` | 결과 화면, 인게임 HUD, 일시정지 메뉴, XR 레이 UI 입력(`XRUIInputModule`). 아래 [UI](#ui--결과-화면-ui_resultscreen) 절 |
| `Kart_Player` | 플레이어 카트(`Prefabs/Kart/Kart_Player.prefab`, 조종석 + XR Origin 포함). **씬 최상위**, `StartPos_1` 위치·회전. 프리팹에 없는 **`RaceProgress`를 씬에서 추가**했고, `RaceManager`의 Player가 이것입니다. |

- 1랩 약 876m, 폭 10m 도로. 카트 크기(약 1.2m x 2m) 기준으로 나란히 4~5대가 달릴 수 있는 폭입니다.
- XR Origin은 `Kart_Player` 안에 있는 것 하나뿐입니다(MainCamera 1개).
- `AIPath`(웨이포인트 146개, 6m 간격)와 `AI 1`~`AI 3`(`Kart.prefab` + `RaceProgress` + `AIKartInput`, `StartPos_2~4`)이 있습니다. 아래 [AI 카트](#ai-카트-aikartinput) 절을 보세요.
- 트랙 조각은 모두 `Static`이라 빌드 시 Static Batching으로 합쳐집니다 (NFR-01 드로우콜 예산).

## 메인 트랙 (`Track_Main.unity`)

`Track_Test`를 복사해서 트랙 부분만 바꾼 씬입니다. 레이스 오브젝트(`RaceManager`, `UI`, `Kart_Player`, `AI 1~3`, 출발선·출발 위치)는 `Track_Test`와 같은 구성·같은 위치입니다(결승선 = (0, 0, 60), +Z 방향).

- 1랩 **약 1,031m**, 폭 10m 도로 + 갓길 3m + 벽(높이 1m). 평지(점프대 없음 — VR 멀미).
- 코스: 메인 직선(약 210m) → 1번 코너(오른쪽 90°, R20) → S자 → 오른쪽 90° 두 번 → **헤어핀(왼쪽 180°, R12)** → 큰 스위퍼(오른쪽 180°, R54) → 뒷 직선(141m) → 마지막 코너(R30). 시계 방향.
- AI 3랩 기록(자동 주행 테스트): 1위 약 185초(랩 61~63초), 4대 모두 완주, 갓길 주행 0%, 리스폰 0회.

**구성**

| 오브젝트 | 설명 |
| --- | --- |
| `Track` | **`TrackLayout`**(중심선) + **`TrackMeshBuilder`**(도로 메시). 아래 참고 |
| `Track/* (generated)` | 도로(`Road`), 흰 가장자리 선, 커브 연석(빨강/흰), 갓길(`Grass`), 벽(`Wall`). **씬에 저장되지 않고** 켜질 때마다 만들어집니다(`HideFlags.DontSave`) |
| `Props/Track_StartLine`, `Props/Track_StartGrid` | `Track_Test`와 같은 위치 |
| `Environment/Ground_Grass` | 1200m x 1200m 바닥 (`Grass`) |
| `Environment/Scenery` | Kenney 모델 장식 (약 830개, 모두 Static): 결승선 게이트, 관중석 10, 피트 6, 조명탑, 깃발, 코너 배너 탑, 광고판, 텐트, 나무 320, 바위, 덤불, 꽃, 풀, 통나무. 모두 벽에서 떨어져 있어 주행에 닿지 않습니다 |
| `RaceTrack/Checkpoint_00~40` | 체크포인트 41개 (약 25m 간격, 버튼으로 배치) |
| `AIPath/WP_000~171` | AI 웨이포인트 172개 (약 6m 간격, 버튼으로 배치) |
| `KillZone` | 바닥 아래 1300m x 1300m |

- 조명: 하늘 = Poly Haven HDRI(`Materials/Environment/Sky_PartlyCloudy.mat`), 환경광 = 3색(Trilight) 고정값, 거리 안개(150~550m, 먼 바닥 끝을 가림).

**트랙 모양 바꾸기 (`TrackLayout`)**

- `Pieces` 배열 = 출발점(이 오브젝트 위치·방향)부터 이어 붙이는 조각. `Turn` 0이면 길이 `Length`의 직선, 아니면 반지름 `Radius`로 `Turn`°만큼 도는 원호(+ 오른쪽, - 왼쪽).
- **각도 합은 360°**, 끝점이 시작점으로 돌아와야 합니다. 1m 넘게 어긋나면 콘솔에 경고가 뜹니다(작은 오차는 전체에 나눠서 자동으로 닫음).
- 값을 바꾸면 도로·벽이 바로 다시 만들어집니다. 그다음 `TrackLayout` 인스펙터 아래 버튼을 누릅니다:
  - **`RaceTrack 체크포인트 다시 배치`**: `Finish Distance`(결승선)부터 간격대로 체크포인트를 새로 놓음 (0번 = 결승선)
  - **`AIPath 웨이포인트 다시 배치`**: 중심선을 따라 웨이포인트를 새로 놓음
- 결승선(`Finish Distance`)을 옮기면 출발선·출발 위치·카트 4대도 직접 옮겨야 합니다. 장식(`Scenery`)은 자동으로 따라오지 않으니 도로와 겹치는 것을 지웁니다.
- 반지름은 도로 중심 기준입니다. **반지름 12m 미만은 피하세요**(안쪽 갓길·벽이 겹침). 평행한 구간은 중심선끼리 **24m 이상** 떨어뜨립니다(벽 바깥끼리 7m).
- `TrackMeshBuilder`: 폭(도로 10, 연석 1, 갓길 3, 벽 두께 0.5·높이 1m)과 머티리얼(`Track_Road`, `Track_LineWhite`, `Track_Curb`, `Track_Runoff`, 벽 = `Track_Curb`). 레이어는 도로·선·연석 `Road`, 갓길 `Grass`, 벽 `Wall`.
- 대시 패드·아이템 박스(#4·#5)가 나오면 `Track_Main`의 직선 구간(메인 직선, 뒷 직선)에 놓습니다.

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

## AI 카트 (`AIKartInput`)

`Scripts/AI/` (`VRKart.AI`)

- AI 카트 = `Prefabs/Kart/Kart_AI_*.prefab`(`Kart.prefab`의 Variant + 차 외형 + `AIKartInput`, [차량 비주얼 규칙](#차량-비주얼-규칙-40)) + 씬에서 **`RaceProgress`를 추가**. `KartController`가 같은 오브젝트의 `IKartInput`으로 읽습니다(#3에서 합의).
- **`WaypointPath`**: 자식 Transform들이 순서대로 웨이포인트이고, 마지막 점은 첫 점으로 이어진 닫힌 경로입니다. 씬 뷰에 주황 선으로 보입니다. `Track_Test`의 `AIPath`는 도로 중심선을 6m 간격으로 딴 것입니다. 메인 트랙(#8)에서는 코스에 맞게 새로 놓습니다.
- **`AIKartInput`** (인스펙터 값)

| 묶음 | 값 | 설명 |
| --- | --- | --- |
| 실력 | `Speed Factor` (0.5~1) | 최고 속도 배율. **AI마다 다르게 해서 순위가 섞이게** (FR-AI-02) |
| | `Line Offset` (m) | 도로 중심에서 오른쪽(+)/왼쪽(-)으로 달리는 라인. 카트끼리 한 줄로 겹치지 않게 |
| | `Line Wobble` (m) | 천천히 흔들리는 좌우 오차 = 라인 실수 |
| 조향 | `Look Ahead Base / Per Speed` | 경로를 따라 `기본 + 속도 × 계수`(m) 앞 지점을 보고 핸들을 꺾음. `Full Steer Angle`(30°) 이상이면 끝까지 |
| 코너 | `Corner Look Ahead`(25m), `Min Corner Speed Factor`(0.55), `Sharp Corner Angle`(90°) | 앞 25m 안에서 가장 크게 꺾이는 각도에 비례해 목표 속도를 낮춤 |
| 끼임 | `Stuck Speed`(1m/s), `Stuck Time`(1.5s), `Reverse Time`(1.2s), `Reverse Tries Before Respawn`(2) | 레이스 중 거의 못 움직이면 후진하며 방향을 틀고, 두 번 실패하면 `IKart.Respawn(LastCheckpointPose)` (FR-AI-03) |

- `Track_Test` 설정: `AI 1` 0.96 / +1.5m, `AI 2` 0.92 / -1.5m, `AI 3` 0.88 / 0m. 플레이어 카트는 최고 속도 20m/s(`KartStats_Default`)입니다.
- 완주한 AI는 목표 속도 60%로 계속 달립니다(멈춰서 뒤 카트를 막지 않게).
- 아이템 사용(FR-ITEM-05, P2)은 #5 이후에 붙입니다(`UseItem`은 지금 항상 false).

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
- `RaceHud` — 대시보드: **두 패널**. 플레이어 카트에 `CockpitHudAnchors`가 있으면(지금 `Kart_Player`) **카운트다운 때 조종석 대시보드 화면 자리로 옮겨 붙습니다**(`HudAnchor_Left`/`Right`: 핸들 양옆 대시보드 면, 눈 기준 약 ±27°·25° 아래, 0.55배 크기). 옮긴 뒤에도 보이기/숨기기(일시정지, 완주)는 그대로입니다. 앵커가 없는 카트에서는 아래처럼 눈에서 0.85m, **8° 아래, 좌우 ±28°** 에 둡니다.
  - `Content/DashLeft`(240 × 360px): 랩 `1/3`, 순위 `1위 /4`, 아이템 칸
  - `Content/DashRight`(240 × 250px): 속도(km/h, `IKart.CurrentSpeed`), 시간(`RaceTime`, 0.1초 단위)
  - (앵커가 없을 때 기준) 왜 양옆인가: 예전 `Kart_Player`의 핸들(`Cockpit/SteeringWheel`, 꽉 찬 원판 — #40에서 링 모양으로 교체)이 눈에서 0.5m 앞 **약 8~43° 아래·좌우 ±22°**를 가립니다. 그래서 [DEVICE.md](DEVICE.md) 2-1의 대시보드 자리(15~30° 아래 가운데)가 통째로 핸들 뒤였습니다. 높이 8~15°, 좌우 20~34°를 시험해서 **핸들 모양에 한 점도 가리지 않으면서 가장 안쪽**(가로 19.6°~36.4°)인 자리를 골랐습니다.
  - 핸들·조종석 모양이 바뀌면 이 자리도 다시 확인해야 합니다. 실제로 핸들을 잡은 손이 패널 안쪽 아래 모서리를 가리는지는 실기기에서 확인이 필요합니다.
  - 값이 바뀔 때만 텍스트를 갱신합니다(매 프레임 문자열 생성 X → GC 부담 없음).
  - 순위는 `RaceProgress.Rank`입니다(RaceManager가 0.2초마다 갱신하는 실시간 순위).
  - **아이템 칸은 비어 있습니다.** #5 `ItemHolder`가 나오면 `DashLeft/ItemSlot/Icon` 이미지에 연결합니다.
- `RaceMessages` — 가운데 메시지: 정면 **2m**, ±15° 안. `Pretendard-SemiBold SDF - Outline` 머티리얼(외곽선)이라 밝은 하늘 위에서도 읽힙니다.
  - 카운트다운 `3 · 2 · 1 · 출발!`(크게 떴다 작아짐), 랩 완료 시 `N랩  0:00.000` 2.5초, 마지막 랩 진입 시 `마지막 랩!`, 역주행 중 `역주행!`(빨강)
  - 게임 시간 기준이라 일시정지하면 메시지도 멈춥니다.
- 일시정지 중에는 HUD를 숨깁니다(일시정지 메뉴를 가리지 않게). 재개하면 같은 자리에 다시 보입니다.

## UI — 메인 메뉴 · 일시정지 (`UI_MainMenu`, `UI_PauseMenu`)

- **`Scenes/MainMenu.unity`**: XR Origin, `EventSystem` + `XRUIInputModule`, 바닥, `UI_MainMenu`
  - `MainMenu` — `시작` → `Race Scene`(`Track_Main`) / `설정` → 볼륨 슬라이더 3개(전체·배경음악·효과음) / `종료`
  - 씬 시작 한 프레임 뒤(XR 트래킹이 잡힌 뒤) 플레이어 정면 1.5m에 놓입니다(`PlayerSpace`).
  - 제목 `VR 카트 레이싱`은 임시입니다. 닌텐도 IP 규칙([ASSETS.md](ASSETS.md)) 때문에 "마리오"는 쓰지 않았습니다. 프리팹 `MainPanel/Title` 텍스트에서 바꿉니다.
- **설정** (`PlayerPrefs`): `GameSettings.MasterVolume`(= `AudioListener.volume`), `AudioVolumes.Music`·`AudioVolumes.Sfx`(아래 사운드 절). 게임 시작 시 저장된 값을 적용하고, 설정 화면을 닫을 때 저장합니다.
  - **비네팅 on/off·강도는 #6 `ComfortSettings`(이창민)가 나오면** 설정 화면에 연결합니다. 멀미 옵션은 `ComfortSettings`가 저장까지 맡습니다.
- **`UI_PauseMenu`** (트랙 씬에 1개): **왼손 컨트롤러 메뉴(≡) 버튼**으로 `RaceManager.TogglePause()`를 부릅니다(XR 시뮬레이터에서도 왼손 컨트롤러의 menu 버튼으로 동작). `PauseChanged`에 따라 정면 1.5m, **눈높이 +0.08m**에 `계속 / 다시 시작 / 메뉴로`를 띄웁니다. 결과 화면은 +0.12m. 조종석 앞유리 안(눈 기준 약 10° 아래 ~ 19° 위)에 패널 전체가 들어오게 한 값입니다(그보다 낮으면 대시보드에 가려짐).
  - XRI 기본 입력 액션에는 메뉴 버튼이 없어서, `PauseMenu`가 `<XRController>{LeftHand}/{MenuButton}` 바인딩을 직접 만듭니다.
- 빌드 씬 목록: `MainMenu`를 **맨 끝에 추가만** 했습니다. **빌드 첫 씬(0번)은 아직 CI용 `Changmin_Setup`**이라, APK를 실행하면 메뉴가 아니라 그 씬이 먼저 뜹니다. 첫 씬을 `MainMenu`로 바꿀지는 이창민과 합의가 필요합니다.

### 한글 폰트 (TextMeshPro)

- **TMP 기본 폰트 = `Assets/_Project/Fonts/Pretendard-SemiBold SDF.asset`** (TMP Settings에서 지정). 새로 만드는 TMP 텍스트는 자동으로 이 폰트를 씁니다. 영문 대체 폰트(fallback)는 LiberationSans입니다.
- 한글 11,172자를 다 넣으면 에셋이 수십 MB가 되므로, **UI에 쓰는 글자만 넣은 정적(Static) 아틀라스**입니다. 실행 중에 에셋이 바뀌지 않아 git에 변경이 생기지 않습니다.
- 들어 있는 글자는 `Assets/_Project/Fonts/Pretendard_Characters.txt`입니다. ASCII 전체, 한글 103자, 결과·메뉴·설정·HUD에 쓸 단어가 들어 있습니다.
- ⚠️ **파일에 없는 한글을 쓰면 □로 나옵니다.** 새 문구를 쓸 때는:
  1. `Pretendard_Characters.txt`에 그 글자(단어)를 추가
  2. `Window > TextMeshPro > Font Asset Creator` — Source Font `Pretendard-SemiBold`, Sampling Point Size **Custom 48**, Padding **6**, Packing Optimum, Atlas **1024 x 1024**, Character Set **Characters from File** → 위 txt, Render Mode **SDFAA**
  3. **Generate Font Atlas → Save** 를 누르고 기존 `Pretendard-SemiBold SDF.asset`에 덮어쓰기 (GUID가 유지돼 프리팹 연결이 그대로)
- 원본 폰트: `Assets/ThirdParty/Fonts/Pretendard/Pretendard-SemiBold.otf` (+ `OFL.txt`). 원본은 수정하지 않습니다.
- 테스트 씬: `Scenes/Sandbox/Seunghee_UI.unity` — 작은 사각 코스(체크포인트 6개)에서 플레이어·AI 테스트 카트가 2랩을 돕니다. HUD가 보이다가 약 25초 뒤 결과 화면이 뜹니다.

**씬 전환 (`SceneLoader`)**: `Load(이름)`, `LoadMainMenu()`, `ReloadCurrent()`. 씬은 **Build Profiles의 Scene List에 등록돼 있어야** 로드됩니다(없으면 에러 대신 경고). 이름 상수는 `SceneLoader.MainMenu`, `TrackMain`, `TrackTest`입니다.

## 사운드 & 진동 (#14)

`Scripts/Audio/` (`VRKart.Audio`). 파일은 `ThirdParty/Kenney/InterfaceSounds`, `ImpactSounds`, `ThirdParty/OpenGameArt/EngineLoops`, `HyperflightRacing`(전부 CC0, [ASSETS.md](ASSETS.md) 등록부).

| 컴포넌트 | 붙는 곳 | 역할 |
| --- | --- | --- |
| `AudioVolumes` | (static) | 배경음악·효과음 볼륨 0~1, `PlayerPrefs`(`settings.musicVolume`, `settings.sfxVolume`). 바뀌면 `Changed` |
| `MusicPlayer` | 씬의 `Audio` | BGM 루프(2D, Streaming). 레이스 씬: 일시정지 중 40%, 플레이어 완주 후 70% |
| `RaceAudio` | `Audio/RaceSfx` | 카운트다운 3·2·1 = 낮은 삐, GO = 같은 소리 1.5배 높이. 플레이어 랩 완료 / 마지막 랩 진입 / 완주 효과음. **일시정지 = `AudioListener.pause`** (엔진·충돌음 정지) |
| `UiAudio` | `Audio/UiSfx` | 씬의 모든 `Button`(꺼진 것 포함)에 클릭음 |
| `EngineAudio` | 카트의 자식 `Audio` (보닛 아래) | 공회전·중간·고회전 루프 3개를 회전수(`SimulatedGearbox`)로 섞고 음높이를 올림. 가속 페달을 떼면 65%. 3D(최소 2~3m, 최대 60m) |
| `CollisionAudio` | 카트 루트 (Rigidbody 쪽) | 벽(`Wall`)·다른 카트와 부딪히면 충돌음(세기 = 상대 속도 1.5~12m/s). 바닥 접촉은 무시. 플레이어 카트는 **양손 컨트롤러 진동**(`ControllerHaptics`) |

- 음악·UI·레이스 효과음은 `ignoreListenerPause`라 일시정지 메뉴에서도 들립니다.
- 소리 볼륨 = 컴포넌트 기본 볼륨 × `AudioVolumes`(× 전체 볼륨은 `AudioListener`가 처리).
- `SimulatedGearbox` (`Scripts/Core/`, 공용): 속도 비율로 단수(6단)·회전수를 흉내 냅니다. 계기판(`CockpitGauges`)과 엔진음이 같은 값을 써서 바늘과 소리가 맞습니다.
- 임포트 설정: 효과음 = Decompress On Load + Vorbis, 엔진 루프 = ADPCM(루프 이음매 끊김 없음, 모노), BGM = Streaming + Vorbis.
- 드리프트·부스트 소리(#4), 아이템 소리(#5)는 해당 기능이 나오면 `RaceAudio`처럼 이벤트를 구독해서 붙입니다.

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```

- `메뉴`는 결과 화면의 `메뉴` 버튼과 일시정지 메뉴의 `메뉴로` 버튼 두 곳에서 갑니다.
