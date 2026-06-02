# instruct_next_is_you.md — 다음 에이전트를 위한 인수인계 문서

> 이 문서를 읽는 너에게: 아래는 이 Unity 프로젝트에서 지금까지 진행한 작업의 **상세 기록**이다. 요약하지 않고
> 운영 방법 / 함정 / 코드 변경 / 검증 절차 / 현재 상태를 그대로 적었다. 새 작업을 시작하기 전에 "운영 규칙"과
> "함정(Gotchas)" 섹션을 반드시 먼저 읽어라. 그대로 따르지 않으면 Unity가 크래시 나거나 변경이 사라진다.

작성 시점 기준 날짜: 2026-06-01

---

## 0. 프로젝트 개요 / 절대 제약

- **프로젝트**: `Unity_LBW_Frozen_Maze` (Frozen Maze 게임), **Unity 6000+ (HDRP)**.
  - 프로젝트 루트: `/Users/mjb/AI-WorkSpace/WorkSpace/Unity_LBW_Frozen_Maze`
  - 주 작업 씬: `Assets/Stage_4.unity`
- **사용자는 한국어로 소통한다.** 답변/문서/커밋도 한국어 기준. 코드 주석도 한국어 스타일을 유지한다.
- **Git 없음.** 되돌릴 안전망(undo)이 전혀 없다. 파괴적 작업(대량 삭제/덮어쓰기) 전에 반드시 한 번 더 생각하고,
  씬/프리팹 저장은 사용자 승인 하에만 한다. (이미 승인된 것: Stage_4 씬, `Assets/Prefabs/Cannonball.prefab`)
- **Unity 제어는 오직 외부 "MCP for Unity" HTTP 서버(`http://127.0.0.1:8080/mcp`)로만 한다.**
  - 도구 목록에 보이는 `mcp__unity-mcp__*` (빌트인 unity-mcp 툴)은 **사용 금지**. 이 프로젝트와 연결된 서버가 아니다.
  - 사용자 메모리(MEMORY.md)에도 동일하게 못박혀 있다.

---

## 1. 운영 규칙 — Unity를 코드로 조작하는 방법 (이게 핵심)

C# 코드를 Unity 에디터 안에서 실행시켜 모든 것을 한다. 절차는 항상 다음 3단계다.

### 1-1. 실행 헬퍼
- `/tmp/mcpcall.py` 가 MCP Streamable-HTTP 클라이언트다. (이미 존재. 없어지면 아래 "헬퍼 사양"으로 재생성)
- 호출 형식:
  ```bash
  # FILE 에서 JSON 인자를 읽어 execute_code 툴 호출 (권장)
  python3 /tmp/mcpcall.py toolf <tool_name> /tmp/args_X.json
  # 인라인 JSON 인자
  python3 /tmp/mcpcall.py tool  <tool_name> '<json>'
  # 사용 가능한 툴/리소스 보기
  python3 /tmp/mcpcall.py tools
  python3 /tmp/mcpcall.py resources
  ```

### 1-2. C# 실행 패턴 (가장 많이 쓰는 것)
사용 툴 이름: `execute_code`. Roslyn이 **메서드 본문(method body)** 으로 컴파일한다 → **`string`을 `return` 해야 결과를 본다.**

```bash
# 1) C# 코드를 파일로 쓴다 (Write 툴 사용; /tmp/code_*.cs)
# 2) 인자 JSON 생성 (safety_checks=False 필수 — 안 그러면 막힌다)
python3 -c 'import json; json.dump({"action":"execute","safety_checks":False,"code":open("/tmp/code_X.cs").read()}, open("/tmp/args_X.json","w"))'
# 3) 실행
python3 /tmp/mcpcall.py toolf execute_code /tmp/args_X.json
```
응답 예: `{"success":true,...,"data":{"result":"...너의 return 문자열...","compiler":"roslyn"}}`

코드 작성 시 주의:
- `System.Text.StringBuilder sb` 에 로그를 쌓고 `return sb.ToString();` 패턴이 편하다.
- **`#if UNITY_EDITOR` 는 execute_code 안에서 스트립된다.** `UnityEditor.*` API를 그냥 직접 호출하면 된다
  (`UnityEditor.EditorApplication`, `UnityEditor.AssetDatabase`, `UnityEditor.SceneManagement.*`, `UnityEditor.LogEntries` 등).
- 반대로 **실제 `.cs` 파일 안에서는 `#if UNITY_6000_0_OR_NEWER` 가 정상 동작한다** (이 프로젝트는 Unity 6000+ 이라
  `rb.linearVelocity` / `rb.linearDamping` 를 쓰고, 구버전 폴백으로 `rb.velocity` / `rb.drag` 를 둔다).
- 변수 이름 충돌 주의: 임시 카메라 변수는 `c` 말고 `cam` 처럼 명확히.

### 1-3. 외부에서 `.cs` 파일을 편집했으면 반드시 Unity에 재컴파일을 시켜라
Edit/Write 툴로 `Assets/**.cs` 를 고쳐도 **Unity는 자동으로 인식하지 않는다.** 플레이 모드 들락거려도 안 된다.
다음을 execute_code로 호출해야 반영된다:
```csharp
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
// 그 다음 EditorApplication.isCompiling 이 false 가 될 때까지 폴링하고,
// 새로 추가한 필드/메서드가 리플렉션으로 보이는지 확인한다.
```
재컴파일 후 검증은 리플렉션으로:
```csharp
var t = System.Type.GetType("Cannonball, Assembly-CSharp");
t.GetField("fragmentPierceLimit");                 // 새 public 필드 확인
t.GetMethod("IsFragment", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
```

### 1-4. 콘솔 에러/경고 개수 확인 (리플렉션)
```csharp
var le = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
int e=0,w=0,l=0; object[] args=new object[]{e,w,l};
le.GetMethod("GetCountsByType").Invoke(null, args);
// args[0]=errors, args[1]=warnings
```
경고 **메시지 본문**을 읽고 싶으면 `LogEntries.StartGettingEntries()` → `GetEntryInternal(i, entry)` (LogEntry의 `mode`,`message` 필드) → `EndGettingEntries()`. (예시는 `/tmp/code_read_warns.cs` 참고)

### 1-5. 씬/프리팹 영속화
```csharp
UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
UnityEditor.AssetDatabase.SaveAssets();
// 프리팹: UnityEditor.PrefabUtility.SavePrefabAsset(prefab);
```
**에디트 모드에서 컴포넌트를 추가했는데 저장 안 하면, 크래시/리로드 시 전부 사라진다.** (실제로 이전에 마커 6개가 날아간 적 있음)

### 1-6. 플레이 모드로 런타임 테스트하기
```csharp
UnityEditor.EditorApplication.isPlaying = true;   // 또는 false
```
- 한 번의 execute_code 호출에서는 현재 프레임만 돈다. **물리는 HTTP 왕복(round-trip) 사이에 실제로 진행된다.**
  → "셋업" 호출과 "결과 읽기" 호출을 분리하면 그 사이에 물리가 흐른다.
- 플레이 모드 진입/종료는 즉시 되지 않는다. `isPlaying = true` 직후 같은 호출에서 읽으면 아직 false다. 다음 호출에서 확인.
- **플레이 모드에서 만든 오브젝트/씬 변경은 플레이 종료 시 전부 복원된다.** (그래서 파괴적 테스트는 플레이 모드에서 하면 안전)
- `GameObject.Find` 는 **활성** 오브젝트만 찾는다. 비활성/전체를 찾으려면
  `Resources.FindObjectsOfTypeAll<GameObject>()` + `o.scene.IsValid()` 로 필터링.

### 1-7. ⚠️ 함정: `Physics.Simulate`(스크립트 시뮬 모드)는 `Time.time` 을 진행시키지 않는다
수동 스텝 시뮬을 쓰면 `Time.time` 이 멈춰서, `armTime` 처럼 `Time.time - spawnTime` 기반 가드가 항상 early-return → "포탄이 관통한다"는 **가짜 양성**이 나온다. 이전에 이걸로 헛디뎠다. 런타임 물리 테스트는 **자동 시뮬(실제 플레이 모드 + 왕복 사이 진행)** 으로 해라.

### 1-8. 오프스크린 렌더로 눈으로 확인하기 (선택)
임시 Camera 생성 → `UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest` → `ReadPixels` → `EncodeToPNG` → `/tmp/*.png` 저장 → Read 툴로 이미지 확인. 다 쓴 임시 오브젝트는 `UnityEngine.Object.DestroyImmediate(..)` 로 정리.

---

## 2. 기능별 함정(Gotchas) — 물리/충돌 관련

- **고속 터널링**: 80m/s (심지어 40m/s)의 포탄은 `CollisionDetectionMode.ContinuousDynamic` CCD만으로는
  정적 non-convex MeshCollider를 안정적으로 못 막는다(뚫고 지나감). → FixedUpdate에서 **예측 SphereCast 스윕**으로 해결.
- **트리거 vs 솔리드**: 보스 데미지는 보스의 **트리거 SphereCollider** → 포탄 `OnTriggerEnter` 로 처리.
  스윕은 `QueryTriggerInteraction.Ignore` 라서 보스 트리거를 스윕에서 제외한다(보스 데미지 경로는 트리거로 유지).
- **퇴화(degenerate) MeshCollider**: `sharedMesh == NULL` 인 MeshCollider는 충돌 형상이 0이라 모든 게 통과하고,
  보스 트리거도 감지 못 한다. 이전에 **타워 4개(Tower, Tower (1), Tower (2), Tower (3))의 MeshCollider가 전부 mesh=NULL**
  이어서 포탄이 통과했었다. → `MeshFilter.sharedMesh` 를 MeshCollider에 할당해서 복구 완료(저장됨).
- **OpenFracture**: `Fracture` 컴포넌트 + `CauseFracture()` + `fragmentRoot`. 런타임 메시 파괴.
  - 파편 오브젝트 이름은 `"Fragment"`(실제로는 `Fragment0`, `Fragment1`... 인덱스가 붙음 → `StartsWith("Fragment")` 로 식별).
  - 파편 루트 오브젝트 이름은 `"<원본이름>Fragments"` (예: `TowerFragments`) → 부모 이름이 `EndsWith("Fragments")`.
  - 파편은 **convex MeshCollider + Rigidbody** 를 가진다(`Assets/OpenFracture/Runtime/Scripts/Fracture.cs` 의 `CreateFragmentTemplate`).
  - 일부 메시(non-manifold 등)에서 "Failed to find final triangle" 워닝과 함께 실패할 수 있어 BossDestruction은 try/catch + 데칼 폴백을 둔다.
- **새 직렬화 필드의 기본값 전파**: 스크립트에 새 `public`/`[SerializeField]` 필드를 추가하면, 기존에 직렬화된
  프리팹/씬 인스턴스는 그 필드에 저장값이 없으므로 **C# 필드 초기값(이니셜라이저)** 을 그대로 갖는다.
  → 이번에 추가한 `fragmentPierceLimit=2`, `towerFragmentCount=90` 은 프리팹/씬을 다시 저장하지 않아도 자동 반영됨(검증함).

---

## 3. 씬/오브젝트 현재 상태 (Stage_4, 디스크에 저장됨)

- **GolemCollapsible 마커가 붙은 7개 구조물** (집과 동일하게 골렘 접촉 시 실제 붕괴):
  `BridgeStart`, `BridgeMiddle`, `BridgeEnd`, `Tower`, `Tower (1)`, `Castle`(중앙 메시, 3741 verts), `CastleTop`.
  - "Castle-center" 라는 이름의 오브젝트는 없다(과거 미저장 리네임 흔적). 실제 대상은 `Medieval_Castle_Set` 아래
    `Castle` 컨테이너의 자식인 **Castle 메시(3741v)** 다.
- **복구된 타워 MeshCollider 4개**: Tower, Tower (1), Tower (2), Tower (3) — 각 MeshFilter의 mesh(668v)를 MeshCollider에 할당.
- `Assets/Prefabs/Cannonball.prefab`: `SphereCollider.isTrigger=false`(솔리드), Rigidbody `useGravity=true` + `ContinuousDynamic` + Interpolate,
  `Cannonball.sweepMask=-1`, `hitMask=-1`, **`fragmentPierceLimit=2`(신규, 기본값 자동 반영)**.

---

## 4. 이번 세션에서 한 작업 (사용자 요청)

### 사용자 요청 원문(한국어)
> "타워파편이 너무 커서 포탄이 못뚫고나가서 보스를 못맞추는 일이 생겨, 파편의경우 포탄이 일정개수 (2개) 는
> 뚫고갈수있게 변경, 그리고 파편또한 지금보다 좀더 (타워만) 자잘하게 부서지게 변경해줘. 여기서 말하는 타워는
> 방금 붕괴효과 적용한 내부타워들만이지 외곽의 대포가있는쪽을 말하는게 아니야."

요구 분해:
1. **포탄이 붕괴 파편을 일정 개수(2개)까지 뚫고 지나갈 수 있게** (그래야 파편에 막히지 않고 보스를 맞춤).
2. **타워 파편만 지금보다 더 잘게 부서지게**. 단, 여기서 "타워"는 **GolemCollapsible 마커가 붙은 내부 타워**
   (Tower, Tower (1))만이고, **외곽의 대포가 있는 쪽 타워는 제외**.
3. (이전 세션부터 유지되는 제약) 진짜 물리엔진 수준으로 과하게 올리지 말고 "그럴듯한" 연출만.

### 4-1. 포탄 파편 관통 — `Assets/Scripts/Cannon/Cannonball.cs`
변경 내용:
- `using System.Collections.Generic;` 추가.
- 새 public 필드: `public int fragmentPierceLimit = 2;` (인스펙터에서 조절 가능).
- 새 private 상태: `int fragmentsPierced;` + `readonly HashSet<Collider> piercedFragments = new HashSet<Collider>();`
  (포탄 1발의 생애 동안 관통한 파편 누적 카운트 + 중복 처리 방지 집합).
- **FixedUpdate 스윕을 단일 `Physics.SphereCast` → `Physics.SphereCastAll` 로 변경**:
  진행선상의 모든 충돌을 거리순 정렬해서 순회한다. (단일 SphereCast는 가장 가까운 1개만 보고 멈추므로 파편 군집을
  뚫고 그 뒤의 보스를 못 본다. 그게 막힘의 핵심 원인이었다.)
  - 순회 우선순위: ① 데미지 대상(`IDamageable`, 보스) → 즉시 명중/데미지. ② 파편(`IsFragment`) → 한도까지 통과.
    ③ 일반 구조물/지형 → 표면에 박힘.
  - 파편이 한도 내면 `PierceFragment(...)` 호출 후 `continue` (같은 스윕의 더 뒤쪽 충돌 계속 확인). 한도 초과면 그 파편에 박힘.
- **`IsFragment(Collider)`** (static): `transform.name.StartsWith("Fragment")` 또는 부모 이름 `EndsWith("Fragments")`.
  (OpenFracture 실제 출력으로 검증함: `Fragment0` → true, 일반 벽 `BridgeMiddle` → false.)
- **`PierceFragment(...)`**: 집합에 추가 + 카운트++ + `Physics.IgnoreCollision(sphere, frag, true)`(물리적으로도 통과) +
  파편이 non-kinematic이면 진행방향으로 살짝 밀어내는 연출(`AddForce(dir*speed*0.3f, VelocityChange)`).
- **`EmbedAt(RaycastHit, dir)`**: 표면 법선/지점 계산 후 살짝 파고든 위치로 스냅 + `BeginEmbed()` (기존 박힘/낙하 연출 재사용).
- **`OnCollisionEnter`(백업 경로)에도 동일한 파편 관통 로직 추가**: 스윕이 놓친 경우, 파편이면 한도까지 통과, 아니면 박힘.

관통 동작 요약: 포탄은 파편을 **fragmentPierceLimit(2)개까지 뚫고** 지나가고, 그 뒤의 보스(IDamageable)는 데미지,
일반 구조물은 박힘. 한도(2개) 초과 후 만나는 파편에는 박힌다.

### 4-2. 타워 파편 세분화 — `Assets/Scripts/BossDestruction.cs`
변경 내용:
- 새 필드: `[SerializeField] [Range(5, 200)] private int towerFragmentCount = 90;` (기존 `fragmentCount = 40` 와 별개).
- `FractureObject(obj)` 안에서 파편 개수를 분기:
  ```csharp
  bool isCollapsibleTower = obj.name.ToLower().Contains("tower")
                            && obj.GetComponentInParent<GolemCollapsible>() != null;
  int countToUse = isCollapsibleTower ? towerFragmentCount : fragmentCount;
  // FractureOptions.fragmentCount = countToUse;
  ```
- 즉 **GolemCollapsible 마커가 있고 이름에 "tower"가 포함된 내부 타워만** 90조각으로 더 잘게 부순다.
  - 외곽 대포 타워: 마커가 없다. 게다가 `IsStaticCrackObject`에서 (마커 없음 + "tower" + Medieval_Castle_Set 소속) → true →
    fracture가 아니라 **데칼** 경로로 가므로 애초에 fracture 자체가 안 된다. 이중으로 안전.
  - 다리(BridgeStart 등)·성(Castle): 이름에 "tower"가 없으므로 기존 `fragmentCount = 40` 유지.

---

## 5. 검증 결과 (전부 통과)

1. **컴파일**: 에러 0. 내가 수정한 두 파일에서 나온 경고 0. (남은 워닝은 전부 기존 에셋의 obsolete API:
   AN Door Pack, CannonController의 `FindObjectOfType`, MountainPalace, Sun_Temple, BossFollow/BossCombat 미사용 필드.)
   리플렉션으로 새 멤버 존재 확인: `fragmentPierceLimit`, `IsFragment`, `PierceFragment`, `towerFragmentCount` 모두 OK.
2. **파편 개수 (플레이 모드, BossDestruction.FractureObject 리플렉션 호출)**:
   - `Tower` → 파편 **90**개 (towerFragmentCount 적용 확인). `hasMarker=True`.
   - `BridgeStart` → 파편 **40**개 (다리는 기존값 유지 확인).
   - `IsFragment(실제 Fragment0)` = **True**, `IsFragment(BridgeMiddle 벽)` = **False**.
3. **관통 동작 (플레이 모드, 합성 테스트)**: x=2, x=3.2에 kinematic 파편 2개 + x=6에 일반 벽 배치, 포탄을 +X 30m/s로 발사.
   - 결과: `ball.x = 5.55`(파편 2개를 통과해 벽에 박힘), `fragmentsPierced = 2`, `consumed = True`, `isKinematic = True`(박힘 단계).
   - 의미: 예전엔 첫 파편(x=2)에서 멈췄을 것 → 이제 2개 통과 후 그 뒤 구조물(보스 위치)에 도달.
4. **에디트 모드 최종 점검**: `isPlaying=False`, `isCompiling=False`, `activeScene dirty=False`,
   `prefab fragmentPierceLimit=2 / isTrigger=False / sweepMask=-1`, `scene BossDestruction towerFragmentCount=90 / fragmentCount=40`,
   `console errors=0`.

> 모든 파괴적 테스트는 **플레이 모드**에서 했고 종료 시 복원됐다. 에디트 모드 씬은 변경 없음(dirty=False).
> 코드 변경은 디스크에 저장 + 컴파일 완료 상태. 새 필드 기본값은 자동 반영되므로 씬/프리팹 추가 저장은 불필요했다.

---

## 6. 조절 가능한 파라미터 (사용자가 더 키우거나 줄이고 싶어 할 수 있음)

- **포탄 관통 개수**: `Cannonball.fragmentPierceLimit` (프리팹/인스펙터, 기본 2). 코드: `Cannonball.cs:40`.
- **타워 파편 잘게 정도**: `BossDestruction.towerFragmentCount` (씬 BossGolem 인스펙터, 기본 90, Range 5~200). 코드: `BossDestruction.cs:13`.
  - 더 잘게 = 값 ↑ (단 fracture 연산이 무거워짐; 90에서도 OpenFracture가 무난히 처리됨. 이전 40은 34~107ms 수준).
- 기타 포탄 연출: `stickDuration`(박힘 유지 0.45s), `slowFallDrag`(낙하 저항 4), `embedDepth`(박힘 깊이 0.1),
  `afterHitLife`(사라지기까지 3.5s), `armTime`(발사 직후 자기충돌 무시 0.06s), `lifeTime`(8s).

---

## 7. 내부 태스크 목록 상태 (TaskList)

- #1~#18: 이전 세션들의 작업, 전부 completed.
- #19 "포탄 솔리드 충돌 + 박힘/낙하 물리": completed.
- #20 "다리/타워/성중앙 골렘 붕괴 적용": completed.
- #21 "포탄 파편 관통(2개) 처리": **completed (이번 세션)**.
- #22 "타워 파편 더 잘게 부서지게": **completed (이번 세션)**.

현재 미해결 사용자 요청 없음.

---

## 8. 잠재적 후속/주의 사항 (요청 시에만 손대라)

- 파편이 90개로 늘어나면서 포탄 경로에 작은 콜라이더가 더 많아졌다. 관통 한도는 2이므로 밀집 잔해에서는 3번째
  파편에 박힐 수 있다(설계 의도). 사용자가 "더 잘 뚫리게" 원하면 `fragmentPierceLimit`를 올리면 된다.
- `IsFragment` 식별은 OpenFracture의 이름 규칙(`"Fragment*"`, 부모 `"*Fragments"`)에 의존한다. OpenFracture를 교체하거나
  이름 규칙이 바뀌면 이 식별이 깨진다. 더 견고하게 하려면 파편 생성 시 마커 컴포넌트를 붙이는 방법도 있다(현재는 미적용).
- 외곽 대포 타워가 혹시 fracture되길 원한다면 GolemCollapsible 마커를 붙이고 `IsStaticCrackObject` 로직을 재검토해야 한다(현재는 데칼 경로).
- 씬에 새 컴포넌트를 **에디트 모드에서** 추가하면 반드시 저장해라(§1-5). 안 그러면 크래시 때 날아간다(실제 발생 이력 있음).

---

## 9. 변경된 파일 목록 (이번 세션)

- `Assets/Scripts/Cannon/Cannonball.cs` — 파편 관통 로직 추가(SphereCastAll 스윕, IsFragment/PierceFragment/EmbedAt, OnCollisionEnter 백업).
- `Assets/Scripts/BossDestruction.cs` — `towerFragmentCount` 필드 + 타워 한정 세분화 분기.
- (참고, 이전 세션) `Assets/Scripts/GolemCollapsible.cs` — 빈 마커 컴포넌트(`public class GolemCollapsible : MonoBehaviour { }`).

---

## 10. /tmp 에 남아있는 유용한 코드 스크립트 (재사용/참고)

- `/tmp/code_recompile.cs` — AssetDatabase.Refresh + RequestScriptCompilation.
- `/tmp/code_verify_compile2.cs` — 컴파일/콘솔/새 멤버 리플렉션 검증.
- `/tmp/code_read_warns.cs` — 콘솔 경고 메시지 본문 덤프(LogEntries 리플렉션).
- `/tmp/code_enter_play_v2.cs`, `/tmp/code_exit_play_v2.cs` — 플레이 모드 진입/종료.
- `/tmp/code_test_frag.cs` — 타워/다리 fracture 후 파편 개수 + IsFragment 검증(플레이 모드).
- `/tmp/code_pierce_setup.cs`, `/tmp/code_pierce_read.cs` — 합성 파편 관통 테스트 셋업/결과 읽기.
- `/tmp/code_final_check2.cs` — 에디트 모드 최종 상태 점검(프리팹/씬 필드값, dirty, 콘솔).
- `/tmp/mcpcall.py` — MCP 클라이언트(§1-1). 없으면 docstring 사양대로 재생성.

> 주의: `/tmp/code_*.cs` 파일을 Write 툴로 다시 만들 때, 동일 경로가 이미 있으면 Write가 "read first" 에러를 낸다.
> 새 파일명을 쓰거나(예: `_v2`) 먼저 Read 하면 된다.
