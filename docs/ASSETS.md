# 에셋 가이드

어떤 에셋을 **어디서** 가져와 **어디에** 쓰는지, 라이선스는 괜찮은지 한 곳에서 맞춰 두는 문서입니다.
새 에셋을 넣기 전에 이 문서를 먼저 보고, 넣은 뒤에는 [에셋 등록부](#6-에셋-등록부-실제로-넣은-것)에 한 줄 추가합니다.

> 라이선스 정보는 2026-09-21 기준으로 각 사이트에서 확인했습니다. 에셋을 받을 때 다운로드 페이지의 라이선스를 한 번 더 확인하세요.

---

## 1. 기본 원칙

1. **레포가 Public이다** → 레포에 올린 파일은 누구나 받을 수 있음 = "재배포"가 됨. 재배포가 허용된 라이선스만 커밋한다.
2. **CC0 우선** → 출처 표기 의무도 없고 재배포도 자유. 가능하면 CC0에서 먼저 찾는다.
3. **닌텐도 IP 금지** → 마리오/쿠파 등 캐릭터, 원작 BGM·효과음, 원작 아이템 모델(초록 등껍질, 물음표 박스 디자인 등)의 추출·모작 파일은 쓰지 않는다. 바나나·쉘·아이템 박스는 **우리 스타일로 새로 만든다.**
4. **스타일 통일** → 로우폴리 + 단색/그라데이션 머티리얼(Kenney·Quaternius 계열)로 맞춘다. Quest 성능(NFR-01)에도 유리.
5. **필요한 파일만** → 팩 전체를 넣지 말고 실제 쓰는 모델/사운드만 골라 넣는다 (LFS 무료 용량 1GB).

## 2. 라이선스 한눈에 보기

| 라이선스 | 레포 커밋 | 출처 표기 | 비고 |
| --- | --- | --- | --- |
| **CC0** (Kenney, Quaternius, Poly Haven, ambientCG 등) | ✅ | 불필요 (해 주면 좋음) | 최우선 사용 |
| **CC-BY** | ✅ | **필수** → [CREDITS](#7-크레딧) 에 기록 | 표기 누락 주의 |
| **SIL OFL** (폰트) | ✅ | 라이선스 파일 동봉 | 폰트 단독 판매만 금지 |
| **Unity Companion License** (Unity 공식 패키지·샘플) | ✅ | 불필요 | Unity 프로젝트 안에서만 사용 |
| **Pixabay Content License** | ⚠️ 가능하나 비권장 | 불필요 | "단독 재배포" 금지 조항이 있어 CC0 대체가 없을 때만 |
| **CC-BY-NC** / **CC-BY-SA** | ❌ | — | NC는 상업 이용 금지, SA는 결과물 라이선스 전염 → 쓰지 않음 |
| **Unity Asset Store (Standard EULA)** | ❌ **커밋 금지** | — | 아래 3번 참고 |
| 라이선스 표기 없음 / 출처 불명 | ❌ | — | 절대 사용 금지 |

## 3. Unity Asset Store 에셋 주의

Asset Store 에셋은 무료라도 **Standard Unity Asset Store EULA**가 적용되고, EULA는 빌드에 포함하는 것 이외의 **배포를 허용하지 않습니다.** Public 레포에 원본 파일을 커밋하면 위반입니다. 또 에셋 라이선스는 **사람마다 따로** 필요합니다 (무료 에셋이면 각자 "Add to My Assets").

그래서 이 프로젝트에서는:

- Asset Store 에셋은 **기본적으로 사용하지 않는다.** CC0 대체재가 거의 항상 있다.
- 꼭 필요하면 → 두 사람 모두 계정에 추가 + 레포를 Private로 전환하는 것을 먼저 합의한다.
- **Karting Microgame**(Unity Learn)은 **참고용**으로만 쓴다. 별도 프로젝트로 열어 구조·수치를 참고하는 것은 괜찮지만, 파일을 우리 레포로 복사하지 않는다.

## 4. 영역별 사용 에셋

담당은 [ROLES.md](ROLES.md) 기준입니다. ⭐ = 1순위 후보.

### 4-1. Unity 패키지 (Package Manager, 커밋 대상 = `Packages/manifest.json`만)

| 패키지 | 용도 | 관련 요구사항 | 담당 |
| --- | --- | --- | --- |
| XR Interaction Toolkit 3.x | 핸들 그랩, 레이 UI | FR-XR-02, FR-UI-01 | 이창민 |
| └ Sample: **Starter Assets** | XR Origin 프리팹, Input Actions, **터널링 비네팅(Comfort)** 프리팹 | FR-XR-06 (#6) | 이창민 |
| └ Sample: **XR Device Simulator** | 헤드셋 없이 PC 테스트 | NFR-07 | 이창민 |
| OpenXR Plugin (+ Meta Quest 지원 기능) | Quest 빌드 | NFR-01 | 이창민 |
| Universal RP | 렌더 파이프라인 | — | 이창민 |
| **Splines** | 트랙 도로 라인, AI 웨이포인트 경로 | FR-TRACK-02, FR-AI-01 | 윤승희 |
| **ProBuilder** | 그레이박스 트랙, 점프대, 아이템 박스·바나나·쉘 간단 모델링 | FR-TRACK-01, FR-ITEM-03 | 공동 |
| TextMeshPro (Unity 6에서는 uGUI에 포함) | 월드 스페이스 UI 텍스트 | FR-UI-01~04 | 윤승희 |

샘플을 Import하면 `Assets/Samples/`에 생기며, Unity Companion License라 커밋해도 됩니다. 버전은 manifest 기준으로 통일 (NFR-05).

### 4-2. 3D 모델 — 카트 & 트랙

| 에셋 | 출처 | 라이선스 | 용도 | 담당 |
| --- | --- | --- | --- | --- |
| ⭐ **Car Kit** (v3.x, 카트 레이서 모델 포함) | [kenney.nl/assets/car-kit](https://kenney.nl/assets/car-kit) | CC0 | 플레이어/AI 카트 본체, 캐릭터 | 이창민 |
| ⭐ **Racing Kit** (110종) | [kenney.nl/assets/racing-kit](https://kenney.nl/assets/racing-kit) | CC0 | 트랙 도로 타일, 펜스·벽, 트랙 소품 | 윤승희 |
| Toy Car Kit (도로·루프 트랙 조각) | [kenney.nl/assets/toy-car-kit](https://kenney.nl/assets/toy-car-kit) | CC0 | 메인 트랙 변형 구간(선택) | 윤승희 |
| Go-Kart (Blender, 로우폴리) | [blendswap.com/blend/11273](https://blendswap.com/blend/11273) | CC0 | Car Kit 카트가 VR 1인칭에서 어색할 때 대안 | 이창민 |
| **핸들(스티어링 휠)** | 직접 제작 (ProBuilder 또는 Blender) | 자체 | VR에서 손으로 잡는 핸들 — 1인칭에서 크게 보이므로 전용 모델 권장 | 이창민 |

> VR 1인칭에서는 카트 **좌석·핸들·대시보드**가 계속 눈앞에 보입니다. Car Kit 모델을 쓰더라도 조종석 부분(핸들, 대시보드 HUD 자리)은 따로 만들어 붙이는 것을 전제로 합니다.

### 4-3. 3D 모델 — 환경 / 아이템

| 에셋 | 출처 | 라이선스 | 용도 | 담당 |
| --- | --- | --- | --- | --- |
| ⭐ **Ultimate Nature Pack** (150종) | [quaternius.com/packs/ultimatenature.html](https://quaternius.com/packs/ultimatenature.html) | CC0 | 나무, 바위, 풀 등 트랙 주변 | 윤승희 |
| Stylized Nature MegaKit | [quaternius.com/packs/stylizednaturemegakit.html](https://quaternius.com/packs/stylizednaturemegakit.html) | CC0 (Standard / Pro / Source 티어로 나뉨 → 무료 티어 사용) | 위 팩 대체/보강 | 윤승희 |
| 아이템 박스 / 바나나 / 쉘 / 대시 패드 | 직접 제작 (ProBuilder + 머티리얼) | 자체 | FR-ITEM-03, FR-KART-09 — 원작과 다른 디자인으로 | 이창민 |

### 4-4. 텍스처 / 스카이박스

| 에셋 | 출처 | 라이선스 | 용도 | 담당 |
| --- | --- | --- | --- | --- |
| ⭐ Prototype Textures | [kenney.nl/assets/prototype-textures](https://kenney.nl/assets/prototype-textures) | CC0 | 그레이박스 트랙 격자 텍스처 | 윤승희 |
| ⭐ HDRI (하늘) | [polyhaven.com/hdris](https://polyhaven.com/hdris) | CC0 | 스카이박스 — **2K 이하**로 받기 | 윤승희 |
| PBR 텍스처 (아스팔트, 잔디, 흙) | [ambientcg.com](https://ambientcg.com) / [polyhaven.com/textures](https://polyhaven.com/textures) | CC0 | 트랙 노면, 트랙 밖 잔디(FR-KART-05) — **1K 권장** | 윤승희 |

### 4-5. 사운드 & 음악

| 에셋 | 출처 | 라이선스 | 용도 | 담당 |
| --- | --- | --- | --- | --- |
| ⭐ Racing Car Engine Sound Loops (피치별 6개) | [opengameart.org/content/racing-car-engine-sound-loops](https://opengameart.org/content/racing-car-engine-sound-loops) | CC0 | 엔진음, 속도 비례 피치 (FR-FX-01) | 윤승희 |
| ⭐ Impact Sounds | [kenney.nl/assets/impact-sounds](https://kenney.nl/assets/impact-sounds) | CC0 | 충돌, 아이템 피격 | 윤승희 |
| ⭐ Interface Sounds / UI Audio | [kenney.nl/assets/interface-sounds](https://kenney.nl/assets/interface-sounds), [ui-audio](https://kenney.nl/assets/ui-audio) | CC0 | 메뉴 클릭, 카운트다운 삑 소리 | 윤승희 |
| Digital Audio / Sci-fi Sounds | [kenney.nl/assets/digital-audio](https://kenney.nl/assets/digital-audio), [sci-fi-sounds](https://kenney.nl/assets/sci-fi-sounds) | CC0 | 부스트, 아이템 획득 | 윤승희 |
| Music Jingles (85개) | [kenney.nl/assets/music-jingles](https://kenney.nl/assets/music-jingles) | CC0 | 레이스 시작 / 완주 / 결과 짧은 음악 | 윤승희 |
| ⭐ BGM 후보: Hyperflight Racing | [opengameart.org/content/hyperflight-racing](https://opengameart.org/content/hyperflight-racing) | CC0 | 레이스 BGM | 윤승희 |
| BGM 더 찾기: CC0 Upbeat/Electronic 모음 | [opengameart.org/content/cc0-upbeat-electronic-music](https://opengameart.org/content/cc0-upbeat-electronic-music) | CC0 (곡마다 확인) | 메뉴 BGM 등 | 윤승희 |
| 부족할 때: Freesound | [freesound.org](https://freesound.org) | **곡마다 다름** | 검색 시 라이선스 필터를 **CC0**로. CC-BY는 크레딧 기록, **CC-BY-NC 금지** | 윤승희 |

> ⚠️ OpenGameArt도 곡마다 라이선스가 다릅니다. 예: "Racing Menu (Looping)"은 **CC-BY 3.0**이라 쓰려면 크레딧 필수.

### 4-6. UI / 폰트 / 이펙트

| 에셋 | 출처 | 라이선스 | 용도 | 담당 |
| --- | --- | --- | --- | --- |
| ⭐ **Pretendard** | [github.com/orioncactus/pretendard](https://github.com/orioncactus/pretendard) | SIL OFL | 한글 UI 폰트 → TMP 폰트 에셋 생성 | 윤승희 |
| (대안) Noto Sans KR | [fonts.google.com/noto/specimen/Noto+Sans+KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) | SIL OFL | 위와 동일 | 윤승희 |
| UI Pack / Game Icons | [kenney.nl/assets/ui-pack](https://kenney.nl/assets/ui-pack), [game-icons](https://kenney.nl/assets/game-icons) | CC0 | 버튼, HUD 아이템 아이콘 | 윤승희 |
| ⭐ Particle Pack (80종) | [kenney.nl/assets/particle-pack](https://kenney.nl/assets/particle-pack) | CC0 | 드리프트 불꽃, 부스트, 연기 파티클 텍스처 | 윤승희 |

> 한글 TMP 폰트: 전체 글자(11,172자) 아틀라스는 무겁습니다. **실제 사용하는 문자만** 넣은 Static 아틀라스(또는 Dynamic)로 만들어 용량을 줄입니다.

## 5. 가져오기 규칙 (폴더, 포맷, 용량)

### 폴더

```
Assets/
├─ _Project/          # 우리가 만든 것 + 외부 에셋을 가공해서 만든 프리팹/머티리얼
└─ ThirdParty/
   ├─ Kenney/
   │  ├─ CarKit/      # 실제 쓰는 모델만
   │  └─ RacingKit/
   ├─ Quaternius/
   │  └─ UltimateNature/
   ├─ PolyHaven/
   ├─ AmbientCG/
   ├─ OpenGameArt/
   └─ Fonts/Pretendard/   # OFL.txt 함께 넣기
```

- `ThirdParty/` 원본 파일은 **직접 수정하지 않는다.** 수정이 필요하면 `_Project/Prefabs/`에 프리팹(Variant)을 만들어 거기서 수정.
- 폴더 담당: 해당 에셋을 처음 넣은 사람. 같은 팩을 둘 다 쓰면 먼저 넣은 사람이 관리.

### 포맷 & 용량

| 종류 | 포맷 | 기준 |
| --- | --- | --- |
| 3D 모델 | **FBX** (Kenney/Quaternius가 제공) | GLB/GLTF는 추가 패키지가 필요하고 LFS 설정에 없으므로 쓰지 않음 |
| 텍스처 | PNG / JPG | **최대 2K**, 대부분 1K 이하. Import 설정에서 Android ASTC 압축 |
| HDRI | HDR / EXR | 2K 이하 |
| 효과음 | WAV (짧은 것) / OGG | Load Type: 짧은 SFX는 Decompress On Load |
| BGM | **OGG로 변환 후 커밋** | 원본 WAV(수 MB~수십 MB)는 올리지 않음. Load Type: Streaming |
| 폰트 | TTF / OTF | 쓰는 굵기(Regular, Bold 등)만 |

- 모든 바이너리는 [.gitattributes](../.gitattributes)로 LFS에 올라갑니다. **새 확장자**를 쓰면 `.gitattributes`에 먼저 추가.
- 팩 zip, 미리보기 이미지, 안 쓰는 모델은 커밋하지 않기.

## 6. 에셋 등록부 (실제로 넣은 것)

에셋을 레포에 추가하는 PR에서 **이 표에 한 줄 추가**합니다. (CONTRIBUTING의 "외부 에셋은 출처/라이선스를 PR에 적기" 규칙을 이 표로 대신함)

| 에셋 | 출처 URL | 라이선스 | 레포 경로 | 사용처 | 추가한 사람 / PR |
| --- | --- | --- | --- | --- | --- |
| _(예시)_ Car Kit — kart 모델 2종 | https://kenney.nl/assets/car-kit | CC0 | `Assets/ThirdParty/Kenney/CarKit/` | Kart.prefab | 이창민 / #00 |

## 7. 크레딧

CC0는 표기 의무가 없지만 예의상, **CC-BY는 의무로** 발표 자료 마지막 장과 결과/크레딧 화면에 적습니다.

| 에셋 | 제작자 | 라이선스 | 표기 문구 |
| --- | --- | --- | --- |
| 3D 모델, 사운드, UI, 파티클 | Kenney (kenney.nl) | CC0 | Assets by Kenney (kenney.nl) |
| 자연 환경 모델 | Quaternius | CC0 | — |
| HDRI / 텍스처 | Poly Haven, ambientCG | CC0 | — |
| 한글 폰트 | Pretendard (orioncactus) | SIL OFL 1.1 | Pretendard © Kil Hyung-jin |
| _(CC-BY 에셋을 쓰면 여기에 필수 기록)_ | | | |

## 8. 체크리스트 — 새 에셋 넣기 전에

- [ ] 라이선스가 **CC0 / CC-BY / OFL / Unity 패키지** 중 하나인가? (Asset Store, NC, SA, 출처 불명 ❌)
- [ ] 닌텐도 등 다른 게임의 IP를 추출·모작한 것이 아닌가?
- [ ] 필요한 파일만 골랐고, 텍스처 2K / BGM OGG 기준을 지켰나?
- [ ] `Assets/ThirdParty/<출처>/<팩>/`에 넣었나?
- [ ] [에셋 등록부](#6-에셋-등록부-실제로-넣은-것)에 한 줄 추가했나? (CC-BY면 [크레딧](#7-크레딧)도)
