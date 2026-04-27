using UnityEngine;
using UnityEngine.SceneManagement;

public class StageTransitionTrigger : MonoBehaviour
{
    [Header("Transition Settings")]
    public string nextStageName = "Stage_2"; // 넘어갈 다음 스테이지 이름

    [Header("Player Settings")]
    public string playerTag = "Player"; // 플레이어를 식별할 태그 (Jammo_Player의 태그가 'Player'인지 확인해주세요)

    // 플레이어가 트리거 영역(Collider)에 닿는 순간 호출됩니다.
    // 이 컴포넌트가 있는 빈 오브젝트의 Collider 설정에서 'Is Trigger'가 체크되어 있어야 합니다.
    void OnTriggerEnter(Collider other)
    {
        // 닿은 오브젝트가 플레이어인지 태그로 확인합니다.
        if (other.CompareTag(playerTag))
        {
            Debug.Log("플레이어가 다음 스테이지 구역에 도달했습니다! " + nextStageName + " 씬을 로드합니다.");
            
            // 다음 씬으로 전환
            SceneManager.LoadScene(nextStageName);
        }
    }
}
