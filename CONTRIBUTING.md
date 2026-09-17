# 작업 규칙

깔끔하게, 서로의 작업을 망가뜨리지 않기 위한 규칙입니다. 규칙을 바꾸고 싶으면 이 파일을 수정하는 PR을 올려 합의합니다.

## 핵심 원칙 5가지

1. **이슈 없는 작업은 없다** — 모든 작업은 이슈에서 시작
2. **main에 직접 push 금지** — 브랜치 → PR → 상대방 승인 → 머지 (GitHub에서 강제됨)
3. **한 PR = 한 이슈 = 한 기능** — 작게 자주 올리기
4. **남의 씬은 건드리지 않기** — 담당 씬/폴더는 [docs/ROLES.md](docs/ROLES.md)
5. **머지 전에 Unity에서 Play 해보기** — 콘솔 에러 0개

---

## 1. 이슈

- 새 기능/버그/할 일은 이슈 템플릿(기능 / 버그 / 작업)으로 등록
- 제목 형식: `[영역] 내용` — 예) `[Kart] 드리프트 & 부스트`
- 필수: **Assignee**, **라벨**(`type:`, `area:`, `담당:`, `priority:`), **마일스톤**
- 이슈 본문의 체크리스트 = 완료 조건. 전부 체크되어야 PR 머지 가능
- 작업이 너무 크면 하위 이슈로 쪼개기

### 라벨

| 종류 | 라벨 |
| --- | --- |
| 유형 | `type: feature`, `type: bug`, `type: chore`, `type: docs` |
| 영역 | `area: xr`, `area: kart`, `area: item`, `area: track`, `area: race`, `area: ai`, `area: ui`, `area: audio`, `area: build` |
| 담당 | `담당: 이창민`, `담당: 윤승희`, `담당: 공동` |
| 우선순위 | `priority: P0` (필수), `priority: P1` (중요), `priority: P2` (여유 시) |
| 상태 | `status: blocked` (다른 작업 대기), `breaking` (인터페이스 변경) |

---

## 2. 브랜치

`main`에서 분기하고, 이름은 `<유형>/<이슈번호>-<짧은-설명>` (영어 소문자, 하이픈)

| 유형 | 용도 | 예시 |
| --- | --- | --- |
| `feature/` | 새 기능 | `feature/3-kart-physics` |
| `fix/` | 버그 수정 | `fix/21-lap-count-double` |
| `chore/` | 설정, 패키지, 빌드 | `chore/1-project-setup` |
| `docs/` | 문서 | `docs/17-update-roles` |

```bash
git switch main
git pull
git switch -c feature/3-kart-physics
```

- 브랜치는 머지되면 자동 삭제됨
- 작업이 길어지면 main 변경 사항을 자주 받아오기: `git pull origin main` (충돌은 작을 때 해결)

---

## 3. 커밋

형식: `<type>: <무엇을 했는지> (#이슈번호)` — 한글 OK

| type | 의미 |
| --- | --- |
| `feat` | 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 동작 변화 없는 코드 개선 |
| `asset` | 모델/텍스처/사운드/씬/프리팹 등 에셋 변경 |
| `chore` | 설정, 패키지, 빌드 |
| `docs` | 문서 |

```
feat: 핸들 Grip 시 회전값으로 조향 입력 계산 (#2)
asset: 테스트 트랙 커브 구간 추가 (#7)
fix: 체크포인트 역방향 통과 시 랩이 증가하는 문제 (#9)
```

- 커밋은 **의미 단위**로 작게. "작업 중", "ㅁㄴㅇㄹ" 같은 메시지 금지
- `.meta` 파일은 **반드시 원본과 함께** 커밋 (빠지면 참조가 깨짐)
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` 는 커밋 금지 (`.gitignore` 처리됨)

---

## 4. Pull Request

1. push 후 GitHub에서 PR 생성 (템플릿 자동 적용)
2. 제목: `[영역] 내용 (#이슈번호)` — 예) `[Kart] 카트 주행 물리 구현 (#3)`
3. 본문에 **`Closes #3`** → 머지 시 이슈 자동 종료
4. 변경 내용, **테스트 방법**, 스크린샷/GIF(가능하면) 첨부
5. Reviewer는 상대방 (자동 지정됨), Assignee는 본인, 라벨·마일스톤은 이슈와 동일하게
6. 작업 중이면 **Draft PR**로 올려도 좋음

### 머지 조건 (GitHub에서 강제)

- ✅ 상대방 **1명 승인**
- ✅ 모든 리뷰 코멘트 **Resolve**
- ✅ 승인 후 새 커밋을 push하면 **다시 승인** 필요
- ✅ main에 직접 push / force push / 브랜치 삭제 불가 (관리자 포함)

### 머지 방식

- **Squash and merge만 허용** → main 히스토리가 PR 단위로 깔끔하게 남음
- 머지 커밋 제목은 PR 제목 그대로 사용
- 머지는 **PR 작성자**가 승인 받은 후 직접 수행

---

## 5. 코드 리뷰

리뷰어는 24시간 안에 리뷰합니다. 확인할 것:

- [ ] 이슈의 완료 조건을 만족하는가
- [ ] 브랜치를 받아서 Unity에서 Play 했을 때 에러 없이 동작하는가
- [ ] 담당 외 씬/폴더를 건드리지 않았는가 (건드렸다면 이유가 있는가)
- [ ] 인터페이스([ARCHITECTURE.md](docs/ARCHITECTURE.md))를 바꿨다면 `breaking` 라벨과 설명이 있는가
- [ ] 불필요한 파일(대용량 테스트 에셋, 빈 폴더, `Debug.Log` 남발)이 없는가

코멘트 접두어로 의도를 표시합니다.

| 접두어 | 의미 |
| --- | --- |
| `[필수]` | 고쳐야 머지 가능 |
| `[제안]` | 고치면 좋음, 작성자 판단 |
| `[질문]` | 이해를 위한 질문 |

---

## 6. Unity 협업 규칙 (충돌 방지)

Unity는 씬/프리팹 충돌이 나면 해결이 매우 어렵습니다. 아래를 꼭 지켜 주세요.

### 프로젝트 설정 (이슈 #1에서 적용)

- Unity 버전: **Unity 6 LTS 단일 버전** — 버전 올릴 때는 합의 후 별도 PR
- `Edit > Project Settings > Editor`
  - Version Control Mode: **Visible Meta Files**
  - Asset Serialization Mode: **Force Text**
- 패키지 추가/변경은 `Packages/manifest.json`, `packages-lock.json`을 함께 커밋하고 PR 설명에 적기

### 씬 & 프리팹

- **씬 하나는 한 사람만 수정** (담당은 [ROLES.md](docs/ROLES.md))
- 기능 개발·실험은 **개인 Sandbox 씬**에서: `Scenes/Sandbox/Changmin_Kart.unity`
- 씬에 직접 오브젝트를 만들기보다 **프리팹으로 만들어** 씬에 배치 → 수정은 프리팹에서
- 큰 프리팹은 **중첩 프리팹(Nested Prefab)** 으로 쪼개서 동시 수정 가능성 줄이기
- 상대 담당 씬을 꼭 수정해야 하면 이슈/카톡으로 먼저 말하고, 그 동안 상대는 해당 씬 수정 중지

### 에셋 & Git LFS

- 텍스처, 모델, 오디오, 영상 등 바이너리는 **Git LFS**로 관리 (`.gitattributes`에 설정됨) → 클론 전에 `git lfs install`
- GitHub 무료 LFS 용량은 1GB → 4K 텍스처, 고용량 원본 파일은 올리지 말고 압축/축소 후 사용
- 외부 에셋은 필요한 파일만 `Assets/ThirdParty/`에 넣고, 출처/라이선스를 PR에 적기

### 충돌이 났을 때

1. `.cs` 충돌 → 일반 코드처럼 해결
2. `.unity` / `.prefab` 충돌 → **혼자 해결하지 말고 상대에게 공유**
   - 보통은 한쪽 버전을 택하고(`git checkout --theirs/--ours <파일>`) 다른 쪽 변경을 Unity에서 다시 적용
3. 해결 후 Unity에서 열어서 Missing Reference가 없는지 확인

---

## 7. 완료의 정의 (Definition of Done)

- [ ] 이슈의 체크리스트 모두 완료
- [ ] Unity 콘솔 에러/경고(우리 코드 기준) 없음
- [ ] XR Device Simulator 또는 실기기에서 동작 확인
- [ ] 담당 문서(필요 시 `docs/`) 업데이트
- [ ] 상대방 승인 후 Squash Merge, 이슈 자동 종료 확인
