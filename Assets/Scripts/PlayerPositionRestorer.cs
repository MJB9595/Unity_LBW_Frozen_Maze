using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPositionRestorer : MonoBehaviour
{
    void Start()
    {
        // 돌아왔을 때, 이전에 저장된 위치가 있는지 확인
        if (GameProgress.hasSavedPosition)
        {
            CharacterController controller = GetComponent<CharacterController>();
            
            // Unity의 CharacterController는 활성화 상태에서 강제로 transform.position을 옮기면
            // 물리 연산 충돌로 인해 위치가 무시될 수 있습니다.
            // 따라서 잠시 끄고 이동시킨 후 다시 켭니다.
            controller.enabled = false;
            transform.position = GameProgress.lastPlayerPosition;
            controller.enabled = true;
            
            Debug.Log("이전 위치(" + GameProgress.lastPlayerPosition + ")로 플레이어 복구 완료!");
            
            // 한 번 복구했으면 다시 씬을 로드할 때를 대비해 초기화 (선택사항)
            // GameProgress.hasSavedPosition = false; 
        }
    }
}
