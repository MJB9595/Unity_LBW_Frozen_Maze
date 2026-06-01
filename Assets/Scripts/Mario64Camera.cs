using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 3인칭 추적 카메라 — 플레이어의 등 뒤를 항상 따라간다.
///
/// 동작:
///   1. Yaw는 플레이어의 forward(또는 transform.eulerAngles.y)를 부드럽게 추적.
///      플레이어가 회전하면 카메라도 따라 돈다 (관성을 위해 약간의 lag).
///   2. 거리/높이는 일정하게 유지.
///   3. 벽이 카메라와 플레이어 사이를 막으면 SphereCast로 가까이 당기고, 풀리면 천천히 복귀.
///   4. 텔레포트(WallMerge 출입 등) 발생 시 즉시 스냅.
///
/// 클래스 이름은 호환성 유지를 위해 Mario64Camera 그대로 두지만, 동작은 표준 3인칭 추적이다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class Mario64Camera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("카메라가 바라볼 지점의 플레이어 발 기준 높이 오프셋")]
    public float lookAtHeight = 1.5f;

    [Header("Distance & Height")]
    [Tooltip("플레이어와 유지하고 싶은 기본 거리(수평)")]
    public float distance = 6f;

    [Tooltip("플레이어 머리 위 카메라 높이 오프셋")]
    public float height = 2.5f;

    [Tooltip("벽에 가려져 카메라가 당겨질 때 가질 수 있는 최소 거리")]
    public float minDistance = 1.5f;

    [Header("Follow Smoothing")]
    [Tooltip("플레이어 회전을 따라잡는 시간(초). 작을수록 빠르게 추적, 클수록 느긋. " +
             "0.1~0.3 권장. 0.2면 자연스러운 3인칭 추적감.")]
    public float yawSmoothTime = 0.2f;

    [Tooltip("수평 위치 SmoothDamp 시간(초). 거리 유지의 부드러움. 0.1~0.3 권장.")]
    public float positionSmoothTime = 0.15f;

    [Tooltip("높이 추적 SmoothDamp 시간(초). 점프/낙하 부드러움. 0.2~0.5 권장.")]
    public float heightSmoothTime = 0.3f;

    [Header("Collision (벽 보정)")]
    public float collisionRadius = 0.3f;
    public LayerMask collisionLayers = ~0;

    [Tooltip("장애물 감지 시 카메라가 당겨지는 시간(초). 작을수록 빠름. 0.05~0.15 권장.")]
    public float collisionDamping = 0.08f;

    [Tooltip("장애물이 사라진 뒤 원래 거리까지 복귀하는 시간(초). 클수록 느릿. 0.4~0.8 권장.")]
    public float recoveryDamping = 0.6f;

    [Header("Teleport Detection")]
    [Tooltip("한 프레임에 이 거리 이상 이동 시 텔레포트로 간주, 카메라 즉시 스냅")]
    public float teleportThreshold = 3f;

    [Header("FOV")]
    public float fieldOfView = 50f;

    // ── 내부 상태 ─────────────────────────────────────────
    private Camera cam;
    private CinemachineBrain brain;

    private float currentYaw;          // 현재 카메라 yaw (도)
    private float yawVelocity;

    private float currentDistance;     // 현재 적용 중인 거리 (충돌로 줄어들 수 있음)
    private float distanceVelocity;

    private Vector3 horizPosVelocity;
    private float verticalPosVelocity;

    private Vector3 lastTargetPosition;
    private float collisionGraceTimer;

    void Awake()
    {
        cam = GetComponent<Camera>();
        brain = GetComponent<CinemachineBrain>();
    }

    void Start()
    {
        if (target == null) return;
        SnapToIdealPosition();
        collisionGraceTimer = 1.5f;
    }

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (brain == null) brain = GetComponent<CinemachineBrain>();

        // Mario64Camera가 켜져 있는 동안 Cinemachine Brain은 끄고 직접 제어
        if (brain != null) brain.enabled = false;
        if (cam != null) cam.fieldOfView = fieldOfView;

        if (target != null) SnapToIdealPosition();
    }

    void OnDisable()
    {
        if (brain != null) brain.enabled = true;
    }

    /// <summary>벽 합체 출입 직후 카메라가 잠깐 충돌 보정을 무시하도록 해주는 헬퍼.</summary>
    public void BeginCollisionGrace(float duration = 1.0f)
    {
        collisionGraceTimer = Mathf.Max(collisionGraceTimer, duration);
    }

    void SnapToIdealPosition()
    {
        currentYaw = target.eulerAngles.y;
        currentDistance = distance;
        yawVelocity = 0f;
        distanceVelocity = 0f;
        horizPosVelocity = Vector3.zero;
        verticalPosVelocity = 0f;
        lastTargetPosition = target.position;

        Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeight;
        // 카메라는 플레이어의 등 뒤에 — 즉 target.forward의 반대 방향에 위치
        Vector3 backOffset = -target.forward * currentDistance;
        backOffset.y = 0f;
        Vector3 pos = target.position + backOffset;
        pos.y = target.position.y + height;

        transform.position = pos;
        transform.LookAt(lookAtPoint);
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (collisionGraceTimer > 0f)
            collisionGraceTimer -= Time.deltaTime;

        // ── 텔레포트 감지 ────────────────────────────────
        float frameDist = Vector3.Distance(target.position, lastTargetPosition);
        bool teleported = frameDist > teleportThreshold;
        lastTargetPosition = target.position;

        if (teleported)
        {
            SnapToIdealPosition();
            return;
        }

        Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeight;

        // ── Yaw 추적: 플레이어의 yaw를 부드럽게 따라간다 ─────────────
        // 표준 3인칭: 카메라는 항상 플레이어의 등 뒤를 향한다.
        float targetYaw = target.eulerAngles.y;
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, yawSmoothTime);

        // ── 등 뒤 방향(수평, 정규화) ─────────────────────────
        // (Unity 기준 yaw=0일 때 forward는 +Z, yaw가 회전하면 forward도 회전)
        Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 fwd = yawRot * Vector3.forward;        // 플레이어가 바라보는 방향
        Vector3 backDir = -fwd;                        // 그 반대 = 등 뒤
        backDir.y = 0f;
        if (backDir.sqrMagnitude < 0.0001f) backDir = -transform.forward;
        backDir.Normalize();

        Vector3 lookHoriz = new Vector3(lookAtPoint.x, 0f, lookAtPoint.z);

        // ── 높이 추적 ────────────────────────────────────
        float targetY = target.position.y + height;
        float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref verticalPosVelocity, heightSmoothTime);

        // ── 충돌 검사용 이상(최대거리) 카메라 위치 ─────────────
        Vector3 fullDesiredHoriz = lookHoriz + backDir * distance;
        Vector3 fullDesiredPos = new Vector3(fullDesiredHoriz.x, newY, fullDesiredHoriz.z);

        float targetDistance = (collisionGraceTimer > 0f)
            ? distance
            : ResolveCollision(lookAtPoint, fullDesiredPos);

        // 가려짐 여부에 따라 당김/복귀 속도만 다르게 적용.
        // ★ 핵심: 카메라 위치는 항상 "방향 × 거리"라는 동일한 공식으로 계산하므로
        //   occluded가 매 프레임 토글되어도 currentDistance만 부드럽게 변해
        //   위치 점프(부들거림)가 발생하지 않는다.
        bool occluded = targetDistance < distance - 0.01f;
        float distSmoothTime = occluded ? collisionDamping : recoveryDamping;
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, distSmoothTime);

        // ── 최종 위치: 항상 동일한 공식(방향 × 거리) ─────────────
        Vector3 rigidHoriz = lookHoriz + backDir * currentDistance;
        Vector3 currentHoriz = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 newHoriz = Vector3.SmoothDamp(currentHoriz, rigidHoriz, ref horizPosVelocity, positionSmoothTime);

        transform.position = new Vector3(newHoriz.x, newY, newHoriz.z);
        transform.LookAt(lookAtPoint);
    }

    /// <summary>플레이어 -> 후보 카메라 위치 사이에 벽이 있으면 거리를 줄여서 반환.</summary>
    float ResolveCollision(Vector3 lookAtPoint, Vector3 desiredPos)
    {
        Vector3 dir = desiredPos - lookAtPoint;
        float dist = dir.magnitude;
        if (dist < 0.001f) return distance;

        if (Physics.SphereCast(lookAtPoint, collisionRadius, dir.normalized, out RaycastHit hit, dist, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(hit.distance - collisionRadius, minDistance);
        }
        return distance;
    }
}
