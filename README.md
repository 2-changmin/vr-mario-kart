# 🏎️ VR Mario Kart (가칭)

> 가상현실 과제 — Unity 기반 **VR 카트 레이싱 게임**
> 마리오카트처럼 짧고 간단하게 즐길 수 있는 1인용 VR 레이싱

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6 LTS (6000.0.x) — 팀원 모두 **동일 버전** 사용 |
| XR | OpenXR + XR Interaction Toolkit (XRI) 3.x |
| 타깃 기기 | Meta Quest 2 / 3 (Android), PC 테스트는 XR Device Simulator |
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

# 3. Unity Hub에서 이 폴더를 열기 (Unity 6 LTS, Android Build Support 모듈 포함)
```

Unity 프로젝트 자체는 이슈 #1 에서 생성됩니다. 그 전까지 레포에는 문서와 설정 파일만 있습니다.

---

## 🗓️ 마일스톤

| 마일스톤 | 목표 |
| --- | --- |
| **M1. 기반 셋업** | Unity/XR 프로젝트, 테스트 트랙 — 둘이 동시에 작업할 수 있는 바닥 만들기 |
| **M2. 코어 게임플레이** | VR 핸들로 카트를 몰고, 랩을 돌고, 레이스가 시작·종료됨 |
| **M3. 콘텐츠 & 완성도** | 드리프트/부스트, 아이템, AI, UI, 메인 트랙, 멀미 저감 |
| **M4. 마무리** | 사운드, 최적화, Quest 빌드, 발표/시연 |

진행 상황은 [Issues](https://github.com/2-changmin/vr-mario-kart/issues)와 [Milestones](https://github.com/2-changmin/vr-mario-kart/milestones)에서 확인합니다.
