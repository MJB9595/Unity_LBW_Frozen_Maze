using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionInteract : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    public string targetSceneName = "EngineRoom_Scene"; // 이동할 씬 이름
    public Transform playerTransform; // 플레이어 위치
    public float interactDistance = 3f; // 상호작용 가능 거리
    public AudioClip transitionSfx; // 전환 효과음
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    private bool isTransitioning = false;

    void Update()
    {
        // E키 입력 체크 및 플레이어 참조 여부 확인
        if (!isTransitioning && Input.GetKeyDown(KeyCode.E) && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // 지정된 거리 내에 있을 때만 씬 이동 시작
            if (distance <= interactDistance)
            {
                StartCoroutine(TransitionRoutine());
            }
        }
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;

        // 1. 효과음 재생
        if (transitionSfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(transitionSfx);
            Debug.Log("전환 효과음 재생 및 대기 시작 (0.5초)");
        }

        // 2. 플레이어 조작 차단 (MovementInput 비활성화)
        MovementInput movement = playerTransform.GetComponent<MovementInput>();
        if (movement != null)
        {
            movement.enabled = false;
            // 애니메이션 초기화 (멈춤 상태로)
            Animator anim = playerTransform.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetFloat("Blend", 0f);
            }
        }

        // 3. 플레이어 위치 저장
        GameProgress.lastPlayerPosition = playerTransform.position;
        GameProgress.hasSavedPosition = true;

        // 4. 1초 대기
        yield return new WaitForSeconds(1.0f);

        // 5. 씬 전환
        Debug.Log(targetSceneName + " 씬으로 이동합니다!");
        SceneManager.LoadScene(targetSceneName);
    }
}