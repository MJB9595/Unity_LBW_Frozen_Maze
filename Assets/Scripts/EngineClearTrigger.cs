using UnityEngine;
using UnityEngine.SceneManagement;

public class EngineClearTrigger : MonoBehaviour
{
    [Header("Return Settings")]
    public string returnSceneName = "Stage_1"; // 돌아갈 씬 이름

    [Header("Interaction Settings (E키 조작용)")]
    public Transform playerTransform; // 플레이어 위치 (인스펙터에서 할당)
    public float interactDistance = 3f; // 상호작용 가능 거리

    void Update()
    {
        // E키 입력 체크 및 플레이어 참조 여부 확인
        if (Input.GetKeyDown(KeyCode.E) && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            // 지정된 거리 내에 있을 때만 클리어 실행
            if (distance <= interactDistance)
            {
                CompleteAndReturn();
            }
        }
    }

    // 이 함수를 인스펙터의 버튼 OnClick 이벤트 등에 연결하거나, 
    // 위 Update() 문처럼 E키를 통해 직접 호출되도록 합니다.
    public void CompleteAndReturn()
    {
        Debug.Log("엔진 가동 성공! 원래 씬으로 돌아갑니다.");
        
        // 전역 상태 업데이트
        GameProgress.isEngineFixed = true;
        
        // 원래 씬으로 복귀
        SceneManager.LoadScene(returnSceneName);
    }
}
