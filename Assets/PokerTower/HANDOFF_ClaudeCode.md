# POKER TOWER · Claude Code 인계 문서 (2026-11-14, v1.3 반영)

## 0. 역할
- 너(Claude Code, 이 PC): **Unity 구현 담당.** 컴파일, 실행, 디버깅, 기능 구현을 맡는다.
- Claude 채팅 세션: **검증 담당.** 기획서와 구현 대조, Godot판과 수치 비교, 기획서 갱신안 작성.
- Codex: 같은 기획서로 **Godot판**을 만든다 (`문서\GodotProjects`). Unity판과 규칙 결과가 같아야 한다.
- 사용자는 기획자이고 프로그래밍 초보다. 설명은 한국어로, 코드 용어에는 짧은 풀이를 붙인다.

## 1. 기준 문서 (Source of Truth)
- 기획서(Google Docs): https://docs.google.com/document/d/1Xbi0chTuKRvgtadPPEduZx6ohqfat9vDn2VfIsKg1mk/edit
  - 현재 기준 버전: v1.3 (2026-11-14). v1.2(턴 종료 버리기·스탯·에너미 등급·치명타) + v1.3(등급별 포인트, 레벨별 투자 비용, 플레이어 치명타율 스탯, 손패 최대 10장·강제 버리기). 사용자가 계속 갱신한다.
- 우선순위: 사용자의 최신 지시 > 최신 기획서 > `기획_반영_메모.md` > 현재 코드
- 기획서의 **[확정]** 값은 임의로 바꾸지 않는다. **미정** 항목을 일반 포커 규칙이나 다른 게임 관례로 채워 확정하지 않는다.
  - 구현에 값이 꼭 필요하면 `GameConfig.cs`에 `[가정]` 주석을 달아 넣고 보고한다.
- 기획서 23.1에는 "Godot 확정, Unity 폐기"라고 적혀 있지만, 현재는 사용자 결정에 따라 두 엔진을 병행한다. 기획서 갱신 전까지 이 상태를 유지한다.

## 2. 프로젝트 상태
- 경로: `C:\Users\USER\Documents\UnityProject\poker tower` (Unity 6000.6.9f1, URP 템플릿, Input System 1.20.1, uGUI 2.6.0)
- 게임 코드: `Assets/PokerTower/` (v0.2, 기획서 v1.2 반영)
- Unity에서 컴파일 오류 0. EditMode 테스트 53개 전부 통과 (`RuleTests` 22 + `RuleTestsV12` 23 + `RuleTestsV13` 8). 셀프체크 26항목 fail=0.
- 설치된 패키지: `com.unity.test-framework` 1.8.0 (manifest 직접 등록), `com.unity.pipeline` 1.0.0 (Claude Code가 CLI로 에디터를 조종할 때 사용).
- CLI 사용 팁: 프로젝트 경로에 공백이 있어 `--project-path` 대신 `UNITY_PROJECT_PATH` 환경 변수를 쓴다. 명령 이름은 `test run`, `console get`, `capture game_view`, `script eval`.
- Game 뷰는 1920×1080 고정으로 맞춰 둠. 프로젝트는 Linear 색 공간이라 반투명 UI 패널 뒤 글자가 생각보다 밝게 비친다 (가려야 하면 불투명으로).
- 저장: PlayerPrefs `PokerTower.Floor`(현재 층), `PokerTower.Progress`(형식 `v3;hp,spd,draw,def,dmg,crit;남은포인트;최고클리어층;속성강화7개;고를후보` — 스탯 값은 레벨. 예전 `v2`·`v1`도 읽음).
- git 저장소가 아니다. 버전 관리 설정 여부는 사용자에게 먼저 물어본다.

## 3. 구조
| 경로 | 내용 |
|---|---|
| `Core/` (asmdef `PokerTower.Core`, noEngineReferences) | 순수 C# 규칙. Godot판과 비교하기 쉽게 Unity 참조 금지 |
| `Core/GameConfig.cs` | 모든 수치. `[확정]`은 기획서 값, `[가정]`은 임시값 |
| `Core/HandEvaluator.cs` | 족보 판정·기본 피해 (기획서 07장 v0.3) |
| `Core/DamageCalculator.cs` | 최종 피해·7속성 효과·금 방깎 연속 규칙 (08장, 06.2, 11.2) |
| `Core/TurnScheduler.cs` | CTB: AV = 10000/SPD, 행동 비용 동일, 수 속성 일시 SPD 1회 반영 |
| `Core/Battle.cs` | 전투 진행: Begin → NextTurn → Submit(공격만) → [PlayerDiscard] EndPlayerTurn(버릴 카드) → [PlayerForcedDiscard] ForceDiscard(넘는 장수) / RunEnemyTurn. 에너미 등급별 행동·치명타. `StageSetup.CreateRandom` = 스테이지 속성 무작위 결정 |
| `Core/Deck.cs` | 덱·손패·묘지·사용 중(InPlay). 이번 턴 사용·버린 카드는 보충이 끝난 뒤 묘지로 → 재섞기에서 제외 |
| `Core/Progress.cs` | 스탯 6종 레벨(`PlayerStats`, N레벨 N점)과 등급별 포인트 지급·비용 계산·확정·저장 문자열(`PlayerProgress`) |
| `Core/SimpleAi.cs` | 족보별 후보 탐색(손패 장수 무관하게 빠름), 막힘 판정(개수 세기), 자동 버리기, `PlayTurn` |
| `Core/SelfCheck.cs` | 테스트 프레임워크 없이 도는 QA 셀프체크 |
| `Game/` (asmdef `PokerTower.Game`) | uGUI·3D 연출. 규칙 계산 금지, Core 결과만 표시 |
| `Game/PokerTowerGame.cs` | `[RuntimeInitializeOnLoadMethod]`로 아무 씬에서나 Play 시 자동 생성. 타이틀 → 스테이지 속성 공개(5초, 시스템이 5속성·메인 무작위 결정) → 전투 → 결과 |
| `Editor/PokerTowerDevReport.cs` | 컴파일 결과를 `Logs/PokerTower_compile.txt`에, 셀프체크 결과를 `Logs/PokerTower_selfcheck.txt`에 기록 |
| `Game/CardWidget.cs` · `Game/CardHover.cs` | 카드 1장 UI. 카드 안에는 랭크와 속성 문양만. 속성 배경 Sprite(3:4) + 랭크(카드 Y 0.75에 대문자 높이 중심, 글자 크기 = 카드 높이 × 0.40, 라이닝 숫자 세리프, 판 없음). 호버 시 확대 |
| `Game/PokerTowerGame.cs` 연출 | `DamagePopup`(메인 피해 숫자), `Popup`(부가 효과), `UpdatePlayerBar`(HP·쉴드 표시값이 규칙 값을 따라감). 모두 표시 전용 — 진행은 Battle.Phase 기준 |
| `Game/MainElementHud.cs` | 메인 속성 HUD (전투 화면 오른쪽). 상태 없이 `Battle.Setup.MainElement`를 넘겨 갱신. 아이콘은 카드 그림 하단 문양 영역 |
| `Resources/PokerTowerCards/` | 카드 배경 7종 (랭크 없는 v2, 1086×1448, Sprite·비압축). 원본은 `Assets/disign`, 바깥 회색 배경 투명화는 `Tools/cut_card_art.py` |
| `Tests/Editor/RuleTests.cs` | NUnit EditMode 테스트 22개 (기획서 QA-DMG·GOLD·ATTR·SPD) |
| `Tests/Editor/RuleTestsV12.cs` | v1.2 테스트 23개 (버리기·스탯·등급·치명타·탐색 정확성/속도·스테이지 무작위) |
| `Tests/Editor/RuleTestsV13.cs` | v1.3 테스트 8개 (포인트 지급·레벨 비용/환불·플레이어 치명타·손패 10장 강제 버리기) |
| `기획_반영_메모.md` | 임시 결정 목록과 기획 결정이 필요한 항목 |

## 4. 진행 기록과 다음 할 일
- [완료 2026-11-14] 컴파일 확인, 셀프체크, 테스트 프레임워크 설치·테스트 실행, Play 확인 (v0.1)
- [완료 2026-11-14] 카드 그림(랭크 없는 7종)·랭크 규격 통일·메인 속성 HUD, 스테이지 속성 무작위 + 공개 화면(5초, 클릭으로 넘기기), 용어 '메인 속성' 통일
- [완료 2026-11-14] 기획서 v1.3 반영: 등급별 포인트(1/2/3), 레벨별 투자 비용, 치명타율 스탯·플레이어 치명타, 손패 최대 10장·강제 버리기
- [완료 2026-11-14] 카드 추가 드로우 레벨당 5%p → 4%p (사용자 결정)
- [완료 2026-11-14] 기획서 18.3 전투 표시: 족보 이름만 한 줄 미리보기, 피해 숫자 연출(크기 3단계·TRUE·CRITICAL·속성 외곽선·스케일 펀치), 부가 효과는 작게·늦게·다른 위치, 체력바 흰색 쉴드 구간(쉴드 먼저 줄어드는 애니메이션), 카메라 각도 조정(에너미 모델이 족보 이름과 안 겹치게)
- [완료 2026-11-14] 보스 첫 처치 보상 '속성 강화'(3개 중 선택, 중첩, 대표 카드 속성 피해 +5%/중첩), 저장 형식 v3, 결과 화면에 보유 포인트 합계 표시
- [완료 2026-11-14] 열린 결정 회신 반영: A1 교체 언제나 가능('교체' 버튼), A8 추가 드로우 카드는 그 턴에만(청록 테두리·안 쓰면 턴 종료 때 묘지·보충 없음), A4·A6·A7 현재 동작 확정
- [완료 2026-11-14] 기획서 v1.2 반영: 턴 종료 버리기, 스탯 포인트·스탯 화면, 손패 장수 제한 없음(보유 카드 존 압축 배치, 빠른 탐색), 에너미 등급·치명타, 개선 4건(교체 로그·쓰러진 에너미 상태·행동 순서 바 겹침·메인 속성 버튼 가독성)
- 다음 후보 (사용자 확인 후 진행):
  1. A5 족보·속성 강화 보상 수치 (기획 결정 필요)
  2. Godot판과 v1.2 규칙 결과 대조 (같은 시드·같은 입력)
  3. 실제 플레이 감각 확인: 버리기 단계가 매 턴 한 번씩 더 눌러야 해서 템포가 느려지는지
- 작업 후 보고는 5장 형식으로.

## 5. 보고 형식 (사용자 선호)
- 변경한 것 / 이유 / 게임에서 달라지는 점 / 검증(실제로 한 것만) / 추가 발견 사항(반드시 수정 권장 · 개선 권장 · 아이디어)
- 테스트하지 않은 것을 테스트했다고 쓰지 않는다.

## 6. 열려 있는 기획 결정 (구현으로 임의 확정 금지)
- A2. 기획서 23.1 엔진 문구 갱신 필요
- A3. '메인 속성' 용어 이중 사용 → UI에서는 '공격 속성'(1번 카드)과 '메인 속성'(스테이지)으로 구분
- A5. 속성 강화는 구현(보스 보상, +5% 수치 미확정). 족보 강화는 미정·미구현. 자동 플레이 승률(층마다 200전, 속성 무작위, A8 반영 후): 투자 없음 1층 100% · 5층 19.5% · 9층 26.5% · 10층 0% / SelfCheck 분산 투자 1층 100% · 5층 39.0% · 9층 53.0% · 10층 0%
- (결정 완료 2026-11-14) A1·A4·A6·A7·A8 → `기획_반영_메모.md` A·D·E장

## 7. 작업 원칙
- 최소 변경. 관련 없는 시스템은 건드리지 않는다. 대규모 리팩터링은 먼저 이유와 범위를 설명한다.
- 규칙은 Core에, 표시는 Game에 둔다. 같은 계산을 UI에 복사하지 않는다.
- 연출 시간이나 `setTimeout`류 타이밍을 규칙 판정에 쓰지 않는다. 상태 전이는 `Battle.Phase`를 기준으로 한다.
- 에너미 AI는 플레이어의 비공개 카드를 참조하지 않는다.
- 아트 방향(기획서 19장)은 미정이다. 임시 도형 아트를 정식 아트처럼 다듬는 데 시간을 쓰지 않는다.
- 커밋·푸시는 사용자가 요청할 때만 한다.
