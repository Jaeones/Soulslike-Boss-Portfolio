# 게임 루프 / UI 리팩토링 가이드

## 문서 분류
- 문서 유형: 세팅 / 구조 가이드 문서
- 역할: 게임 루프 / UI / 씬 연결 스레드에서 프레임워크 시스템화 리팩토링을 실제로 진행하기 위한 설계 기준 문서다.

## 문서 목적
이 문서는 현재 `GameLoopManager`, `CheckpointManager`, `BossZoneTrigger`, `FogWallController`,
`UIBossHealthBar`, `UIPlayerStatusBars`, `UIEstusFlask`, `UIYouDied`,
그리고 씬 / 프리팹 참조 연결이 섞여 있는 구조를
`이벤트`, `흐름 조립`, `체크포인트 등록`, `UI 바인딩`, `씬 참조 검증` 관점으로 분해하기 위한 기준 문서다.

이 문서의 목표는 새 기능 추가가 아니라,
이미 구현된 게임 루프와 UI 연결을 더 안정적으로 유지 / 검증 / 확장할 수 있는 구조로 정리하는 것이다.

## 우선 참조 문서
1. `Docs/Rules/ThreadOperationRules.md`
2. `Docs/Rules/GameLoopUIThreadRules.md`
3. `Docs/Reports/ProgressReport.md`
4. `Docs/Setup/PlaytestStartGuide.md`
5. `Docs/Reports/ValidationCurrentStatus_20260318.md`

## 6단계 설계 메모 (2026-03-18)
- 목표:
  - 플레이 시작 직후 핵심 씬 / 프리팹 누락을 로그로 빠르게 드러내는 검증 구조 추가
  - `코드는 맞는데 씬 연결이 안 된 상태`와 `의도된 런타임 바인딩 null`을 코드 레벨에서도 구분
- 적용 대상:
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/GameLoopManager.cs`
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointManager.cs`
  - `Assets/00Game/01Scripts/00_Core/BossZone/BossZoneTrigger.cs`
  - `Assets/00Game/01Scripts/05_UI/UIPlayerStatusBars.cs`
  - `Assets/00Game/01Scripts/05_UI/UIEstusFlask.cs`
  - `Assets/00Game/01Scripts/05_UI/UIBossHealthBar.cs`
  - `Assets/00Game/01Scripts/05_UI/UIYouDied.cs`
- 원칙:
  - 실제 전투 / 리스폰 흐름은 바꾸지 않는다.
  - 자동 복구 가능한 참조와 반드시 씬에서 채워야 하는 참조를 구분한다.
  - 검증 구조를 추가한 단계는 관련 `Docs`와 검증 메모를 같은 턴에 갱신한다.
- 적용 결과:
  - `SceneReferenceValidator`가 `Required`, `Recommended`, `RuntimeBound` 3종 분류로 로그를 남기도록 추가됐다.
  - `GameLoopManager`는 씬 내 핵심 UI 컴포넌트 존재 여부를 시작 시점에 점검한다.
  - `CheckpointManager`, `BossZoneTrigger`, `UIPlayerStatusBars`, `UIEstusFlask`, `UIBossHealthBar`, `UIYouDied`는 각자 수동 검증용 `ContextMenu`를 제공한다.
  - `BossZoneTrigger.player`와 플레이어 기반 UI의 `player` 슬롯은 의도된 런타임 바인딩 null로 구분된다.
  - `UIEstusFlask(scene)` 부재와 `UIPlayerStatusBars.hpText`, `staminaText` 비연결은 기준본 보강 필요 로그로 노출된다.
- 완료 기준:
  - 빌드가 유지되고, 플레이 시작 직후 누락 참조가 분류된 로그로 드러나야 한다.
- 다음 확인:
  - `developScenes.unity` 플레이 모드에서 실제 검증 로그 종류가 의도와 일치하는지 확인
  - `GameLoopManager`, `CheckpointManager`의 `DontDestroyOnLoad` 경고를 해소할지 확인
  - `Missing Script` 경고는 소스 오브젝트를 확정한 뒤 범위 포함 여부를 판단

## 7단계 설계 메모 (2026-03-18)
- 목표:
  - `GameLoopCanvas.prefab`에 `UIEstusFlask` 기준본을 실제 배치
  - `EstusPanel`을 HUD 전용 루트로 재구성
- 적용 대상:
  - `Assets/00Game/02Prefabs/GameLoop/GameLoopCanvas.prefab`
  - `Assets/00Game/01Scripts/05_UI/UIEstusFlask.cs`
- 원칙:
  - 코드 구조는 최소 수정으로 유지하고 프리팹 기준 연결을 우선 만든다.
  - 플레이어 바인딩은 기존 `PlayerRespawned` 흐름을 그대로 사용한다.
  - `Missing Script` 경고는 이번 단계와 분리해 기록한다.
- 적용 결과:
  - `EstusPanel`을 비활성 풀스크린 패널에서 좌하단 HUD 패널로 재구성했다.
  - `UIEstusFlask` 컴포넌트와 `countText`, `useSlots` 5칸 기준 연결을 프리팹에 추가했다.
  - 이제 `GameLoopManager`의 `UIEstusFlask(scene)` 검증은 프리팹 기준본이 있는 상태를 전제로 동작한다.
- 표시 정책:
  - 에스투스 카운트 텍스트는 `현재 사용 가능 개수`만 노출한다.
  - `현재 / 최대` 형식은 사용하지 않는다.
- 다음 확인:
  - 플레이 시작 시 `UIEstusFlask(scene)` 경고가 사라지는지
  - 실제 에스투스 사용 / 리필 / 리스폰에서 슬롯과 숫자 표시가 함께 갱신되는지
  - 위 항목은 플레이테스트 기준 통과했다.
  - 플레이어 HP / 스태미나 숫자 텍스트(`hpText`, `staminaText`)는 미사용 정책으로 확정한다.
  - `Missing Script`는 `Boss.prefab > B-spineProxy`의 끊긴 컴포넌트로 특정했고, 사용 흔적이 없어 제거 대상으로 본다.

## 적용 범위
- `Assets/00Game/01Scripts/00_Core/Events/GameLoopEvents.cs`
- `Assets/00Game/01Scripts/00_Core/BossZone/BossZoneTrigger.cs`
- `Assets/00Game/01Scripts/00_Core/BossZone/FogWallController.cs`
- `Assets/00Game/01Scripts/01_Managers/GameLoop/GameLoopManager.cs`
- `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointManager.cs`
- `Assets/00Game/01Scripts/01_Managers/GameLoop/Checkpoint.cs`
- `Assets/00Game/01Scripts/05_UI/UIBossHealthBar.cs`
- `Assets/00Game/01Scripts/05_UI/UIPlayerStatusBars.cs`
- `Assets/00Game/01Scripts/05_UI/UIEstusFlask.cs`
- `Assets/00Game/01Scripts/05_UI/UIYouDied.cs`
- `Assets/00Game/00Scenes/developScenes.unity`
- `Assets/00Game/02Prefabs/GameLoop/GameLoopCanvas.prefab`
- `Assets/00Game/02Prefabs/BossZone/BossZoneTrigger_Entrance.prefab`
- `Assets/00Game/02Prefabs/BossZone/FogWall_Entrance.prefab`
- `Assets/00Game/02Prefabs/CheckPoint/Checkpoint_Start.prefab`

## 제외 범위
- 플레이어 FSM 상세 로직
- 보스 패턴 / AI 로직
- 카메라 회전 / 락온 계산 로직
- 애니메이터 구조 및 애니메이션 이벤트 설계
- HP / 스태미나 / 에스투스 값 계산 규칙 자체

## 현재 구조 핵심 요약

### 이벤트 계층
- `GameLoopEvents`는 보스전 시작 / 종료, 플레이어 사망 / 리스폰, 보스 HP, 에스투스 UI 갱신 이벤트를 발행한다.
- 현재 구조는 전역 정적 이벤트 기반 연결이다.

### 게임 루프 계층
- `GameLoopManager`는 플레이어 사망 이벤트를 받아 `YOU DIED` 연출과 리스폰 시퀀스를 실행한다.
- `CheckpointManager`는 현재 체크포인트와 리스폰 위치 계산을 담당한다.
- `Checkpoint`는 플레이어 진입 시 현재 체크포인트를 등록한다.

### 보스전 계층
- `BossZoneTrigger`는 플레이어 진입 감지, 보스전 시작 조립, 보스전 종료 후처리를 한 클래스에서 담당한다.
- `FogWallController`는 안개벽 표시와 콜라이더 활성화를 제어한다.

### UI 계층
- `UIBossHealthBar`는 보스 HP 이벤트와 보스 인스턴스 바인딩을 함께 처리한다.
- `UIPlayerStatusBars`는 플레이어 직접 바인딩과 UI 갱신을 함께 처리한다.
- `UIEstusFlask`는 이벤트 수신만 담당한다.
- `UIYouDied`는 표시 / 페이드 연출만 담당한다.

### 씬 / 프리팹 계층
- 기준 씬은 `developScenes.unity`다.
- 기준 프리팹은 `GameLoopCanvas`, `BossZoneTrigger_Entrance`, `FogWall_Entrance`, `Checkpoint_Start`다.
- 현재 구조는 일부 참조가 프리팹보다 씬 인스턴스에서만 완성되는 구간이 있어,
  기준본 관리와 참조 검증 책임이 부족하다.

## 현재 구조의 리팩토링 포인트

### 1. 책임 혼합
- `BossZoneTrigger` 안에 트리거 감지, 이벤트 구독, 보스전 시작, UI 연결, BGM 제어, 종료 정책이 함께 들어 있다.
- `GameLoopManager` 안에 이벤트 수신, 연출, 리스폰 요청, 후처리가 함께 들어 있다.

### 2. UI 바인딩 방식 불균형
- 보스 HP UI는 이벤트 수신 + 대상 보스 직접 바인딩을 같이 사용한다.
- 플레이어 상태 UI는 플레이어 직접 바인딩 기반이다.
- 에스투스 UI는 이벤트 수신만 사용한다.
- 즉 UI마다 구독 패턴이 다르므로 유지보수가 어렵다.

### 3. 씬 / 프리팹 검증 포인트 부족
- 코드가 맞아도 인스펙터 슬롯 누락으로 동작하지 않을 수 있다.
- 필수 참조가 비어 있어도 현재는 런타임 중 증상으로만 드러난다.

### 4. 기준본 추적 부족
- 동일 오브젝트가 프리팹 기본값과 씬 인스턴스 오버라이드 중 어디를 기준으로 봐야 하는지 즉시 드러나지 않는다.
- 따라서 "프리팹은 비었는데 씬은 연결됨" 상태를 별도로 다뤄야 한다.

## 리팩토링 목표

### 목표 1. 흐름 조립과 감지를 분리한다.
- 트리거 감지
- 이벤트 발행
- 연출 조립
- 리스폰 후처리
- UI 표시 조립

위 책임이 같은 클래스에 섞이지 않도록 구조를 나눈다.

### 목표 2. UI는 표시 책임만 가진다.
- UI 클래스는 값을 계산하지 않는다.
- 가능하면 UI 클래스는 "누가 값을 줬는지"보다 "값을 받아 표시한다"에 집중한다.
- 바인딩 / 언바인딩 정책은 별도 조립 계층이 담당한다.

### 목표 3. 씬 / 프리팹 참조 검증을 명시화한다.
- 필수 슬롯 검증
- 기준 씬 / 프리팹 확인
- "코드는 맞는데 씬 연결 누락" 상태 기록

### 목표 4. 테스트 가능한 단위로 흐름을 나눈다.
- 보스전 시작
- 보스전 종료
- 플레이어 사망
- 리스폰
- UI 갱신
- 체크포인트 등록

각 단위를 개별적으로 점검할 수 있어야 한다.

## 권장 모듈 구조

### `BossFightFlowCoordinator`
담당 책임:
- 보스전 시작 / 종료 흐름 조립
- `BossZoneTrigger`에서 감지한 입력을 실제 전투 시작 로직으로 연결
- 안개벽 / 보스 UI / BGM / 보스 이벤트 바인딩
- 전투 종료 정책 적용

기존 이동 대상 후보:
- `BossZoneTrigger.StartBossFight()`
- `BossZoneTrigger.BindBossEvents()`
- `BossZoneTrigger.UnbindBossEvents()`
- `BossZoneTrigger.EndBossFight()`
- `BossZoneTrigger.PlayBossBgm()`
- `BossZoneTrigger.StopBossBgm()`

### `PlayerRespawnFlow`
담당 책임:
- 플레이어 사망 이벤트 수신
- `YOU DIED` 연출 진행
- 체크포인트 조회
- 리스폰 요청
- 리스폰 후 UI 후처리

기존 이동 대상 후보:
- `GameLoopManager.HandlePlayerDiedEvent(...)`
- `GameLoopManager.PlayerDeathRespawnSequence()`

### `CheckpointRegistry`
담당 책임:
- 기본 체크포인트
- 현재 체크포인트 등록
- 현재 체크포인트 조회
- 리스폰 위치 계산

기존 이동 대상 후보:
- `CheckpointManager.SetDefaultCheckpoint(...)`
- `CheckpointManager.SetCurrentCheckpoint(...)`
- `CheckpointManager.TryGetRespawnPose(...)`

### `UIBindingMediator`
담당 책임:
- 플레이어 / 보스 / 에스투스 UI 바인딩 진입점 통일
- UI별 이벤트 구독 방식 정리
- 초기 표시 상태와 종료 상태 정리

적용 대상:
- `UIBossHealthBar`
- `UIPlayerStatusBars`
- `UIEstusFlask`
- 필요 시 `UIYouDied` 초기 상태 조립

### `SceneReferenceValidator`
담당 책임:
- 기준 씬 / 기준 프리팹 필수 슬롯 점검
- 누락 슬롯 진단 로그
- "코드는 맞는데 씬 연결 누락" 상태 표준 기록

대상:
- `BossZoneTrigger_Entrance.prefab`
- `FogWall_Entrance.prefab`
- `GameLoopCanvas.prefab`
- `Checkpoint_Start.prefab`
- `developScenes.unity`

## 클래스별 목표 책임 표

### `GameLoopManager`
남길 책임:
- 게임 루프 진입점
- `PlayerRespawnFlow` 호출 / 조립

줄일 책임:
- 직접 코루틴 내부에서 모든 리스폰 과정을 처리하는 구조

### `CheckpointManager`
남길 책임:
- 체크포인트 레지스트리 역할

줄일 책임:
- 게임 루프 후처리까지 끌어안는 구조

### `Checkpoint`
남길 책임:
- 플레이어 진입 감지
- 체크포인트 등록 요청

### `BossZoneTrigger`
남길 책임:
- 플레이어 진입 감지
- `BossFightFlowCoordinator` 호출

줄일 책임:
- 보스전 시작 / 종료 후처리 직접 수행
- BGM 직접 제어
- UI 바인딩 직접 제어

### `FogWallController`
남길 책임:
- 안개벽 표시 / 차단 제어

### `UIBossHealthBar`
남길 책임:
- 표시
- 보스 HP 값 반영

줄일 책임:
- 보스전 흐름과 직접 결합된 이벤트 처리 일부

### `UIPlayerStatusBars`
남길 책임:
- 플레이어 상태 표시

줄일 책임:
- 플레이어 탐색 + 직접 이벤트 구독을 한 클래스가 모두 갖는 구조

### `UIEstusFlask`
남길 책임:
- 에스투스 횟수 표시

보완 포인트:
- 실제 씬 / 프리팹 연결 상태 검증

### `UIYouDied`
남길 책임:
- 표시 / 페이드 연출 전용

## 단계별 리팩토링 순서

### 1단계. 사망 / 리스폰 흐름 분리
대상:
- `GameLoopManager`
- `CheckpointManager`
- `UIYouDied`

목표:
- `PlayerDied -> YOU DIED -> Respawn -> PlayerRespawned` 흐름을 한 구조에서 읽을 수 있게 정리

완료 기준:
- 사망 연출과 체크포인트 조회가 분리된 책임으로 정리된다.

### 2단계. 보스전 시작 / 종료 조립 분리
대상:
- `BossZoneTrigger`
- `FogWallController`
- `UIBossHealthBar`

목표:
- 트리거 감지와 전투 시작 조립을 분리

완료 기준:
- `BossZoneTrigger`는 진입 감지 중심, 실제 시작 / 종료 조립은 별도 흐름 담당 구조로 이동한다.

### 3단계. 체크포인트 구조 정리
대상:
- `CheckpointManager`
- `Checkpoint`

목표:
- 등록 / 조회 / 좌표 계산 책임을 일관되게 정리

완료 기준:
- 리스폰 위치 관련 정책이 한 곳에 모인다.

### 4단계. UI 바인딩 패턴 통일
대상:
- `UIBossHealthBar`
- `UIPlayerStatusBars`
- `UIEstusFlask`
- `UIYouDied`

목표:
- `Bind / Unbind / Refresh / InitialState` 패턴 정리

완료 기준:
- UI마다 제각각인 바인딩 방식을 공통 패턴으로 이해할 수 있다.

### 5단계. 씬 / 프리팹 검증 구조 추가
대상:
- 기준 씬
- 기준 프리팹

목표:
- 인스펙터 누락을 조기에 드러낼 수 있게 정리

완료 기준:
- 필수 슬롯 체크리스트가 있고,
- "코드는 맞는데 씬 연결 누락" 상태를 표준 형식으로 남길 수 있다.

## 작업 전 준비 체크리스트
1. 현재 작업 브랜치와 PR 대상 통합 브랜치를 함께 확인
2. 기준 씬이 `developScenes.unity`인지 확인
3. 기준 프리팹이 실제 배치본과 일치하는지 확인
4. 작업이 코드 수정인지 인스펙터 세팅인지 먼저 분리
5. 현재 이슈가 게임 루프 / UI / 씬 범위인지 확인
6. 보스 / 카메라 / FSM 범위로 새지 않는지 확인

## 씬 / 프리팹 필수 점검 항목

### `GameLoopCanvas.prefab`
- `UIBossHealthBar` 연결
- `UIPlayerStatusBars` 연결
- `UIEstusFlask` 존재 여부와 슬롯 연결
- `UIYouDied` 초기 비활성 상태

### `BossZoneTrigger_Entrance.prefab`
- `bossEnemy`
- `fogWall`
- `bossHealthBar`
- `bossBgmClip`
- `bossBgmSource`

### `FogWall_Entrance.prefab`
- `visualRoot`
- `blockCollider`
- 초기 비활성 정책

### `Checkpoint_Start.prefab`
- `respawnPoint`
- 트리거 설정

### `developScenes.unity`
- 기준 프리팹 인스턴스 배치 여부
- 씬 오버라이드가 프리팹 기본값과 다른지
- 필수 슬롯이 씬에서만 채워진 상태인지

### 2026-03-18 인스펙터 기준본 점검 메모
- `BossZoneTrigger_Entrance.prefab`
  - 기본 프리팹에서는 `player`, `bossEnemy`, `fogWall`, `bossHealthBar`, `bossBgmSource`가 비어 있다.
  - `developScenes.unity`에서는 `bossEnemy`, `fogWall`, `bossHealthBar`가 씬 오브젝트로 오버라이드되어 있다.
  - `player`는 씬에서도 비어 있지만 `BossZoneTrigger.OnTriggerEnter(...)`가 진입 플레이어를 런타임에 대입하므로 의도된 null로 본다.
- `Checkpoint_Start.prefab`
  - `respawnPoint`는 연결되어 있다.
  - `useOwnTransformWhenRespawnPointMissing = 1`이라 누락 시 자기 Transform fallback이 유지된다.
- `GameLoopCanvas.prefab`
  - `Canvas.m_RenderMode = Screen Space Overlay`, `Canvas.m_Camera = null`은 정상이다.
  - `UIPlayerStatusBars`, `UIBossHealthBar`, `UIYouDied`는 프리팹에서 확인된다.
  - `UIEstusFlask` 컴포넌트는 아직 프리팹에서 확인되지 않았다.
- `developScenes.unity`
  - 보스존 관련 참조는 씬 오버라이드 의존 항목과 런타임 바인딩 항목이 혼재한다.
  - 따라서 이후 점검은 `의도된 런타임 null`과 `실제 누락`을 분리해서 기록해야 한다.

## 리팩토링 중 기록 규칙

### 기능 리팩토링 기록
- 대상 클래스:
- 분리 전 책임:
- 분리 후 책임:
- 이동 메서드:
- 영향 이벤트:
- 씬 / 프리팹 영향:

### 인스펙터 세팅 기록
- 씬 / 프리팹:
- 오브젝트:
- 컴포넌트:
- 슬롯:
- 변경 전:
- 변경 후:
- 목적:

### 코드 정상 / 씬 연결 누락 기록
- 관련 기능:
- 코드 상태: 정상 / 확인 완료
- 누락 슬롯:
- 실제 증상:
- 필요한 세팅:
- 기준본 반영 여부:

## 리팩토링 완료 판정 기준
- 보스전 시작 흐름이 감지 / 조립 / 표시 책임으로 구분된다.
- 사망 / 리스폰 흐름이 연출 / 체크포인트 / 후처리 책임으로 구분된다.
- UI는 표시 책임 위주로 정리된다.
- 기준 씬과 기준 프리팹의 필수 슬롯 점검 절차가 문서화된다.
- "코드는 맞는데 씬 연결 누락" 상태를 별도 항목으로 남길 수 있다.

## 바로 시작할 작업 순서
1. `GameLoopManager`에서 사망 / 리스폰 조립 책임 분리 설계
2. `BossZoneTrigger`에서 보스전 시작 / 종료 조립 책임 분리 설계
3. `CheckpointManager` / `Checkpoint`의 책임 경계 재정의
4. UI 4종의 바인딩 방식 비교표 작성
5. 기준 씬 / 기준 프리팹 필수 슬롯 점검표 작성

## 1단계 리팩토링 세부 설계

### 1단계 대상
- `Assets/00Game/01Scripts/01_Managers/GameLoop/GameLoopManager.cs`
- `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointManager.cs`
- `Assets/00Game/01Scripts/05_UI/UIYouDied.cs`
- `Assets/00Game/01Scripts/00_Core/Events/GameLoopEvents.cs`

### 1단계 목표
- 플레이어 사망 이후의 흐름을 `이벤트 수신`, `연출`, `리스폰 위치 조회`, `리스폰 요청`, `후처리`로 분리한다.
- `GameLoopManager`가 모든 세부 과정을 직접 수행하지 않도록 한다.
- 이후 보스전 종료 정책이나 체크포인트 검증 로직이 붙더라도, 사망 / 리스폰 흐름이 한 모듈에서 읽히도록 만든다.

### 현재 문제 구조

#### `GameLoopManager`가 동시에 담당하는 것
- `PlayerDied` 이벤트 구독
- 현재 플레이어 참조 갱신
- 사망 중복 실행 방지
- `YOU DIED` UI 표시
- 리스폰 대기 시간 관리
- 체크포인트 기반 리스폰 요청
- 리스폰 실패 경고
- UI 닫기

즉 "입구", "흐름 조립", "표시 연출", "리스폰 호출", "예외 처리"가 한 클래스에 섞여 있다.

#### `CheckpointManager`와의 결합 방식
- `GameLoopManager`가 `CheckpointManager.Instance.RespawnPlayer(player)`를 직접 호출한다.
- 현재 구조는 단순하지만, 이후 체크포인트 검증 / 리스폰 실패 처리 / 리스폰 후 카메라 정리 같은 후처리가 늘어나면
  `GameLoopManager`가 점점 비대해질 가능성이 높다.

### 1단계 완료 후 목표 구조

#### `GameLoopManager`
남길 책임:
- 게임 루프 진입점
- `PlayerDied` 이벤트 연결
- `PlayerRespawnFlow`에 플레이어 사망 사실 전달

제거 / 축소할 책임:
- 직접 코루틴으로 전체 사망 / 리스폰 시퀀스를 수행하는 책임

#### `PlayerRespawnFlow`
새로 맡길 책임:
- 사망 시퀀스 중복 방지
- 현재 플레이어 참조 유지
- `YOU DIED` 표시 / 숨김
- 리스폰 대기 시간
- 체크포인트 리스폰 요청
- 리스폰 성공 / 실패 후처리

#### `CheckpointManager`
유지할 책임:
- 리스폰 위치 계산
- 플레이어 리스폰 수행
- `PlayerRespawned` 이벤트 발행

### 1단계 권장 파일 구조

권장 신규 파일:
- `Assets/00Game/01Scripts/01_Managers/GameLoop/PlayerRespawnFlow.cs`

초기 위치 제안 이유:
- 현재 `GameLoopManager`, `CheckpointManager`와 같은 게임 루프 계층에 속한다.
- 플레이어 FSM, UI 전용 폴더, 보스전 폴더 어디에도 직접 종속되지 않는다.

### 1단계 API 설계 초안

#### `PlayerRespawnFlow`
권장 필드:
- `PlayerController player`
- `UIYouDied youDiedUI`
- `float youDiedHoldTime`
- `bool isRunning`

권장 공개 메서드:
- `Initialize(PlayerController player, UIYouDied youDiedUI, float youDiedHoldTime)`
- `HandlePlayerDied(PlayerController deadPlayer)`
- `bool IsRunning { get; }`

권장 내부 메서드:
- `IEnumerator RunRespawnSequence()`
- `void UpdatePlayerReference(PlayerController deadPlayer)`
- `bool TryRespawnPlayer()`

#### `GameLoopManager`
권장 필드 변화:
- 기존 `player`, `youDiedUI`, `youDiedHoldTime` 유지 가능
- 신규 `PlayerRespawnFlow respawnFlow`

권장 메서드 변화:
- `Start()` 또는 `Awake()`에서 `respawnFlow` 초기화
- `HandlePlayerDiedEvent(...)`는 `respawnFlow.HandlePlayerDied(...)` 호출만 담당

### 1단계 이벤트 흐름 설계

#### 현재 흐름
1. `PlayerController`가 `GameLoopEvents.RaisePlayerDied(this)` 호출
2. `GameLoopManager`가 이벤트 수신
3. `GameLoopManager`가 직접 코루틴 시작
4. `UIYouDied` 표시
5. `CheckpointManager.RespawnPlayer(player)` 호출
6. `CheckpointManager`가 `GameLoopEvents.RaisePlayerRespawned(...)` 호출
7. `UIYouDied` 숨김

#### 목표 흐름
1. `PlayerController`가 `GameLoopEvents.RaisePlayerDied(this)` 호출
2. `GameLoopManager`가 이벤트 수신
3. `GameLoopManager`가 `PlayerRespawnFlow.HandlePlayerDied(...)` 호출
4. `PlayerRespawnFlow`가 중복 실행 여부 확인
5. `PlayerRespawnFlow`가 `UIYouDied` 표시
6. `PlayerRespawnFlow`가 `CheckpointManager.RespawnPlayer(...)` 호출
7. `CheckpointManager`가 `GameLoopEvents.RaisePlayerRespawned(...)` 호출
8. `PlayerRespawnFlow`가 UI 닫기 및 상태 종료

핵심 차이:
- 이벤트 입구는 유지하되, 흐름 조립 책임만 분리한다.

### 1단계 메서드 이동 설계

#### `GameLoopManager`에서 분리할 후보
- `HandlePlayerDiedEvent(PlayerController deadPlayer)`의 내부 실행 제어 일부
- `PlayerDeathRespawnSequence()`

#### `GameLoopManager`에 남길 후보
- `OnEnable()`, `OnDisable()`의 이벤트 연결
- 초기 참조 보정
- `respawnFlow` 생성 / 초기화

#### `UIYouDied`는 유지할 것
- `ShowImmediate()`
- `HideImmediate()`
- `PlayShow()`
- `PlayHide()`

설명:
- `UIYouDied`는 연출 전용 뷰로 유지하고, 흐름 제어는 `PlayerRespawnFlow`가 맡는다.

### 1단계 상태 / 책임 표

#### 사망 이벤트 수신
- 현재: `GameLoopManager`
- 목표: `GameLoopManager`

#### 사망 시퀀스 중복 방지
- 현재: `GameLoopManager`
- 목표: `PlayerRespawnFlow`

#### 플레이어 참조 갱신
- 현재: `GameLoopManager`
- 목표: `PlayerRespawnFlow`

#### `YOU DIED` 표시
- 현재: `GameLoopManager` + `UIYouDied`
- 목표: `PlayerRespawnFlow` + `UIYouDied`

#### 리스폰 대기
- 현재: `GameLoopManager`
- 목표: `PlayerRespawnFlow`

#### 체크포인트 리스폰 호출
- 현재: `GameLoopManager`
- 목표: `PlayerRespawnFlow`

#### 리스폰 위치 계산
- 현재: `CheckpointManager`
- 목표: `CheckpointManager`

#### 리스폰 성공 이벤트 발행
- 현재: `CheckpointManager`
- 목표: `CheckpointManager`

### 1단계 구현 순서
1. `PlayerRespawnFlow` 클래스 추가
2. 현재 `GameLoopManager.PlayerDeathRespawnSequence()` 내용을 `PlayerRespawnFlow`로 이동
3. `GameLoopManager`는 이벤트 수신 후 `respawnFlow.HandlePlayerDied(...)`만 호출하도록 축소
4. `GameLoopManager.Start()`의 초기 UI 숨김 로직을 `respawnFlow` 초기화와 함께 정리
5. 기존 동작과 동일한지 검증

### 1단계에서 유지해야 하는 동작
- 동일 프레임 또는 짧은 간격의 중복 사망 이벤트가 와도 시퀀스는 1번만 실행
- `Time.timeScale`이 영향을 주더라도 사망 시퀀스는 멈추지 않음
- `CheckpointManager.RespawnPlayer(...)` 실패 시 경고 로그 유지
- 리스폰 성공 시 `PlayerRespawned` 이벤트는 계속 발행
- `YOU DIED` UI는 시작 시 숨김 상태 유지

### 1단계 비목표
- 체크포인트 저장 방식 변경
- 리스폰 위치 계산 규칙 변경
- 리스폰 시 플레이어 능력치 리필 규칙 변경
- 카메라 리스폰 동기화 구조 변경
- 보스전 종료 정책 변경

### 1단계 리스크
- `GameLoopManager`가 `MonoBehaviour` 코루틴 실행 주체이므로,
  `PlayerRespawnFlow`가 코루틴을 어떻게 실행할지 결정해야 한다.

권장안:
- 1단계에서는 `PlayerRespawnFlow`를 순수 C# 객체로 두고,
  코루틴 시작은 `GameLoopManager`가 위임받아 실행한다.

대안:
- `PlayerRespawnFlow`를 `MonoBehaviour`로 두고 별도 컴포넌트화

1단계 권장 선택:
- 순수 C# 객체 + `GameLoopManager.StartCoroutine(...)` 위임 방식

이유:
- 구조 변경 폭이 작고 현재 동작을 가장 안전하게 유지할 수 있다.

### 1단계 검증 체크리스트
1. 플레이어 사망 시 `YOU DIED`가 1회만 표시되는지
2. 사망 직후 리스폰까지의 대기 시간이 기존과 같은지
3. 체크포인트가 없을 때 fallback 동작이 유지되는지
4. 리스폰 후 `YOU DIED`가 정상적으로 사라지는지
5. 리스폰 후 `PlayerRespawned` 이벤트에 의존하는 다른 시스템이 계속 동작하는지

### 1단계 완료 후 다음 단계로 넘길 메모
- `PlayerRespawnFlow` 분리 후 `BossZoneTrigger`의 사망 종료 처리와 어떻게 연결할지 검토
- 리스폰 후 보스전 상태 정리 여부는 2단계 보스전 흐름 정리에서 다룬다
- 체크포인트 책임 축소는 3단계에서 이어서 정리한다

### 1단계 반영 메모 (2026-03-18)
- 신규 파일:
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/PlayerRespawnFlow.cs`
- 반영 내용:
  - 사망 시퀀스 중복 방지 책임을 `PlayerRespawnFlow`로 이동
  - `YOU DIED` 표시 / 숨김과 리스폰 대기 시간을 `PlayerRespawnFlow`로 이동
  - `CheckpointManager.RespawnPlayer(...)` 호출 책임을 `PlayerRespawnFlow`로 이동
  - `GameLoopManager`는 이벤트 수신과 `PlayerRespawnFlow` 초기화 / 호출 책임 중심으로 축소
- 유지한 책임:
  - `CheckpointManager`의 리스폰 위치 계산
  - `CheckpointManager`의 `PlayerRespawned` 이벤트 발행
  - `UIYouDied`의 연출 전용 뷰 책임
- 다음 단계 진입 전 문서 갱신 규칙:
  - 다음 단계 구현 전에 `ProgressReport.md`, 해당 스레드 규칙 문서, 리팩토링 가이드 문서 중 영향받는 문서를 먼저 갱신한다.

#### Notion 위키 작성 스레드 확인용 요약
- 이번 단계 변경:
  - `GameLoopManager`에서 사망 / 리스폰 시퀀스를 분리했다.
  - `PlayerRespawnFlow`를 도입해 시퀀스 조립 책임을 이동했다.
- 유지한 책임:
  - `CheckpointManager`는 위치 계산과 리스폰 수행을 유지한다.
  - `UIYouDied`는 연출 전용 뷰로 유지한다.
- 검증 상태:
  - `Assembly-CSharp.csproj` 반영 상태 추가 확인 필요
- 다음 단계:
  - `BossZoneTrigger`의 보스전 시작 / 종료 조립 책임 분리

### 2단계 반영 메모 (2026-03-18)
- 신규 파일:
  - `Assets/00Game/01Scripts/00_Core/BossZone/BossFightFlowCoordinator.cs`
- 반영 내용:
  - 보스 이벤트 바인딩 / 해제 책임을 `BossFightFlowCoordinator`로 이동
  - 안개벽 활성 / 비활성, 보스 HP UI 바인딩 / 언바인딩, 보스 BGM 재생 / 종료 책임을 `BossFightFlowCoordinator`로 이동
  - `BossZoneTrigger`는 플레이어 진입 감지, 플레이어 참조 확보, 플레이어 사망 이벤트 입구, 보스 클리어 후 트리거 비활성 정책 위주로 축소
  - 로컬 빌드 검증을 위해 `Assembly-CSharp.csproj`에 `BossFightFlowCoordinator.cs` 항목을 반영
- 유지한 책임:
  - `FogWallController`는 벽 표시 / 충돌 제어만 유지
  - `UIBossHealthBar`는 표시와 값 반영 책임만 유지
  - 보스전 종료 후 트리거 비활성 정책은 `BossZoneTrigger`에 유지
- 검증 결과:
  - `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 다음 단계 진입 전 문서 갱신:
  - 3단계 구현 전에 `ProgressReport.md`, `ValidationCurrentStatus_20260318.md`, 해당 스레드 가이드 문서를 먼저 갱신한다.

#### Notion 위키 작성 스레드 확인용 요약
- 완료 단계:
  - 2단계 `BossFightFlowCoordinator` 분리 완료
- 변경 파일:
  - `Assets/00Game/01Scripts/00_Core/BossZone/BossFightFlowCoordinator.cs`
  - `Assets/00Game/01Scripts/00_Core/BossZone/BossZoneTrigger.cs`
  - `Assembly-CSharp.csproj`
- 핵심 변경 내용:
  - `BossZoneTrigger`에서 보스전 시작 / 종료 조립 책임을 분리했다.
  - `BossFightFlowCoordinator`가 보스 이벤트, 안개벽, 보스 UI, BGM, 종료 이벤트 발행을 담당한다.
  - `BossZoneTrigger`는 트리거 감지와 종료 후 트리거 정책 위주로 축소됐다.
- 유지한 책임:
  - `FogWallController`의 벽 제어
  - `UIBossHealthBar`의 표시 전용 책임
  - 보스 클리어 후 콜라이더 비활성 정책은 `BossZoneTrigger`에 유지
- 검증 상태:
  - 로컬 `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 다음 단계:
  - `CheckpointManager` / `Checkpoint` 책임 정리

### 3단계 반영 메모 (2026-03-18)
- 신규 파일:
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointRegistry.cs`
- 반영 내용:
  - 기본 체크포인트 / 현재 체크포인트 보관 책임을 `CheckpointRegistry`로 이동
  - 리스폰 좌표 계산과 체크포인트 기준 `Transform` 조회 책임을 `CheckpointRegistry`로 이동
  - `CheckpointManager`는 레지스트리 조립, 실제 플레이어 리스폰 실행, `PlayerRespawned` 이벤트 발행 중심으로 축소
  - `Checkpoint`는 플레이어 진입 시 `RegisterCheckpoint(...)` 요청만 보내는 구조로 정리
  - 로컬 빌드 검증을 위해 `Assembly-CSharp.csproj`에 `CheckpointRegistry.cs` 항목을 반영
- 유지한 책임:
  - `CheckpointManager`는 리스폰 실행 입구를 유지
  - `Checkpoint`는 씬 트리거 입구를 유지
  - 리스폰 fallback으로 플레이어 현재 transform을 쓰는 정책은 유지
- 검증 결과:
  - `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 다음 단계 진입 전 문서 갱신:
  - 4단계 구현 전에 `ProgressReport.md`, `ValidationCurrentStatus_20260318.md`, 해당 스레드 가이드 문서를 먼저 갱신한다.

#### Notion 위키 작성 스레드 확인용 요약
- 완료 단계:
  - 3단계 `CheckpointRegistry` 분리 완료
- 변경 파일:
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointRegistry.cs`
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/CheckpointManager.cs`
  - `Assets/00Game/01Scripts/01_Managers/GameLoop/Checkpoint.cs`
  - `Assembly-CSharp.csproj`
- 핵심 변경 내용:
  - 체크포인트 등록 상태와 리스폰 좌표 계산 책임을 `CheckpointRegistry`로 분리했다.
  - `CheckpointManager`는 리스폰 실행과 이벤트 발행 중심으로 축소했다.
  - `Checkpoint`는 플레이어 진입 시 등록 요청만 보내는 입구로 정리됐다.
- 유지한 책임:
  - `CheckpointManager`의 실제 리스폰 실행
  - `Checkpoint`의 트리거 감지
  - fallback 리스폰 정책
- 검증 상태:
  - 로컬 `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 다음 단계:
  - UI 바인딩 패턴 정리

### 4단계 반영 메모 (2026-03-18)
- 신규 파일:
  - `Assets/00Game/01Scripts/05_UI/UIBindingMediator.cs`
- 반영 내용:
  - 플레이어 기반 UI 재바인딩 흐름을 `UIBindingMediator`로 공통화
  - `UIPlayerStatusBars`는 `PlayerRespawned` 기준으로 플레이어 재바인딩과 초기 갱신을 수행하도록 정리
  - `UIEstusFlask`는 글로벌 이벤트 수신 중심 구조에서 플레이어 바인딩 + `OnFlaskChanged` 수신 구조로 정리
  - `UIBossHealthBar`는 `BindBoss / UnbindBoss / Refresh` 중심 구조로 단순화
  - `UIYouDied`는 숨김 초기 상태를 전용 메서드로 통일
  - `PlayerController`에 `MaxFlaskCount` 읽기 전용 프로퍼티를 추가해 UI가 초기 상태를 직접 갱신할 수 있게 정리
  - 로컬 빌드 검증을 위해 `Assembly-CSharp.csproj`에 `UIBindingMediator.cs` 항목을 반영
- 유지한 책임:
  - `UIYouDied`는 연출 전용 뷰 유지
  - `UIBossHealthBar`는 표시 전용 책임 유지
  - 플라스크 수량 계산과 회복 규칙은 `PlayerController` / `PlayerEstusFlask`에 유지
- 검증 결과:
  - `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 플레이테스트 필요 항목:
  - 플레이어 사망 후 리스폰 시 HP / 스태미나 UI가 새 플레이어 참조로 정상 갱신되는지
  - 에스투스 사용 / 리필 / 리스폰 후 UI 수량 표시가 정상 갱신되는지
  - 보스전 시작 / 종료 시 보스 HP UI가 정상 표시 / 해제되는지
  - `YOU DIED` UI가 시작 시 숨김 상태를 유지하고 사망 시에만 표시되는지
- 후속 안정화 메모:
  - 실제 플레이에서 보스전 UI와 `YOU DIED` 미표시 증상이 보고돼 `UIBossHealthBar`는 보스전 이벤트 구독을 복구했고, `UIYouDied`는 public 표시 메서드에서 `CanvasGroup` 참조를 지연 보정하도록 수정했다.
- 후속 안정화 원인 상세:
  - `BossHPPanel`은 기본 비활성 패널이라 `BossFightStarted` 기준 표시 보장 경로가 필요했는데, 4단계 단순화 중 해당 이벤트 구독을 제거하면서 실제 전투 시작 시 표시가 누락됐다.
  - `YouDiedPanel`도 기본 비활성 패널이라 `Awake()`에서만 `CanvasGroup`을 준비하는 구조는 안전하지 않았고, public 메서드 호출 시 참조를 다시 보정하는 방어가 필요했다.
  - 따라서 두 문제 모두 씬 인스펙터 누락이 아니라 `비활성 패널 시작 상태`를 충분히 고려하지 못한 코드 리팩토링 원인이었다.

### 5단계 감사 메모 (2026-03-18)
- 목표:
  - 씬 / 프리팹 기준본 판정
  - `코드는 맞는데 씬 연결이 안 된 상태`와 `의도된 런타임 바인딩 null` 분리
- 감사 대상:
  - `Assets/00Game/00Scenes/developScenes.unity`
  - `Assets/00Game/02Prefabs/GameLoop/GameLoopCanvas.prefab`
  - `Assets/00Game/02Prefabs/BossZone/BossZoneTrigger_Entrance.prefab`
  - `Assets/00Game/02Prefabs/CheckPoint/Checkpoint_Start.prefab`
- 감사 결과:
  - `developScenes.unity`의 `GameLoopManager.youDiedUI`, `CheckpointManager.defaultCheckpoint`, `BossZoneTrigger.bossHealthBar`는 연결돼 있다.
  - `BossZoneTrigger_Entrance.prefab`의 `bossEnemy`, `fogWall`, `bossHealthBar`는 프리팹 기본값으로는 비어 있고, `developScenes.unity` 인스턴스에서 씬 오브젝트로 오버라이드된다.
  - 같은 프리팹의 `player`는 씬에서도 비어 있지만 런타임 진입 플레이어를 대입하는 구조라 의도된 null이다.
  - `Checkpoint_Start.prefab`의 `respawnPoint`는 연결돼 있고 fallback 정책도 유지된다.
  - `GameLoopCanvas.prefab`에는 `UIPlayerStatusBars`, `UIBossHealthBar`, `UIYouDied`는 존재한다.
  - `GameLoopCanvas.prefab`의 `EstusPanel`은 현재 비활성 이미지 패널만 있고 `UIEstusFlask` 컴포넌트와 `countText`, `useSlots` 기준본이 없다.
  - `UIPlayerStatusBars`의 `hpText`, `staminaText`는 현재 기준본에서 비어 있다.
- 분류 결과:
  - 의도된 런타임 바인딩 null:
    - `BossZoneTrigger.player`
  - 코드 정상 / 씬 연결 확인 필요:
    - `UIEstusFlask` 프리팹 기준본 부재
    - `UIPlayerStatusBars.hpText`, `staminaText` 슬롯 비어 있음

#### Notion 위키 작성 스레드 확인용 요약
- 완료 단계:
  - 5단계 씬 / 프리팹 기준본 감사 완료
- 변경 내용:
  - 코드 변경 없이 기준본 판정과 누락 슬롯 분류를 정리했다.
- 핵심 결과:
  - `BossZoneTrigger_Entrance.prefab`의 일부 참조는 씬 오버라이드 기반이다.
  - `UIEstusFlask`는 코드가 준비됐지만 `GameLoopCanvas.prefab` 기준본은 아직 없다.
  - `UIPlayerStatusBars` 텍스트 슬롯은 현재 비어 있다.
- 다음 단계:
  - `GameLoopCanvas.prefab` 에스투스 UI 기준본 확정 및 필요 시 프리팹 반영
- 다음 단계 진입 전 문서 갱신:
  - 씬 / 프리팹 점검 단계에 들어가기 전에 `ProgressReport.md`, `ValidationCurrentStatus_20260318.md`, 해당 스레드 가이드 문서를 먼저 갱신한다.

#### Notion 위키 작성 스레드 확인용 요약
- 완료 단계:
  - 4단계 UI 바인딩 패턴 정리 완료
- 변경 파일:
  - `Assets/00Game/01Scripts/05_UI/UIBindingMediator.cs`
  - `Assets/00Game/01Scripts/05_UI/UIBossHealthBar.cs`
  - `Assets/00Game/01Scripts/05_UI/UIPlayerStatusBars.cs`
  - `Assets/00Game/01Scripts/05_UI/UIEstusFlask.cs`
  - `Assets/00Game/01Scripts/05_UI/UIYouDied.cs`
  - `Assets/00Game/01Scripts/02_Characters/Player/Core/PlayerController.cs`
  - `Assembly-CSharp.csproj`
- 핵심 변경 내용:
  - 플레이어 UI 둘은 `PlayerRespawned` 기준 재바인딩 패턴으로 정리했다.
  - `UIEstusFlask`는 플레이어 이벤트 기반 갱신 구조로 변경했다.
  - `UIBossHealthBar`는 보스 직접 바인딩 흐름 중심으로 단순화했다.
  - `UIYouDied` 초기 숨김 상태를 명시적으로 통일했다.
- 유지한 책임:
  - HP / 스태미나 / 플라스크 실제 값 계산은 플레이어 쪽에 유지
  - `UIYouDied` 연출 책임 유지
- 검증 상태:
  - 로컬 `dotnet build Assembly-CSharp.csproj -nologo` 통과
- 코드 정상 / 씬 연결 확인 필요:
  - `UIEstusFlask` 컴포넌트는 코드상 준비됐지만 `GameLoopCanvas.prefab` 기준 연결 여부를 별도 점검해야 한다.
  - `BossZoneTrigger_Entrance.prefab`의 `player` null은 현재 구조상 런타임 바인딩이라 바로 누락으로 분류하면 안 된다.
- 다음 단계:
  - `GameLoopCanvas.prefab`의 `UIEstusFlask` 반영 필요 여부를 확정
  - `developScenes.unity` 기준 HP / 스태미나 / 에스투스 / 보스 HP / `YOU DIED` UI 플레이테스트 수행
  - Inspector 원인 메모를 기능 변경 메모와 분리해 누적

## 작업 메모
- 이 문서는 실제 코드를 바로 바꾸기 위한 준비 문서다.
- 구현 전에 먼저 "현재 책임", "옮길 책임", "남길 책임"을 적고 진행한다.
- 리팩토링 도중 기능 구현 범위가 새로 생기면 이 문서를 확장하지 말고,
  먼저 해당 스레드 규칙 문서와 진행 현황 문서에 범위 반영 여부를 판단한다.
