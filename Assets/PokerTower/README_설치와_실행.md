# POKER TOWER · Unity 프로토타입 v0.1

기획서 `POKER_TOWER_상세_게임_기획_작성_템플릿` v1.1(전투·속성 v0.5, 2026-11-12) 기준입니다.

## 1. 처음 한 번만: 프로젝트 만들기 (약 5분)

1. **Unity Hub** 실행 → **New project**
2. 에디터 버전 **6000.6.9f1** 선택 → 템플릿 **Universal 3D**(또는 3D Built-in) 선택
3. Project name: `PokerTower` / Location: `문서\UnityProjects` → **Create project**
4. 에디터가 열리면 **이 `PokerTower` 폴더 통째로** 프로젝트의 `Assets` 폴더 안에 복사
   - 최종 위치: `문서\UnityProjects\PokerTower\Assets\PokerTower\...`
5. Unity로 돌아가면 자동으로 컴파일됩니다. 하단 **Console** 창에 빨간 오류가 없으면 성공입니다.

## 2. 실행

- 아무 씬(기본 `SampleScene`)에서 상단 **▶ Play**를 누르면 됩니다. 씬을 편집할 필요가 없습니다.
- 게임 창 해상도는 **1920×1080**(Game 탭 상단 해상도 메뉴)을 권장합니다.
- 흐름: 타이틀(현재 N층 / 이어하기·새로하기) → 5속성 + 메인 속성 편성 → 전투 → 결과

### 조작
- 아래 **보유 카드 존**의 카드를 클릭하면 위의 **사용 카드 존**으로 올라갑니다. 한 번 더 클릭하면 돌아갑니다.
- 처음 올린 카드가 **1번(대표)**이고, 그 속성이 공격 속성이 됩니다. 카드 존 색상도 그 속성 색으로 바뀝니다.
- ★ 표시는 스테이지 메인 속성 카드입니다.
- **공격**으로 제출합니다. **묘지**에서 사용한 카드를 볼 수 있습니다.
- 왼쪽 **행동 순서**가 SPD 기반 예상 턴 순서입니다.
- 우측의 **자동 ON/OFF**는 테스트용 자동 플레이입니다.

## 3. 규칙 테스트 실행

`Window > General > Test Runner > EditMode > Run All`
기획서 24장의 QA 기대값(QA-DMG, QA-GOLD, QA-ATTR, QA-SPD 등)을 그대로 옮긴 테스트입니다.

## 4. 폴더 구조

| 폴더 | 역할 |
|---|---|
| `Core/` | 규칙 코드. Unity를 참조하지 않는 순수 C#. 족보 판정, 피해 공식, 7속성, CTB 속도, 덱/묘지, 에너미 행동 |
| `Core/GameConfig.cs` | **모든 수치가 모인 곳.** `[확정]`은 기획서 값, `[가정]`은 기획서 미정 항목에 넣은 임시값 |
| `Game/` | Unity 화면·연출. 규칙 계산 없이 Core의 결과만 표시 |
| `Tests/Editor/` | 규칙 자동 테스트 |

기획서 수치가 바뀌면 대부분 `GameConfig.cs` 한 파일만 고치면 됩니다.

## 5. Claude Code에서 Unity 직접 조작하기 (권장)

Unity 공식 Claude Code 플러그인을 쓰면 Claude Code가 에디터 콘솔 확인, 테스트 실행, 씬 조작을 직접 할 수 있습니다.
프로젝트 폴더에서 `claude`를 실행한 뒤:

```
/plugin marketplace add Unity-Technologies/unity-agent-plugin
/plugin install unity@unity-agent-plugin
```

설치 후 `/unity:`를 입력하면 Unity 스킬 목록이 보입니다.
