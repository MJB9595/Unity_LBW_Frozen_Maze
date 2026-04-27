// PlayerKnockback.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerKnockback : MonoBehaviour
{
    private CharacterController characterController;
    private Vector3 knockbackVelocity;

    [Header("Status")]
    public float knockbackTimer;

    [Header("Knockback Settings")]
    public float knockbackForce = 15f;
    public float knockbackDuration = 0.25f;
    public float friction = 5f;

    // 충돌 이벤트를 담을 리스트 (재사용으로 GC 최적화)
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (knockbackTimer > 0)
        {
            knockbackTimer -= Time.deltaTime;
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * friction);

            if (knockbackTimer <= 0)
                knockbackVelocity = Vector3.zero;
        }
    }

    void OnParticleCollision(GameObject other)
    {
        ParticleSystem ps = other.GetComponent<ParticleSystem>();
        if (ps == null) return;

        // 충돌 이벤트 데이터를 가져옴
        int numEvents = ps.GetCollisionEvents(gameObject, collisionEvents);

        if (numEvents > 0)
        {
            // 첫 번째 충돌 이벤트 기준으로 파티클의 속도(진행 방향)를 가져옴
            Vector3 direction = collisionEvents[0].velocity.normalized;

            // Y축(위아래) 회전이나 이동을 무시하여 바닥을 따라 밀리도록 고정
            direction.y = 0;
            direction.Normalize();

            // 혹시라도 속도가 측정되지 않는 파티클인 경우 대비 (안전장치)
            if (direction == Vector3.zero) 
            {
                direction = (transform.position - collisionEvents[0].intersection).normalized;
                direction.y = 0;
                direction.Normalize();
            }

            ApplyKnockback(direction);
        }
    }

    void ApplyKnockback(Vector3 direction)
    {
        knockbackTimer = knockbackDuration;
        knockbackVelocity = direction * knockbackForce;

        Debug.Log("💥 파티클 충돌! 밀려남 발생!");
    }
}