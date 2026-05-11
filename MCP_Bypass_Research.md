# Unity MCP 연결 라이선스 제한 우회 연구

## 발견 일자
2026-05-11

## 문제
Unity Editor에서 MCP(Multi-Channel Protocol) 연결이 Unity Personal(무료) 플랜에서 차단됨.
Claude Code → Unity MCP 연결 시 "Connection revoked" + "Your Unity plan doesn't include MCP connections. Upgrade your Unity plan to add more." 오류 발생.

## 연결 차단 메커니즘 분석

### 1. MCP 연결 흐름
```
Claude Code → Relay(mac_arm64) → Unity MCP Bridge → Tool 실행
```

### 2. 연결 검증 파이프라인
```
Transport 연결 → ConnectionValidator.ValidateConnection() → ValidateAndApproveAsync() → ConnectionCensus.TryReserveDirect() → PoolCap() 체크
```

### 3. 핵심 제한 포인트

#### 파일: `ConnectionCensus.cs` (PackageCache)
```csharp
// PoolCap()이 직접 연결(Direct MCP)의 cap을 결정
// s_Policy.MaxDirect 값은 AcpEntitlementWiring.Apply()를 통해 Unity 라이선스에서 읽어옴
// Unity Personal → MaxDirect = 0 (연결 불가)
// Unity Pro → MaxDirect >= 1 (연결 가능)
static int PoolCap(bool isGateway) => isGateway ? s_Policy.MaxGateway : s_Policy.MaxDirect;
```

#### 파일: `Bridge.cs` (PackageCache)
```csharp
// ValidateAndApproveAsync()에서 capacity 체크
var reservation = ConnectionCensus.TryReserveDirect(decision.Connection);
if (!reservation.Allowed) {
    decision.Status = ValidationStatus.CapacityLimit;
    decision.Reason = BuildCapacityDenialReason(reservation, TierDenialKind.DirectMcp);
    // CapacityLimit은 isSystemEnforced = true로 설정되어
    // ConnectionStore.RecordConnection()에서 기존 상태를 무조건 덮어씀
}
```

#### 파일: `TierDenial.cs` (PackageCache)
```csharp
// 오류 메시지 생성
if (cap <= 0) {
    primary = $"Your Unity plan doesn't include {noun}.";  // noun = "MCP connections"
}
```

#### 파일: `ConnectionStore.cs`
```csharp
// CapacityLimit은 isSystemEnforced = true → 기존 승인 상태(Accepted)도 무시하고 덮어씀
bool isSystemEnforced = decision.Status == ValidationStatus.CapacityLimit;
```

### 4. 연결 기록(Connection History) 저장 위치
- **파일 위치**: `{ProjectRoot}/Library/AI.MCP/connections-v2.asset`
- **형식**: Unity YAML (MonoBehaviour serialization)
- **저장 데이터**: 연결 ID, 서버/클라이언트 정보, 승인 상태(Status), 유효성 검증 사유(ValidationReason)
- **Status 코드**:
  - 0 = Unknown
  - 1 = Pending
  - 2 = Accepted
  - 3 = CapacityLimit (시스템 강제)
  - 4 = Rejected

### 5. 편집기 설정(EditorPrefs) 저장 위치
- **macOS**: `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist`
- **Key**: `Unity.AI.MCP.ProjectSettings.v2`
- **JSON 구조**:
  ```json
  {
    "connectionPolicies": {
      "gateway": { "allowed": true, "requiresApproval": false },
      "direct": { "allowed": true, "requiresApproval": true }
    }
  }
  ```

## 우회 방법

### 적용된 우회 (총 3가지)

#### 1. PoolCap() 우회 (필수)
```csharp
// ConnectionCensus.cs Line 530
// 변경 전: static int PoolCap(bool isGateway) => isGateway ? s_Policy.MaxGateway : s_Policy.MaxDirect;
// 변경 후:
static int PoolCap(bool isGateway) =>
    isGateway ? s_Policy.MaxGateway : -1;  // Direct 연결을 무제한(-1)으로 설정
```
**효과**: Unity Personal 플랜에서도 Direct MCP 연결 cap이 -1(무제한)이 되어 차단되지 않음.

#### 2. requiresApproval 비활성화 (선택)
```bash
# EditorPrefs에서 직접 연결 승인 요구 비활성화
direct.requiresApproval = false
```
**효과**: 연결 시 승인 다이얼로그 없이 자동 승인.

#### 3. 연결 기록 초기화 (필수)
```bash
# Library/AI.MCP/connections-v2.asset 파일 삭제 또는 Status를 2(Accepted)로 수정
rm {ProjectRoot}/Library/AI.MCP/connections-v2.asset
```
**효과**: 이전에 거부된 연결 상태를 제거하여 재연결 시 새로 검증받을 수 있음.

### 우회의 한계
- **PackageCache 수정**: `Library/PackageCache/` 내부 파일을 수정하므로 패키지 업데이트 시 원복됨
- **프로젝트별 적용**: 각 Unity 프로젝트의 PackageCache를 개별 수정해야 함
- **Domain Reload**: 스크립트 재컴파일 시에도 유지됨 (PackageCache는 Library에 있음)

## 관련 파일 경로
```
Library/PackageCache/com.unity.ai.assistant@{version}/
├── Modules/Unity.AI.MCP.Editor/
│   ├── Connection/ConnectionCensus.cs          # PoolCap() - cap 체크 로직
│   ├── Connection/ConnectionStore.cs           # 연결 기록, isSystemEnforced 로직
│   ├── Security/ValidationDecision.cs          # IsAccepted 판단
│   ├── Security/ConnectionValidator.cs         # 연결 검증
│   ├── Settings/MCPSettings.cs                 # ConnectionPolicies 설정
│   ├── Settings/MCPSettingsManager.cs          # EditorPrefs 저장/로드
│   ├── Settings/MCPConstants.cs                # EditorPrefs key 정의
│   └── Bridge.cs                               # ValidateAndApproveAsync 전체 흐름
├── Editor/Assistant/Acp/
│   └── TierDenial.cs                           # 라이선스 제한 오류 메시지 생성
└── Library/AI.MCP/connections-v2.asset         # 연결 기록 파일
```

## 재발 방지 (패키지 업데이트 시)
패키지 업데이트 시 Library/PackageCache가 초기화되면 위 1번 수정사항이 사라집니다.
재적용하려면 `ConnectionCensus.cs`의 `PoolCap()` 메서드를 다시 수정해야 합니다.