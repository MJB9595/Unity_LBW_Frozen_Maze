using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

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
    private bool isPortalTransition;

    private void Start()
    {
        anim = GetComponentInChildren<Animator>();
        
        // 파티클이 캐릭터 이동 시 따라다니며 뭉치는 현상을 막기 위해 Simulation Space를 World로 강제 고정합니다.
        if (mergeParticle != null)
        {
            var main = mergeParticle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        if (exitParticle != null)
        {
            var main = exitParticle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // 텍스처 겹침으로 인한 하얀 섬광(눈뽕) 현상을 줄이기 위해 색상을 검보라색으로 강제 변경합니다.
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.0f, 0.3f, 1.0f)); 
        }
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
       if (Input.GetKeyDown(KeyCode.Q))
        {
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                Debug.Log($"[MASK] {r.gameObject.name}: {r.renderingLayerMask}");
        }
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

            // 현재 위치가 각 코너(targetPos 또는 originPos)에 근접했는지 확인
            bool nearTarget = Vector3.Distance(transform.position, originPos) > (Vector3.Distance(originPos, targetPos) - distanceToTurn);
            bool nearOrigin = Vector3.Distance(transform.position, originPos) < distanceToTurn;

            // 코너에 가까이 있으면서, 그 코너를 향해 '계속 전진'할 때만 회전을 시작하도록 제한
            // (코너 근처에서 반대 방향으로 돌아서는 경우에는 회전이 발동하지 않아 날아가는 버그 방지)
            if ((nearTarget && axis > 0) || (nearOrigin && axis < 0))
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

        if (wallMerge != null && (movementMode || rotationMode))
        {
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
        if (wallMerge == null || search == null) return;

        if (!Physics.Raycast(transform.position, -transform.forward, out RaycastHit hit, 1.5f))
            return;

        RaySearch hitSearch = hit.collider.transform.root.GetComponentInChildren<RaySearch>();
        if (hitSearch == null || hitSearch != search)
            return;

        Debug.Log($"[TrackCurrentWall] 통과 - hit: {hit.collider.gameObject.name} (root: {hit.collider.transform.root.name})");

        if (hit.collider.gameObject == lastWallObject) return;

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

        isPortalTransition = false;

        // 회전하는 중심축(pivot) 반경 0.5m 내에 GapTrigger(틈새 식별용 스크립트)가 겹쳐 있는지 확인합니다.
        // 유니티 에디터에서 배치한 트리거 구역에 들어오면 확정적으로 포탈 연출이 발동합니다.
        Collider[] colliders = Physics.OverlapSphere(pivot.position, 0.5f);
        foreach (Collider col in colliders)
        {
            if (col.GetComponent<GapTrigger>() != null)
            {
                isPortalTransition = true;
                break;
            }
        }

        if (isPortalTransition)
        {
            Transform decalVisual = transform.GetChild(0);

            // 1. 데칼 찌그러진 후 완전히 끄기 (왜곡 효과와 함께 사라짐)
            decalVisual.DOScaleX(0f, 0.15f).SetEase(Ease.InBack).OnComplete(() => {
                decalVisual.gameObject.SetActive(false);
            });

            // 2. 포스트 프로세싱 줌/블러 효과 적용
            if (wallMerge.zoomVolume != null)
                DOVirtual.Float(wallMerge.zoomVolume.weight, 1f, 0.2f, wallMerge.ZoomVolume);
            if (wallMerge.dofVolume != null)
                DOVirtual.Float(wallMerge.dofVolume.weight, 1f, 0.2f, wallMerge.DofPostVolume);

            // 3. 파티클 재생 (World 설정으로 인해 부모 분리 불필요)
            if (mergeParticle != null)
            {
                mergeParticle.Play();
            }
        }
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

            // --- Portal Transition Effect End ---
            if (isPortalTransition)
            {
                Transform decalVisual = transform.GetChild(0);

                // 1. 데칼 다시 켜고 원래 크기(1)로 튕기며 복구
                decalVisual.gameObject.SetActive(true);
                decalVisual.DOScaleX(1f, 0.2f).SetEase(Ease.OutBack);

                // 2. 포스트 프로세싱 효과 복구
                if (wallMerge.zoomVolume != null)
                    DOVirtual.Float(wallMerge.zoomVolume.weight, 0f, 0.2f, wallMerge.ZoomVolume);
                if (wallMerge.dofVolume != null)
                    DOVirtual.Float(wallMerge.dofVolume.weight, 0f, 0.2f, wallMerge.DofPostVolume);

                // 3. 검보라색으로 설정된 탈출 파티클 재생
                if (exitParticle != null)
                {
                    exitParticle.Play();
                }
            }
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

        if (isActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, -transform.forward * 1.5f);
        }
    }
    }
