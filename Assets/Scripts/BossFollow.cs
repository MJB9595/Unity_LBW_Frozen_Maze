using UnityEngine;
using UnityEngine.AI;

public class BossFollow : MonoBehaviour
{
    private Transform player;
    private Animator animator;
    private AudioSource audioSource;
    private NavMeshAgent navAgent;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2.4f; 
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float animSpeedMultiplier = 1.0f;
    [SerializeField] private float animSpeedParamScale = 1.0f;
    [SerializeField] private float stoppingDistance = 12.4f;

    [Header("Sound Settings")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private float stepDistance = 2.5f;
    private float distanceTraveled = 0f;
    private Vector3 lastPosition;

    private float currentSpeed;
    private float animationVelocity;

    private BossCombat combat;
    private float currentMoveSpeed;

    void Start()
    {
        animator = GetComponent<Animator>();
        combat = GetComponent<BossCombat>();
        audioSource = GetComponent<AudioSource>();
        navAgent = GetComponent<NavMeshAgent>();

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.speed = 0.5f; // Scale down global animation speed for the giant
        }

        if (navAgent != null)
        {
            navAgent.speed = moveSpeed;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.angularSpeed = rotationSpeed;
            navAgent.acceleration = 8f;
            
            // Warp to NavMesh to ensure functionality
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        lastPosition = transform.position;

        GameObject playerObj = GameObject.Find("Jammo_Player");
        if (playerObj == null) playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        bool isAttacking = combat != null && combat.IsAttacking;

        if (isAttacking)
        {
            StopMovement();
            RotateTowards(player.position);

            currentSpeed = 0f;
            animationVelocity = 0f;
        }
        else
        {
            MoveTowardsPlayer();
            
            if (navAgent == null || !navAgent.isOnNavMesh || navAgent.velocity.sqrMagnitude < 0.1f)
            {
                HandleRotation();
            }

            if (combat != null && combat.CanAttack(player))
            {
                combat.PerformAttack();
            }
        }

        HandleAnimation();
        HandleFootsteps();
    }

    private void StopMovement()
    {
        currentMoveSpeed = 0f;
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }

    private void MoveTowardsPlayer()
    {
        if (navAgent != null && !navAgent.isOnNavMesh && Time.frameCount % 30 == 0)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
            currentMoveSpeed = navAgent.velocity.magnitude;
        }
        else
        {
            // Direct movement fallback
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            float horizontalDist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                   new Vector3(player.position.x, 0, player.position.z));

            if (horizontalDist > stoppingDistance)
            {
                transform.position += direction * moveSpeed * Time.deltaTime;
                currentMoveSpeed = moveSpeed;
            }
            else
            {
                currentMoveSpeed = 0f;
            }
        }
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 dirToTarget = (targetPos - transform.position);
        dirToTarget.y = 0;
        if (dirToTarget.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToTarget.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 60f * Time.deltaTime);
        }
    }

    private void HandleRotation()
    {
        if (player != null)
        {
            RotateTowards(player.position);
        }
    }

    private void HandleAnimation()
    {
        if (animator == null) return;

        // Calculate target motion value based on velocity
        float targetMotion = 0f;
        if (currentMoveSpeed > 0.1f)
        {
            // Giant scale: Full speed (2.4) should blend into Walk (0.5), not Run (1.0)
            targetMotion = Mathf.Clamp(currentMoveSpeed / moveSpeed, 0f, 0.5f);
            if (targetMotion < 0.2f) targetMotion = 0.2f; 
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetMotion, ref animationVelocity, 0.3f);
        if (currentSpeed < 0.05f) currentSpeed = 0f;

        animator.SetFloat("MotionAmount", currentSpeed);
        animator.SetFloat("Speed", currentSpeed); // Sync both speed floats
        
        // AnimationSpeed controls the playback rate. 
        float speedFactor = (currentMoveSpeed / moveSpeed) * animSpeedMultiplier;
        if (currentMoveSpeed < 0.1f) speedFactor = 1.0f;
        
        animator.SetFloat("AnimationSpeed", Mathf.Max(0.4f, speedFactor));
    }

    private void HandleFootsteps()
    {
        if (footstepClip == null) return;

        float distanceThisFrame = Vector3.Distance(transform.position, lastPosition);
        distanceTraveled += distanceThisFrame;
        lastPosition = transform.position;

        if (distanceTraveled >= stepDistance)
        {
            audioSource.PlayOneShot(footstepClip, 1.0f);
            distanceTraveled = 0f;
        }
    }
}
