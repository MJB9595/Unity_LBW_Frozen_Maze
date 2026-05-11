using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using Unity.Cinemachine;

public class WallMerge : MonoBehaviour
{
    private Animator playerAnimator;
    private CharacterController playerController;
    private MovementInput playerMovement;
    private Vector3 closestCorner;
    private Vector3 nextCorner;
    private Vector3 previousCorner;
    private Vector3 chosenCorner;
    private float playerZScale;

    [Header("Parameters")]
    public float transitionTime = .8f;

    [Space]

    [Header("Public References")]
    public ProjectorMovement decalMovement;
    public Transform frameQuad;
    private Renderer frameRenderer;
    private CinemachineImpulseSource impulseSource;

    [Space]
    [Header("Frame Settings")]
    public Color frameLitColor;

    [Space]

    [Header("Cameras")]
    public GameObject gameCam;
    public GameObject wallCam;

    [Space]

    [Header("Post Processing")]
    public Volume dofVolume;
    public Volume zoomVolume;

    private CinemachineBrain brain;

    [Space]
    [Header("Decal Layer Isolation")]
    [Tooltip("DecalProjector의 Decal Layer 슬롯 번호 (0~7).\n" +
             "예) DecalLayer1 → 1 입력 / DecalLayer2 → 2 입력")]
    [Range(0, 7)]
    public int decalRenderingLayerIndex = 1;

    // 현재 합체된 벽 렌더러 + 원본 Layer 백업
    private Renderer[] currentWallRenderers;
    private uint[]         originalWallLayerMasks;

    private void Start()
    {
        playerAnimator   = GetComponent<Animator>();
        playerMovement   = GetComponent<MovementInput>();
        playerController = GetComponent<CharacterController>();

        if (Camera.main != null)
        {
            brain         = Camera.main.GetComponent<CinemachineBrain>();
            impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();
        }

        playerZScale  = transform.GetChild(0).localScale.z;
        frameRenderer = frameQuad.GetComponent<Renderer>();

        RemoveDecalLayerFromAllWalls();
    }

    /// 씬 내 RaySearch를 가진 모든 벽 오브젝트의 renderingLayerMask에서
    /// MergedWallDecal 비트를 제거합니다. 기본값이 uint.MaxValue이기 때문에 필요합니다.

    void RemoveDecalLayerFromAllWalls()
    {
        uint decalBit = 1u << (decalRenderingLayerIndex + 8);
        uint clearMask = ~decalBit;

        Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer r in allRenderers)
        {
            r.renderingLayerMask &= clearMask;
        }
    }

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (Physics.Raycast(transform.position + (Vector3.up * 1f), transform.forward, out RaycastHit hit, 1))
            {
                if (hit.transform.GetComponentInChildren<RaySearch>() != null)
                {
                    RaySearch search = hit.transform.GetComponentInChildren<RaySearch>();

                    Vector3 rayOrigin = transform.position + (Vector3.up * 1f);
                    search.DoPointsFromPlayer(rayOrigin, transform.forward);

                    if (search.cornerPoints.Count == 0)
                        return;

                    List<Vector3> cornerPoints = new List<Vector3>();
                    for (int i = 0; i < search.cornerPoints.Count; i++)
                        cornerPoints.Add(search.cornerPoints[i].position);

                    closestCorner = GetClosestPoint(cornerPoints.ToArray(), hit.point);
                    int index = search.cornerPoints.FindIndex(x => x.position == closestCorner);

                    nextCorner = (index < search.cornerPoints.Count - 1)
                        ? search.cornerPoints[index + 1].position
                        : search.cornerPoints[0].position;
                    previousCorner = (index > 0)
                        ? search.cornerPoints[index - 1].position
                        : search.cornerPoints[search.cornerPoints.Count - 1].position;

                    chosenCorner = Vector3.Dot((closestCorner - hit.point), (nextCorner - hit.point)) > 0
                        ? previousCorner : nextCorner;
                    bool nextCornerIsRight = isRightSide(-hit.normal, chosenCorner - closestCorner, Vector3.up);

                    float distance  = Vector3.Distance(closestCorner, chosenCorner);
                    float playerDis = Vector3.Distance(chosenCorner, hit.point);

                    if (playerDis > (distance - decalMovement.distanceToTurn))
                        playerDis = distance - decalMovement.distanceToTurn;
                    if (playerDis < decalMovement.distanceToTurn)
                        playerDis = decalMovement.distanceToTurn;

                    float positionLerp = Mathf.Abs(distance - playerDis) / ((distance + playerDis) / 2);

                    decalMovement.SetPosition(closestCorner, chosenCorner, positionLerp, search, nextCornerIsRight, hit.normal);

                    // ★ Fix: WallMerge 참조 주입
                    decalMovement.wallMerge = this;

                    // ★ Fix: 합체된 벽에 Layer 적용 후,
                    //         ProjectorMovement의 lastWallObject를 즉시 동기화
                    //         → TrackCurrentWall이 첫 프레임에 잘못된 오브젝트를 건드리지 않음
                    ApplyDecalLayerToWall(hit.collider.gameObject);
                    decalMovement.lastWallObject = hit.collider.gameObject;

                    Transition(true, Vector3.Lerp(closestCorner, chosenCorner, positionLerp), hit.normal);
                }
            }
        }
    }

    // ── Decal Layer 제어 ────────────────────────────────────────


    public void ApplyDecalLayerToWall(GameObject wallObject)
    {
        RestoreWallLayer();
        if (wallObject == null) return;

        var renderers          = wallObject.GetComponentsInChildren<Renderer>();
        currentWallRenderers   = renderers;
        originalWallLayerMasks = new uint[renderers.Length];

        uint bit = 1u << (decalRenderingLayerIndex + 8);

        for (int i = 0; i < renderers.Length; i++)
        {
            uint before = renderers[i].renderingLayerMask;
            originalWallLayerMasks[i]        = before;
            renderers[i].renderingLayerMask |= bit;
            uint after = renderers[i].renderingLayerMask;

            Debug.Log($"[ApplyDecal] obj={wallObject.name} | renderer={renderers[i].name} | before={before} → after={after} | bit={bit}");
        }
    }



    public void RestoreWallLayer()
    {
        if (currentWallRenderers == null) return;

        for (int i = 0; i < currentWallRenderers.Length; i++)
            if (currentWallRenderers[i] != null)
                currentWallRenderers[i].renderingLayerMask = originalWallLayerMasks[i];

        currentWallRenderers   = null;
        originalWallLayerMasks = null;
    }


    // ────────────────────────────────────────────────────────────

    public void Transition(bool merge, Vector3 point, Vector3 normal)
    {
        Vector3 finalNormal   = merge ? -normal : normal;
        float   groundY       = transform.position.y;

        if (merge && Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit groundHit, 10f))
            groundY = groundHit.point.y;

        Vector3 finalPosition   = merge ? new Vector3(point.x, groundY, point.z) : point;
        string  animatorStatus  = merge ? "turn" : "normal";
        float   scale           = merge ? .01f : playerZScale;
        float   finalTransition = merge ? .5f  : .3f;

        if (merge)
        {
            // ... (기존 로직이 있었다면 유지)
        }
        
        FrameMovement(normal, finalPosition, finalTransition, merge);

        transform.forward = finalNormal;
        playerAnimator.SetTrigger(animatorStatus);
        PlayerActivation(merge);
        MergeSequence(merge, finalPosition, scale, finalTransition);

        float dofDelay  = merge ? finalTransition + .3f : 0;
        float dofAmount = merge ? 1 : 0;
        DOVirtual.Float(dofVolume.weight, dofAmount, finalTransition, DofPostVolume).SetDelay(dofDelay);

        if (merge)
            DOVirtual.Float(zoomVolume.weight, 1, .7f, ZoomVolume)
                     .OnComplete(() => DOVirtual.Float(zoomVolume.weight, 0, .3f, ZoomVolume));

        if (!merge)
            RestoreWallLayer();
    }

    void PlayerActivation(bool active)
    {
        if (active)
        {
            playerMovement.enabled   = false;
            playerController.enabled = false;
        }
        else
        {
            playerMovement.gameObject.SetActive(true);
        }
    }

    void FrameMovement(Vector3 normal, Vector3 finalPosition, float finalTransition, bool merge)
    {
        // 너무 밝게(눈뽕) 빛나는 현상을 방지하기 위해 기본 색상의 알파(투명도)값을 40%로 확 낮춥니다.
        if (frameLitColor.a == 0) frameLitColor = new Color(1f, 1f, 1f, 0.1f);

        string colorProperty = "_UnlitColor";
        
        // 기존 진행 중인 트윈(애니메이션) 강제 종료하여 꼬임 방지
        frameRenderer.material.DOKill();
        frameQuad.DOKill();

        frameRenderer.material.SetColor(colorProperty, Color.clear);

        if (merge)
        {
            // 들어갈 때: 캐릭터 위치에서 시작해서 벽으로 날아감
            frameQuad.position = transform.position + new Vector3(0, 1f, 0) - (transform.forward * .5f);
            frameQuad.forward  = -normal;

            frameQuad.DOMove(
                finalPosition + new Vector3(0, 1f, 0) - (transform.forward * .05f),
                finalTransition).SetEase(Ease.InBack).SetDelay(.2f);
        }
        else
        {
            // 나올 때: 캐릭터가 있던 벽의 그 위치에 프레임이 남아서 나타남
            frameQuad.position = transform.position + new Vector3(0, 1f, 0) - (normal * .05f);
            frameQuad.forward  = -normal;
        }

        // 프레임 페이드인 -> 유지 -> 페이드아웃 애니메이션 시퀀스
        Sequence frameSeq = DOTween.Sequence();
        frameSeq.AppendInterval(0.2f);
        frameSeq.Append(frameRenderer.material.DOColor(frameLitColor, colorProperty, 0.4f)); // 서서히 나타남
        frameSeq.AppendInterval(0.5f); // 잠깐 유지
        frameSeq.Append(frameRenderer.material.DOColor(Color.clear, colorProperty, 0.8f));   // 다시 사라짐
    }

    Vector3 GetClosestPoint(Vector3[] points, Vector3 currentPoint)
    {
        Vector3 pMin    = Vector3.zero;
        float   minDist = Mathf.Infinity;

        foreach (Vector3 p in points)
        {
            float dist = Vector3.Distance(p, currentPoint);
            if (dist < minDist) { pMin = p; minDist = dist; }
        }
        return pMin;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position + (Vector3.up * 1f), transform.forward);
        Gizmos.DrawSphere(closestCorner, .2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(previousCorner, .2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(nextCorner, .2f);
    }

    public bool isRightSide(Vector3 fwd, Vector3 targetDir, Vector3 up)
    {
        Vector3 right = Vector3.Cross(up.normalized, fwd.normalized);
        return Vector3.Dot(right, targetDir.normalized) > 0f;
    }

    public void DofPostVolume(float x) { dofVolume.weight  = x; }
    public void ZoomVolume(float x)    { zoomVolume.weight = x; }

    Sequence MergeSequence(bool merge, Vector3 finalPosition, float scale, float finalTransition)
    {
        Sequence s = DOTween.Sequence();

        if (merge)
            s.AppendInterval(.2f);
        else
            s.AppendCallback(() => decalMovement.exitParticle.Play());

        s.AppendCallback(() => gameCam.SetActive(!merge));
        s.AppendCallback(() => wallCam.SetActive(merge));
        s.Append(transform.DOMove(finalPosition, finalTransition).SetEase(Ease.InBack));
        s.Join(transform.GetChild(0).DOScaleZ(scale, finalTransition).SetEase(Ease.InSine));

        if (merge)
            s.AppendCallback(() => playerMovement.gameObject.SetActive(false));

        s.AppendCallback(() => decalMovement.transform.GetChild(0).gameObject.SetActive(merge));
        s.AppendCallback(() => decalMovement.mergeParticle.Play());

        if (merge && impulseSource != null)
            s.AppendCallback(() => impulseSource.GenerateImpulse());

        if (!merge)
        {
            s.AppendCallback(() => playerMovement.enabled   = true);
            s.AppendCallback(() => playerController.enabled = true);
        }

        s.AppendCallback(() => decalMovement.isActive = merge);
        // 프레임 페이드아웃은 FrameMovement 내부 시퀀스로 통합했으므로 여기서 삭제합니다.

        return s;
        }
        }
