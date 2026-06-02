using UnityEngine;

/// <summary>
/// 대포 근처 트리거. 플레이어가 범위 안에서 F 를 누르면 탑승시킨다.
/// 하차(F/Esc)는 CannonController 가 담당한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CannonMountTrigger : MonoBehaviour
{
    public CannonController cannon;
    public string playerTag = "Player";
    public GameObject promptUI;         // 선택: "F로 탑승" 안내

    bool playerInRange;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (promptUI != null) promptUI.SetActive(false);
    }

    void Update()
    {
        bool show = playerInRange && cannon != null && !cannon.isMounted;
        if (promptUI != null && promptUI.activeSelf != show) promptUI.SetActive(show);

        if (show && Input.GetKeyDown(KeyCode.F))
            cannon.Mount();
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other)) playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) playerInRange = false;
    }

    bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag) || other.GetComponentInParent<MovementInput>() != null;
    }
}
