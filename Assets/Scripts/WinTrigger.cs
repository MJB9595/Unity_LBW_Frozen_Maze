using UnityEngine;

public class WinTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (WinLossController.Instance != null)
                WinLossController.Instance.Win();
        }
    }
}
