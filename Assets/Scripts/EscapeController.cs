using UnityEngine;
using Unity.Cinemachine;

public class EscapeController : MonoBehaviour
{
    public BossFollow boss;
    public GameObject player;
    public CinemachineCamera wakeUpCam;
    public float wakeUpDuration = 2f;

    private bool isStarted = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!isStarted && other.CompareTag("Player"))
        {
            StartCoroutine(WakeUpSequence());
        }
    }

    private System.Collections.IEnumerator WakeUpSequence()
    {
        isStarted = true;
        
        // Pause player or just show camera
        if (wakeUpCam != null) wakeUpCam.Priority = 20;
        
        // Play boss animation if exists
        Animator bossAnim = boss.GetComponent<Animator>();
        if (bossAnim != null) bossAnim.SetTrigger("WakeUp"); // Assuming there's a trigger

        yield return new WaitForSeconds(wakeUpDuration);

        if (wakeUpCam != null) wakeUpCam.Priority = 0;
        
        boss.enabled = true;
        Debug.Log("Boss Awakened! Run!");
    }
}
