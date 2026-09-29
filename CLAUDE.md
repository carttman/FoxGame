# FoxGame 작업 안내

이 파일은 Claude Code가 이 저장소에서 작업할 때 참고하는 프로젝트 지침입니다.

## 프로젝트

- Unity 6(`6000.3.10f1`) 기반의 3D 복셀 여우 게임입니다.
- 실제 Unity 프로젝트 루트는 `FoxGame/`입니다.
- 메인 씬은 `FoxGame/Assets/Scenes/FoxGame.unity`입니다.
- 런타임 코드는 `FoxGame/Assets/Scripts/`, 에디터 도구는 `FoxGame/Assets/Editor/`에 있습니다.
- Blender 원본과 생성 스크립트는 `Blender/`에 있습니다.

## 작업 원칙

- 기존 사용자의 변경 사항을 덮어쓰거나 되돌리지 않습니다.
- Unity가 생성하는 `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 커밋하지 않습니다.
- Unity 에셋을 추가하거나 이동할 때 대응하는 `.meta` 파일도 함께 관리합니다.
- 게임 동작을 바꾼 뒤에는 가능한 범위에서 `Tools > Fox > 전체 검증`과 PlayMode 테스트를 확인합니다.
- 세부 진행 내역과 알려진 문제는 `개발계획서.md`, `이슈기록.md`를 참고합니다.

## 로컬 실행

1. Unity Hub에서 `FoxGame/` 폴더를 엽니다.
2. `Assets/Scenes/FoxGame.unity`를 열고 Play를 실행합니다.
3. WebGL 빌드 미리보기는 `.claude/launch.json`의 `foxgame-webgl` 구성을 사용합니다.

## Claude 인증(OAuth)

- Claude Code의 OAuth 로그인 정보는 프로젝트 파일이 아니라 각 사용자의 로컬 계정 영역에서 관리합니다.
- 새 환경에서는 저장소 루트에서 Claude Code를 실행한 뒤 표시되는 로그인 절차를 완료합니다.
- 액세스 토큰, 갱신 토큰, 세션 쿠키 또는 개인 API 키를 이 저장소에 기록하거나 커밋하지 않습니다.
- 프로젝트에서 외부 서비스 인증이 필요해지면 비밀값은 환경 변수 또는 로컬 전용 설정에 두고, 저장소에는 변수 이름과 설정 예시만 남깁니다.

