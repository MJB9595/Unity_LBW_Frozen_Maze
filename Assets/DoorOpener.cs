using UnityEngine;

public class DoorOpener : MonoBehaviour
{
    [Header("Door Target")]
    public Transform doorTransform;

    [Header("Settings")]
    public float openAngle = 90f; // 열리는 각도
    public float speed = 5f;      // 문 속도

    [Header("Interaction Settings")]
    public int enginePower = 0; // 0이면 작동안함, 1이면 작동
    public Transform playerTransform; // 플레이어 위치 (인스펙터에서 할당)
    public float interactDistance = 3f; // 상호작용 가능한 최대 거리

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        // 대상이 없으면 자기 자신으로 설정
        if (doorTransform == null) doorTransform = transform;

        closedRotation = doorTransform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
    }

    void Update()
    {
        // 1. E키 입력 및 거리 체크 (상호작용)
        if (Input.GetKeyDown(KeyCode.E) && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            // 거리가 충분히 가까울 때만 상호작용 실행
            if (distance <= interactDistance)
            {
                TryInteract();
            }
        }

        // 2. 문 회전 애니메이션 (매 프레임 부드럽게 회전)
        Quaternion target = isOpen ? openRotation : closedRotation;
        doorTransform.localRotation = Quaternion.Slerp(doorTransform.localRotation, target, Time.deltaTime * speed);
    }

    // 상호작용 실제 동작 로직
    private void TryInteract()
    {
        // 전역 상태가 클리어(엔진 고침) 상태라면 자동으로 enginePower를 1로 변경
        if (GameProgress.isEngineFixed)
        {
            enginePower = 1;
        }

        if (enginePower == 1)
        {
            isOpen = !isOpen; // 문 상태 반전 (열림 <-> 닫힘)
            Debug.Log(isOpen ? "문 열림!" : "문 닫힘!");
        }
        else
        {
            Debug.Log("엔진을 작동시켜야한다");
        }
    }
}