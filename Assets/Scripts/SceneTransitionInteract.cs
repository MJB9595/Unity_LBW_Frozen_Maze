using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionInteract : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    public string targetSceneName = "EngineRoom_Scene"; // 이동할 씬 이름
    public Transform playerTransform; // 플레이어 위치
    public float interactDistance = 3f; // 상호작용 가능 거리

    void Update()
    {
        // E키 입력 체크 및 플레이어 참조 여부 확인
        if (Input.GetKeyDown(KeyCode.E) && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // 지정된 거리 내에 있을 때만 씬 이동
            if (distance <= interactDistance)
            {
                TransitionToScene();
            }
        }
    }

    private void TransitionToScene()
    {
        // 씬 전환 전에 플레이어 위치 기억하기
        if (playerTransform != null)
        {
            GameProgress.lastPlayerPosition = playerTransform.position;
            GameProgress.hasSavedPosition = true;
        }

        Debug.Log(targetSceneName + " 씬으로 이동합니다!");
        SceneManager.LoadScene(targetSceneName);
    }
}