# 소울라이크 버티컬 슬라이스 로드맵

## 문서 분류
- 문서 유형: 총괄 실행 로드맵
- 역할: 현재 구현을 기준으로 앞으로 어떤 순서로 소울라이크 버티컬 슬라이스를 완성할지 정리하는 문서다.

## 문서 목적
- 활성 작업을 장기 히스토리와 분리한 상태에서 다음 구현 단계를 한 장으로 본다.
- 각 스레드 우선순위를 프로젝트 레벨 순서로 묶는다.
- 다음 단계에 들어가기 전에 무엇을 완료로 볼지 빠르게 판단할 기준을 둔다.

## 우선 참조 문서
1. `Docs/Rules/ThreadOperationRules.md`
2. `Docs/Reports/ProgressReport.md`
3. `Docs/Reports/ThreadHandoffInbox.md`
4. `Docs/Setup/GameLoopExperienceGuide.md`
5. `Docs/Setup/BossAIFrameworkRefactorGuide.md`
6. `Docs/Setup/PlayerCombatRefactorStages.md`

## 현재 기준 요약
- `2026-03-20` 기준 직전 활성 이슈였던 이동 중 발도 / 납도 상하체 미분리 문제는 해결되었다.
- 다음 단계의 중심은 개별 버그 대응이 아니라 버티컬 슬라이스 완성도 상승이다.
- 프로젝트 우선순위는 아래 세 축으로 본다.
  - 플레이어 전투 체감 고도화
  - 보스 AI 시스템화
  - 게임 루프 완성도와 회귀 검증 정착

## 단계별 로드맵

### 1단계. 플레이어 전투 감각 고도화
- 목표:
  - 기본 조작만으로도 소울라이크다운 리듬이 느껴지게 만든다.
- 핵심 범위:
  - `3타 콤보` 연결감
  - `Guard_Start / Guard_Loop / Guard_End / Guard_Accept` 유지 검수
  - `HitFront / HitBack` 피격 반응 유지 검수
  - 이동 중 전투 상태 전환과 locomotion 유지감 검수
  - 필요 시 `PlayerCombatModule`, `PlayerAttackFacingModule` 책임 분리 준비
- 완료 기준:
  - 공격 1~3타의 연결이 끊기지 않고 체감상 리듬이 안정적이다.
  - 가드 수용, 유지, 종료가 눈에 띄게 끊기지 않는다.
  - 피격 인터럽트와 공격 중 전환이 모션과 판정 기준에서 어긋나지 않는다.
  - 이동 중 전투 상태 전환에서 하체 locomotion과 상체 액션이 안정적으로 공존한다.
- 관련 문서:
  - `Docs/Rules/AnimationThreadRules.md`
  - `Docs/Rules/PlayerCoreCombatThreadRules.md`
  - `Docs/Setup/PlayerAnimatorSetupGuide.md`
  - `Docs/Setup/CombatDamagePipelineGuide.md`
  - `Docs/Setup/PlayerCombatRefactorStages.md`

### 2단계. 보스 AI 시스템화
- 목표:
  - 현재 보스전을 "동작하는 보스" 수준에서 "확장 가능한 보스 프레임워크" 수준으로 올린다.
- 핵심 범위:
  - `BossCombatRuntime`
  - `BossPatternSelector`
  - `BossAttackExecutor`
  - `BossPhaseController`
  - 공격 정의 데이터화
- 완료 기준:
  - `BossController`에 몰린 전투 상태, 패턴 선택, 공격 실행, 페이즈 전환 책임이 분리된다.
  - 패턴 추가 시 상태 파일을 억지로 늘리기보다 데이터와 선택 규칙으로 확장 가능하다.
  - `Slam / Stomp / Dash` 같은 기존 패턴의 역할 구분이 코드 구조에도 드러난다.
- 관련 문서:
  - `Docs/Rules/BossAIThreadRules.md`
  - `Docs/Setup/BossAIFrameworkRefactorGuide.md`
  - `Docs/Setup/BossPatternSpec.md`

### 3단계. 게임 루프와 전투 피드백 완성도 정리
- 목표:
  - 플레이어가 `진입 -> 조우 -> 결투 -> 결말` 흐름을 한 번에 이해할 수 있게 만든다.
- 현재 착수 상태:
  - `2026-03-20` 착수
  - 1차 하위 작업으로 `UIBossHealthBar`, `UIEstusFlask`의 TextMeshPro 텍스트 바인딩 정리를 시작
- 현재 확인 메모:
  - `GameLoopCanvas.prefab`에는 이미 `UIEstusFlask` 기준본이 존재한다.
  - 따라서 남은 문제는 "프리팹 부재"보다 "코드와 자산 타입 정합성", "씬/프리팹 기준 재검증"에 가깝다.
- 핵심 범위:
  - 보스전 시작 -> 안개벽 -> 보스 UI -> BGM 흐름
  - 플레이어 사망 -> `YOU DIED` -> 체크포인트 리스폰
  - 보스전 종료 후 안개벽 / 트리거 / UI 상태 정리
  - 플레이어 상태 UI / 에스투스 UI 갱신 구조 점검
- 완료 기준:
  - 보스전 시작과 종료가 연출, UI, 공간 상태 기준으로 일관되게 보인다.
  - 리스폰 후에도 체크포인트, UI, 씬 참조가 다시 안정적으로 붙는다.
  - `SceneReferenceValidator` 경고 없이 기준 씬이 유지된다.
- 관련 문서:
  - `Docs/Rules/GameLoopUIThreadRules.md`
  - `Docs/Setup/GameLoopExperienceGuide.md`
  - `Docs/Setup/GameLoopUIRefactorGuide.md`
  - `Docs/Setup/BossArenaLevelSpec.md`

### 4단계. 카메라 / 락온 체감 재정리
- 목표:
  - 플레이어 조작이 완성된 뒤 시점이 그것을 방해하지 않게 만든다.
- 핵심 범위:
  - 락온 프레이밍
  - 락온 해제 후 자유시점 복귀 안정성
  - 이동 / 회피 / 공격 중 카메라 추적 체감
- 완료 기준:
  - 보스와 플레이어가 동시에 프레임 안에서 읽힌다.
  - 락온 해제 직후 시점이 튀거나 과하게 상향되지 않는다.
  - 전투 입력과 카메라 복귀 타이밍이 충돌하지 않는다.
- 관련 문서:
  - `Docs/Rules/CameraLockOnThreadRules.md`
  - `Docs/Setup/CameraLockOnRefactorGuide.md`

### 5단계. 검증 루프 정착
- 목표:
  - 기능 추가보다 늦게 검증하는 운영을 끝내고, 단계마다 회귀 기준을 남긴다.
- 핵심 범위:
  - 날짜 기반 검증 리포트 작성
  - `ThreadHandoffInbox.md` 기준 인계 정리
  - 스모크 테스트와 회귀 테스트 체크리스트 고정
- 완료 기준:
  - 각 단계 종료 시 `ValidationCurrentStatus`, 스모크 테스트, 필요 시 회귀 리포트가 남는다.
  - 활성 이슈와 보관 이슈가 섞이지 않는다.
  - 다음 작업자가 긴 문서를 다 읽지 않아도 현재 상태를 파악할 수 있다.
- 관련 문서:
  - `Docs/Rules/ValidationRegressionThreadRules.md`
  - `Docs/Reports/README.md`
  - `Docs/Reports/ValidationReportTemplate.md`

## 추천 실행 순서
1. 1단계 플레이어 전투 감각 고도화
2. 2단계 보스 AI 시스템화
3. 3단계 게임 루프와 전투 피드백 완성도 정리
4. 4단계 카메라 / 락온 체감 재정리
5. 5단계 검증 루프 정착

## 운영 규칙
- 새 기능 구현 전에 현재 단계의 완료 기준과 제외 범위를 먼저 적는다.
- 단계가 끝나면 `ProgressReport.md`, 관련 `Rules`, 관련 `Setup`, 필요 시 `ThreadHandoffInbox.md`를 같은 턴에 갱신한다.
- 작업 과정 로그가 길어지면 `Docs/History/`로 옮기고 현재 문서에는 요약만 남긴다.
- 인계가 스레드 간으로 갈라질 때만 `ThreadHandoffInbox.md`에 올린다.

## 다음 바로 착수할 후보
1. 플레이어 전투 체감 검수 회차를 열고 `3타 콤보`, `가드`, `피격 인터럽트` 체크리스트 작성
2. 보스 AI 리팩터링 1차 착수 범위를 `BossCombatRuntime`로 고정
3. 기준 씬 `developScenes.unity`에서 보스전 시작/종료와 리스폰 스모크 테스트 한 회차 수행
4. `GameLoopCanvas.prefab` / `developScenes.unity` 기준 UI 숫자 표시와 `SceneReferenceValidator` 로그 재확인
