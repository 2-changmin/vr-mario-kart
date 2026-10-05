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
│  │  ├─ Track_Campus.unity      # 두 번째 트랙: 동아대 승학캠퍼스 (#48)
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
│  │  ├─ Audio/                  # 음악, 효과음, 엔진음, 충돌음·진동, 볼륨
│  │  └─ Effects/                # 카트 파티클(드리프트 불꽃, 부스트 배기 불꽃)
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

멀미는 몸(전정기관)이 느끼지 못하는 **회전·속도 변화** 때 시야 가장자리가 흐르면서 크게 옵니다. 그래서 **일정한 속도로 직진할 때는 가리지 않고**, 회전·가감속할 때만 주변 시야를 가립니다.

- **`ComfortSettings`** (static, `VRKart.XR`): `VignetteEnabled`(기본 켬), `VignetteStyle`(기본 `WindowTint`), `VignetteIntensity`(0~1, 기본 0.6), `HorizonLock`(기본 켬), `ShowPresetLabel`(기본 끔), `event Changed`, `Save()`. PlayerPrefs 키 `settings.vignette.enabled`·`.style`·`.intensity`, `settings.horizonLock`, `settings.comfort.showLabel`. 설정 UI(#12)는 setter만 부르고 화면을 닫을 때 `Save()`를 한 번 부릅니다.
- **가리는 방식 `ComfortVignetteStyle`**

| 값 | 보이는 모습 | 영상·발표 화면 |
| --- | --- | --- |
| `WindowTint` (기본) | **앞유리 좌우 가장자리**(가운데는 투명한 그라데이션)와 **옆유리**가 짙어짐 — 차에 붙어 있어 고개를 돌려도 차와 함께 고정 | 선팅된 차 유리처럼 보여 자연스러움 |
| `Soft` | 화면 가장자리를 반투명(최대 55%)하게 어둡게 | 은은한 비네트 |
| `Black` | 화면 가장자리를 검게 (가장 강함) | 검은 원이 보임 |

- **`ComfortVignette`** (`Kart_Player` 루트): 가리는 정도 = max(**회전 속도** 10~60°/s, **가감속** 1.5~6 m/s²) × 강도. 가감속은 물리 스텝마다 속도 변화로 재고 0.15초로 평활. 0.3초에 가려지고 0.6초에 걷힘, 일시정지 중에는 걷힘.
  - `WindowTint`: `Cockpit/ComfortWindows`의 `WindshieldTint`(앞유리 면, `Textures/XR/Comfort_WindshieldEdge.png` 그라데이션) + `WindowTint_L/R`(옆유리) — URP Unlit 반투명(`Materials/XR/Comfort_WindshieldTint.mat`, `Comfort_WindowTint.mat`), 최대 불투명도 0.9 × 강도. [차량 비주얼 규칙](#차량-비주얼-규칙-40) 12대로 `Cockpit_Interior`와 형제.
  - `Soft`·`Black`: 메인 카메라 자식 `ComfortVignette` 반구(XRI 샘플 `VR/TunnelingVignette` 셰이더, `Materials/XR/Comfort_Vignette.mat`).
  - ⚠️ 옆유리는 운전석 눈에서 약 80° 옆이라 정면을 볼 때는 거의 시야 밖입니다. 주된 효과는 앞유리 가장자리입니다.
- **`HorizonLock`** (`ViewPivot`): 켜져 있으면 카트의 앞뒤·좌우 기울기를 상쇄하고 방향(요)만 따라갑니다 → 경사·점프대에서 지평선이 기울지 않음. 조종석은 카트와 함께 기울어 보입니다. `ViewRecenter`는 `ViewPivot`의 위쪽을 기준으로 맞춥니다.
- **프리셋** (멀미 평가 #46): `ApplyPreset(ComfortPreset.Off | On)` — Off = 시야 가림·수평 유지 모두 끔, On = 모두 켬. **`ComfortPresetToggle`**: **왼손 X 1.5초**(PC 운전 모드 `F2` 1.5초)로 켬 ↔ 끔 + 저장, 전환 후 3초 동안 시야 위쪽에 `COMFORT ON/OFF`(메인 카메라 자식 `ComfortLabel`). `ShowPresetLabel`이 켜져 있으면 계속 표시.
- 강도 기본값(0.6)과 회전·가감속 기준값은 PC에서 정한 값입니다. **실기기에서 확정**합니다(#15, #46).

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
| `Props/ItemBoxes` | 아이템 박스 3줄 × 4개(2.5m 간격): 메인 직선 110m, 2번째 90° 뒤 직선 445m, 뒷 직선 870m (출발점 기준 중심선 거리) |
| `Props/DashPads` | 대시 패드 4개: 1번 코너 뒤 238m, 헤어핀 탈출 615m, 뒷 직선 915m(좌우 ±2.5m 두 개). 헤어핀·급커브 **직전에는 두지 않음** |
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
- **경사(#50)**: 조각마다 `Rise`(그 조각 동안 오르내리는 높이, m)를 줍니다. 한 바퀴 합은 0이어야 하고(0.5m 넘게 어긋나면 경고, 작은 오차는 전체에 나눠 닫음), 높이는 `Height Smoothing`(기본 30m) 거리만큼 이동 평균을 두 번 걸어 꼭대기·골짜기가 완만해집니다. 각 샘플에 `Slope`(경사)가 들어갑니다.
  - **기준: 경사 10% 이하, 언덕 꼭대기 곡률 반지름 80m 이상**(20m/s, 부스트 27m/s에서 뜨지 않게). 인스펙터에 최대 경사·최소 꼭대기 반지름이 나오고 기준을 넘으면 경고로 바뀝니다.
  - 도로가 바닥(`Ground Height`, 기본 -0.1)보다 높으면 벽 바깥면이 바닥까지 내려가고, 바깥에 **흙 둑**(`Embankments`, 높이 1m당 옆으로 `Embankment Ratio` 2m, 콜라이더 없음)이 생깁니다. 바닥보다 낮은 도로(파인 길)는 지원하지 않습니다 — 출발점 높이 0을 기준으로 위로만 올리세요.
  - 출발·결승 구간은 평지(Rise 0)로 두세요(카운트다운 정지 중 미끄러짐 방지, 출발 위치는 y 0).
  - 테스트 씬 `Scenes/Sandbox/Seunghee_Slopes.unity`: `Track_Main` 복사본에 최고 8m 언덕 두 개(최대 경사 6.7%, 꼭대기 반지름 468m). 4대 3랩 자동 주행에서 **공중에 뜬 시간 0초, 리스폰 0**, 기록은 평지와 비슷(AI 1 184.9s).
- 반지름은 도로 중심 기준입니다. **반지름 12m 미만은 피하세요**(안쪽 갓길·벽이 겹침). 평행한 구간은 중심선끼리 **24m 이상** 떨어뜨립니다(벽 바깥끼리 7m).
- `TrackMeshBuilder`: 폭(도로 10, 연석 1, 갓길 3, 벽 두께 0.5·높이 1m)과 머티리얼(`Track_Road`, `Track_LineWhite`, `Track_Curb`, `Track_Runoff`, 벽 = `Track_Curb`). 레이어는 도로·선·연석 `Road`, 갓길 `Grass`, 벽 `Wall`.
- `Track_Test`에도 아이템 박스 2줄(85m, 535m)·대시 패드 2개(335m, 680m)를 직선 구간에 놓았습니다(AI 아이템 테스트용).

**성능 (#8, 에디터 측정 — Quest 실측은 #15)**

| 항목 | 값 | 기준 |
| --- | --- | --- |
| 씬 전체 삼각형 | 15.7만 (나무 4.2만, 도로 2.4만, 조종석 2.2만, 풀 2만 …) | — |
| 운전 중 한 프레임 삼각형(그림자 포함) | 최대 13.1만 / 평균 10.8만 | **30만 이하 ✅** |
| SetPass | 최대 38 | 낮음 |
| 배치 | 최대 457 | SRP Batcher라 SetPass가 더 중요. Quest에서 프레임이 부족하면 풀(90)·꽃(140) 수부터 줄이기 |

- 작은 식물(풀·꽃·덤불·통나무·바위 465개)은 그림자를 끔. 그림자는 `Mobile_RPAsset`(그림자 50m, 캐스케이드 1, 1024)에서 실시간 방향광 1개.
- **라이트 베이크는 하지 않음**: 도로·벽 메시가 실행 시 생성돼서(`TrackMeshBuilder`) 라이트맵을 받을 수 없고, 장식만 굽으면 도로와 밝기가 달라집니다. 대신 실시간 조명 + 3색 환경광 + 안개.

## 캠퍼스 트랙 (`Track_Campus.unity`, #48)

동아대학교 승학캠퍼스 둘레를 도는 두 번째 트랙입니다. `Track_Main`을 복사해 트랙·장식만 바꿨고, 레이스 오브젝트(`RaceManager`, `UI`, 카트 4대, 출발선·출발 위치, 결승 게이트)는 같은 구성입니다.

**코스 만든 방법**
- 승희가 지도에 그린 코스(캠퍼스 밖 린다프레스티지 블록 → 인문과학대 → 중앙 광장 → 소프트웨어대 → 꼭대기 회차 → 광장 복귀 → 생명자원과학대 → 출발)를 따라 꼭짓점을 찍고, 꼭짓점마다 반지름 22m(최소 14.4m) 원호로 모서리를 깎아 `TrackLayout` 조각 81개로 만들었습니다. 한 바퀴 **1,048m**.
- 지도 이미지는 OpenStreetMap 도로와 자동 정합해 축척을 구했습니다(0.67m/px). 실제 코스는 약 2.0km → 게임은 **가로 0.52배**.
- 지도에서 코스가 겹치는 곳(광장 ↔ 소프트웨어대 구간)은 **두 갈래 길로 나눴습니다**: 올라갈 때는 북쪽, 내려올 때는 남쪽 길. 두 길 중심 간격 최소 23m(벽 사이 약 6m)라 서로 겹치지 않습니다.
- **높이 = 실측 표고**: 코스를 따라 SRTM·ASTER 30m 위성 표고(OpenTopoData)를 받아 평균하고, 가로와 같은 0.52배로 줄여 **실제 경사를 유지**했습니다. 단 15%보다 가파른 곳만 15% 근처로 눌렀습니다(`tanh` 압축). 결과: 출발점 기준 **-5.7m(캠퍼스 밖 서쪽) ~ +36m(꼭대기 회차)**, 최대 경사 **14.7%**, 언덕 꼭대기 반지름 최소 90m. 실측(같은 축척) 최고 높이 약 57m의 약 65%입니다. 출발·결승 구간(-30 ~ +45m)은 평평합니다.
  - #50 기준(10%)보다 가파르지만, 실제 캠퍼스 경사를 살리려고 15%까지 허용했습니다. 자동 주행 테스트에서 문제 없었습니다(아래).
- 원본 데이터·스크립트(지도 정합, 표고, 조각 계산)는 저장소에 넣지 않았습니다. 지도·로드뷰 이미지도 넣지 않았습니다(참고만).

**구성**
| 오브젝트 | 내용 |
| --- | --- |
| `Track` | `TrackLayout`(회전 180° = 남쪽 출발, 결승선 35m, Height Smoothing 8m) + `TrackMeshBuilder`(흙 둑 끔, Ground Height -7, **벽 숨김(투명 벽 1.5m) + 벽 자리에 낮은 연석 0.25m(`Campus_Curb`)·빨강/흰 연석 없음·갓길 = 보도(`Campus_Paving`)**) + **`TrackTerrain`**(Verge Width 5m, 비탈 = 콘크리트 바닥 `Campus_Ground`) — 경기장처럼 따로 놀지 않고 캠퍼스 길처럼 주변 땅과 이어짐 |
| `Campus/Buildings` | **OpenStreetMap 건물 윤곽**(+ 구글 위성사진 확인)을 그대로 세운 건물 300동(캠퍼스 23동 + 남쪽 시내·아파트). 높이 = OSM 층수 × 2.4m(없으면 캠퍼스 6층·주택 3층·아파트 12층), 층마다 창문 띠(10층 이상 캠퍼스 건물은 세로 창문 줄 — 인문과학대학 11층). 캠퍼스 = 흰 벽·회색 평지붕, 체육관(예술체육대학1관) = 파란 지붕, 시내 = 초록 방수 옥상(위성사진). 트랙에서 10.25m 안으로 들어오면 중심 쪽으로 줄이거나(19동) 트랙 반대쪽으로 옮기고(4동), 그래도 겹치면 뺌(38동, 대부분 캠퍼스 밖 주택). 벽·지붕·창문을 머티리얼별로 합친 메시 6개(삼각형 약 1.4만). 건물 바닥은 벽을 따라 2m마다 잰 **보이는 가장 낮은 면**(지형·비탈)보다 1.5m 아래까지 내려서 비탈 위에서 떠 보이지 않음. 이름 간판 5개(인문과학대학·소프트웨어대학·예술체육대학1관·2관·생명자원과학대학)는 트랙에 가장 가까운 벽에 |
| `Campus/SportsField`, `Campus/CentralPlaza` | 위성사진의 **인조잔디 운동장**(주황 트랙 테두리, 32×49m, 비탈 위 석축 단)과 **흙 광장**(반지름 12m, 가운데 흰 기둥) |
| `Campus/MainGate` | **정문**(로드뷰·정문 사진 참고): 복귀 구간이 출발 직선으로 꺾이는 코너 바깥(1,013m 옆). 석축 옹벽(폭 36m, 높이 +6.4m) 가운데 넓은 계단 22단, 계단 왼쪽 콘크리트 기둥(11m), 옹벽 위 산울타리·나무, 오른쪽 옹벽의 주차장 입구, 계단 위 `동아대학교 승학캠퍼스` 현수막, 위 광장의 흰 기둥 조형물(세로 `동아대학교`) |
| `Campus/GateStreet` | **정문 앞 도로**(953m ~ 다음 바퀴 25m): 빨강/흰 연석을 도로색으로 덮고, 가장자리 노란 선, 갓길 = 보도블록, 벽 = 석재 마감, 999m에 횡단보도. 보이기만 하는 상자를 머티리얼별로 합친 메시 5개(콜라이더 없음 → 주행·갓길 감속은 그대로) |
| `Campus/Pavement` | **건물 단지 느낌**: 지형 중 트랙에서 30m 안 또는 건물·시설 근처(+16m) 삼각형을 5cm 위에 콘크리트 포장(`Campus_Ground`)으로 덮은 메시(약 1.3만 삼각형, 씬에 저장). 나머지(바깥 산)는 짙은 숲색(`Campus_ForestGround`) |
| `Environment/Scenery/Trees` | 캠퍼스 안 조경수 70그루(활엽수) + 바깥 산 숲 380그루(침엽수). 트랙 13.75m 안·건물·주택가에는 심지 않음 |
| `Props/ItemBoxes` | 3줄: 141m(캠퍼스 밖), 464m(중앙 광장), 877m(내리막) |
| `Props/DashPads` | 325m·560m(오르막 가운데), 978m(내리막 좌우 2개) — 경사에 맞춰 기울임 |
| `Environment/Ground_Grass` | y -7.55, 1,370m 사방 (지형 가장자리 아래) / `KillZone` y -25 |

- **`TrackTerrain`**(새 컴포넌트, `[ExecuteAlways]`, 씬에 저장 안 함): 트랙 둘레 80m까지 4m 격자 지형(버텍스 약 1.4만, Grass 레이어, 메시 콜라이더). 격자 점 높이 = 트랙 높이 거리 가중 평균, 도로·갓길 아래는 도로보다 0.4m 낮게, 벽 바깥은 1m당 0.5m까지만 벗어남, 가장자리 50m에서 `Edge Height`(-7m)로 내려감. 높낮이가 큰 트랙에서 흙 둑(`Embankments`) 대신 씁니다. **`Verge Width`**(비탈 폭)를 주면 갓길 가장자리(도로 높이)에서 자연 지형 높이까지 비탈 + 바깥 끝 치마(틈 가림)를 만들고, 그 아래 격자 지형은 낮춥니다. 두 갈래 길처럼 다른 구간이 가까운 곳은 비탈 폭을 줄여 겹치지 않게 합니다.
- `TrackMeshBuilder` 옵션(기본값은 기존과 같음 → `Track_Main`·`Track_Test` 변화 없음): `Build Embankments`(흙 둑), **`Walls Visible`**(끄면 벽은 콜라이더만 = 투명 벽), **`Build Curbs`**(빨강/흰 연석), **`Low Curb Height`·`Low Curb Material`**(벽을 숨겼을 때 벽 자리에 보이는 낮은 연석, 콜라이더 없음). `ShoulderEdge` 속성 추가.
- `TrackTerrain`: `Verge Material`(비탈만 다른 머티리얼), 비탈에도 메시 콜라이더(Grass 레이어, 장식 배치용 — 투명 벽 바깥이라 카트는 닿지 않음).
- 머티리얼 `Materials/Environment/Campus_*`(URP Lit): Building·Window·Sign·White·Stone·Hedge·Granite·Concrete·Paving·PaintYellow·RoofGray·RoofGreen·RoofBlue·Turf·TrackRed·Dirt·ForestGround.
- **자동 주행 테스트**(4대 모두 AI, 3랩): 전원 완주(AI 1 185.3s ~ AI 3 214.6s), **공중에 뜬 시간 0초, 리스폰 0**, 3m/s 이하 정체 최장 1.5초, 아이템 사용 32회.
- 메인 메뉴의 **`트랙: 서킷 / 동아대 캠퍼스`** 버튼으로 고릅니다(아래 메뉴 절).

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
- **아이템 사용**(FR-ITEM-05): 같은 카트의 `ItemHolder`를 보고, 받은 뒤 1~3초(무작위) 기다렸다가 기회를 봅니다. **부스터** = 앞 25m 커브가 15° 이하(직선), **쉘** = 40m 안 정면 ±8°에 카트, **바나나** = 20m 안 뒤쪽 ±35°에 카트. 8초가 지나면 기회가 없어도 씁니다. `UseItem`은 두 프레임 동안 true(`ItemHolder`와 Update 순서 무관).

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
  - **아이템 칸**: 플레이어 `ItemHolder.ItemChanged` → `DashLeft/ItemSlot/Icon`에 아이콘(`Textures/UI/Item_Booster·Banana·Shell.png`, 직접 그림), 받을 때 1.35배로 커졌다가 돌아옴. 없으면 빈 칸.
- `RaceMessages` — 가운데 메시지: 정면 **2m**, ±15° 안. `Pretendard-SemiBold SDF - Outline` 머티리얼(외곽선)이라 밝은 하늘 위에서도 읽힙니다.
  - 카운트다운 `3 · 2 · 1 · 출발!`(크게 떴다 작아짐), 랩 완료 시 `N랩  0:00.000` 2.5초, 마지막 랩 진입 시 `마지막 랩!`, 역주행 중 `역주행!`(빨강)
  - 게임 시간 기준이라 일시정지하면 메시지도 멈춥니다.
- 일시정지 중에는 HUD를 숨깁니다(일시정지 메뉴를 가리지 않게). 재개하면 같은 자리에 다시 보입니다.

## UI — 메인 메뉴 · 일시정지 (`UI_MainMenu`, `UI_PauseMenu`)

- **`Scenes/MainMenu.unity`**: XR Origin, `EventSystem` + `XRUIInputModule`, 바닥, `UI_MainMenu`
  - `MainMenu` — `트랙: …`(누를 때마다 `서킷`(`Track_Main`) ↔ `동아대 캠퍼스`(`Track_Campus`), 메뉴로 돌아와도 기억) / `시작` → 고른 트랙 / `설정` → 볼륨 슬라이더 3개(전체·배경음악·효과음) / `종료`
  - 씬 시작 한 프레임 뒤(XR 트래킹이 잡힌 뒤) 플레이어 정면 1.5m에 놓입니다(`PlayerSpace`).
  - 제목 `VR 카트 레이싱`은 임시입니다. 닌텐도 IP 규칙([ASSETS.md](ASSETS.md)) 때문에 "마리오"는 쓰지 않았습니다. 프리팹 `MainPanel/Title` 텍스트에서 바꿉니다.
- **설정** (`PlayerPrefs`): `GameSettings.MasterVolume`(= `AudioListener.volume`), `AudioVolumes.Music`·`AudioVolumes.Sfx`(아래 사운드 절). 게임 시작 시 저장된 값을 적용하고, 설정 화면을 닫을 때 저장합니다.
  - 멀미 저감(#6 `ComfortSettings`): **멀미 저감 켬/끔**(버튼) → `VignetteEnabled`, **강도** 슬라이더 → `VignetteIntensity`(끔이면 비활성), **수평 유지 켬/끔** → `HorizonLock`. `뒤로`를 누를 때 `ComfortSettings.Save()`. 방식(틴팅/가장자리)과 평가용 표시는 설정 화면에 넣지 않았습니다(기본 틴팅).
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
- **`KartSfx`** (카트 자식 `Audio`, 플레이어·AI 공통): 드리프트 타이어 소리 루프(`DriftBoost.DriftStarted/Ended`), 충전 1·2단 딸깍(`BoostLevelChanged`), **부스트 휙**(`KartController.BoostStarted` — 미니 터보·대시 패드·부스터 공통), 아이템 획득(`ItemHolder.ItemChanged`)·바나나 놓기·쉘 발사(`ItemUsed`), 스핀아웃 피격(`SpunOut`). 플레이어 카트는 진동도(부스트 약하게, 피격 강하게).
- 타이어 소리(`Audio/Kart/Drift_Skid_Loop.wav`)와 부스트 소리(`Boost_Whoosh.wav`)는 **코드로 직접 합성**한 파일입니다(잡음 대역 필터 + 배음). 더 좋은 소리로 바꾸려면 `KartSfx` 필드만 교체.

### 카트 파티클 (`Scripts/Effects/KartEffects.cs`, AI 차량만)

- `Kart_AI_*`의 자식 `Effects`에 붙음. **드리프트 불꽃**: `DriftFX/RearLeft·RearRight/Sparks`(차 모델 뒷바퀴 바닥으로 옮김 — 세단 z -0.76, 해치백 z -0.93). 충전 단계 0 → 1 → 2마다 색이 흰색 → 주황 → 파랑. **부스트 불꽃**: `Effects/Exhaust_L·R/Flame`(차 뒤 배기구), `BoostStarted`의 지속 시간 동안.
- 플레이어 카트에는 없습니다(운전석에서 뒤가 안 보임 — 소리·진동으로 대신).
- 머티리얼 `VFX/VFX_Spark·VFX_Flame`(URP Particles/Unlit 가산), 텍스처 `VFX_SoftDot.png`(직접 생성). 파티클 수 최대 60/40개라 Quest 부담 작음.

## 씬 흐름

```
MainMenu ──시작──▶ Track_Main 또는 Track_Campus (Countdown → Racing → Finished) ──재시작──▶ Track_Main
    ▲                                                          │
    └──────────────────────────메뉴──────────────────────────────┘
```

- `메뉴`는 결과 화면의 `메뉴` 버튼과 일시정지 메뉴의 `메뉴로` 버튼 두 곳에서 갑니다.
