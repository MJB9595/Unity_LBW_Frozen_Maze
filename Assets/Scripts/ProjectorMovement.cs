using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProjectorMovement : MonoBehaviour
{
    private Animator anim;

    [Header("Movement Parameters")]
    public float movSpeed       = 3;
    public float rotSpeed       = 2;
    public float rotationLerp;
    public float distanceToTurn = 1f;

    [Space]

    [Header("Booleans")]
    public bool isActive;
    public bool movementMode;
    public bool rotationMode;
    public bool isGoingRight;
    public bool isMoving;
    private bool activation;

    private Vector3 originPos;
    private Vector3 targetPos;
    private Vector3 debug;

    private int currentIndex, previousIndex, nextIndex;

    [Space]

    [Header("Public References")]
    public WallMerge player;
    private RaySearch search;
    public Transform pivot;
    public Transform lineRef1, lineRef2;
    public ParticleSystem mergeParticle;
    public ParticleSystem exitParticle;

    // WallMerge에서 Merge 시점에 주입됨
    [HideInInspector] public WallMerge wallMerge;

    // 현재 Decal Layer가 적용된 벽 오브젝트
    // ★ public으로 변경 — WallMerge.cs에서 직접 초기화할 수 있도록
    [HideInInspector] public GameObject lastWallObject;

    private Vector3 savedNormal;

    private void Start()
    {
        anim = GetComponentInChildren<Animator>();
    }

    public void SetPosition(Vector3 orig, Vector3 target, float lerp, RaySearch ray, bool nextCornerIsRight, Vector3 normal)
    {
        transform.forward  = normal;
        transform.position = Vector3.Lerp(orig, target, lerp);
        search             = ray;
        originPos          = nextCornerIsRight ? orig   : target;
        targetPos          = nextCornerIsRight ? target : orig;
        movementMode       = true;

        // ★ Bug 2 수정: 여기서 null로 초기화하지 않습니다.
        // lastWallObject는 WallMerge.cs에서 ApplyDecalLayerToWall 직후에 주입됩니다.
        // null로 초기화하면 다음 프레임 TrackCurrentWall이 잘못된 오브젝트에 Layer를 적용합니다.
    }

    void DebugKey()
    {
        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        DebugKey();

        if (Input.GetKeyDown(KeyCode.Space) && isActive)
        {
            transform.parent = null;
            isActive         = false;
            movementMode     = false;
            rotationMode     = false;
            activation       = false;
            anim.speed       = 1;
            anim.SetFloat("axis", 0);
            anim.SetTrigger("reset");

            player.transform.position = new Vector3(
                transform.position.x,
                player.transform.position.y,
                transform.position.z) - (transform.forward * .5f);

            Vector3 playerFinalPos = player.transform.position + transform.forward;
            player.Transition(false, playerFinalPos, transform.forward);

            lastWallObject = null;
        }

        float axis = Input.GetAxis("Horizontal");

        if (movementMode && !rotationMode && isActive)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                axis > 0 ? targetPos : originPos,
                Mathf.Abs(axis) * Time.deltaTime * movSpeed);

            if (axis != 0 && !activation)
                activation = true;

            if (Vector3.Distance(transform.position, originPos) > (Vector3.Distance(originPos, targetPos) - distanceToTurn)
                || Vector3.Distance(transform.position, originPos) < distanceToTurn)
            {
                StartRotation(axis > 0);
            }

            TrackCurrentWall();
        }

        if (rotationMode && !movementMode && isActive)
        {
            CornerRoration(axis);
            TrackCurrentWall();
        }

        if (!activation) return;

        anim.SetFloat("axis", Input.GetAxisRaw("Horizontal"));
        anim.speed = (Input.GetAxis("Horizontal") == 0) ? 0 : 1;
    }

    /// <summary>
    /// 프로젝터에서 벽 안쪽(-transform.forward)으로 Raycast해서
    /// 현재 붙어있는 벽 오브젝트를 감지합니다.
    /// 이전과 다른 오브젝트면 Decal Layer를 이전합니다.
    /// </summary>
    void TrackCurrentWall()
    {
        if (wallMerge == null) return;

        // ★ Bug 1 수정: -transform.forward (벽 안쪽 방향)
        // transform.forward = hit.normal (벽 바깥 방향)이므로
        // 반대로 쏴야 현재 붙어있는 벽에 맞습니다.
        if (!Physics.Raycast(transform.position, -transform.forward, out RaycastHit hit, 1.5f))
            return;

        GameObject hitRoot  = hit.collider.transform.root.gameObject;
        GameObject lastRoot = lastWallObject != null
            ? lastWallObject.transform.root.gameObject
            : null;

        // 같은 오브젝트면 아무것도 하지 않음
        if (hitRoot == lastRoot) return;

        // 다른 오브젝트로 넘어갔을 때만 Layer 이전
        wallMerge.ApplyDecalLayerToWall(hit.collider.gameObject);
        lastWallObject = hit.collider.gameObject;
    }

    public void StartRotation(bool right)
    {
        isGoingRight = right;
        movementMode = false;
        savedNormal  = transform.forward;

        currentIndex = search.cornerPoints.FindIndex(
            x => x.position == (right ? targetPos : originPos));

        pivot.position   = GetPivotPosition(currentIndex, right);
        pivot.forward    = transform.forward;
        transform.parent = pivot;

        rotationLerp = .01f;
        rotationMode = true;
    }

    public void CornerRoration(float axis)
    {
        float n = isGoingRight ? 1 : -1;

        Vector3 normal = isGoingRight
            ? search.cornerPoints[currentIndex].normal
            : search.cornerPoints[previousIndex].normal;

        rotationLerp = Mathf.Clamp(
            rotationLerp + (axis * n * Time.deltaTime * rotSpeed), 0, 1);

        pivot.forward = Vector3.Lerp(savedNormal, normal, rotationLerp);

        if (rotationLerp >= 1 || rotationLerp <= 0)
        {
            rotationMode = false;
            bool complete = (rotationLerp >= 1);

            if (isGoingRight)
            {
                originPos = complete ? search.cornerPoints[currentIndex].position : originPos;
                targetPos = complete ? search.cornerPoints[nextIndex].position    : targetPos;
            }
            else
            {
                originPos = complete ? search.cornerPoints[previousIndex].position : originPos;
                targetPos = complete ? search.cornerPoints[currentIndex].position  : targetPos;
            }

            transform.parent = null;
            movementMode     = true;
            rotationLerp     = .01f;
        }
    }

    public Vector3 GetPivotPosition(int currentIndex, bool right)
    {
        Vector3 pos = search.cornerPoints[currentIndex].position;

        previousIndex = (currentIndex - 1 > -1) ? currentIndex - 1 : search.cornerPoints.Count - 1;
        nextIndex     = (currentIndex + 1 < search.cornerPoints.Count) ? currentIndex + 1 : 0;

        bool origin = Vector3.Distance(transform.position, originPos)
                    < Vector3.Distance(transform.position, targetPos);

        lineRef1.position = pos;
        lineRef1.LookAt(right
            ? search.cornerPoints[nextIndex].position
            : search.cornerPoints[previousIndex].position);
        lineRef1.localPosition += lineRef1.forward * distanceToTurn;
        lineRef1.forward = origin
            ? search.cornerPoints[previousIndex].normal
            : search.cornerPoints[currentIndex].normal;

        lineRef2.position = pos;
        lineRef2.LookAt(right
            ? search.cornerPoints[previousIndex].position
            : search.cornerPoints[nextIndex].position);
        lineRef2.localPosition += lineRef2.forward * distanceToTurn;
        lineRef2.forward = savedNormal;

        Vector3 intersection;
        LineLineIntersection(
            out intersection,
            lineRef1.position, lineRef1.forward,
            lineRef2.position, lineRef2.forward);

        return intersection;
    }

    public static bool LineLineIntersection(
        out Vector3 intersection,
        Vector3 linePoint1, Vector3 lineVec1,
        Vector3 linePoint2, Vector3 lineVec2)
    {
        Vector3 lineVec3      = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s      = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }

        intersection = Vector3.zero;
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(originPos, .1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(targetPos, .1f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lineRef1.position, .1f);
        Gizmos.DrawRay(lineRef1.position,  lineRef1.forward * 3);
        Gizmos.DrawRay(lineRef1.position, -lineRef1.forward * 3);
        Gizmos.DrawSphere(lineRef2.position, .1f);
        Gizmos.DrawRay(lineRef2.position,  lineRef2.forward * 3);
        Gizmos.DrawRay(lineRef2.position, -lineRef2.forward * 3);
        Gizmos.color = Color.green;
        Vector3 inter;
        LineLineIntersection(out inter, lineRef1.position, lineRef1.forward, lineRef2.position, lineRef2.forward);
        Gizmos.DrawSphere(inter, .1f);

        // 벽 추적 레이 시각화 (씬 뷰에서 확인용)
        if (isActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, -transform.forward * 1.5f);
        }
    }
}