# 대포 발사 / 보스 전투 시스템 구현 계획

> 작성: 2026-06-01 · 대상 프로젝트: Unity_LBW_Frozen_Maze (Unity 6000.3.11f1, HDRP)
> 이 문서는 **구현 전 계획서**입니다. 승인 후 실제 작업에 들어갑니다.

---

## 1. 확정된 요구사항 (사용자 Q&A 결과)

| 항목 | 결정 내용 |
|---|---|
| **조작** | `F` 키로 대포 **탑승** → `WASD`로 조준(상하=고각, 좌우=좌우 선회) → `Space`로 발사 → `F`/`Esc`로 하차 |
| **탑승 중** | 대포 전용 카메라로 전환, 플레이어 이동/회전 잠금 |
| **포탄** | 물리 기반 포물선(Rigidbody + 중력). **추가로** 조준을 돕는 **반투명 예상 탄도 곡선**을 시야에 실시간 표시 |
| **보스 HP** | BossGolem 머리 위에 **월드 스페이스 HP 바** 추가 (DOTween으로 감소 연출) |
| **피격** | 포탄이 BossGolem에 명중 시 데미지 + **피격 모션 재생** |

---

## 2. 프로젝트 분석 (현재 상태)

### 2.1 재사용 가능한 자산
- **데미지 시스템 존재**: `Assets/Scripts/Health.cs`
  - `interface IDamageable { void TakeDamage(float damage); }`
  - `Health : MonoBehaviour, IDamageable` — `maxHealth`, `CurrentHealth`, `OnHealthChanged(float)`, `OnDeath` 이벤트 보유.
  - 기존 `BossAttackHitbox.cs`가 `other.GetComponentInParent<IDamageable>().TakeDamage(...)` 패턴 사용 → **포탄도 동일 패턴으로 재사용 가능.**
- **DOTween 사용 가능** (HP 바 연출에 활용).

### 2.2 새로 만들어야 하는 것
- **BossGolem에 `Health` 컴포넌트가 붙어있는지 확인 필요** — 없으면 추가.
- **애니메이터에 피격 상태 없음**: `Assets/arto/Golem_Controller.controller`
  - 현재 상태: `Attack`, `Blend Tree` / 파라미터: `AnimationSpeed`, `Attack`, `IsJumping`, `MotionAmount`, `Speed`.
  - **`Hit` 트리거 + `Hit` 상태를 신규 추가해야 함.**
  - 피격 클립 후보: `RPG Tiny Hero Duo/Animation/SwordAndShield/GetHit01_SwordAndShield.fbx` (휴머노이드) → 골렘으로 **리타겟** 필요.
- **HP 바 UI 없음** — 월드 스페이스 Canvas 신규 제작.
- **대포 모델 피벗 구조 없음**: 현재 `Cannon` 루트 아래 모든 파츠가 **평면적 직속 자식**. 조준을 위해 회전 피벗 분리 필요 (아래 4.A).

### 2.3 입력/카메라
- 플레이어 이동: `Assets/Jammo-Character/Scripts/MovementInput.cs` — **레거시 `Input` 시스템**(`Input.GetAxis`, `Input.GetKeyDown`). 새 입력 코드도 레거시로 통일.
- 탑승 시 `MovementInput` (및 플레이어 카메라 `Mario64Camera` 류) 비활성화 → 하차 시 복구.

### 2.4 공간 정보 (중요 — 튜닝 영향)
- 대포 위치: `TowerB (8)` 플랫폼, 약 `(-132.9, 28.7, 161.8)`.
- 대포 ↔ 보스: **수평 약 107m, 높이차 약 21m**.
- 대포 기본 전방이 보스에서 **약 145° 빗나가 있음** → 기본 방향 재설정 또는 넓은 좌우 선회각 필요.
- 보스 콜라이더: `isTrigger=true` + Rigidbody `isKinematic` → 포탄은 **`OnCollisionEnter`가 아니라 `OnTriggerEnter`로 명중 감지**해야 함.

---

## 3. 시스템 구성 개요

```
[플레이어] --F--> [CannonMountTrigger] --탑승--> 대포 모드 진입
   |                                              |
   |  MovementInput 비활성 / 카메라 전환           |
   v                                              v
[CannonController] <--WASD-- 조준(Yaw/Pitch)   [TrajectoryPredictor] (반투명 곡선)
   |                                              
   --Space--> [Cannonball 생성/발사] --물리 포물선-->
                       |
                  OnTriggerEnter (BossGolem)
                       |
        +--------------+--------------+
        v                             v
   IDamageable.TakeDamage      [BossHitReaction] Hit 트리거
        |                             |
   [BossHealthBar] 갱신(DOTween)  골렘 피격 애니메이션
```

---

## 4. 작업 항목

### A. 대포 모델 피벗 재구성
현재 평면 구조를 **조준 가능한 계층**으로 재편:
```
Cannon (루트, 위치/스케일)
└─ YawPivot (좌우 선회, 월드 Y축 회전)
   └─ ElevationPivot (상하 고각, 로컬 X축 회전) ← 포신·트러니언 중심
      ├─ Barrel / BoreCore / 밴드 등 (포신 파츠)
      └─ MuzzlePoint (포구 끝 빈 GameObject — 포탄 생성 위치/방향)
   └─ Carriage / Wheels / Axle (고정 — 선회는 따라가되 고각은 안 따라감)
```
- 바퀴·차대는 `YawPivot`에만 종속(좌우 선회 시 함께 회전), 포신만 `ElevationPivot`에 종속.
- 완성 후 **프리팹으로 저장** (`Assets/Prefabs/Cannon.prefab`) 및 씬 저장.

### B. 신규 스크립트 (`Assets/Scripts/Cannon/`)

1. **`CannonController.cs`**
   - 상태: `IsMounted`. 탑승/하차 처리.
   - `WASD`로 `YawPivot`(좌우)·`ElevationPivot`(상하) 회전, 각도 클램프(min/max 고각, 좌우 한계).
   - `Space` → `Fire()`: `MuzzlePoint`에서 `Cannonball` 인스턴스화, `muzzleVelocity`로 발사. 쿨다운/재장전 타이머.
   - 탑승 시 플레이어 `MovementInput` 비활성, 카메라 전환; 하차 시 복구.
   - 공개 파라미터: `muzzleVelocity`, `yawSpeed`, `pitchSpeed`, 각도 한계, `fireCooldown`.

2. **`Cannonball.cs`**
   - `Rigidbody`(중력 ON, `useGravity=true`, `interpolate`), `SphereCollider`(`isTrigger`).
   - `OnTriggerEnter`: `GetComponentInParent<IDamageable>()` 발견 시 `TakeDamage(damage)` 호출 + `BossHitReaction` 트리거 + 히트 VFX/SFX(선택) → 자기 파괴.
   - 일정 시간/거리 후 자동 소멸(`Destroy`).

3. **`TrajectoryPredictor.cs`**
   - 현재 `MuzzlePoint` 위치/방향 + `muzzleVelocity` + 중력으로 포물선 샘플링(고정 스텝 수치 적분).
   - `LineRenderer`로 **반투명 곡선** 렌더(투명 머티리얼). 지면/보스에 닿으면 그 지점에서 곡선 종료(레이캐스트로 충돌점 표시 마커 선택).
   - 탑승 중에만 표시.

4. **`CannonMountTrigger.cs`**
   - 대포 근처에 트리거 영역. 플레이어가 범위 내 + `F` → `CannonController.Mount()`.
   - "F로 탑승" 안내 UI(선택).

### C. BossGolem 통합

5. **`BossHitReaction.cs`** (BossGolem에 부착)
   - `Animator` 참조. `PlayHit()` → `animator.SetTrigger("Hit")`.
   - 짧은 무적/경직 처리(선택). DOTween 펀치 스케일 등 추가 연출 가능.

6. **`BossHealthBar.cs`** + 월드 스페이스 Canvas 프리팹
   - 보스 머리 위 빌보드 HP 바(Slider/Image fill).
   - `Health.OnHealthChanged` 구독 → DOTween으로 fill 감소 트윈, 카메라 향하도록 빌보드.
   - 0 도달 시 페이드/숨김, `OnDeath` 연동.

### D. 애니메이터 — Hit 상태 추가
- `Golem_Controller.controller`에 **`Hit` 트리거 파라미터** + **`Hit` 상태** 추가.
- `GetHit01_SwordAndShield`(휴머노이드) → 골렘 아바타로 **리타겟**, 거대 스케일에 맞게 확인.
- `Any State → Hit` 전이(트리거), `Hit → 이전 상태` 자동 복귀(Has Exit Time).

### E. 카메라 / 입력 전환
- 대포 전용 카메라 추가(포신 뒤/위). 탑승 시 활성, 하차 시 플레이어 카메라 복구.
- 입력 충돌 방지: 탑승 중 `MovementInput.enabled=false`, 플레이어 카메라 컨트롤러 비활성.

### F. 필요 자산 / 설정
- `Assets/Prefabs/`: `Cannon.prefab`, `Cannonball.prefab`, `BossHealthBar.prefab`.
- 반투명 탄도용 머티리얼(Unlit/Transparent, HDRP).
- 레이어/태그: 포탄이 자기 대포·플레이어와 충돌하지 않도록 레이어 분리 또는 무시 설정.

---

## 5. 구현 순서 (제안)

1. 대포 모델 피벗 재구성 + 프리팹화 (A) — 이후 작업의 토대.
2. `CannonController` + `CannonMountTrigger` 골격: 탑승/조준/입력·카메라 전환 (B1, B4, E).
3. `Cannonball` 물리 발사 + 명중 데미지(`IDamageable`) (B2).
4. `TrajectoryPredictor` 반투명 예상 곡선 (B3).
5. BossGolem `Health` 확인/부착 + `BossHitReaction` + 애니메이터 `Hit` 상태/리타겟 (C5, D).
6. `BossHealthBar` 월드 UI + DOTween (C6).
7. 통합 튜닝: 머즐 속도(107m/21m 도달), 좌우 선회각(145° 보정), 데미지 밸런스, 쿨다운.

각 단계마다 임시 카메라 렌더(PNG)로 시각 확인.

---

## 6. 리스크 / 주의

- **명중 감지**: 보스 콜라이더가 트리거+키네마틱 → 포탄은 `OnTriggerEnter` 사용(물리 충돌 아님). 포탄 콜라이더는 트리거로, 자체 이동은 Rigidbody로.
- **사거리/방향**: 약 107m·고도차 21m → 머즐 속도를 충분히 크게, 기본 포신 방향을 보스 쪽으로 재정렬(현재 145° 빗나감).
- **리타겟 스케일**: GetHit01은 소형 휴머노이드 클립 → 거대 골렘에서 모션 크기/속도 점검 필요.
- **입력 시스템**: 레거시 `Input`으로 통일(프로젝트가 레거시 사용).
- **카메라 복구**: 하차/사망/씬 전환 시 플레이어 카메라·이동이 항상 복구되도록 보장.

---

## 7. 산출물 위치 요약
- 스크립트: `Assets/Scripts/Cannon/`
- 프리팹: `Assets/Prefabs/`
- 애니메이터: `Assets/arto/Golem_Controller.controller` (Hit 상태 추가)
- 머티리얼: 탄도용 반투명 머티리얼 (신규)
