# 🦊 여우 복셀 3D 게임

Blender로 만든 복셀 여우가 4x4 타일맵을 뛰어다니며 체리 5개를 모으는 Unity 6 캐주얼 게임입니다.

![게임 시작 화면](FoxGame/Captures/step5_start.png)

## 규칙

- 체리 5개를 모두 모으면 **CLEAR!**
- 구멍이나 맵 밖으로 떨어지면 **GAME OVER**
- 구멍 위 공중에 떠 있는 체리(빛기둥 표시)는 점프해야 먹을 수 있습니다

| 키 | 동작 |
|---|---|
| `W` `A` `S` `D` / 방향키 / 왼쪽 스틱 | 이동 |
| `Space` / 게임패드 A | 점프 |
| `R` / 게임패드 Start | 재시작 (언제든) |
| `Enter` / 클릭 | 결과 화면 [다시 하기] |

| 걷기 | 점프 | 결과 |
|---|---|---|
| ![walk](FoxGame/Captures/step4_walk.png) | ![jump](FoxGame/Captures/step4_jump.png) | ![clear](FoxGame/Captures/step6_clear.png) |

## 실행

1. [Unity Hub](https://unity.com/download)에서 **Add** → `FoxGame` 폴더 선택 (Unity **6000.3.10f1**)
2. `Assets/Scenes/FoxGame.unity` 열기 → **Play**

## 구성

| 폴더 | 내용 |
|---|---|
| `Blender/` | 복셀 모델 생성 스크립트 (`make_fox.py`, `make_cherry.py`) 와 결과물(.blend / .fbx) |
| `FoxGame/Assets/Scripts/` | 게임 코드 — 이동·점프, 부위 회전 애니메이션, 아이템, 게임 상태, HUD, 결과 화면 |
| `FoxGame/Assets/Editor/` | 단계별 씬 셋업·검증 스크립트 (메뉴 **Tools > Fox**) |
| `FoxGame/Assets/Tests/PlayMode/` | Play 모드 테스트 (가상 키보드로 실제 게임 루프 확인) |
| `개발계획서.md` / `.html` | 단계별 개발 계획과 진행 기록 |

### 모델 다시 만들기

```bash
blender -b -P Blender/make_fox.py
```

```bash
blender -b -P Blender/make_cherry.py
```

만들어진 `.fbx`를 `FoxGame/Assets/Models/`에 복사하면 Unity가 다시 가져옵니다.

## 검증

- **Tools > Fox > 전체 검증**: 편집 모드 시뮬레이션 21건 (이동, 점프, 애니메이션, 아이템, 게임 결과)
- **Window > General > Test Runner > PlayMode > Run All**: Play 모드 테스트 7건 (키 입력, 게임 오버, 재시작)

## 사용 도구

Unity 6 (URP, Input System) · Blender 5.2
