# 보스전 버티컬 슬라이스 진행 설계 스펙

## 문서 분류
- 문서 유형: 세팅/진행 구조 스펙 문서
- 역할: 현재 보스전 버티컬 슬라이스의 메인 진행 라인, 플레이어 동기, 보스 전후 맥락, 저장에 반영할 진행 상태를 정리한다.

## 문서 목적
이 문서는 현재 프로젝트의 `진입 -> 조우 -> 결투 -> 결말` 루프에
`왜 이곳에 왔는가`, `무엇을 보고 결심하는가`, `어떤 조건으로 문턱을 넘는가`, `보스 처치 후 무엇이 달라지는가`
를 추가해 구현 스레드가 바로 이어받을 수 있는 진행 기준을 만든다.

## 우선 참조 문서
1. `Docs/Rules/QuestStoryThreadRules.md`
2. `Docs/Reports/ProgressReport.md`
3. `Docs/Setup/SoulslikeVerticalSliceRoadmap.md`
4. `Docs/Setup/GameLoopExperienceGuide.md`
5. `Docs/Setup/BossArenaLevelSpec.md`
6. `Docs/Setup/BossPatternSpec.md`
7. 필요 시 `Docs/Reports/ThreadHandoffInbox.md`

## 설계 전제
- 플레이어는 성 안쪽으로 들어가는 유일한 통로가 `보스룸 앞 전실`에서 끊겨 있기 때문에 이곳에 왔다.
- 이 구간의 보스는 단순 적이 아니라 `안쪽 길을 막고 있는 마지막 수문장`이다.
- 스토리는 긴 설명 대신 `체크포인트`, `시체/석판`, `안개벽`, `열린 뒤쪽 문`의 상태 변화로 전달한다.
- 현재 기준 씬은 `Assets/00Game/00Scenes/TestScenes/developScenes.unity`로 해석한다.
- 현재 실제 루프는 `Checkpoint_Start -> BossZoneTrigger_Entrance -> FogWall_Entrance -> Boss -> YOU DIED/Respawn` 구조이며, 진행 설계는 이 루프를 해치지 않는 선에서 밀도를 높인다.

### 진행 설계 정의
- 진행 구간:
  - `보스룸 전 진입 복도 -> 봉인 문턱 -> 보스 결투 -> 처치 후 개방 구간`
- 플레이어 목표:
  - `성 안쪽 통로를 막는 수문장을 넘어 봉인을 해제하고 다음 길이 열렸다는 결말을 확인한다.`
- 프로젝트 내 사용 씬:
  - `Assets/00Game/00Scenes/TestScenes/developScenes.unity`
  - 필요 시 시작 진입은 `Assets/00Game/00Scenes/Prototype/Title.unity`에서 연결
- 관련 오브젝트/NPC:
  - 현재 사용:
    - `Checkpoint_Start`
    - `BossZoneTrigger_Entrance`
    - `FogWall_Entrance`
    - `Boss`
    - `GameLoopCanvas`
  - 진행 연출용 추가 권장:
    - `CollapsedRetainer` 또는 `FallenKnight` 시체 1기
    - `SealPlaque` 상호작용 오브젝트 1개
    - `RearGate` 또는 `VictoryAltar` 1개
    - `BossRemainsMarker` 또는 `SealFragment` 1개
- 완료 조건:
  - 최소 완료:
    - `bossCleared == true`
  - 권장 완료:
    - `bossCleared == true`
    - `endingClaimed == true`
- 기준 문서:
  - `Docs/Rules/QuestStoryThreadRules.md`
  - `Docs/Setup/GameLoopExperienceGuide.md`
  - `Docs/Setup/BossArenaLevelSpec.md`
  - `Docs/Setup/BossPatternSpec.md`
  - `Docs/Reports/ProgressReport.md`

### 진행 구조 요약
- 시작 상태:
  - 플레이어는 `Checkpoint_Start`에 도달한 직후이며, 안쪽 통로는 안개와 수문장에게 막혀 있다.
  - 아직 싸워야 하는 이유는 완전히 설명되지 않았지만, `여기가 마지막 준비 지점`이라는 감각은 분명해야 한다.
- 중간 목표:
  - 복도 안의 짧은 환경 힌트를 통해 `문을 열려면 문지기를 넘겨야 한다`는 사실을 이해한다.
  - `SealPlaque`를 조사해 결투 의도를 확정한 뒤 문턱을 넘는다.
- 보스 진입 조건:
  - 최소 현재 호환 조건:
    - `Checkpoint_Start` 활성화 후 `BossZoneTrigger_Entrance` 진입
  - 권장 설계 조건:
    - `goalKnown == true`
    - `sealAccepted == true`
    - `BossZoneTrigger_Entrance` 진입
- 결말 상태:
  - 보스 처치 후 안개벽이 걷히고, 뒤쪽 문 또는 제단이 활성화된다.
  - 플레이어는 `길이 열렸다`는 상태 변화를 확인하고 슬라이스 종료 지점을 획득한다.

### 단계별 진행표
| 단계 | 플레이어 행동 | 트리거 / 조건 | 힌트 방식 | 실패 시 복귀 | 다음 상태 |
|---|---|---|---|---|---|
| 1. 도착 | 체크포인트를 지나며 전실에 진입한다 | `Checkpoint_Start` 진입, `currentCheckpoint` 등록 | 따뜻한 체크포인트 대비 차가운 안개벽, 보스룸 축선이 바로 보이는 배치 | 없음, 첫 시작점 | `checkpointBound` |
| 2. 발견 | 시체 또는 짧은 기록물을 조사해 왜 싸워야 하는지 안다 | `CollapsedRetainer` 또는 기록물 1회 조사 | 짧은 문장 1개, 피 자국, 부서진 방패, 안쪽을 향한 시선 유도 | 사망 시 체크포인트로 복귀, 조사 완료 상태는 유지 | `goalKnown` |
| 3. 추적 | 복도를 걸으며 문턱 앞까지 접근한다 | 복도 끝 시야 확보, 보스 실루엣 또는 무기 흔적 확인 | 긁힌 바닥, 끌린 자국, 저음 환경음, 먼 거리 실루엣 | 사망 시 체크포인트로 복귀 | `approachConfirmed` |
| 4. 개방 | `SealPlaque`를 조사해 결투를 받아들인다 | `goalKnown == true` 상태에서 `SealPlaque` 상호작용 | 짧은 문구, 문양 점등, 안개가 얇아지는 시각 변화 | 사망 시 체크포인트로 복귀, `sealAccepted`는 유지 | `sealAccepted` |
| 5. 조우 | 안개를 통과해 보스전을 시작한다 | `sealAccepted == true` 상태에서 `BossZoneTrigger_Entrance` 진입 | 안개벽 폐쇄, 보스 UI/BGM 시작, 보스가 플레이어 쪽으로 반응 | `YOU DIED -> Checkpoint_Start -> 재진입`, 안개와 BGM은 초기화 | `bossFightActive` |
| 6. 결투 | 보스를 쓰러뜨린다 | `Boss` HP 0, `bossCleared == true` | 1페이즈 학습, 2페이즈 압박 강화, 처치 순간 공간 정적 | 사망 시 단계 5 재도전 | `bossCleared` |
| 7. 결말 | 뒤쪽 문 또는 제단을 조사해 길이 열렸음을 확인한다 | `bossCleared == true` 후 `RearGate` 개방 또는 `VictoryAltar` 상호작용 | 안개 해제, 후면 빛/바람/소리 변화, 짧은 문구 1개 | 전투 실패 없음, 저장 후 이어하기는 결말 직후 상태로 복귀 | `endingClaimed` |

### 스토리 / 퀘스트 메모
- 플레이어 동기:
  - `안쪽으로 가려면 여기서 더는 우회할 수 없다.`가 핵심이다.
  - 거창한 구원 서사보다 `막힌 길을 열기 위해 수문장을 넘는다`는 단일 동기가 우선이다.
- 공간의 의미:
  - `Checkpoint_Start` 앞은 결투 전 마지막 준비 지점이다.
  - 복도는 탐색 구간이 아니라 `결심을 굳히는 압축된 전조 구간`이다.
  - 보스룸은 왕좌실보다 `심판실` 또는 `봉인 전실`에 가깝다.
- 보스와의 관계:
  - 보스는 플레이어 개인의 원수보다 `길을 막는 역할`이 먼저 읽혀야 한다.
  - 플레이어가 안으로 들어가려는 의지와 보스가 그것을 거부하는 역할 충돌이 중심이다.
  - 패턴상 `Slam / Stomp / Dash`는 수문장이 문턱을 지키고 밀어내는 전투 감정과 연결된다.
- 처치 후 변화:
  - 안개벽이 걷히고 재도전 상태가 `개방 상태`로 바뀌어야 한다.
  - 뒤쪽 문, 제단, 봉인 파편 중 하나가 활성화되어 `이 싸움이 길을 여는 싸움이었다`는 의미를 남긴다.
  - 체크포인트 또는 주변 조명이 준비 상태와 다른 색/강도로 바뀌면 좋다.

## 힌트 배치 기준
- 설명 텍스트는 길게 쓰지 않는다.
- 텍스트 힌트는 최대 1~2개만 사용한다.
- 나머지는 아래 우선순위로 전달한다.
  1. 축선: 복도에서 보스룸 중심이 바로 읽혀야 한다.
  2. 잔해: 실패한 도전자 흔적이 문턱 앞에 몰려 있어야 한다.
  3. 상태 변화: 체크포인트의 안전함과 안개벽의 차단감이 대비돼야 한다.
  4. 후면 보상: 보스 뒤에 `열려야 할 것`이 미리 보이거나 실루엣으로 암시돼야 한다.

## 진행 조건 설계 기준
- 체크포인트는 `전투 직전 서약 지점` 역할로 사용한다.
- `SealPlaque`는 선택을 확정하는 장치이며, 열쇠 아이템 수집으로 흐름을 늘리지 않는다.
- `BossZoneTrigger_Entrance`는 단순 트리거가 아니라 `결투 선언선`으로 해석한다.
- `FogWall_Entrance`는 전투 중에는 후퇴 차단, 처치 후에는 상태 변화 증거 역할을 맡는다.
- `RearGate` 또는 `VictoryAltar`는 보상보다 `다음 길이 열렸음`을 확인하는 장치로 쓴다.

## 저장 / 이어하기 반영 상태 정의
- 현재 저장은 `sceneName`만 저장하므로, 아래 진행 상태는 확장 필요 항목으로 정의한다.
- 권장 저장 필드:
  - `questStep`
  - `currentCheckpointId`
  - `goalKnown`
  - `sealAccepted`
  - `bossFightStarted`
  - `bossCleared`
  - `endingClaimed`
  - `rearGateOpened`
- 복구 규칙:
  - `goalKnown == true`면 조사 오브젝트는 재상호작용이 필수가 아니어야 한다.
  - `sealAccepted == true && bossCleared == false`면 플레이어는 체크포인트에서 바로 재도전 가능해야 한다.
  - `bossCleared == true`면 보스 트리거는 비활성, 안개벽은 해제 상태여야 한다.
  - `endingClaimed == true`면 결말 직후 상태 또는 다음 씬 진입 직전 상태로 복원한다.

## 진행 상태 계약
| 상태 키 | 기본값 | 세팅 시점 | 유지 규칙 | 복구 시 처리 |
|---|---|---|---|---|
| `questStep` | `Arrival` | 새 게임 시작 | 마지막 완료 단계 기준 갱신 | 해당 단계 이후 오브젝트 상태 복원 |
| `currentCheckpointId` | `Checkpoint_Start` 또는 빈값 | 체크포인트 등록 시 | 가장 최근 체크포인트 유지 | 리스폰 위치와 이어하기 기준으로 사용 |
| `goalKnown` | `false` | 시체/기록물 조사 완료 시 | 보스 클리어 전까지 유지 | 조사 오브젝트 재강제 금지 |
| `sealAccepted` | `false` | `SealPlaque` 상호작용 성공 시 | 보스 클리어 전까지 유지 | 보스 재도전 가능 상태로 복원 |
| `bossFightStarted` | `false` | `BossZoneTrigger_Entrance` 통과 시 | 보스 종료 또는 플레이어 사망 시 false | 로드시 true 상태로 직접 복구하지 않고 재도전 상태로 정규화 |
| `bossCleared` | `false` | 보스 사망 이벤트 수신 시 | 영구 유지 | 보스 비활성, 안개 해제, 후면 장치 개방 |
| `endingClaimed` | `false` | `RearGate` 또는 `VictoryAltar` 상호작용 시 | 영구 유지 | 결말 직후 상태 또는 다음 진입 직전 상태 복원 |
| `rearGateOpened` | `false` | 보스 처치 직후 또는 결말 상호작용 시 | `bossCleared`와 함께 유지 | 후면 문/제단 상태 복원 |

## 트리거 / 조건 계약
| 오브젝트 | 입력/이벤트 | 선행 조건 | 상태 변화 | 실패/예외 처리 |
|---|---|---|---|---|
| `Checkpoint_Start` | 플레이어 진입 | 없음 | `currentCheckpointId = Checkpoint_Start`, `questStep >= Arrival` | 누락 시 기본 체크포인트 fallback 유지 |
| `CollapsedRetainer` 또는 기록물 | 상호작용 1회 | `bossCleared == false` | `goalKnown = true`, `questStep = Discovery` | 미조사 상태여도 진행 차단 금지, 대신 약한 힌트만 유지 |
| 시야 확보 지점 | 플레이어 접근 | 없음 | `questStep = Approach` | 별도 저장은 선택 사항, 연출 실패 시 전투 루프 영향 없음 |
| `SealPlaque` | 상호작용 1회 | `goalKnown == true` 권장 | `sealAccepted = true`, `questStep = Opened` | 미조사 상태에서 상호작용해도 허용 가능, 대신 `goalKnown = true`를 함께 세팅 가능 |
| `BossZoneTrigger_Entrance` | 플레이어 진입 | 최소 현재 구조는 없음, 권장 조건은 `sealAccepted == true` | `bossFightStarted = true`, `questStep = Encounter` | 저장 복구 직후라도 재진입 가능해야 함 |
| 보스 HP 0 | `OnDied` | `bossFightStarted == true` | `bossCleared = true`, `bossFightStarted = false`, `questStep = Cleared`, `rearGateOpened = true` | 보스 클리어 직후 트리거 비활성화 |
| 플레이어 사망 | `PlayerDied` | `bossFightStarted == true` | `bossFightStarted = false` | `YOU DIED -> currentCheckpointId` 기준 복귀 |
| `RearGate` 또는 `VictoryAltar` | 상호작용 | `bossCleared == true` | `endingClaimed = true`, `questStep = Ending` | 미클리어 상태에서는 상호작용 차단 또는 무반응 처리 |

## 실패 / 복귀 규칙
- 전투 전 실패:
  - 사실상 없음. 추락/테스트 예외를 제외하면 체크포인트 이전으로 되감지 않는다.
- 전투 중 사망:
  - `bossFightStarted = false`
  - `goalKnown`, `sealAccepted`는 유지
  - 리스폰 위치는 `currentCheckpointId`
  - 안개벽, 보스 UI, BGM은 전투 전 상태로 초기화
- 보스 처치 후 종료:
  - 재도전 루프 종료
  - `bossCleared = true`가 저장되면 보스 트리거는 다시 켜지지 않는다
  - 플레이어가 즉시 결말 장치를 확인하지 않아도 개방 상태는 유지한다
- 이어하기 정규화:
  - 저장 시점이 전투 도중이더라도 로드시 `bossFightStarted = false`로 정규화한다
  - 이어하기는 항상 `체크포인트에서 재도전` 또는 `클리어 후 개방 상태` 둘 중 하나로 복원한다

## 보스 전후 맥락 계약
- 진입 전 맥락:
  - 플레이어는 여기서 막히면 더 안으로 갈 수 없다는 사실을 먼저 읽어야 한다.
  - 전실은 휴식처가 아니라 `마지막 준비 구역`이다.
  - `SealPlaque`는 "여기를 지나려면 대가를 치러야 한다"는 선언 장치다.
- 진입 순간 맥락:
  - 안개벽이 닫히는 이유는 길이 막혀서가 아니라 `결투가 성립했기 때문`으로 읽혀야 한다.
  - 보스는 매복보다 정면 대면형이 좋다. 입장 즉시 존재가 확인돼야 한다.
- 처치 후 맥락:
  - 전투 승리와 길 개방은 같은 사건이지만 다른 피드백으로 분리한다.
  - `안개 해제`는 전투 종료의 피드백이다.
  - `RearGate` 개방 또는 `VictoryAltar` 활성화는 서사 결말의 피드백이다.
- 다음 목표 암시:
  - 문/제단 너머 빛, 바람, 먼 배경 실루엣 중 하나로 "안쪽이 있다"는 사실만 암시한다.
  - 다음 콘텐츠가 아직 없어도 `다음 길이 열렸다`는 감각만 확보하면 슬라이스 목표에는 충분하다.

## 구현 체크리스트
- 게임 루프 / 씬 통합 체크:
  - `SealPlaque`가 없어도 기존 루프는 깨지지 않아야 한다.
  - `SealPlaque`가 있으면 보스전 의미가 강화되어야 한다.
  - `bossCleared` 후 `BossZoneTrigger_Entrance` 재진입이 전투를 재시작시키지 않아야 한다.
  - `RearGate` 또는 `VictoryAltar`는 보스 클리어 전후 상태 차이가 분명해야 한다.
- 저장 체크:
  - `Continue` 직후 체크포인트 위치가 일관적인가
  - `bossCleared` 저장 후 로드시 안개벽/보스/후면 장치 상태가 충돌하지 않는가
  - `endingClaimed` 저장 후 같은 결말 상호작용이 중복되지 않는가
- 검증 체크:
  - 처음 보는 플레이어가 기록물 1개와 공간 배치만으로 목표를 이해하는가
  - 전투 1패 후에도 다시 무엇을 해야 하는지 헷갈리지 않는가
  - 보스 처치 후 10초 안에 `무엇이 달라졌는지` 눈으로 확인 가능한가

### 구현 전달 메모
- 게임 루프 / 씬 구현 필요 항목:
  - `Checkpoint_Start` 진입 후 `currentCheckpoint`가 항상 등록되는지 유지
  - `SealPlaque` 상호작용 1회로 `sealAccepted` 상태를 세팅
  - `BossZoneTrigger_Entrance`가 `sealAccepted`와 연동되거나 최소 연출 상 그 의미를 반영
  - `bossCleared` 시 `FogWall_Entrance` 해제 유지
  - `RearGate` 또는 `VictoryAltar` 활성화 후 결말 이벤트 발행
- 저장 / 이어하기 반영 필요 항목:
  - 현재 `SaveGameService`에 `sceneName` 외 진행 상태 저장 구조 추가
  - `Continue` 시 `bossCleared`, `endingClaimed`, `currentCheckpointId` 복구
  - 보스 미클리어 상태 이어하기는 `Checkpoint_Start` 복귀가 기본
- 아트 / 에셋 요청 필요 항목:
  - 문턱 앞 시체 또는 무기 잔해 1세트
  - `SealPlaque`용 간단한 석판/문양
  - 뒤쪽 문 또는 제단용 개방 상태 시각 요소
  - 보스 처치 후 조명/먼지/바람 변화용 간단한 이펙트
- 검증 포인트:
  - 처음 보는 플레이어가 `왜 안으로 들어가야 하는지` 30초 안에 이해하는가
  - `SealPlaque` 없이 바로 뛰어들어도 흐름이 끊기지 않는가, 또는 차단 의도가 분명한가
  - 사망 후 `YOU DIED -> 체크포인트 -> 재진입`이 1회전 안에 다시 성립하는가
  - 보스 처치 후 `전투 종료`와 `길 개방`이 서로 다른 피드백으로 읽히는가
  - `Continue` 시 보스 전/후 상태가 충돌 없이 복구되는가

## 구현 우선순위 제안
1. 현재 루프 유지형 최소안
   - `Checkpoint_Start` + 환경 힌트 + `SealPlaque` + 보스 처치 후 `RearGate` 개방
2. 그 다음 보강
   - 보스 진입 전 짧은 조우 전용 카메라/사운드
   - 보스 처치 후 결말 상호작용
3. 저장 확장
   - `bossCleared`, `endingClaimed`, `currentCheckpointId`부터 우선 저장

## 작업 진행 순위
1. 메인 진행선 최소안 고정
   - 현재 씬 구조를 유지한 채 `goalKnown -> sealAccepted -> bossCleared -> endingClaimed` 흐름을 구현 기준으로 확정한다.
2. 진입 전 동기 힌트 배치
   - 시체/잔해/짧은 기록물로 `왜 여기까지 왔는가`를 30초 안에 읽히게 만든다.
3. `SealPlaque` 결투 확정 장치 추가
   - 보스전 시작을 단순 충돌이 아니라 문턱 선택으로 바꾼다.
4. 보스 처치 후 개방 상태 추가
   - `RearGate` 또는 `VictoryAltar`를 열어 결말과 다음 길 암시를 분리한다.
5. 저장 상태 확장
   - `currentCheckpointId`, `sealAccepted`, `bossCleared`, `endingClaimed`를 우선 저장/복구한다.
6. 조우/결말 체감 보강
   - 카메라, 사운드, 조명 변화로 조우 전후의 감정 차이를 높인다.

## 즉시 착수 작업
1. 게임 루프 / 씬 통합 스레드
   - `SealPlaque`와 `RearGate`를 최소 상호작용 오브젝트로 추가하고 `sealAccepted`, `endingClaimed` 상태를 이벤트 흐름에 연결한다.
2. 타이틀 / 메뉴 / 저장 / 옵션 스레드
   - `SaveGameService` 확장 초안에 `currentCheckpointId`, `sealAccepted`, `bossCleared`, `endingClaimed`를 추가한다.
3. 아트 프롬프트 / 비주얼 디렉션 스레드
   - `SealPlaque`, 문턱 앞 시체/잔해, 뒤쪽 문 또는 제단의 역할 중심 시각 기준을 잡는다.
4. 검증 / 회귀 체크 스레드
   - 위 1~3이 붙은 뒤 `진입 동기 이해`, `사망 후 재도전`, `보스 처치 후 개방 확인`, `Continue 복구`를 체크리스트로 재검증한다.
