# 타깃 기기 — Meta Quest 2 (Oculus Quest 2)

우리가 **대여해서 쓰는 실기기는 Quest 2** 입니다. 시야각, 해상도, 성능 예산, UI 크기를 처음부터 Quest 2 기준으로 잡아서 나중에 실기기에서 "안 맞는" 일이 없게 합니다.

- **기준 기기 = Quest 2.** Quest 3/3S는 더 좋은 기기라 Quest 2에서 돌아가면 대부분 그대로 돌아갑니다. 반대는 보장되지 않습니다.
- 평소 개발은 PC + XR Device Simulator, **주 1회 이상은 대여 기기로 실기기 확인** ([4. 테스트 방법](#4-테스트-방법)).

> 스펙과 권장값은 2026-09-21에 Meta 개발자 문서, Unity 문서 등에서 확인했습니다. 출처는 문서 끝에 있습니다.

---

## 1. 하드웨어 스펙

| 항목 | Quest 2 | 개발에 주는 의미 |
| --- | --- | --- |
| 디스플레이 | LCD 단일 패널, **눈당 1832 × 1920** | 실제 렌더 해상도는 아래 "기본 렌더 해상도" |
| 기본 렌더 해상도 (eye buffer) | **눈당 1440 × 1584**, 4x MSAA | Render Scale 1.0 = 이 해상도. 올리면 GPU 부담 급증 |
| 주사율 | 60 / **72** / 90 / 120 Hz | **72Hz 기준** → 프레임당 **13.9ms** |
| 시야각 (FOV) | 수평 약 **89~97°**, 수직 약 **93°** (측정 방식·얼굴형에 따라 다름) | 아래 [2-1](#2-1-시야각-fov) 참고 |
| 픽셀 밀도 | 약 **20 px/도** (1832px ÷ 약 90°, 계산값) | 작은 글씨·얇은 선은 뭉개짐 → UI 크기 기준 |
| IPD (눈 사이 거리) | 렌즈 3단계 수동: **58 / 63 / 68mm** | 사용자가 맞추는 것. 앱에서 할 일 없음 |
| 칩셋 | Qualcomm Snapdragon XR2 | 모바일 GPU(타일 기반) → 포스트 프로세싱·실시간 그림자에 약함 |
| RAM | 6GB | 텍스처 크기 제한 ([ASSETS.md](ASSETS.md): 최대 2K) |
| 트래킹 | 6DoF inside-out (카메라 4개) | 좌석형 게임이라 좁은 공간에서도 가능 |
| 컨트롤러 | Touch (3세대) | 아래 [2-4](#2-4-컨트롤러-버튼) |
| 무게 / 배터리 | 503g / 약 2~3시간 (사용량에 따라 다름) | 실기기 테스트는 충전해 두고 짧게 여러 번 |
| PC 연결 | Quest Link (USB-C 케이블), Air Link (Wi-Fi) | Unity Play 모드를 헤드셋으로 바로 확인 가능 |
| 지원 현황 | 2024-09 단종. 기능 업데이트는 2026년까지, 중요 업데이트는 2027년까지 | 과제 기간에는 문제없음 |

## 2. 설계 기준값 (개발할 때 지킬 것)

### 2-1. 시야각 (FOV)

- **VR에서는 Unity 카메라의 Field of View 값을 쓰지 않습니다.** FOV는 헤드셋 렌즈와 OpenXR이 정하므로 코드나 인스펙터에서 FOV를 바꾸는 연출(부스트 시 FOV 확대 등)은 **하지 않습니다** (멀미 원인이기도 함, NFR-03).
- 대신 **"어디에 무엇을 놓을지"** 를 Quest 2 시야각 기준으로 맞춥니다.

| 영역 (정면 기준 각도) | 무엇을 놓나 |
| --- | --- |
| 중앙 **±15°** 안 | 카운트다운, 역주행 경고 등 꼭 봐야 하는 것 |
| 중앙 **±30°** 안 | HUD(랩, 순위, 속도, 아이템) — 눈만 돌려 편하게 보이는 범위 |
| **±45°** 근처 (시야 가장자리) | 중요한 정보 금지. 렌즈 가장자리라 흐리고 비네팅이 덮는 곳 |
| 아래쪽 **약 15~30°** | 카트 대시보드 HUD 자리 (운전하며 살짝 내려다보는 위치) |

> 위 각도는 VR UI에서 흔히 쓰는 권장값을 Quest 2(수평 약 90°)에 맞춘 것입니다. **실기기에서 1회 확인 후 조정**합니다.

### 2-2. UI 거리 & 글자 크기

| 항목 | 기준 |
| --- | --- |
| 메뉴/결과 UI 거리 | 약 **1.5m** (NFR-04), 너무 가까운 UI(0.5m 미만)는 눈이 피로해서 피함 |
| 글자 높이 (1.5m 거리) | **약 3cm 이상에서 시작** (20px/도 기준 계산값, 실기기에서 조정) |
| 월드 스페이스 Canvas | 1 Unity 단위 = 1m. Canvas Scale을 0.001 수준으로 두고 픽셀 단위로 레이아웃 |
| HUD 방식 | 머리에 붙는(head-locked) HUD보다 **카트에 붙은 대시보드 HUD** 우선 (멀미·가독성) |
| 얇은 선·작은 아이콘 | 2px 이하 선은 깜빡이거나 사라짐 → 굵게 |

### 2-3. 성능 예산

| 항목 | Meta 권장 (Quest 2) | **우리 목표** (NFR-01) |
| --- | --- | --- |
| 프레임 | 최소 72 FPS | **72 FPS 고정** (13.9ms) |
| 드로우콜 | 80~200 (물리·로직이 많은 앱) | **< 150** |
| 삼각형 | 75만~100만 | **< 30만 (트랙)** — 카트·AI·이펙트 여유분 확보 |
| 스크립트 시간 | — | Update 로직 합계 **2ms 이내** 목표 |

- 카트 레이싱은 물리 + AI 3대 + 파티클이 돌아가므로 Meta 기준에서 **"무거운 앱" 구간**으로 잡고 보수적으로 목표를 둡니다.
- 확인 방법: 실기기 + Unity Profiler (Development Build), OVR Metrics Tool 또는 Meta Quest Developer Hub의 성능 오버레이.

### 2-4. 컨트롤러 버튼

[PRD 조작표](PRD.md#6-조작-quest-컨트롤러-기준)와 Quest 2 Touch 컨트롤러 버튼 대응입니다.

| 버튼 | 위치 | 우리 게임 |
| --- | --- | --- |
| Grip | 양손 중지 | 핸들 잡기 |
| Trigger | 양손 검지 | 오른손 가속 / 왼손 브레이크·후진 |
| A / B | 오른손 | 드리프트 / 아이템 |
| X / Y | 왼손 | (미사용, 예비) |
| 썸스틱 | 양손 | 왼손: 대체 조향 |
| 메뉴(≡) 버튼 | 왼손 | 일시정지 |
| Meta(Oculus) 버튼 | 오른손 | **시스템 예약 — 게임에서 사용 불가** |

진동(햅틱, FR-FX-04)은 XRI의 Haptic 기능으로 양손 컨트롤러 모두 지원합니다.

## 3. Unity 설정 (이슈 #1에서 적용)

Quest 2 기준으로 처음부터 이렇게 설정합니다. 바꿀 때는 `ProjectSettings/` 변경이므로 PR 설명에 이유 필수 ([ROLES.md](ROLES.md)).

### Build / Player (Android 탭)

| 설정 | 값 | 이유 |
| --- | --- | --- |
| Platform | **Android** | Quest는 Android 기반 |
| Scripting Backend | **IL2CPP** | ARM64 빌드에 필요 |
| Target Architectures | **ARM64만** | Quest는 64비트 전용 |
| Graphics APIs | **Vulkan** (맨 위) | Meta 권장, 최신 최적화 기능은 Vulkan 전용 |
| Color Space | **Linear** | Meta/Unity 권장 |
| Texture Compression | **ASTC** | 모바일 GPU용 압축, 용량 절감 |
| Minimum API Level | Unity 6 + OpenXR 패키지가 요구하는 값 (#1에서 확인해 여기 기록) | |

### XR Plug-in Management → OpenXR (Android 탭)

| 설정 | 값 |
| --- | --- |
| Plug-in | **OpenXR** 체크 |
| Feature Group | **Meta Quest Support** 켜기 → Target Devices에 **Quest 2** 포함 |
| Interaction Profile | **Oculus Touch Controller Profile** 추가 |
| Render Mode | **Single Pass Instanced / Multiview** (눈 두 개를 한 번에 렌더) |
| Foveated Rendering | 켜기 (시야 가장자리 해상도를 낮춰 GPU 절약) — 가장자리가 흐리면 레벨 낮춤 |
| Symmetric Projection / Optimize Buffer Discards (Vulkan) | 켜기 (Meta 권장 최적화) |
| 주사율 | 기본 **72Hz** 유지. 성능 여유가 확인되면 90Hz 검토 |

### URP Asset (Quest용)

| 설정 | 값 | 이유 |
| --- | --- | --- |
| HDR | **끄기** | Meta 권장, 모바일 GPU 부담 |
| Post-processing | **끄기** | Bloom 등은 Quest 2에서 비쌈 → 이펙트는 파티클로 |
| MSAA | **4x** | VR에서 계단 현상이 매우 잘 보임. Quest 2 기본값 |
| Render Scale | **1.0** | 올리지 않기 |
| 그림자 | 메인 라이트 1개, 거리 짧게(약 30~50m), 해상도 낮게. 나머지는 라이트맵(Bake) | |
| 추가 라이트 | 최소화 (Per-pixel 추가 라이트는 비쌈) | |
| Depth / Opaque Texture | 필요할 때만 켜기 | |

## 4. 테스트 방법

| 단계 | 방법 | 언제 |
| --- | --- | --- |
| 1. PC 시뮬레이터 | XR Device Simulator (헤드셋 없이 키보드·마우스로 조작) | 평소 개발 |
| 2. Quest Link | USB-C 케이블(데이터 지원)로 PC 연결 → Meta Horizon Link PC 앱 → Unity **Play** 버튼으로 헤드셋에서 바로 실행 | 핸들 조작감, UI 크기, 멀미 확인 |
| 3. APK 실기기 | Android 빌드 → Build And Run 또는 Meta Quest Developer Hub로 설치 | **성능(FPS) 확인은 반드시 이 단계에서** (Link는 PC GPU로 돌아가서 성능이 다름) |

> ⚠️ Quest Link에서 잘 돌아가도 **APK에서는 느릴 수 있습니다.** 성능 판단은 항상 APK 기준.

### 개발자 모드 준비 (대여 기기 받으면 1회)

1. Meta Horizon 개발자 대시보드에서 **조직(Organization) 생성** + 계정 보안 인증(2단계 인증 또는 결제수단 등록)
2. 폰의 **Meta Horizon 앱**에서 헤드셋 연결 → 헤드셋 설정 → **개발자 모드 켜기**
3. PC와 USB 연결 후 헤드셋에서 "USB 디버깅 허용" 수락
4. 헤드셋 펌웨어를 최신으로 업데이트

> 대여 기기가 다른 사람 계정에 연결되어 있으면 개발자 모드 설정이 막힐 수 있습니다. 대여할 때 **어떤 계정으로 쓰는지, 초기화해도 되는지** 먼저 확인합니다.

## 5. 대여 기기 관리

| 항목 | 내용 |
| --- | --- |
| 기기 | Meta Quest 2 (컨트롤러 2개, 충전기) |
| 대여처 / 기간 | _(대여 후 기록)_ |
| 연결 계정 | _(대여 후 기록 — 비밀번호는 여기에 쓰지 않기)_ |
| 현재 보관자 | _(이창민 / 윤승희)_ |

### 체크리스트

- [ ] 받을 때: 본체·컨트롤러 2개·충전기, 컨트롤러 AA 건전지, 렌즈 흠집 확인
- [ ] **USB-C 데이터 케이블** 준비 (충전 전용 케이블은 Link 안 됨)
- [ ] 개발자 모드 설정 ([4. 테스트 방법](#개발자-모드-준비-대여-기기-받으면-1회))
- [ ] 실기기 테스트 일정은 이슈나 카톡으로 미리 잡기 (한 대를 둘이 나눠 씀)
- [ ] ⚠️ **렌즈에 직사광선 금지** — 창가·햇빛 아래 두면 화면이 타버림. 쓰지 않을 때는 케이스에 보관
- [ ] 렌즈는 마른 극세사 천으로만 닦기
- [ ] 반납 전: 우리 앱 삭제, 개발자 모드·계정 원상복구 (대여처 규칙에 따라)

## 6. 출처

- [Meta — Testing and performance analysis (Quest 2 성능 목표)](https://developers.meta.com/horizon/documentation/unity/unity-perf/)
- [Meta — OpenXR Meta Quest Support settings (1440×1584, 4x MSAA)](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/)
- [Unity OpenXR Meta — Optimize graphics settings (Vulkan, URP HDR/Post-processing 끄기)](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.1/manual/get-started/graphics-settings.html)
- [Wikipedia — Quest 2 (하드웨어 스펙, 단종일)](https://en.wikipedia.org/wiki/Quest_2)
- [VRcompare — Quest 2 스펙 (FOV)](https://vr-compare.com/headset/oculusquest2)
- [Meta Community — 2026년 Quest 업데이트 안내 (지원 기간)](https://communityforums.atmeta.com/discussions/product-news-/updates-to-your-meta-quest-experience-in-2026/1369489)
- [Meta — Device Setup (개발자 모드)](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/)
