using UnityEngine;
using System.Collections.Generic;

public class BossAttackHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 20f;
    private bool isActive = false;
    private List<IDamageable> hitTargets = new List<IDamageable>();

    public void Activate()
    {
        isActive = true;
        hitTargets.Clear();
    }

    public void Deactivate()
    {
        isActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null && !hitTargets.Contains(damageable))
        {
            damageable.TakeDamage(damage);
            hitTargets.Add(damageable);
        }
    }
}
