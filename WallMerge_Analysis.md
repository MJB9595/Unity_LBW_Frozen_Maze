# WallMerge 기믹 분석 — 3D → 벽화(Decal) 시스템

> 프로젝트: `Unity_LBW_Frozen_Maze` | Unity 6000.3.11f1

---

## 1. 개요

플레이어가 벽 앞에서 `Space`를 누르면 **3D 캐릭터가 납작하게 눌려 벽 속으로 스며들고**,
이후 데칼(Decal Projector) 형태로 벽면을 좌우로 이동하는 기믹이다.
다시 `Space`를 누르면 원래 3D 상태로 복귀한다.

```
[3D 플레이어] ──Space──> [벽화 모드] ──Space──> [3D 복귀]
                  ↑                      ↑
             WallMerge.cs          ProjectorMovement.cs
```

---

## 2. 관련 스크립트 구조

| 스크립트 | 부착 대상 | 역할 |
|---|---|---|
| `WallMerge.cs` | Player | 진입/탈출 제어, 카메라·PP 전환, DecalLayer 관리 |
| `ProjectorMovement.cs` | DecalProjector 오브젝트 | 벽화 상태의 이동·회전 제어 |
| `RaySearch.cs` | 벽(Wall) 오브젝트 자식 | 벽 형상 탐색 → 코너 포인트 목록 생성 |
| `GapTrigger.cs` | 좁은 틈새 트리거 | 포탈 스퀴즈 연출 식별용 마커 |
| `MovementInput.cs` | Player | 3D 이동 (벽화 모드 중 비활성) |

---

## 3. 전체 흐름

### 3-1. 진입 (`WallMerge.cs` → `Update`)

```
Space 키 입력
  └─ Raycast(forward, 1m)로 벽 감지
       └─ 벽에 RaySearch 컴포넌트 존재 확인
            └─ DoPointsFromPlayer() : 벽 형상 재귀 탐색
                 └─ 코너 포인트 목록 확보
                      ├─ 가장 가까운 코너(closestCorner) 선택
                      ├─ 이웃 코너(chosenCorner) 계산 (좌·우 판별)
                      ├─ positionLerp : 플레이어의 벽 내 상대 위치
                      ├─ ApplyDecalLayerToWall() : 해당 벽에 Decal Layer 비트 ON
                      └─ Transition(merge=true) 호출
```

### 3-2. Transition 애니메이션 (`Transition`)

| 단계 | 동작 |
|---|---|
| 플레이어 forward 설정 | 벽 노멀 반대 방향으로 회전 |
| Animator Trigger | `"turn"` 트리거 발동 |
| PlayerActivation(true) | `MovementInput`, `CharacterController` 비활성 |
| MergeSequence | DOTween 시퀀스 실행 (아래 표 참고) |
| DoF Volume | 진입 후 0.3s 딜레이 → 무게 1로 페이드인 |
| Zoom Volume | 0.7s 동안 1로 → 0.3s 동안 다시 0 (강조 후 복귀) |

**MergeSequence 단계 (DOTween Sequence)**

```
[딜레이 0.2s]
→ gameCam OFF / wallCam ON
→ 플레이어 위치 → 벽 위치 이동 (Ease.InBack, 0.5s)
→ 플레이어 Z 스케일 → 0.01 (납작해짐)
→ playerMovement 오브젝트 SetActive(false)
→ DecalProjector 자식 오브젝트 SetActive(true)
→ mergeParticle 재생
→ CinemachineImpulse 발생 (화면 진동)
→ decalMovement.isActive = true
```

### 3-3. 벽화 이동 (`ProjectorMovement.cs` → `Update`)

```
Horizontal 입력
  └─ movementMode: MoveTowards(originPos ↔ targetPos)
       └─ 코너 근접 시 StartRotation() 호출
            └─ rotationMode: CornerRotation() 으로 pivot 기준 회전
                 └─ 회전 완료 시 다음 구간의 originPos·targetPos 갱신
                      └─ movementMode 재개
```

#### 코너 Pivot 계산

두 라인 (`lineRef1`, `lineRef2`) 의 교점(Line-Line Intersection)을 구해
코너의 정확한 회전 중심점을 수학적으로 계산한다.

```
lineRef1 : 코너에서 다음 방향으로 distanceToTurn 오프셋 + 이전 노멀 방향
lineRef2 : 코너에서 이전 방향으로 distanceToTurn 오프셋 + 현재 노멀 방향
교점 = pivot 위치
```

#### TrackCurrentWall

데칼이 다른 벽 메시로 이동할 때 Decal Layer를 자동으로 이전한다.

```
매 프레임 -transform.forward 방향으로 Raycast(1.5m)
  └─ 히트한 오브젝트가 lastWallObject와 다를 경우
       └─ ApplyDecalLayerToWall(new) + RestoreWallLayer(old)
```

### 3-4. GapTrigger (틈새 포탈 연출)

코너 회전 시작 시, pivot 위치 반경 0.5m 내에 `GapTrigger` 컴포넌트가 있으면
**포탈 스퀴즈 연출**이 발동한다.

| 단계 | 동작 |
|---|---|
| 시작 | 데칼 X 스케일 → 0 (찌그러짐) + SetActive(false) |
| 효과 | Zoom Volume·DoF Volume 강화, mergeParticle 재생 |
| 종료 | 데칼 SetActive(true), X 스케일 → 1 (탄성 복귀) |
| 복구 | Zoom·DoF Volume 원복, exitParticle 재생 |

### 3-5. 탈출 (`ProjectorMovement.cs`)

```
Space 키 입력 (isActive == true)
  └─ 부모 해제, 모드 초기화
  └─ 플레이어 위치 = 데칼 위치 - forward * 0.5m (벽에서 한 발 앞)
  └─ player.Transition(merge=false) 호출
       ├─ gameCam ON / wallCam OFF
       ├─ exitParticle 재생
       ├─ 플레이어 Z 스케일 원복
       ├─ MovementInput·CharacterController 재활성
       ├─ DoF Volume → 0
       └─ RestoreWallLayer() : Decal Layer 비트 원복
```

---

## 4. RaySearch — 벽 형상 탐색 알고리즘

벽 오브젝트에 부착되어 있으며, 플레이어 진입 시 **재귀적 레이캐스트**로
벽의 모든 코너 좌표를 실시간으로 수집한다.

### 탐색 3단계 (매 재귀 호출)

```
현재 포인트(pt)에서:

1. [Tangent Ray]  pt + normal*margin → +tangent 방향으로 stepSize 만큼 발사
   ├─ 히트 → 다음 포인트 확정
   └─ 미스 →
2. [Normal Ray]  tangentCheckPoint에서 -normal 방향으로 발사 (안쪽 확인)
   ├─ 히트 → 다음 포인트 확정
   └─ 미스 →
3. [Behind Ray]  negativeCheckPoint에서 -tangent 방향으로 발사 (뒤돌아 확인)
   ├─ 히트 → 다음 포인트 확정
   └─ 미스 → 탐색 종료
```

### 코너 판별 (Current 모드)

```
법선 변화량 측정: Vector3.Dot(lastNormalForCorner, newNormal) < 0.98
→ 임계값 이상 꺾이면 cornerPoints에 등록
→ 첫 포인트는 무조건 코너로 등록
→ 시작 포인트 재방문 시 루프 탈출 (cornerCheck)
```

### Legacy vs Current 모드 차이

| | Legacy | Current |
|---|---|---|
| 기준 | 인접 두 포인트 법선 비교 | `lastNormalForCorner` 누적 비교 |
| 첫 포인트 | 스킵 | 코너로 등록 |
| 안정성 | 낮음 | 높음 (현재 사용) |

---

## 5. Decal Layer 격리 시스템

DecalProjector가 모든 벽에 투영되는 문제를 막기 위해
**RenderingLayerMask 비트 제어**로 현재 합체된 벽에만 데칼을 표시한다.

```
시작 시  : 씬 내 모든 Renderer에서 DecalLayer 비트 제거
진입 시  : 히트된 벽의 Renderer에 DecalLayer 비트 OR 추가 + 원본 백업
이동 중  : TrackCurrentWall이 새 벽 감지 시 이전(Restore) → 적용(Apply)
탈출 시  : RestoreWallLayer()로 원본 마스크 복원
```

```csharp
uint bit = 1u << (decalRenderingLayerIndex + 8);  // 예: Layer1 = 비트 9
renderer.renderingLayerMask |= bit;   // 적용
renderer.renderingLayerMask  = orig;  // 복원
```

---

## 6. 카메라 & 포스트 프로세싱

| 상태 | gameCam | wallCam | DoF Volume | Zoom Volume |
|---|---|---|---|---|
| 3D 모드 | ON | OFF | 0 | 0 |
| 진입 중 | OFF→ON | ON→OFF | 0→1 (딜레이) | 0→1→0 |
| 벽화 모드 | OFF | ON | 1 | 0 |
| 탈출 중 | ON | OFF | 1→0 | 0 |

**FrameQuad** (액자 이펙트):
- 진입: 플레이어 위치 → 벽 위치로 날아가며 페이드인 → 페이드아웃
- 탈출: 벽 위치에 고정된 채로 페이드인 → 페이드아웃
- 셰이더 프로퍼티: `_UnlitColor` (DOTween Color 애니메이션)

---

## 7. 컴포넌트 의존 관계

```
Player GameObject
├── WallMerge.cs
│    ├── ref: ProjectorMovement (decalMovement)
│    ├── ref: Transform (frameQuad)
│    ├── ref: GameObject (gameCam, wallCam)
│    ├── ref: Volume (dofVolume, zoomVolume)
│    └── ref: CinemachineImpulseSource (Camera.main)
├── MovementInput.cs
├── CharacterController
└── Animator

DecalProjector GameObject
└── ProjectorMovement.cs
     ├── ref: WallMerge (player, wallMerge)
     ├── ref: Transform (pivot, lineRef1, lineRef2)
     ├── ref: ParticleSystem (mergeParticle, exitParticle)
     └── RaySearch (런타임 주입)

Wall GameObject
└── [자식] RaySearch.cs
     └── MeshPoint[] (position + normal)

Gap GameObject (틈새)
└── GapTrigger.cs  ← 마커만, 로직 없음
```

---

## 8. 알려진 주의사항 / 버그 히스토리

| 항목 | 내용 |
|---|---|
| `SetPosition`에서 `lastWallObject = null` 제거 | `ApplyDecalLayerToWall` 직후 동기화 타이밍 문제 방지 |
| `wallMerge` 참조 주입 시점 | `SetPosition` 이후에 `decalMovement.wallMerge = this`로 명시 주입 |
| `TrackCurrentWall` 중복 호출 | `movementMode`/`rotationMode` 조건부 + `wallMerge != null` 이중 가드 |
| 파티클 월드 스페이스 고정 | `simulationSpace = World` 강제 → 캐릭터 이동 시 파티클 뭉침 방지 |
| exitParticle 색상 | 텍스처 겹침 눈뽕 방지를 위해 검보라(0.2, 0, 0.3) 강제 설정 |
| 코너 반대 방향 버그 | `nearTarget && axis > 0` 조건으로 진행 방향 일치 시에만 회전 발동 |

---

## 9. 핵심 인터랙션 요약

```
[Space] 벽 앞에서
  → RaySearch 벽 감지
  → 코너 포인트 수집 (RaySearch 재귀)
  → 데칼 위치 계산 (positionLerp)
  → Decal Layer 적용
  → 카메라 전환 (game → wall)
  → 플레이어 납작해지며 벽으로 이동 (DOTween)
  → 데칼 활성화

[←→] 벽화 이동
  → originPos ↔ targetPos 사이 MoveTowards
  → 코너 도달 시 pivot 회전
  → GapTrigger 감지 시 포탈 스퀴즈

[Space] 벽화 상태에서
  → 데칼 위치 기준 플레이어 복귀
  → 카메라 전환 (wall → game)
  → 플레이어 복귀 애니메이션
  → Decal Layer 복원
```
