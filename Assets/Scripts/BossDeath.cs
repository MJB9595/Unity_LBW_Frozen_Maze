using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 보스 사망 처리: Health.OnDeath 를 받아 모든 행동(이동/공격/파괴)을 정지하고
/// 사망 모션을 재생한다. Golem_Controller 에는 전용 Die 클립이 없으므로
/// 코드로 "쓰러짐 + 가라앉음" 연출을 만든다.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossDeath : MonoBehaviour
{
    [Header("연결(자동 탐색)")]
    public Health health;
    public Animator animator;

    [Header("사망 모션")]
    public float toppleAngle = 88f;      // 뒤로 쓰러지는 각도
    public float toppleDuration = 1.4f;  // 쓰러지는 시간
    public float holdAfterTopple = 2.0f; // 쓰러진 뒤 멈춰있는 시간
    public float sinkDepth = 8f;         // 땅속으로 가라앉는 깊이
    public float sinkDuration = 3f;      // 가라앉는 시간
    public bool deactivateWhenDone = true;

    bool dead;

    void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void OnEnable()  { if (health != null) health.OnDeath += HandleDeath; }
    void OnDisable() { if (health != null) health.OnDeath -= HandleDeath; }

    void HandleDeath()
    {
        if (dead) return;
        dead = true;

        // 1) 모든 행동 정지
        var follow = GetComponent<BossFollow>();      if (follow != null) follow.enabled = false;
        var combat = GetComponent<BossCombat>();      if (combat != null) combat.enabled = false;
        var destr  = GetComponent<BossDestruction>(); if (destr  != null) destr.enabled  = false;
        foreach (var hb in GetComponentsInChildren<BossAttackHitbox>()) hb.Deactivate();

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            agent.enabled = false; // 쓰러짐/가라앉음 동안 위치 고정 해제
        }

        // 이동 애니메이션 정지
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("MotionAmount", 0f);
            animator.SetFloat("AnimationSpeed", 1f);
        }

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        // 뒤로 쓰러지기
        Quaternion start = transform.rotation;
        Quaternion end = Quaternion.AngleAxis(toppleAngle, transform.right) * start;
        float t = 0f;
        while (t < toppleDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / toppleDuration);
            transform.rotation = Quaternion.Slerp(start, end, k);
            yield return null;
        }
        transform.rotation = end;

        // 쓰러진 포즈 고정
        if (animator != null) animator.speed = 0f;

        yield return new WaitForSeconds(holdAfterTopple);

        // 땅속으로 가라앉으며 사라지기
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.down * sinkDepth;
        t = 0f;
        while (t < sinkDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, t / sinkDuration);
            yield return null;
        }

        if (deactivateWhenDone) gameObject.SetActive(false);
    }
}
