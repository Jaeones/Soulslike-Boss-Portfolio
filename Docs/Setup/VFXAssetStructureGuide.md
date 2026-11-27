# VFX 자산 구조 / 네이밍 가이드

## 문서 분류
- 문서 유형: VFX 자산 구조 / 네이밍 기준 문서
- 상태: Draft
- 담당 스레드: VFX / 이펙트 제작

## 문서 목적
이 문서는 `2ndProject`에서 재사용 가능한 VFX 프리팹을 어떤 경로와 이름 규칙으로 관리할지 정리한다.

핵심 목적은 아래 두 가지다.
- VFX 자산 자체와 씬/로직 연결 프리팹을 분리한다.
- 연결 스레드가 바로 사용할 수 있는 공용 프리팹 구조를 만든다.

## 조사 기준
- 작업 브랜치:
  - `Beta`
- 조사 기준 씬:
  - `Assets/00Game/00Scenes/TestScenes/developScenes.unity`
- 조사 기준 프리팹:
  - `Assets/00Game/02Prefabs/Effect/*`
  - `Assets/00Game/02Prefabs/BossZone/FogWall_Entrance.prefab`
  - `Assets/00Game/02Prefabs/Prop/Bonfire.prefab`
- 조사 기준 아트 경로:
  - `Assets/00Game/03Art/Materials`
  - `Assets/00Game/03Art/Textures`
  - `Assets/00Game/03Art/Imported/MedCastle/Source/Fire VFX`

## 현재 구조 스냅샷
### 1. 현재 `Effect` 폴더
현재 `Assets/00Game/02Prefabs/Effect`에는 아래 프리팹이 있다.
- `BloodImpact_Player.prefab`
- `BloodImpact_Boss.prefab`
- `FloorMist.prefab`
- `Fog.prefab`
- `LakeFloorFog.prefab`
- `Hit_02.prefab`

관찰 결과:
- `BloodImpact_Player`, `BloodImpact_Boss`, `FloorMist`, `Fog`, `LakeFloorFog`는 단일 `Particle System` 중심 프리팹이다.
- `Hit_02.prefab`은 다중 파티클 자식(`Haze Flash`, `Sparks`, `Particles`)을 가진 원샷 임팩트 프리팹이다.
- 현재 `Effect` 폴더 내부에는 카테고리 하위 폴더가 없다.

### 2. 현재 기술 기준
`Assets/00Game` 기준으로 조사했을 때 아래 항목은 아직 확인되지 않았다.
- `.vfx`
- `VisualEffect`
- `TrailRenderer`
- `LineRenderer`
- `DecalProjector`

현재 실사용 기준은 `Particle System + Material` 조합으로 보는 것이 맞다.

### 3. 현재 분산 상태
- `Assets/00Game/02Prefabs/BossZone/FogWall_Entrance.prefab`
  - `FogWallController`, `BoxCollider`, 비활성 `Visual` 메시 자식이 있는 씬 연결 프리팹이다.
  - 재사용 가능한 안개벽 VFX 프리팹이 아니라, 게임 루프 연결용 래퍼에 가깝다.
- `Assets/00Game/02Prefabs/Prop/Bonfire.prefab`
  - 현재는 정적 메시 + 콜라이더 프리팹이다.
  - 불꽃 VFX가 같이 들어 있지 않다.
- `Assets/00Game/03Art/Imported/MedCastle/Source/Fire VFX/Prefabs/LargeFlames.prefab`
  - 외부 임포트된 화염 프리팹이다.
  - `developScenes.unity`에서 직접 여러 개가 배치되어 있다.
- `Assets/00Game/02Prefabs/BG/Fog.prefab`
  - `Effect/Fog.prefab`과 별개의 대형 환경 포그 프리팹이다.

### 4. 현재 머티리얼 상태
- `BloodImpact_Player`, `BloodImpact_Boss`는 `Assets/00Game/03Art/Materials/BloodImpact.mat`를 사용한다.
- `FloorMist`는 `Assets/00Game/03Art/Materials/FloorMist.mat`를 사용한다.
- `Fog`, `LakeFloorFog`는 현재 커스텀 머티리얼보다 기본 파티클 머티리얼 의존이 강하다.
  - 이 판단은 프리팹 머티리얼 참조 기준 추정이다.

## 공용 폴더 구조 기준
### 원칙
- 재사용 VFX는 `Assets/00Game/02Prefabs/Effect` 아래에서 관리한다.
- 씬 흐름, 트리거, 충돌, 상호작용을 가진 래퍼 프리팹은 기존 소유 폴더에 둔다.
- 즉, `BossZone/FogWall_Entrance.prefab` 같은 래퍼는 유지하고, 실제 VFX 자산만 `Effect` 아래로 분리한다.

### 권장 구조
```text
Assets/00Game/02Prefabs/Effect/
  Common/
    Environment/
      FogWall/
      Fog/
      Dust/
    Interaction/
      Bonfire/
      Glow/
      Seal/
    Combat/
      Hit/
      Blood/
      Trajectory/
    Utility/
      Test/
      Shared/
```

### 소유 경계
- `Assets/00Game/02Prefabs/Effect/Common/...`
  - VFX 스레드 소유
  - 순수 이펙트 프리팹, 이펙트 자식 구조, 머티리얼/텍스처 의존성 관리
- `Assets/00Game/02Prefabs/BossZone/...`
  - 게임 루프 / UI / 씬 통합 스레드 소유
  - 트리거, 콜라이더, 활성/비활성 흐름, 씬 배치 관리
- `Assets/00Game/02Prefabs/Prop/...`
  - 씬 배치용 프롭 프리팹 소유
  - 필요 시 자식으로 공용 VFX 프리팹 참조

## 네이밍 기준
### 프리팹
신규 공용 VFX 프리팹은 아래 형식을 기본으로 쓴다.

```text
FX_<Category>_<Subject>_<Variant>_<Loop|Burst|OneShot>
```

예시:
- `FX_Environment_FloorMist_Loop`
- `FX_Environment_FogWall_Entrance_Loop`
- `FX_Interaction_Bonfire_Flame_Loop`
- `FX_Combat_BloodImpact_Player_Burst`

적용 이유:
- `FogWall_Entrance` 같은 씬 래퍼 프리팹과 이름이 겹치지 않는다.
- 검색 시 `FX_` 접두어만으로 이펙트 자산을 빠르게 찾을 수 있다.
- 반복형/원샷형을 파일명만 보고 구분할 수 있다.

### 머티리얼
```text
M_FX_<Subject>_<Variant>
```

예시:
- `M_FX_FloorMist_Soft`
- `M_FX_FogWall_Primary`
- `M_FX_BloodImpact_Splat`

### 텍스처
```text
T_FX_<Subject>_<Variant>_<Alpha|Noise|Mask|Flipbook|Normal>
```

예시:
- `T_FX_BloodImpact_Splat_Alpha`
- `T_FX_FogWall_SoftNoise_Noise`

### 파일명 기본 규칙
- 경로와 파일명은 ASCII 기준으로 유지한다.
- 공백 대신 `_`를 사용한다.
- 씬 연결 프리팹 이름과 VFX 프리팹 이름을 동일하게 쓰지 않는다.
- `New Material`, `New Prefab` 같은 임시 이름은 남기지 않는다.

## 프리팹 내부 구조 기준
### 공용 이펙트 프리팹
가능하면 아래 최소 구조를 맞춘다.

```text
FX_...
  SpawnRoot
  PS_Primary
  PS_Secondary
  Mesh_Primary
```

기록 항목:
- loop / one-shot 구분
- auto destroy 여부
- world/local space 여부
- 기본 회전 기준
- 추천 배치 축
- 최대 파티클 수
- 커스텀 머티리얼 사용 여부

### 씬 래퍼 프리팹
씬 소유 프리팹은 아래 역할만 유지한다.
- 콜라이더
- 트리거/컨트롤러 스크립트
- 공용 VFX 프리팹을 매다는 루트
- 필요 시 위치/스케일 보정용 자식

즉, 씬 래퍼 안에 전용 VFX 값을 직접 크게 품지 않는다.

## 즉시 우선 후보 메모
### 1. 보스방 안개벽
- 현재 상태:
  - `FogWall_Entrance.prefab`은 `FogWallController` + `BoxCollider` + 비활성 `Visual` 메시 자식 구조다.
  - 순수 안개벽 이펙트 프리팹은 별도 공용 프리팹으로 분리했다.
- 자산 제작 범위:
  - `Assets/00Game/02Prefabs/Effect/Common/Environment/FogWall/FX_Environment_FogWall_Entrance_Loop.prefab`
  - `Assets/00Game/03Art/Materials/M_FX_FogWall_Primary.mat`
  - `Assets/00Game/03Art/Materials/M_FX_FogWall_Secondary.mat`
  - 3장 메시 시트 + 3중 `Fog.prefab` 위스프 레이어 기반 루프형 구조
  - 문턱 폭 50 / 높이 10 기준에 맞춘 기본 배치 스케일
- 연결 범위:
  - `FogWall_Entrance.prefab`의 `visualRoot` 아래에 VFX 자식 연결
  - 활성/비활성 타이밍은 `FogWallController.SetFogWallActive()` 유지
  - 보스 시작/종료, 리스폰, 저장 상태에 따른 흐름 연결은 게임 루프 / UI / 씬 통합 스레드 소유

### 안개벽 1차 구현 메모
- 생성 자산:
  - `FX_Environment_FogWall_Entrance_Loop.prefab`
- 내부 구성:
  - `Mesh_Fog_Back`
  - `Mesh_Fog_Core`
  - `Mesh_Fog_Front`
  - `PS_Wisps_Back`
  - `PS_Wisps_Core`
  - `PS_Wisps_Front`
- 구현 의도:
  - 메시 3장으로 기본 불투명 실루엣을 먼저 확보
  - 기존 `Assets/00Game/02Prefabs/Effect/Fog.prefab`를 3중으로 중첩해 표면 유동감 추가
  - 씬 래퍼 프리팹은 수정하지 않고, 공용 VFX 자산만 독립적으로 생성
- 한계:
  - 전용 노이즈 텍스처와 스크롤 셰이더가 아직 없어서, 다크소울식 찢기는 가장자리 표현은 다음 패스가 필요하다
  - 현재는 `URP/Lit` 투명 머티리얼 기반 1차 형태다

### 2. 화톳불
- 현재 상태:
  - `Bonfire.prefab`은 정적 메시 프리팹이고, 화염은 외부 임포트 `LargeFlames.prefab`가 씬에서 직접 배치된 흔적이 강하다.
- 자산 제작 범위:
  - `FX_Interaction_Bonfire_Flame_Loop`
  - 필요 시 `FX_Interaction_Bonfire_Ember_Loop`
- 연결 범위:
  - `Bonfire.prefab` 자식 배치
  - 상호작용 애니메이션 / 휴식 UI / 체크포인트 흐름 연결은 게임 루프 / UI / 씬 통합 스레드 소유

## 이번 조사 결론
- 현재 프로젝트의 VFX 자산은 `Effect`, `BG`, `BossZone`, `Prop`, `Imported`에 분산돼 있다.
- 실사용 기술은 거의 `Particle System` 중심이라서, 지금 시점에 폴더/네이밍 기준을 먼저 고정하는 것이 효율적이다.
- 안개벽과 화톳불은 "씬 래퍼 프리팹"과 "공용 VFX 프리팹"을 분리해야 이후 연결 충돌이 줄어든다.
