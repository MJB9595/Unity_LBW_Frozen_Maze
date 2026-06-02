using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 물리 포물선 포탄.
/// - 보스(콜라이더가 isTrigger)는 OnTriggerEnter 로 명중 감지 → IDamageable.TakeDamage + 피격모션.
/// - 그 외 모든 솔리드 구조물/지형은 표면에 박혔다가 천천히 떨어진 뒤 사라진다.
///   (진짜 파괴 시뮬레이션이 아니라 "그럴듯한" 연출)
///
/// 고속 포탄은 ContinuousDynamic 만으로는 정적 MeshCollider 를 관통(터널링)할 수 있어,
/// FixedUpdate 마다 직전 위치 → 현재 위치 구간을 SphereCast 로 스윕 검사해서
/// 벽을 뚫기 직전에 표면에 박아준다. (가벼운 연출용 — 과한 사양 X)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Cannonball : MonoBehaviour
{
    public float damage = 25f;
    public float lifeTime = 8f;
    public GameObject hitVfxPrefab;     // 선택
    public LayerMask hitMask = ~0;      // 명중(데미지) 허용 레이어

    [Header("구조물 충돌(그럴듯한 물리)")]
    [Tooltip("충돌 후 표면에 박혀 멈춰있는 시간")]
    public float stickDuration = 0.45f;
    [Tooltip("박힌 뒤 천천히 떨어질 때의 저항(클수록 느리게 낙하)")]
    public float slowFallDrag = 4f;
    [Tooltip("표면 안쪽으로 살짝 박히는 깊이")]
    public float embedDepth = 0.1f;
    [Tooltip("충돌 후 사라지기까지 시간")]
    public float afterHitLife = 3.5f;
    [Tooltip("발사 직후 자기/대포 충돌을 무시하는 시간")]
    public float armTime = 0.06f;
    [Tooltip("고속 관통 방지 스윕이 검사할 레이어(구조물/지형). 보스 트리거는 스윕에서 제외된다)")]
    public LayerMask sweepMask = ~0;

    [Header("붕괴 파편 관통")]
    [Tooltip("포탄이 뚫고 지나갈 수 있는 붕괴 파편 개수. 이 수를 넘으면 파편에 박힌다.")]
    public int fragmentPierceLimit = 2;

    bool consumed;
    float spawnTime;
    Rigidbody rb;
    SphereCollider sphere;
    float worldRadius;

    int fragmentsPierced;
    readonly HashSet<Collider> piercedFragments = new HashSet<Collider>();

    void Reset()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = false; // 솔리드 → 구조물과 물리 충돌
        var r = GetComponent<Rigidbody>();
        r.useGravity = true;
        r.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();
        spawnTime = Time.time;
        Vector3 s = transform.lossyScale;
        worldRadius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        if (worldRadius < 0.01f) worldRadius = 0.2f;
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    // 고속 관통 방지(예측 스윕): 이번 물리 스텝에 이동할 거리만큼 진행방향으로 미리 쏴서
    // 벽을 뚫기 전에 표면에 박아준다. (물리 이동 전 FixedUpdate 에서 처리)
    // 붕괴 파편(Fragment)은 fragmentPierceLimit 개까지 뚫고 지나가며, 그 뒤 보스/구조물을 만나면 처리한다.
    void FixedUpdate()
    {
        if (consumed || rb.isKinematic) return;
        if (Time.time - spawnTime < armTime) return;

        Vector3 vel = GetVel();
        float speed = vel.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = vel / speed;
        float travel = speed * Time.fixedDeltaTime + worldRadius; // 다음 스텝 이동량 + 반경 여유

        // SphereCastAll 로 진행선상의 모든 충돌을 가까운 순서대로 검사한다.
        // (단일 SphereCast 는 파편 1개만 보고 멈추므로, 파편 군집을 뚫고 그 뒤의 보스를 못 본다)
        var hits = Physics.SphereCastAll(transform.position, worldRadius, dir, travel,
                                         sweepMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            Collider other = hit.collider;
            if (other == null) continue;
            if (other.transform.IsChildOf(transform)) continue;
            if (other.GetComponentInParent<MovementInput>() != null) continue; // 플레이어 무시

            // 보스 등 데미지 대상이면 즉시 명중
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg != null) { DealDamage(other.transform, dmg); return; }

            // 붕괴 파편 → 제한 개수까지 뚫고 통과
            if (IsFragment(other))
            {
                if (piercedFragments.Contains(other)) continue;     // 이미 통과 처리한 파편 → 뒤쪽 확인
                if (fragmentsPierced < fragmentPierceLimit)
                {
                    PierceFragment(other, dir, speed);
                    continue;                                       // 통과: 같은 스윕의 더 뒤쪽 충돌 계속 확인
                }
                EmbedAt(hit, dir);                                  // 관통 한도 초과 → 이 파편에 박힘
                return;
            }

            // 일반 구조물/지형 → 표면에 박힘
            EmbedAt(hit, dir);
            return;
        }
    }

    // 붕괴 파편 식별: OpenFracture 는 파편 오브젝트 이름을 "Fragment",
    // 그 부모(루트) 이름을 "<원본>Fragments" 로 만든다.
    static bool IsFragment(Collider c)
    {
        if (c == null) return false;
        Transform t = c.transform;
        if (t.name.StartsWith("Fragment")) return true;
        Transform p = t.parent;
        return p != null && p.name.EndsWith("Fragments");
    }

    void PierceFragment(Collider frag, Vector3 dir, float speed)
    {
        piercedFragments.Add(frag);
        fragmentsPierced++;
        Physics.IgnoreCollision(sphere, frag, true);   // 물리적으로도 통과
        // 그럴듯한 연출: 뚫고 지나가며 파편을 진행방향으로 살짝 밀어냄
        var frb = frag.attachedRigidbody;
        if (frb != null && !frb.isKinematic)
            frb.AddForce(dir * speed * 0.3f, ForceMode.VelocityChange);
    }

    void EmbedAt(RaycastHit hit, Vector3 dir)
    {
        Vector3 normal = hit.normal.sqrMagnitude > 1e-4f ? hit.normal : -dir;
        Vector3 surface = hit.distance > 1e-3f ? hit.point : transform.position;
        transform.position = surface + normal * Mathf.Max(0.01f, worldRadius - embedDepth);
        BeginEmbed();
    }

    Vector3 GetVel()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    // 보스(트리거 콜라이더) 명중
    void OnTriggerEnter(Collider other)
    {
        if (consumed) return;
        if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;
        if (other.GetComponentInParent<MovementInput>() != null) return; // 플레이어 오사 방지

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null) return; // 데미지 대상이 아니면 트리거는 무시(솔리드 충돌로 처리됨)

        DealDamage(other.transform, dmg);
    }

    // 구조물/지형 등 솔리드 충돌(스윕이 놓친 경우의 백업)
    void OnCollisionEnter(Collision col)
    {
        if (consumed) return;
        if (Time.time - spawnTime < armTime) return;           // 발사 직후 자기/대포 충돌 무시
        if (col.collider.GetComponentInParent<MovementInput>() != null) return; // 플레이어 무시

        // 혹시 보스 솔리드 콜라이더에 먼저 닿으면 데미지 처리
        var dmg = col.collider.GetComponentInParent<IDamageable>();
        if (dmg != null) { DealDamage(col.collider.transform, dmg); return; }

        // 붕괴 파편이면 한도까지 뚫고 통과 (스윕이 놓친 경우의 백업)
        if (IsFragment(col.collider))
        {
            if (piercedFragments.Contains(col.collider)) return;        // 이미 통과 중인 파편 — 무시
            if (fragmentsPierced < fragmentPierceLimit)
            {
                Vector3 v = GetVel();
                float sp = v.magnitude;
                PierceFragment(col.collider, sp > 0.01f ? v / sp : transform.forward, sp);
                return;                                                 // 통과
            }
            // 한도 초과 → 아래에서 박힘
        }

        // 그 외 = 구조물/지형 → 박혀서 천천히 떨어짐
        Vector3 n = col.contactCount > 0 ? col.GetContact(0).normal : Vector3.up;
        transform.position += -n * embedDepth;
        BeginEmbed();
    }

    void DealDamage(Transform hit, IDamageable dmg)
    {
        if (consumed) return;
        consumed = true;
        dmg.TakeDamage(damage);

        var reaction = hit.GetComponentInParent<BossHitReaction>();
        if (reaction != null) reaction.PlayHit();

        if (hitVfxPrefab != null)
            Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    void BeginEmbed()
    {
        if (consumed) return;
        consumed = true;

        if (hitVfxPrefab != null)
            Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);

        StartCoroutine(EmbedThenFall());
    }

    IEnumerator EmbedThenFall()
    {
        // 1) 박힘: 표면에서 멈추고 고정
        StopBall();
        rb.isKinematic = true;
        yield return new WaitForSeconds(stickDuration);

        // 2) 천천히 떨어짐
        rb.isKinematic = false;
        rb.useGravity = true;
        SetDrag(slowFallDrag);
        yield return new WaitForSeconds(afterHitLife);

        Destroy(gameObject);
    }

    void StopBall()
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    void SetDrag(float d)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = d;
#else
        rb.drag = d;
#endif
    }
}
