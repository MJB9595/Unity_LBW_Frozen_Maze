using UnityEngine;

public class BossCrumbleTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Object destruction logic removed as requested

        if (other.CompareTag("Player"))
        {
            if (WinLossController.Instance != null)
                WinLossController.Instance.Loss();
        }
    }
}
