# AGENTS.md — 여우 복셀 3D 게임

Codex 같은 코딩 에이전트가 이 저장소에서 작업할 때 읽는 안내입니다.

## 저장소
- Unity 6000.3.10f1 프로젝트: `FoxGame/` (URP, Input System)
- Blender 복셀 모델 생성 스크립트: `Blender/`
- 문서: `개발계획서.md/.html`, `이슈기록.md/.html`, `README.md`
- git은 PATH에 없음 → `%LOCALAPPDATA%\GitHubDesktop\app-*\resources\app\git\cmd\git.exe` (git 명령이 꼭 필요하지 않으면 쓰지 않는다)

## 그림 만들기 (이미지 생성)

그림을 요청받으면 이미지 생성 도구로 만들고 **`Art/` 폴더에만** 저장한다. 다른 파일은 고치지 않는다.

### 화풍 가이드 (게임과 맞추기)
- **복셀**: 모든 것이 정육면체 블록으로 된 마인크래프트풍, 부드러운 조명, 밝고 깨끗한 캐주얼 느낌
- **여우**: 주황 몸, 흰색 가슴·볼·주둥이·꼬리 끝, 검정 코·눈·발·귀 끝, 귀 안쪽 갈색
- **바닥 타일**: 떠 있는 정육면체 타일, 윗면 밝은 연두 풀(두 가지 초록 체크무늬), 아래는 갈색 흙
- **체리**: 빨간 복셀 체리 두 알 + 초록 줄기·잎. 공중 체리는 은은한 금색 빛기둥
- **무너지는 바닥**: 지나간 타일이 금 가고 작은 큐브로 부서지며 떨어짐
- **배경**: 부드러운 하늘색 (#8FC7F2 근처), 쿼터뷰(아이소메트릭) 시점 선호
- 참고 캡처: `FoxGame/Captures/step5_start.png`(전체 맵), `step4_jump.png`(점프), `step8_crumble.png`(무너지는 바닥)
- 요청에 따로 없으면 **글자·로고·UI를 넣지 않는다**

### 파일 규칙
- 이름: 영어 소문자 + 밑줄, `.png` (예: `Art/fox_keyart.png`, `Art/title_bg.png`)
- 같은 이름이 있으면 덮어쓰지 말고 `_2`, `_3`을 붙인다
- 끝나면 저장한 경로, 크기(가로×세로), 그림 설명 한 문장을 답한다

### 실행 방법 (사람용)
```
powershell -ExecutionPolicy Bypass -File Art/codex-draw.ps1 -Name title_bg -Prompt "타이틀 화면 배경: 여우가 체리 타일 위에서 손을 흔듦"
```
