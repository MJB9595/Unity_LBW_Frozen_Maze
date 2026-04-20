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
    [Tooltip("DecalProjector Rendering Layer Mask와 일치하는 인덱스.\n" +
             "Light Layer 1 = 1, Light Layer 2 = 2 ...")]
    [Range(0, 7)]
    public int decalRenderingLayerIndex = 1;

    // 현재 합체된 벽 렌더러 + 원본 Layer 백업
    private MeshRenderer[] currentWallRenderers;
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

        var renderers        = wallObject.GetComponentsInChildren<MeshRenderer>();
        currentWallRenderers  = renderers;
        originalWallLayerMasks = new uint[renderers.Length];

        uint bit = 1u << decalRenderingLayerIndex;

        for (int i = 0; i < renderers.Length; i++)
        {
            originalWallLayerMasks[i]        = renderers[i].renderingLayerMask;
            renderers[i].renderingLayerMask |= bit;
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
            FrameMovement(normal, finalPosition, finalTransition);

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

    void FrameMovement(Vector3 normal, Vector3 finalPosition, float finalTransition)
    {
        frameQuad.position = transform.position + new Vector3(0, 1f, 0) - (transform.forward * .5f);
        frameQuad.forward  = -normal;

        string colorProperty = "_UnlitColor";
        frameRenderer.material.SetColor(colorProperty, Color.clear);
        frameRenderer.material.DOColor(frameLitColor, colorProperty, 1f).SetDelay(.3f);
        frameQuad.DOMove(
            finalPosition + new Vector3(0, 1f, 0) - (transform.forward * .05f),
            finalTransition).SetEase(Ease.InBack).SetDelay(.2f);
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
        s.Append(frameRenderer.material.DOColor(Color.clear, "_UnlitColor", 1));

        return s;
    }
}