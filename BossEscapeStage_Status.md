# 도주형 보스 스테이지 작업 현황 (BossEscapeStage_Status)

## 1. 완료된 작업
- **성 외곽 도주로 구축**: `Wall_3M` 프리팹을 사용하여 바닥과 성벽이 있는 흉벽(Rampart) 도주로 생성 (`Assets/Scenes/BossEscapeStage_Final.unity`).
- **보스 실시간 추격**: `BossFollow`와 `NavMeshAgent`를 통해 보스가 플레이어를 추격하며, 속도 밸런싱 완료.
- **실시간 맵 파괴**: 보스가 밟거나 지나가는 바닥이 물리적으로 낙하하며 파괴되는 `CrumblingSegment` 시스템 구현. 파괴 시 `RockDebris` 파편 생성 추가.
- **WallMerge 통합**: `WallMerge_Scene`의 원본 `ProjectorController` 및 `Frame` 시스템을 이식하여 도주 중 벽 합체 기능 완벽 지원.
- **게임 루프**:
    - **승리**: 도주로 끝 `WinTrigger` 도달 시 "ESCAPE SUCCESS" 출력.
    - **패배**: 보스에게 잡히거나 추락 시 "CAUGHT BY BOSS" 출력.
    - **UI**: `WinLossController`와 TMP 기반 결과 화면 구성.
- **시각 효과**: HDRP용 스톤 머티리얼 적용 및 Magenta(재질 유실) 문제 해결.

## 2. 주요 에셋 위치
- **Scene**: `Assets/Scenes/BossEscapeStage_Final.unity`
- **Scripts**: `Assets/Scripts/` 폴더 내 `CrumblingSegment.cs`, `WinLossController.cs`, `WallMerge.cs` 등.
- **Prefabs**: `Assets/Prefabs/RockDebris.prefab` (파괴 파편).

## 3. 조작 방법
- **이동**: WASD
- **벽 합체**: 벽 근처에서 `Space` (WallMerge)
- **도주 시작**: 스테이지 시작 지점의 트리거 진입 시 보스가 깨어나며 시작.

---
*모든 핵심 기능이 구현되었으며, `BossEscapeStage_Final` 씬을 실행하여 테스트할 수 있습니다.*
