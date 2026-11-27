# 퀘스트 시스템 흐름 계약서 (2026-03-25)

## 목적
- 버티컬 슬라이스 메인 퀘스트 1개의 진행 흐름을 시스템 기준으로 고정한다.
- 이 문서는 `무엇이 퀘스트를 전진시키는지`, `어떤 상태를 저장하는지`, `사망/이어하기에서 무엇을 유지하는지`를 정의한다.
- 인벤토리와 다이얼로그는 별도 스레드 범위로 두고, 여기서는 퀘스트 상태와 전이 규칙만 다룬다.

## 퀘스트 범위
- 대상 퀘스트:
  - 시작 구간에서 경고를 읽고, 문턱을 받아들이고, 메인 보스 진입까지 이어지는 메인 진행선
- 구조:
  - `발견 -> 추적 -> 수락 -> 조우 -> 결말`
- 현재 우선 구현 범위:
  - `발견 -> 수락 -> 조우`
- 후속 구현 범위:
  - `결말`

## 퀘스트 상태
### 핵심 상태
- `questStep`
- `goalKnown`
- `sealAccepted`
- `bossFightStarted`
- `bossCleared`
- `endingClaimed`
- `currentCheckpointId`

### 상태 역할
- `questStep`
  - 메인 퀘스트의 현재 단계
- `goalKnown`
  - 플레이어가 안쪽으로 가야 할 이유와 문턱 존재를 인지했는지
- `sealAccepted`
  - 플레이어가 결투를 받아들였는지
- `bossFightStarted`
  - 메인 보스 조우가 시작되었는지
- `bossCleared`
  - 메인 보스를 처치했는지
- `endingClaimed`
  - 결말 확인 장치를 확인했는지
- `currentCheckpointId`
  - 복귀 기준 체크포인트

## 단계 정의
### 1. `Arrival`
- 의미:
  - 시작 지점 도착
- 진입 조건:
  - 게임 시작 또는 씬 진입
- 기대 경험:
  - 아직 이유는 모르지만 안쪽에 무언가 있다는 감각

### 2. `Discovery`
- 의미:
  - 경고와 목적 인지
- 진입 조건:
  - 시체 조사
  - 기록물 조사
  - 또는 시작 구간 강한 일반 적 조우/처치 보강
- 기대 경험:
  - `여기서 멈출 수 없고 안쪽으로 가야 한다`

### 3. `Approach`
- 의미:
  - 문턱으로 향하는 추적 단계
- 진입 조건:
  - 시작 구간에서 안쪽 축선 진입
  - 또는 문턱 직전 공간 도달
- 기대 경험:
  - `이제 선택만 남았다`
- 메모:
  - 이 단계는 선택적이다.
  - 씬 구조 단순화를 위해 `Discovery -> Opened`로 바로 넘어가도 된다.

### 4. `Opened`
- 의미:
  - 문턱 수락 완료
- 진입 조건:
  - `SealPlaque` 상호작용
- 기대 경험:
  - `이제 피하지 않고 들어간다`

### 5. `Encounter`
- 의미:
  - 메인 보스 조우 시작
- 진입 조건:
  - `BossZoneTrigger_Entrance` 진입
- 기대 경험:
  - 선택이 실제 결투로 전환됨

### 6. `Cleared`
- 의미:
  - 메인 보스 처치
- 진입 조건:
  - 보스 사망 이벤트

### 7. `Ending`
- 의미:
  - 결말 확인 완료
- 진입 조건:
  - `RearGate` 또는 `VictoryAltar` 확인

## 단계 전이표
| 현재 단계 | 트리거 | 필수 조건 | 상태 변화 | 다음 단계 |
|---|---|---|---|---|
| `Arrival` | 시체 조사 | 없음 | `goalKnown = true` | `Discovery` |
| `Arrival` | 기록물 조사 | 없음 | `goalKnown = true` | `Discovery` |
| `Arrival` | 시작 구간 강한 일반 적 조우/처치 | 없음 | `goalKnown = true` 가능 | `Discovery` |
| `Discovery` | 문턱 직전 구역 접근 | 선택 사항 | 없음 또는 약한 진행 갱신 | `Approach` |
| `Discovery` | `SealPlaque` 상호작용 | 권장: `goalKnown == true` | `sealAccepted = true` | `Opened` |
| `Approach` | `SealPlaque` 상호작용 | 권장: `goalKnown == true` | `sealAccepted = true` | `Opened` |
| `Opened` | `BossZoneTrigger_Entrance` 진입 | 권장: `sealAccepted == true` | `bossFightStarted = true` | `Encounter` |
| `Encounter` | 보스 사망 | 없음 | `bossCleared = true`, `bossFightStarted = false` | `Cleared` |
| `Cleared` | 결말 장치 확인 | 없음 | `endingClaimed = true` | `Ending` |

## 오브젝트-상태 연결표
| 오브젝트 | 역할 | 기본 결과 | 비고 |
|---|---|---|---|
| `Checkpoint_Start` | 복귀 기준 | `currentCheckpointId` 등록 | 퀘스트 단계 직접 변경 없음 |
| 시체 | 경고 | `goalKnown = true` | 기본 경로 |
| 기록물 | 보강 | `goalKnown = true` | 시체 보강 경로 |
| 시작 구간 강한 일반 적 | 위협 증명 | `goalKnown` 보강 가능 | 보스형 시스템 아님 |
| `SealPlaque` | 선택 | `sealAccepted = true` | 필요시 `goalKnown = true` 동시 보정 |
| `BossZoneTrigger_Entrance` | 조우 시작 | `bossFightStarted = true` | 메인 보스 진입 |
| 보스 사망 이벤트 | 결투 종료 | `bossCleared = true` | `bossFightStarted = false` 동시 처리 |
| `RearGate` / `VictoryAltar` | 결말 확인 | `endingClaimed = true` | 후속 범위 |

## 실패/복귀 규칙
### 기본 원칙
- 퀘스트는 설명 반복보다 재도전을 우선한다.
- 사망은 전투 상태를 초기화하지만, 이미 이해한 진행 정보는 유지한다.

### 사망 시 유지
- `currentCheckpointId`
- `goalKnown`
- `sealAccepted`
- `bossCleared`

### 사망 시 초기화
- `bossFightStarted`

### 단계별 복귀 기준
#### `Arrival` 중 사망
- 복귀 상태:
  - `questStep = Arrival`
  - `goalKnown = false`
  - `sealAccepted = false`

#### `Discovery` 중 사망
- 복귀 상태:
  - `questStep >= Discovery`
  - `goalKnown = true`
  - `sealAccepted = false`

#### `Opened` 중 사망
- 복귀 상태:
  - `questStep >= Opened`
  - `goalKnown = true`
  - `sealAccepted = true`

#### `Encounter` 중 사망
- 복귀 상태:
  - `bossFightStarted = false`
  - `goalKnown = true`
  - `sealAccepted = true`
  - 체크포인트에서 재도전

## `Continue` 복구 규칙
- `questStep` 복구
- `currentCheckpointId` 복구
- `goalKnown` 복구
- `sealAccepted` 복구
- `bossCleared` 복구
- `endingClaimed` 복구
- `bossFightStarted`는 항상 `false`로 정규화

## 시스템 연결 원칙
### 인벤토리
- 퀘스트 진행 필수 조건은 인벤토리 아이템이 아니라 상태값으로 본다.
- 아이템은 보강 정보나 기록 보존용으로만 연결 가능하다.

### 다이얼로그
- 퀘스트 진행 필수 조건은 대사 소비가 아니라 상태값으로 본다.
- 다이얼로그는 상태 전이의 표현 계층으로만 연결 가능하다.

### 저장
- 퀘스트 시스템은 오브젝트 단위보다 상태 단위로 복구한다.
- 이미 달성한 단계는 재설명을 강제하지 않는다.

## 구현 우선순위
1. `Arrival -> Discovery`
   - 시체/기록물/위협 조우로 `goalKnown`
2. `Discovery -> Opened`
   - `SealPlaque`로 `sealAccepted`
3. `Opened -> Encounter`
   - `BossZoneTrigger_Entrance`
4. `Encounter -> Cleared`
   - 보스 사망 이벤트
5. `Cleared -> Ending`
   - 후속 범위

## 검증 기준
- 시체를 봤을 때 `goalKnown`이 세팅되는가
- `SealPlaque` 상호작용 시 `sealAccepted`가 세팅되는가
- 사망 후 `goalKnown`, `sealAccepted`가 유지되는가
- `Continue` 시 `bossFightStarted`만 초기화되고 나머지 퀘스트 상태는 유지되는가
- 오브젝트를 한 번 놓쳐도 다른 보강 경로로 퀘스트가 막히지 않는가

## 메모
- 지금 구현 우선 범위에서 퀘스트 시스템의 핵심은 `동기 인지`와 `문턱 수락`이다.
- 메인 보스 조우는 이 두 상태 위에 얹히는 후속 단계다.
