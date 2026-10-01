# 🏎️ VR Mario Kart (가칭)

> 가상현실 과제 — Unity 기반 **VR 카트 레이싱 게임**
> 마리오카트처럼 짧고 간단하게 즐길 수 있는 1인용 VR 레이싱

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6.3 LTS **`6000.3.15f1`** — 팀원 모두 **동일 버전** 사용 ([개발 환경](#-개발-환경)) |
| XR | OpenXR + XR Interaction Toolkit (XRI) 3.x |
| 타깃 기기 | **Meta Quest 2 기준** (대여 실기기, Android) — Quest 3 호환, PC 테스트는 XR Device Simulator. 상세: [docs/DEVICE.md](docs/DEVICE.md) |
| 렌더 파이프라인 | URP |
| 작업자 | 이창민 ([@2-changmin](https://github.com/2-changmin)), 윤승희 ([@realp0tato](https://github.com/realp0tato)) |

---

## 📚 문서

| 문서 | 내용 |
| --- | --- |
| [docs/PRD.md](docs/PRD.md) | 제품 요구사항 문서 — 목표, 타깃, 핵심 경험, 범위, 마일스톤 |
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | 기능/비기능 요구사항 명세 (ID 기반, 이슈와 매핑) |
| [docs/ROLES.md](docs/ROLES.md) | 역할 분담 — 누가 무엇을 담당하는지, 담당 폴더/씬 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 폴더 구조, 씬 구성, 주요 컴포넌트와 인터페이스 약속 |
| [docs/DEVICE.md](docs/DEVICE.md) | 타깃 기기(Quest 2) 스펙, 시야각·UI·성능 기준값, Unity 설정, 실기기 테스트·대여 기기 관리 |
| [docs/ASSETS.md](docs/ASSETS.md) | 사용할 외부 에셋 목록, 라이선스 규칙, 가져오기 규칙, 에셋 등록부/크레딧 |
| [CONTRIBUTING.md](CONTRIBUTING.md) | **작업 규칙** — 이슈 → 브랜치 → PR → 리뷰 → 머지 |

---

## 👥 역할 요약

| 작업자 | 담당 영역 |
| --- | --- |
| **이창민** | 프로젝트/XR 셋업, VR 조작(핸들), 카트 물리, 드리프트·부스트, 아이템, 멀미 저감, 빌드·최적화 |
| **윤승희** | 트랙 제작, 체크포인트·랩, 레이스 흐름(GameManager), AI 카트·순위, 메뉴/HUD UI, 사운드·이펙트 |
| **공동** | 통합 테스트, 발표 자료, 시연 영상 |

자세한 내용은 [docs/ROLES.md](docs/ROLES.md)를 보세요. 이슈마다 `담당: 이창민` / `담당: 윤승희` 라벨과 Assignee가 붙어 있습니다.

---

## 🔁 작업 흐름 한눈에 보기

```
Issue 선택/생성 ─▶ 브랜치 생성 ─▶ 작업 & 커밋 ─▶ PR 생성 ─▶ 상대방 리뷰(1명 승인) ─▶ Squash Merge ─▶ 브랜치 삭제
 (#12)            feature/12-kart-physics   feat: ...        Closes #12
```

- `main` 브랜치는 **보호**되어 있어 직접 push 불가, 반드시 PR + 1명 승인 필요
- 한 PR = 한 이슈 = 한 기능
- 규칙 전체는 [CONTRIBUTING.md](CONTRIBUTING.md)

---

## 🚀 시작하기

```bash
# 1. Git LFS 설치 (최초 1회) — 텍스처/모델/사운드 파일은 LFS로 관리합니다
git lfs install

# 2. 클론
git clone https://github.com/2-changmin/vr-mario-kart.git
cd vr-mario-kart

# 3. Unity Hub에서 이 폴더를 열기 (Unity 6000.3.15f1, Android Build Support 모듈 포함)
```

처음 열면 패키지 설치와 임포트에 몇 분 걸립니다. 헤드셋 없이 테스트하려면 `Assets/_Project/Scenes/Sandbox/Changmin_Setup.unity`를 열고 Play (XR Interaction Simulator — 키보드·마우스로 헤드셋·컨트롤러 조작).

**헤드셋 없이 운전 테스트 (PC 운전 모드)**: `Track_Main` 또는 `Track_Test`를 열고 Play하면 헤드셋이 없을 때 자동으로 켜집니다(화면 왼쪽 위에 안내). 에디터·PC에서만 동작하고 Quest 빌드에는 들어가지 않습니다.

| 키 | 기능 | 키 | 기능 |
| --- | --- | --- | --- |
| `T` | 가속 | `J` / `L` | 좌회전 / 우회전 |
| `Shift` | 브레이크 (멈추면 놓음) | `Space` | 드리프트 (#4) |
| `R` | 후진 (달리는 중이면 먼저 감속) | `E` | 아이템 (#5) |
| `P` | 일시정지 / 재개 | `F1` | PC 운전 ↔ XR 시뮬레이터(핸들 잡기 테스트) |

메뉴 버튼은 마우스로 클릭합니다. `Esc`는 에디터가 가로채서 쓰지 않습니다.

---

## 🛠️ 개발 환경

| 항목 | 버전 / 값 |
| --- | --- |
| Unity Editor | **6000.3.15f1** (Unity Hub → Installs → Install Editor → Archive에서 정확한 버전 설치) |
| Unity 모듈 | Android Build Support (OpenJDK, Android SDK & NDK Tools 포함) |
| 렌더 파이프라인 | URP 17.3.0 |
| XR Interaction Toolkit | 3.3.1 (+ Starter Assets, XR Interaction Simulator 샘플) |
| OpenXR Plugin | 1.16.1 (Meta Quest Support 기능 그룹) |
| XR Plug-in Management | 4.5.4 |
| Input System | 1.19.0 |

패키지 버전의 기준은 `Packages/manifest.json` / `packages-lock.json`이며, 임의로 올리지 않습니다 (NFR-05).

---

## 🗓️ 마일스톤

| 마일스톤 | 목표 |
| --- | --- |
| **M1. 기반 셋업** | Unity/XR 프로젝트, 테스트 트랙 — 둘이 동시에 작업할 수 있는 바닥 만들기 |
| **M2. 코어 게임플레이** | VR 핸들로 카트를 몰고, 랩을 돌고, 레이스가 시작·종료됨 |
| **M3. 콘텐츠 & 완성도** | 드리프트/부스트, 아이템, AI, UI, 메인 트랙, 멀미 저감 |
| **M4. 마무리** | 사운드, 최적화, Quest 빌드, 발표/시연 |

진행 상황은 [Issues](https://github.com/2-changmin/vr-mario-kart/issues)와 [Milestones](https://github.com/2-changmin/vr-mario-kart/milestones)에서 확인합니다.
