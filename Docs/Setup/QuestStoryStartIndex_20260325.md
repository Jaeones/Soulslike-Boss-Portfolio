# 시작 구간 진행 문서 인덱스 (2026-03-25)

## 목적
- 시작 구간 진행 설계와 퀘스트 시스템 관련 문서를 한 곳에서 찾을 수 있게 정리한다.
- 구현 스레드가 문서 역할을 헷갈리지 않도록, 각 문서의 용도와 읽는 순서를 고정한다.

## 우선 읽는 순서
1. [QuestStoryStartFocus_20260325.md](QuestStoryStartFocus_20260325.md)
   - 시작 구간의 전체 방향과 우선 범위
2. [QuestStoryStartScenePlacementChecklist_20260325.md](QuestStoryStartScenePlacementChecklist_20260325.md)
   - 씬 배치 체크리스트
3. [QuestStoryStartStateContract_20260325.md](QuestStoryStartStateContract_20260325.md)
   - `goalKnown`, `sealAccepted` 상태 계약
4. [QuestStorySealPlaqueContract_20260325.md](QuestStorySealPlaqueContract_20260325.md)
   - `SealPlaque` 상호작용 의미와 처리 규칙
5. [QuestSystemFlowContract_20260325.md](QuestSystemFlowContract_20260325.md)
   - 메인 퀘스트 단계 전이와 복귀 규칙
6. [QuestStoryBossEntranceBridge_20260325.md](QuestStoryBossEntranceBridge_20260325.md)
   - 시작 구간에서 메인 보스 진입까지의 서사 연결
7. [QuestStoryStartImplementationSteps_20260325.md](QuestStoryStartImplementationSteps_20260325.md)
   - 실제 구현 단계와 작업 카드

## 문서 역할 요약
### 방향 문서
- [QuestStoryStartFocus_20260325.md](QuestStoryStartFocus_20260325.md)
  - 무엇을 먼저 구현해야 하는지
  - 시작 구간을 어떤 감정 흐름으로 읽히게 할지

### 배치 문서
- [QuestStoryStartScenePlacementChecklist_20260325.md](QuestStoryStartScenePlacementChecklist_20260325.md)
  - 시체, 흔적, 강한 일반 적, `SealPlaque`를 어디에 어떤 순서로 배치할지

### 상태 문서
- [QuestStoryStartStateContract_20260325.md](QuestStoryStartStateContract_20260325.md)
  - `goalKnown`, `sealAccepted`가 정확히 무엇을 뜻하는지
  - 사망/Continue에서 무엇이 유지되는지

### 상호작용 문서
- [QuestStorySealPlaqueContract_20260325.md](QuestStorySealPlaqueContract_20260325.md)
  - `SealPlaque` 문구 방향
  - 상호작용 전후 피드백
  - `sealAccepted` 처리 기준

### 퀘스트 문서
- [QuestSystemFlowContract_20260325.md](QuestSystemFlowContract_20260325.md)
  - `Arrival -> Discovery -> Opened -> Encounter` 흐름
  - 오브젝트-상태 연결표
  - 실패/복귀 규칙

### 브리지 문서
- [QuestStoryBossEntranceBridge_20260325.md](QuestStoryBossEntranceBridge_20260325.md)
  - 시체와 안개벽 사이가 왜 하나의 이야기로 이어지는지

### 실행 문서
- [QuestStoryStartImplementationSteps_20260325.md](QuestStoryStartImplementationSteps_20260325.md)
  - 실제 구현 순서
  - 작업 카드 A~E

## 스레드별 참고 문서
### 게임 루프 / UI / 씬 통합
- 우선:
  - [QuestStoryStartScenePlacementChecklist_20260325.md](QuestStoryStartScenePlacementChecklist_20260325.md)
  - [QuestStoryStartStateContract_20260325.md](QuestStoryStartStateContract_20260325.md)
  - [QuestStorySealPlaqueContract_20260325.md](QuestStorySealPlaqueContract_20260325.md)

### 타이틀 / 메뉴 / 저장 / 옵션
- 우선:
  - [QuestStoryStartStateContract_20260325.md](QuestStoryStartStateContract_20260325.md)
  - [QuestSystemFlowContract_20260325.md](QuestSystemFlowContract_20260325.md)

### 아트 프롬프트 / 비주얼 디렉션
- 우선:
  - [QuestStoryStartFocus_20260325.md](QuestStoryStartFocus_20260325.md)
  - [QuestStoryStartScenePlacementChecklist_20260325.md](QuestStoryStartScenePlacementChecklist_20260325.md)
  - [QuestStoryBossEntranceBridge_20260325.md](QuestStoryBossEntranceBridge_20260325.md)

## 현재 기준 한 줄 요약
- 시체는 경고, 강한 일반 적은 증명, `SealPlaque`는 수락, 안개벽은 그 수락의 실행이다.
