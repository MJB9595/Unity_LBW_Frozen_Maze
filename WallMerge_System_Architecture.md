# WallMerge 시스템 아키텍처

## 개요

WallMerge는 플레이어(Jammo_Player)가 **벽 표면에 달라붙어 이동**할 수 있게 해주는 시스템입니다.
Spacebar를 누르면 앞에 있는 벽의 표면을 탐색(RaySearch)하고, 디졸브/트래킹 연출과 함께 플레이어를 벽 속으로 밀어 넣습니다.
이후 플레이어는 DecalProjector(데칼) 형태로 벽 표면을 따라 이동하며, 모서리에서 코너를 돌거나 좁은 틈새를 통과할 수 있습니다.

---

## 시스템 구성도

```
┌─────────────────────────────────────────────────────────────────────┐
│                        WallMerge System                             │
│                                                                     │
│  ┌──────────────┐     ┌──────────────────┐     ┌──────────────┐    │
│  │  WallMerge    │ ──▶ │ ProjectorMovement │ ◀── │  RaySearch   │    │
│  │  (Jammo_Player│     │ (Decal Projector) │     │  (Wall Mesh) │    │
│  │   의 컴포넌트) │     └──────────────────┘     └──────────────┘    │
│  └──────┬───────┘               │                                    │
│         │                       │                                    │
│         ▼                       ▼                                    │
│  ┌──────────────┐     ┌──────────────────┐                          │
│  │ MovementInput│     │   GapTrigger     │                          │
│  │ (플레이어 이동) │     │ (틈새 감지 마커)  │                          │
│  └──────────────┘     └──────────────────┘                          │
│                                                                     │
│  ┌──────────────┐     ┌──────────────────┐                          │
│  │  Boss 관련    │     │  Post-Processing  │                         │
│  │  스크립트들   │     │  (DOF / Zoom)     │                         │
│  └──────────────┘     └──────────────────┘                          │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 핵심 스크립트

### 1. WallMerge.cs (`Assets/Scripts/WallMerge.cs`)

**역할:** WallMerge 시스템의 **진입점(Entry Point)**. Jammo_Player GameObject에 Attach됨.

**주요 기능:**

| 기능 | 설명 |
|------|------|
| 벽 감지 및 합체 시작 | Spacebar 입력 → Raycast로 전방 벽 감지 → `RaySearch.DoPointsFromPlayer()` 호출 |
| 코너 위치 계산 | 감지된 cornerPoints 중 가장 가까운 코너 + 다음/이전 코너 판별 |
| Decal Layer 제어 | 합체된 벽에만 Decal Projector가 보이도록 `renderingLayerMask` 조작 (`ApplyDecalLayerToWall` / `RestoreWallLayer`) |
| Transition 연출 | DOF(Depth of Field) + Zoom Volume + 카메라 전환(gameCam ↔ wallCam) + 프레임 Quad 애니메이션 |
| Merge Sequence | DOTween 시퀀스: 이동, 스케일 조정(Z축 0.01), 카메라 전환, 파티클 재생, CineMachine Impulse |

**Public Reference 연결:**

```csharp
public ProjectorMovement decalMovement;   // → ProjectorMovement 참조
public Transform frameQuad;               // → 합체 연출용 스프라이트 Quad
public GameObject gameCam;                // → 일반 3인칭 카메라
public GameObject wallCam;                // → 합체 중 전환되는 카메라
public Volume dofVolume;                  // → Depth of Field Post-Process
public Volume zoomVolume;                 // → Zoom Post-Process
public Color frameLitColor;               // → 프레임 Quad의 발광 색상
public int decalRenderingLayerIndex;      // → Decal Layer 슬롯 번호 (0~7)
```

**Mode Toggle:**

```csharp
[Header("Mode")]
public WallMergeMode mergeMode = WallMergeMode.Current;
```

Inspector에서 `Merge Mode` 드롭다운으로 **Legacy (구버전)** / **Current (신버전)** 선택 가능.
- WallMerge가 RaySearch.`DoPointsFromPlayer()`를 호출하기 직전에 `search.mergeMode = this.mergeMode`를 주입
- RaySearch의 `FindNext()`에서 모드에 따라 서로 다른 코너 탐색 알고리즘 실행

**FlOW:**

```
Spacebar 입력
  ↓
Physics.Raycast (전방 1m)
  ↓
RaySearch.DoPointsFromPlayer() 호출
  ↓
cornerPoints 목록 수신
  ↓
closestCorner / nextCorner / previousCorner / chosenCorner 결정
  ↓
decalMovement.SetPosition() 호출 (데칼 위치 초기화)
  ↓
ApplyDecalLayerToWall() (현재 벽에만 Decal Layer ON)
  ↓
Transition(true, ...) 호출
  ↓
  ├─ FrameMovement()   → Quad가 벽으로 날아가는 연출
  ├─ PlayerActivation() → MovementInput/CharacterController 비활성화
  ├─ MergeSequence()   → DOTween: 위치이동, Z스케일 0.01, 카메라 전환, 파티클
  ├─ DOF/Zoom Volume   → Post-Processing 효과
  └─ playerAnimator.SetTrigger("turn") → 턴 애니메이션
```

---

### 2. ProjectorMovement.cs (`Assets/Scripts/ProjectorMovement.cs`)

**역할:** 합체 후 **데칼(Decal) 프로젝터의 이동/회전**을 담당. 벽 표면 위에서의 모든 움직임을 처리.

**Public Reference:**

```csharp
public WallMerge player;              // WallMerge 참조 (인스펙터)
[HideInInspector] public WallMerge wallMerge;  // WallMerge에서 코드로 주입
[HideInInspector] public GameObject lastWallObject; // 현재 Decal Layer 적용된 벽
public Transform pivot;               // 코너 회전 시 부모로 사용
public Transform lineRef1, lineRef2;  // 회전축 계산용 Line Intersection
public ParticleSystem mergeParticle;  // 합체 파티클
public ParticleSystem exitParticle;   // 탈출 파티클
```

**상태 머신 (Movement Mode):**

```
┌─────────────┐     Space(탈출)      ┌─────────────┐
│    Idle     │ ◀────────────────── │  Movement   │
│ (isActive=  │                      │ (isActive=  │
│   false)    │                      │   true)     │
└─────────────┘                      └──────┬──────┘
       ▲                                    │
       │              코너 도착              │
       │    ┌───────────────────────────────┘
       │    ▼
       │ ┌─────────────┐
       │ │  Rotation   │
       └─ │ (Corner)   │
          └─────────────┘
```

**핵심 기능:**

| 기능 | 설명 |
|------|------|
| `SetPosition()` | WallMerge에서 호출. 데칼 시작/목표 위치, 방향 설정 |
| 벽 이동 | Horizontal axis 입력 → `Vector3.MoveTowards`로 origin/targetPos 사이 이동 |
| `StartRotation()` | 코너 도착 시 회전 모드 진입. `GetPivotPosition()`으로 회전축 계산, GapTrigger 감지 |
| `CornerRoration()` | Lerp 기반 pivot 회전 → 회전 완료 시 movementMode 복귀 |
| `TrackCurrentWall()` | 매 프레임 Raycast로 현재 벽 감지 → 다른 벽으로 넘어가면 Decal Layer 이전 |
| 탈출 (Space) | `WallMerge.Transition(false, ...)` 호출 → 플레이어를 벽 밖으로 복귀 |
| 포탈 연출 | GapTrigger 감지 시 Decal 스케일 왜곡 + Post-Processing + 파티클 |

**GapTrigger 포탈 연출 상세:**

```
GapTrigger 감지
  ↓
DecalVisual.DOScaleX(0, 0.15s)  → 데칼 찌그러짐
ZoomVolume.weight → 1            → 줌인
DOFVolume.weight → 1             → 블러
mergeParticle.Play()             → 합체 파티클
  ↓
회전 완료 후:
  ↓
DecalVisual.SetActive(true) + DOScaleX(1, 0.2s OutBack)
ZoomVolume.weight → 0
DOFVolume.weight → 0
exitParticle.Play()              → 탈출 파티클 (검보라색)
```

---

### 3. RaySearch.cs (`Assets/Scripts/RaySearch.cs`)

**역할:** 벽 Mesh 표면을 따라 **Ray를 순회하며 코너 포인트(모서리)를 탐색**. 벽의 형상을 분석.

**핵심 데이터 구조:**

```csharp
public List<MeshPoint> meshPoints;    // 모든 탐색 포인트
public List<MeshPoint> cornerPoints;  // 코너(모서리) 포인트만 저장

[System.Serializable]
public struct MeshPoint {
    public Vector3 position;
    public Vector3 normal;
}
```

**주요 메서드:**

| 메서드 | 설명 |
|--------|------|
| `DoPoints()` | 에디터 ContextMenu 용. 자신의 위치에서 forward 방향 탐색 |
| `DoPointsFromPlayer(rayOrigin, rayDirection)` | **WallMerge에서 호출**. 플레이어 위치/방향 기준 탐색 |

**탐색 알고리즘 (`FindNext` recursive):**

```
① 현재 지점(hit.point)의 normal 획득
② normal 방향으로 offsetMargin 만큼 이동
③ tangent( = Cross(normal, up) ) 방향으로 stepSize 만큼 전진 시도
   ├─ 레이가 hit → 그 지점에서 재귀 (①로)
   └─ miss → -normal 방향으로 Raycast
              ├─ hit → 그 지점에서 재귀
              └─ miss → -tangent 방향으로 역추적
④ normal 변화 감지 → cornerPoints에 추가
⑤ 최대 checkCountMax 도달 or 원점 복귀 시 종료
```

**코너 감지 조건:**

- Current 모드: 첫 번째 포인트는 항상 코너로 추가 (`meshPoints.Count == 0`), 이후 `lastNormalForCorner`(마지막 **코너**의 normal)와 비교하여 `Dot < 0.98`이면 새 코너로 추가
- Legacy 모드: `meshPoints.Count > 1`일 때부터, 이전 **인접 포인트**(`meshPoints[Count-1]`)의 normal과 비교하여 `Dot < 0.98`이면 새 코너로 추가

**루프백 감지 조건:**

- Current 모드: `Vector3.Dot(cornerPoints[0].normal, normal) > .99f` (임계값 기반)
- Legacy 모드: `cornerPoints[0].normal == normal` (정확 일치 기반)

**모드 선택:** WallMerge 컴포넌트의 Inspector → `Merge Mode` 드롭다운에서 Legacy / Current 선택 가능

---

### 4. MovementInput.cs (`Assets/Jammo-Character/Scripts/MovementInput.cs`)

**역할:** Jammo_Player의 일반 이동/회전 제어.

**WallMerge와의 관계:**

| 연동 포인트 | 설명 |
|------------|------|
| WallMerge.PlayerActivation(true) | `movementInput.enabled = false` (합체 중 이동 차단) |
| WallMerge.PlayerActivation(false) | `movementInput.gameObject.SetActive(true)` (탈출 후 복구) |
| WallMerge.MergeSequence | 탈출 시 `movementInput.enabled = true` |

---

### 5. GapTrigger.cs (`Assets/Scripts/GapTrigger.cs`)

**역할:** 좁은 틈새를 감지하는 **식별용 마커(Marker)** 스크립트.

- BoxCollider Trigger로 배치
- 로직 없음 — ProjectorMovement가 `Physics.OverlapSphere`로 감지만 함
- 감지되면 포탈 스퀴즈 연출(Portal Squeeze Effect) 발동

---

## 보조 스크립트

### 6. PlayerKnockback.cs (`Assets/Scripts/PlayerKnockback.cs`)

- **역할:** 보스 파티클 충돌 시 플레이어 넉백 처리
- `OnParticleCollision` → 넉백 방향 계산 → `CharacterController.Move()`
- MovementInput에서 `knockbackTimer > 0`이면 이동 입력 차단

### 7. Health.cs (`Assets/Scripts/Health.cs`)

- **역할:** IDamageable 인터페이스 구현. 체력 관리
- `BossAttackHitbox`가 `OnTriggerEnter` 시 `IDamageable.TakeDamage()` 호출
- 현재 Jammo_Player에 이 컴포넌트가 붙어있다면 보스 공격 시 데미지 처리됨

### 8. BossFollow.cs (`Assets/Scripts/BossFollow.cs`)

- **역할:** 보스의 NavMesh 기반 플레이어 추적
- `GameObject.Find("Jammo_Player")`로 플레이어 참조 획득
- WallMerge와 직접적인 연관은 없으나, 합체 중 보스 추적은 계속됨 (긴장감 유지)

### 9. BossCrumbleTrigger.cs (`Assets/Scripts/BossCrumbleTrigger.cs`)

- **역할:** 특정 Zone에서 플레이어 사망 처리
- `WinLossController.Instance.Loss()` 호출

### 10. CinemachineImpulseSource

- WallMerge 합체 시 `impulseSource.GenerateImpulse()` → 카메라 쉐이크
- Camera.main에서 참조

---

## 렌더링 Layer 시스템 (Decal Layer Isolation)

WallMerge의 핵심 트릭: **합체된 벽에만 Decal Projector가 보이도록** 렌더링 Layer를 조작합니다.

```csharp
// 시작 시: 모든 벽의 renderingLayerMask에서 Decal 비트 제거
RemoveDecalLayerFromAllWalls()
  └─ decalBit = 1u << (decalRenderingLayerIndex + 8)

// 합체 시: 해당 벽에만 Decal 비트 추가
ApplyDecalLayerToWall(GameObject wallObject)
  └─ currentWallRenderers의 originalLayerMasks 백업
  └─ renderingLayerMask |= decalBit

// DecalProjector의 Decal Layer: 인스펙터에서 matching Layer로 설정

// 이동 중: TrackCurrentWall()가 다른 벽으로 넘어가면 자동으로 Layer 이전
```

---

## 카메라 시스템

| 카메라 | 사용 시점 | 설명 |
|--------|-----------|------|
| **gameCam** | 일반 이동 | 3인칭 Cinemachine 카메라 |
| **wallCam** | 합체 중 | 벽 표면 뷰로 전환 |

- `MergeSequence`에서 `gameCam.SetActive(!merge)` / `wallCam.SetActive(merge)`로 전환
- Post-Processing: DOF(Depth of Field) + Zoom Volume으로 합체/포탈 연출

---

## 의존성 그래프 (Dependency Graph)

```
Jammo_Player GameObject
├── WallMerge.cs
│   ├── → MovementInput.cs      (enabled/disabled 제어)
│   ├── → CharacterController     (enabled/disabled 제어)
│   ├── → Animator                ("turn"/"normal" 트리거)
│   ├── → ProjectorMovement.cs  (decalMovement 참조)
│   │   ├── → RaySearch.cs      (search 참조 - 벽 표면 정보)
│   │   ├── → GapTrigger.cs     (GapTrigger 감지)
│   │   ├── → WallMerge.cs      (wallMerge 역참조)
│   │   └── → ParticleSystem x2 (mergeParticle, exitParticle)
│   ├── → Transform frameQuad   (연출용 Quad)
│   ├── → Volume dofVolume      (Post-Processing)
│   ├── → Volume zoomVolume     (Post-Processing)
│   ├── → GameObject gameCam    (3인칭 카메라)
│   ├── → GameObject wallCam    (벽 카메라)
│   └── → CinemachineImpulseSource (카메라 쉐이크)
│
├── MovementInput.cs
│   └── → PlayerKnockback.cs    (넉백 중 이동 차단)
│
└── Boss 관련 (간접 연관)
    ├── BossFollow.cs           (Jammo_Player 추적)
    ├── BossCombat.cs → BossAttackHitbox.cs
    └── BossCrumbleTrigger.cs   (사망 처리)
```

---

## 씬 설정 요구사항

WallMerge 기능이 동작하려면 씬 내에 다음 요소들이 배치되어야 합니다:

1. **Jammo_Player** (WallMerge + MovementInput + CharacterController + Animator)
2. **ProjectorMovement** (Decal Projector 자식으로 파티클 포함)
3. **RaySearch** 가 붙은 벽 GameObject들
4. **frameQuad** (Quad Sprite, 투명 머티리얼)
5. **gameCam** / **wallCam** (Cinemachine 카메라들)
6. **DOF Volume** + **Zoom Volume** (Post-Processing)
7. **GapTrigger** (선택사항 — 틈새 포탈 연출용)
8. **Boss** (선택사항 — 추적/전투)

---

## 주요 플로우 요약

```
[일반 상태]
  Spacebar 입력
  ↓
[벽 탐색] RaySearch.DoPointsFromPlayer()
  ↓
[합체] WallMerge.Transition(true)
  ├─ FrameMovement → Quad 연출
  ├─ PlayerActivation(false) → 이동 차단
  ├─ MergeSequence → DOTween 이동 + 카메라 전환 + 파티클
  └─ DOF/Zoom → Post-Processing
  ↓
[벽 이동] ProjectorMovement (Horizontal Input)
  ├─ 코너 도착 → StartRotation → CornerRoration
  │   └─ GapTrigger 감지 시 → Portal Squeeze Effect
  └─ TrackCurrentWall → 벽 변경 시 Decal Layer 이전
  ↓
  Spacebar (탈출)
  ↓
[탈출] WallMerge.Transition(false)
  ├─ PlayerActivation(true) → 이동 복구
  └─ RestoreWallLayer → Decal Layer 정리
```
