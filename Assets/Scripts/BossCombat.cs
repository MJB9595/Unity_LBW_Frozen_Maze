using UnityEngine;
using System.Collections;

public class BossCombat : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 20.31f; // 13.54 * 1.5
    [SerializeField] private float attackCooldown = 4f;
    [SerializeField] private float damage = 25f;

    [Header("Hitbox")]
    [SerializeField] private BossAttackHitbox hitbox;
    [SerializeField] private float hitboxActiveStart = 0.5f; // Seconds into animation
    [SerializeField] private float hitboxActiveDuration = 0.5f;

    private Animator animator;
    private float nextAttackTime;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;
    public float AttackRange => attackRange;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (hitbox == null) hitbox = GetComponentInChildren<BossAttackHitbox>();
    }

    public bool CanAttack(Transform target)
    {
        if (Time.time < nextAttackTime || isAttacking) return false;
        
        // Calculate horizontal distance (XZ plane) to ignore height differences
        Vector3 flatSelf = transform.position; flatSelf.y = 0;
        Vector3 flatTarget = target.position; flatTarget.y = 0;
        float horizontalDistance = Vector3.Distance(flatSelf, flatTarget);
        
        // Also check vertical distance is within a reasonable range (Golem height is ~20)
        float verticalDistance = Mathf.Abs(transform.position.y - target.position.y);
        
        return horizontalDistance <= attackRange && verticalDistance <= 25f;
    }

    public void PerformAttack()
    {
        if (isAttacking) return;
        
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        animator.SetTrigger("Attack");
        
        // Wait for hitbox activation
        yield return new WaitForSeconds(hitboxActiveStart);
        if (hitbox != null) hitbox.Activate();
        
        // Wait for duration
        yield return new WaitForSeconds(hitboxActiveDuration);
        if (hitbox != null) hitbox.Deactivate();
        
        // Wait for animation to finish (approximate)
        yield return new WaitForSeconds(1f); // Total animation length wait
        
        isAttacking = false;
        nextAttackTime = Time.time + attackCooldown;
    }
}
