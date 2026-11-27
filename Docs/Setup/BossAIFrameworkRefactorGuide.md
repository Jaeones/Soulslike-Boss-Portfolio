# 보스 AI 프레임워크 리팩토링 가이드

## 문서 분류
- 문서 유형: 세팅 / 구조 가이드 문서
- 역할: 현재 보스 AI 구조를 진단하고, 프레임워크 시스템화 방향과 1차 리팩토링 순서를 정리하는 기준 문서다.

## 문서 목적
이 문서는 현재 프로젝트의 보스 AI가
어디까지 구조화되어 있고,
어디서부터 `BossController`에 책임이 몰려 있으며,
어떤 순서로 분리해야 실제로 유지보수 가능한 프레임워크형 구조가 되는지 정리하기 위한 문서다.

이 문서의 목표는 새 기능 추가가 아니라
기존 보스 AI를 `확장 가능`, `테스트 가능`, `협업 충돌에 덜 취약한 구조`로 재정렬하는 것이다.

## 우선 참조 문서
1. `Docs/Rules/ThreadOperationRules.md`
2. `Docs/Rules/BossAIThreadRules.md`
3. `Docs/Reports/ProgressReport.md`
4. `Docs/Setup/BossPatternSpec.md`
5. `Docs/Setup/CombatDamagePipelineGuide.md`

## 적용 대상
- `Assets/00Game/01Scripts/02_Characters/Enemies/EnemyBase.cs`
- `Assets/00Game/01Scripts/02_Characters/Enemies/BossController.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossState.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossIdleState.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossMoveState.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossActionState.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossGroggyState.cs`
- `Assets/00Game/01Scripts/02_Characters/Boss/States/BossDeadState.cs`
- `Assets/00Game/01Scripts/03_Combat/HitBox/Boss/BossHitbox.cs`
- `Assets/00Game/01Scripts/03_Combat/HitBox/Boss/ShockwaveHitbox.cs`

## 현재 구조 요약

### 1. 공통 베이스 계층
- `EnemyBase`
  - HP
  - 그로기 게이지
  - 사망 이벤트
  - 코루틴 기반 FSM 루프
  - 공통 `Intro / Idle / Move / Action / Groggy / Dead` 상태 틀

### 2. 보스 메인 조립 계층
- `BossController`
  - 상태 인스턴스 생성
  - 상태 전환 진입점
  - 패턴 선택
  - 공격 큐 준비
  - 페이즈 전환
  - 추적 / 회전 / `NavMeshAgent`
  - 히트박스 활성 / 비활성
  - 보스 공격 데미지 계산과 플레이어 전달

### 3. 상태 계층
- `BossIdleState`
  - 거리와 확률 기준으로 이동 / 공격 진입
- `BossMoveState`
  - 추적과 사거리 진입 시 공격 전환
- `BossActionState`
  - 공격 큐 소비, 대기, 애니메이션 재생, `Dash` 이동 처리
- `BossGroggyState`
  - 무력화 진입과 복귀
- `BossDeadState`
  - 사망 연출과 제거

### 4. 공격 판정 계층
- `BossHitbox`
  - 현재 공격명 해석
  - 플레이어 충돌 감지
  - `HitTarget(...)` 호출
- `ShockwaveHitbox`
  - `Stomp` 웨이브 전진

## 현재 평가

### 현재 구조에서 잘된 점
- `EnemyBase`와 `BossController`가 1차로 나뉘어 있어 공통 적 처리와 보스 전용 처리가 완전히 섞여 있지는 않다.
- 상태 파일이 `Idle / Move / Action / Groggy / Dead`로 분리되어 있어 흐름 읽기가 쉬운 편이다.
- 히트박스 파일이 따로 있어 판정 입구 자체는 별도 파일에서 볼 수 있다.

### 현재 구조에서 부족한 점
- `BossController`가 너무 많은 책임을 가진다.
  - 패턴 선택
  - 공격 큐
  - 페이즈 전환
  - 전투 정리
  - 히트박스 제어
  - 실제 데미지 전달
- 상태 클래스가 `Enter()` 중심의 얇은 래퍼라서, 진짜 프레임워크형 FSM이라기보다 `코루틴 분리형 FSM`에 가깝다.
- 공격 정의가 문자열과 하드코딩 값에 묶여 있어 새 패턴을 추가할 때 충돌이 커진다.
- 패턴 우선순위, 페이즈 조건, 콤보 가능 여부가 데이터로 정리되어 있지 않다.
- 보스 홈 포지션 복귀, 전투 종료 후 AI 리셋, 타겟 해제 같은 운영 축이 전용 모듈로 정리되어 있지 않다.

## 현재 시스템화 수준 판단
- 상태 분리: 완료
- 공통 베이스 분리: 완료
- 보스 전용 조립자 분리: 완료
- 패턴 선택 모듈화: 미완료
- 페이즈 관리 모듈화: 미완료
- 공격 실행 모듈화: 미완료
- 공격 정의 데이터화: 미완료
- 전투 종료 후 리셋 / 홈 복귀 구조화: 미완료

정리하면 현재 보스 AI는
`부분 구조화된 상태 분리형 FSM`이고,
`프레임워크 시스템화 완료 상태`로 보기는 어렵다.

## 핵심 문제 요약

### 문제 1. `BossController` 책임 과밀
- 한 파일이 너무 많은 역할을 맡고 있어 수정 충돌 위험이 높다.
- 다른 팀원이 패턴 선택만 수정해도 페이즈 / 히트박스 / 사망 정리와 쉽게 충돌한다.

### 문제 2. 패턴이 데이터가 아니라 조건문 묶음
- 거리, 확률, 첫 공격 예외, 페이즈 조건이 분산되어 있다.
- 새 패턴 추가 시 `BossController`, 상태 클래스, 애니메이션 이벤트, 히트박스 해석을 같이 건드려야 한다.

### 문제 3. 공격 실행과 공격 정의가 섞여 있음
- 현재는 공격 이름 문자열이 곧 애니메이션 이름, 판정 이름, 데미지 분기 키 역할을 같이 한다.
- 이 구조는 공격 수가 늘어날수록 유지보수가 급격히 어려워진다.

### 문제 4. 페이즈 전환과 전투 정리 책임이 독립되지 않음
- 2페이즈 진입, 그로기 진입, 사망 시 정리 흐름이 모두 `BossController`와 상태 클래스에 분산돼 있다.
- 정리 순서가 늘어나면 회귀 위험이 커진다.

### 문제 5. 전투 종료 후 AI 운영 축 부족
- 플레이어 사망 후 타겟 해제
- 추적 중단
- 홈 포지션 복귀
- 필요 시 HP / FSM 상태 리셋
같은 운영 흐름이 아직 프레임워크 수준으로 분리돼 있지 않다.

## 리팩토링 목표

### 목표 1. 판단과 실행을 분리한다
- 무엇을 할지 정하는 로직
- 실제로 공격 / 이동 / 전환을 수행하는 로직
을 다른 객체로 나눈다.

### 목표 2. 패턴을 데이터처럼 다룰 수 있게 만든다
- 거리 조건
- 페이즈 조건
- 우선순위
- 콤보 허용 여부
- 데미지 / 히트박스 타입
을 공격 정의로 묶을 수 있어야 한다.

### 목표 3. 전환 축을 독립 모듈로 만든다
- 패턴 선택
- 페이즈 전환
- 그로기 전환
- 전투 종료 정리
를 따로 읽고 검증할 수 있어야 한다.

### 목표 4. `BossController`를 상위 조립자로 축소한다
- 직접 모든 판단을 하지 않고
- 상태, 패턴 셀렉터, 페이즈 관리자, 공격 실행기를 호출하는 방향으로 축소한다.

## 권장 목표 구조

### `BossController`
담당 책임:
- 보스 AI 상위 조립
- 상태 간 공통 참조 전달
- 외부 공개 API
- 생명주기 시작점

직접 맡지 않도록 줄일 책임:
- 패턴 우선순위 계산
- 개별 공격 정의
- 페이즈 세부 조건 판정
- 공격별 실제 실행 흐름

### `BossPatternSelector`
담당 책임:
- 현재 상황에서 어떤 패턴을 선택할지 계산
- 거리 / 페이즈 / 첫 공격 / 확률 / 연속 공격 조건 판단
- 예약 공격과 일반 선택 우선순위 정리

예상 이동 대상:
- `BossController.GetAppropriateAttack()`
- `BossController.DetermineMeleeAttack()`
- `BossController.PrepareAttackSequence()`의 선택 로직
- `BossIdleState` 일부 거리 / 대쉬 진입 판단

### `BossAttackExecutor`
담당 책임:
- 공격 큐 소비
- 공격 시작 전 정리
- 공격 애니메이션 재생
- 공격 종료 판정
- `Dash` 이동과 도달 중단 처리

예상 이동 대상:
- `BossActionState`의 실행 루프 대부분
- 공격 시작 / 종료 공통 처리

### `BossPhaseController`
담당 책임:
- 1페이즈 / 2페이즈 진입 조건 관리
- 중복 진입 방지
- 전환 직전 전투 흐름 정리
- 전환 후 값 보정

예상 이동 대상:
- `BossController.TransitionToPhase2()`
- `BossController.CanEnterGroggyState()` 일부와 전환 차단 규칙

### `BossGroggyController`
담당 책임:
- 그로기 게이지 임계치 판정
- 그로기 진입 가능 상태 판정
- 진입 시 전투 정리
- 복귀 시 게이지 / 포이즈 / 슈퍼아머 복구

예상 이동 대상:
- `EnemyBase`와 `BossController.TakeDamage()` 사이의 그로기 전환 조립 일부
- `BossGroggyState`의 값 복구 정책

### `BossCombatRuntime`
담당 책임:
- 현재 공격 이름
- 공격 큐
- 예약 공격
- 첫 공격 여부
- 최근 공격 카운터
- 현재 활성 히트 여부

목표:
- 현재 `BossController` 안의 전투 임시 상태 필드를 한 군데로 모은다.

### `BossAttackDefinition`
담당 책임:
- 공격 이름
- 애니메이션 이름
- 기본 데미지
- 히트박스 타입
- 페이즈 조건
- 유효 거리
- 콤보 허용 여부

목표:
- 공격 문자열 하드코딩을 줄이고,
  새 패턴 추가 시 조건과 실행 정의를 더 명시적으로 관리한다.

## 1차 리팩토링 권장 순서

### 1단계. `BossCombatRuntime` 분리
목표:
- `BossController` 내부 임시 전투 상태를 모은다.

우선 옮길 후보:
- `reservedAttack`
- `currentExecutingAttack`
- `hasHitTarget`
- `attackQueue`
- `slamCount`
- `hasPerformedFirstAttack`
- `isSlamActive`

이 단계의 장점:
- 이후 패턴 선택기와 공격 실행기가 같은 런타임 상태를 공유하기 쉬워진다.
- `BossController` 필드 난잡도가 즉시 줄어든다.

### 2단계. `BossPatternSelector` 분리
목표:
- 패턴 선택 조건을 `BossController`에서 걷어낸다.

우선 옮길 후보:
- `GetAppropriateAttack()`
- `DetermineMeleeAttack()`
- `PrepareAttackSequence()`의 패턴 선택 부분
- `BossIdleState`의 거리 / 대쉬 확률 판단 일부

이 단계 완료 기준:
- `BossController`는 "패턴을 직접 계산"하지 않고 selector 결과만 받는다.

### 3단계. `BossAttackExecutor` 분리
목표:
- 현재 `BossActionState`가 가진 공격 실행 루프를 전용 실행기로 옮긴다.

우선 옮길 후보:
- 공격 시작 전 대기
- 애니메이션 시작
- `Dash` 이동 처리
- 종료 normalized time 판정
- 콤보 간격 처리
- 에이전트 정리

이 단계 완료 기준:
- `BossActionState`는 실행기 호출만 담당한다.

### 4단계. `BossPhaseController` 분리
목표:
- 페이즈 전환을 독립 축으로 만든다.

우선 옮길 후보:
- `TransitionToPhase2()`
- 페이즈 진입 조건 검사
- 전환 중 차단 규칙
- 전환 직전 `InterruptCombatFlow()` 호출 정책

이 단계 완료 기준:
- 2페이즈 규칙을 한 파일에서 읽고 수정할 수 있다.

### 5단계. 공격 정의 데이터화
목표:
- `Slam / Dash / Stomp`의 정의를 조건문에서 분리한다.

후보 방식:
- 간단한 C# 정의 클래스
- 추후 `ScriptableObject` 기반 공격 정의

권장 시작 방식:
- 먼저 코드 객체로 정리하고,
  안정화 후 데이터 에셋으로 올리는 순서를 권장한다.

### 6단계. 전투 종료 운영 축 정리
목표:
- 플레이어 사망, 전투 이탈, 리셋, 홈 복귀 같은 운영 흐름을 보스 AI 구조 안에서 정리한다.

우선 정의할 것:
- 언제 타겟을 해제하는가
- 언제 추적을 중단하는가
- 언제 홈으로 복귀하는가
- 복귀 시 HP / 페이즈 / 그로기 / 공격 큐를 어디까지 리셋하는가

## 지금 당장 하지 않는 것
- 플레이어 입력 / 플레이어 FSM 변경
- 카메라 / UI 로직 변경
- 씬 프리팹 최종 연결
- 보스 공격의 최종 데미지 규약 자체 변경
- 공격 판정 프레임의 애니메이션 이벤트 재배치

## 현재 기준 추천 1차 작업안
지금 시점에서 가장 현실적인 순서는 아래다.

1. `BossCombatRuntime` 분리
2. `BossPatternSelector` 분리
3. `BossActionState` 실행부를 `BossAttackExecutor`로 이동
4. `BossPhaseController` 분리

이 순서를 권장하는 이유:
- 지금 가장 큰 병목은 `BossController` 책임 과밀이다.
- 먼저 런타임 상태와 패턴 선택을 떼야 이후 작업 충돌이 줄어든다.
- 페이즈 전환은 중요하지만, 선택과 실행이 분리된 뒤 옮기는 편이 구조가 덜 꼬인다.

## 단계별 완료 후 반드시 남길 기록
- 완료 단계
- 변경 파일
- 수정 범위
- 수정 전 동작
- 수정 후 동작
- 스크립트 역할 설명
- 건드리지 않은 범위
- 협업 충돌 주의점
- 다음 단계
- 보류 사항

## Notion 위키 반영용 요약
- 현재 보스 AI는 `EnemyBase + BossController + 상태 파일` 구조의 부분 구조화 상태다.
- 상태 분리는 되어 있지만 `BossController` 책임 과밀이 남아 있다.
- 프레임워크 시스템화의 핵심 과제는
  - 전투 런타임 상태 분리
  - 패턴 선택 모듈화
  - 공격 실행 모듈화
  - 페이즈 전환 모듈화
  - 공격 정의 데이터화
  다.
- 권장 1차 리팩토링 순서는
  1. `BossCombatRuntime`
  2. `BossPatternSelector`
  3. `BossAttackExecutor`
  4. `BossPhaseController`
  이다.
