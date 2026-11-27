# 소울라이크 보스전 (6인 팀 프로젝트) — 코드 포트폴리오

소울라이크의 **'패턴 학습 → 사망 → 재도전'** 루프를 한 구간으로 구현한 Unity 팀 프로젝트의 코드 공개용 저장소입니다.
타이틀 → 성 진입 → 보스전 → 체크포인트 리스폰 → 엔딩까지 연결했습니다.

> 이 저장소는 **포트폴리오 열람용**입니다. 원본은 팀 비공개 저장소이며, 팀원이 제작한 3D 모델·컨셉아트·오디오와 에셋스토어 리소스는 저작권·라이선스 문제로 제외했습니다. 따라서 이 저장소만으로는 실행되지 않습니다.

| 항목 | 내용 |
| --- | --- |
| 장르 | 3인칭 소울라이크 액션 RPG, 1:1 보스전 |
| 엔진·플랫폼 | Unity 2022.3 (URP, Cinemachine), PC |
| 기간 | 2026.01 – 2026.05 (팀 1~3월, 이후 개인 고도화) |
| 팀 구성 | 6명 (개발 2 · 3D 모델링 3 · 컨셉아트 1) |
| 본인 담당 | 시스템 개발 — 플레이어 전투, 락온 카메라, 보스전 흐름, 팀 세팅 |

## 담당 파트와 코드 위치

모든 경로는 `Assets/00Game/01Scripts/` 기준입니다.

### 플레이어 전투 (FSM)
이동·구르기·공격·가드·회복·사망을 상태로 분리했습니다. 3타 콤보에 선입력 버퍼와 구르기 캔슬을 넣고, 타수별 선딜/후딜을 1타 0.42/0.60초, 2타 0.47/0.50초, 3타 0.70/1.35초로 조정했습니다.

- `00_Core/StateMachine.cs`, `00_Core/IState.cs` — 공용 상태 머신
- `02_Characters/Player/StateMachine/States/` — Idle·Move·Attack·Roll·Parry·UseItem 상태
- `02_Characters/Player/Combat/Modules/` — 전투·스태미나·패리·이동 모듈
- `03_Combat/Data/CombatAttackDefinition.cs` — 공격 판정 구간을 normalized time 데이터로 정의
- `04_Data/Player/PlayerAttackLibrary.asset` — 타수별 공격 데이터

### 락온 카메라
Cinemachine으로 자유·락온 시점을 전환합니다. 반경 12, 시야각 70° 안에서 대상을 고르고 Linecast로 벽 뒤 대상을 제외합니다. 락온 마커는 거리에 따라 크기를 보정합니다.

- `02_Characters/Player/CameraLockOn/LockOnSystem.cs`
- `02_Characters/Player/CameraLockOn/Modules/LockOnTargetSelector.cs`, `LockOnTargetValidator.cs` — 대상 선택·가림 판정
- `02_Characters/Player/CameraLockOn/Modules/PlayerLockOnFlowCoordinator.cs` — 해제 순서 고정
- `02_Characters/Player/CameraLockOn/Modules/FreeLookSyncModule.cs` — 해제 직전 자유 시점 각도 동기화

### 보스전 흐름
보스방 진입 시 안개벽, 보스 체력바, BGM이 켜집니다. 사망하면 YOU DIED 후 체크포인트에서 체력·스태미나·회복약을 채워 재시작합니다.

- `00_Core/BossZone/` — 보스존 진입·안개벽·인트로·전투 흐름
- `01_Managers/GameLoop/` — 체크포인트(화톳불), 리스폰, 퀘스트 진행
- `01_Managers/TitleFlow/` — 타이틀·세이브·옵션·씬 전환
- `05_UI/` — 보스 체력바, YOU DIED, 상태 바, 상호작용 프롬프트

> 보스·적 AI(`02_Characters/Boss`, `02_Characters/Enemies`)는 콘텐츠 담당 팀원이 초기 구현했고, 이후 2페이즈·패턴 확장 등 개인 고도화 과정에서 함께 수정했습니다.

## 문제 해결

**락온 해제 시 화면 튐.** 해제 순간 자유 시점 카메라 각도가 어긋났고, 락온 중 실시간으로 각도를 맞추면 회전 버그가 생겼습니다. 카메라 전환을 Priority에서 State-Driven Camera로 바꾸고, 해제 직전 한 번만 각도를 맞추도록 했습니다. '마커 정리 → 각도 동기화 → 자유 시점 복귀' 순서는 `PlayerLockOnFlowCoordinator`에서 고정했습니다.

**애니메이터 변경 시 여러 파일 수정.** 상태 클래스마다 애니메이터 파라미터 이름을 직접 써서, 애니메이터 구조가 바뀌면 여러 곳이 깨졌습니다. 이름 정의는 `PlayerAnimatorParams` 한 파일로 모으고 호출은 `PlayerAnimatorDriver`만 하도록 바꿨습니다. 상태 클래스는 `TrySetGuard(true)`, `TryCrossFadeToState(...)`처럼 드라이버에 행동만 요청하고, 파라미터·상태가 없으면 `Try*` 호출이 false를 돌려줄 뿐 게임이 멈추지 않습니다.

**애니메이션 이벤트에 묶인 공격 판정.** 모션을 바꿀 때마다 판정 이벤트를 다시 찍어야 했습니다. 판정 구간을 normalized time 값으로 데이터(`CombatAttackDefinition`)에 넣어, 모션이 바뀌어도 숫자만 수정하게 했습니다.

**미해결.** 절차적 애니메이션은 기간 내 완성하지 못해 격투게임용 애니메이션으로 대체했고, 카메라 떨림은 일부만 개선했습니다.

## 팀 세팅

Unity 프로젝트와 GitHub(Git LFS)를 세팅하고, 비개발 팀원용 GitHub·Unity 사용법 문서를 작성했습니다. 개발자와 모델러의 작업 씬을 분리하고, 일일 작업 기록 규칙을 정했습니다.
코드는 Cursor·ChatGPT로 작성했고, 주차별 세팅 문서와 테스트 체크리스트로 동작을 검증했습니다. 설계 문서 일부는 `Docs/Setup/`에 있습니다.

## 저장소 구성

```
Assets/00Game/01Scripts/   C# 스크립트와 데이터(.asset)
Docs/Setup/                보스 패턴·보스 아레나·게임 루프·퀘스트 흐름 설계 문서
Packages/manifest.json     사용 패키지 목록
ProjectSettings/           Unity 버전 정보
```
