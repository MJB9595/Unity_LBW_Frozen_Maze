using UnityEngine;

/// <summary>
/// 포탄 명중 시 보스 피격 모션을 재생한다(Animator "Hit" 트리거).
/// 선택적으로 DOTween 펀치 연출을 추가한다.
/// </summary>
public class BossHitReaction : MonoBehaviour
{
    public Animator animator;
    public string hitTrigger = "Hit";
    public float minInterval = 0.25f;   // 피격모션 연타 방지
    public Health health;               // 사망 시 피격모션 생략

    float lastHit = -999f;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<Health>();
    }

    public void PlayHit()
    {
        if (health != null && health.CurrentHealth <= 0f) return;
        if (Time.time - lastHit < minInterval) return;
        lastHit = Time.time;

        if (animator != null) animator.SetTrigger(hitTrigger);
    }
}
