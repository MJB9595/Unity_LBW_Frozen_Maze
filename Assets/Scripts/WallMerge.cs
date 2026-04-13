using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

// Unity 6 (6000.3.11f1) Compatible Version
// Cinemachine 3.x API 기준으로 수정됨
// Cinemachine 3.x 에서는 CinemachineBrain, CinemachineImpulseSource 네임스페이스가 변경됨
// Package Manager에서 com.unity.cinemachine 3.x 설치 필요

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

    // Unity 6 / Cinemachine 3.x: CinemachineBrain은 여전히 Camera에 붙어있지만 네임스페이스 변경
    private CinemachineBrain brain;

    private void Start()
    {
        playerAnimator = GetComponent<Animator>();
        playerMovement = GetComponent<MovementInput>();
        playerController = GetComponent<CharacterController>();

        // Cinemachine 3.x: Camera.main에서 CinemachineBrain 가져오기
        if (Camera.main != null)
        {
            brain = Camera.main.GetComponent<CinemachineBrain>();
            impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();
        }

        playerZScale = transform.GetChild(0).localScale.z;
        frameRenderer = frameQuad.GetComponent<Renderer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (Physics.Raycast(transform.position + (Vector3.up * .1f), transform.forward, out RaycastHit hit, 1))
            {
                if (hit.transform.GetComponentInChildren<RaySearch>() != null)
                {
                    RaySearch search = hit.transform.GetComponentInChildren<RaySearch>();

                    // 스페이스바 누르는 순간 플레이어 위치/방향 기준으로 포인트 재탐색
                    // EdgeSearch 오브젝트를 움직이지 않고 직접 좌표를 넘겨줌
                    Vector3 rayOrigin = transform.position + (Vector3.up * .1f);
                    search.DoPointsFromPlayer(rayOrigin, transform.forward);

                    // 포인트가 없으면 중단
                    if (search.cornerPoints.Count == 0)
                        return;

                    List<Vector3> cornerPoints = new List<Vector3>();

                    for (int i = 0; i < search.cornerPoints.Count; i++)
                        cornerPoints.Add(search.cornerPoints[i].position);

                    closestCorner = GetClosestPoint(cornerPoints.ToArray(), hit.point);
                    int index = search.cornerPoints.FindIndex(x => x.position == closestCorner);

                    nextCorner = (index < search.cornerPoints.Count - 1) ? search.cornerPoints[index + 1].position : search.cornerPoints[0].position;
                    previousCorner = (index > 0) ? search.cornerPoints[index - 1].position : search.cornerPoints[search.cornerPoints.Count - 1].position;

                    chosenCorner = Vector3.Dot((closestCorner - hit.point), (nextCorner - hit.point)) > 0 ? previousCorner : nextCorner;
                    bool nextCornerIsRight = isRightSide(-hit.normal, chosenCorner - closestCorner, Vector3.up);

                    float distance = Vector3.Distance(closestCorner, chosenCorner);
                    float playerDis = Vector3.Distance(chosenCorner, hit.point);

                    if (playerDis > (distance - decalMovement.distanceToTurn))
                        playerDis = distance - decalMovement.distanceToTurn;
                    if (playerDis < decalMovement.distanceToTurn)
                        playerDis = decalMovement.distanceToTurn;

                    float positionLerp = Mathf.Abs(distance - playerDis) / ((distance + playerDis) / 2);

                    decalMovement.SetPosition(closestCorner, chosenCorner, positionLerp, search, nextCornerIsRight, hit.normal);

                    Transition(true, Vector3.Lerp(closestCorner, chosenCorner, positionLerp), hit.normal);
                }
            }
        }
    }

    public void Transition(bool merge, Vector3 point, Vector3 normal)
    {
        Vector3 finalNormal = merge ? -normal : normal;
        Vector3 finalPosition = merge ? point - new Vector3(0, .9f, 0) : point;
        string animatorStatus = merge ? "turn" : "normal";
        float scale = merge ? .01f : playerZScale;
        float finalTransition = merge ? .5f : .3f;

        if (merge == true)
            FrameMovement(normal, finalPosition, finalTransition);

        transform.forward = finalNormal;
        playerAnimator.SetTrigger(animatorStatus);
        PlayerActivation(merge);
        MergeSequence(merge, finalPosition, scale, finalTransition);

        float dofDelay = merge ? finalTransition + .3f : 0;
        float dofAmount = merge ? 1 : 0;
        DOVirtual.Float(dofVolume.weight, dofAmount, finalTransition, DofPostVolume).SetDelay(dofDelay);
        if (merge)
            DOVirtual.Float(zoomVolume.weight, 1, .7f, ZoomVolume).OnComplete(() => DOVirtual.Float(zoomVolume.weight, 0, .3f, ZoomVolume));
    }

    void PlayerActivation(bool active)
    {
        if (active == true)
        {
            playerMovement.enabled = false;
            playerController.enabled = false;
        }
        else
        {
            playerMovement.gameObject.SetActive(true);
        }
    }

    void FrameMovement(Vector3 normal, Vector3 finalPosition, float finalTransition)
    {
        frameQuad.position = transform.position + new Vector3(0, .85f, 0) - (transform.forward * .5f);
        frameQuad.forward = -normal;

        // Unity 6 URP/HDRP Material property 접근 방식
        // HDRP: "_UnlitColor" → URP: "_BaseColor" or "_Color"
        // 사용하는 렌더 파이프라인에 맞게 아래 프로퍼티 이름을 변경하세요
        string colorProperty = "_UnlitColor"; // HDRP Unlit: "_UnlitColor", URP Unlit: "_BaseColor"

        frameRenderer.material.SetColor(colorProperty, Color.clear);
        frameRenderer.material.DOColor(frameLitColor, colorProperty, 1f).SetDelay(.3f);
        frameQuad.DOMove(finalPosition + new Vector3(0, .85f, 0) - (transform.forward * .05f), finalTransition).SetEase(Ease.InBack).SetDelay(.2f);
    }

    Vector3 GetClosestPoint(Vector3[] points, Vector3 currentPoint)
    {
        Vector3 pMin = Vector3.zero;
        float minDist = Mathf.Infinity;

        foreach (Vector3 p in points)
        {
            float dist = Vector3.Distance(p, currentPoint);
            if (dist < minDist)
            {
                pMin = p;
                minDist = dist;
            }
        }
        return pMin;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position + (Vector3.up * .1f), transform.forward);
        Gizmos.DrawSphere(closestCorner, .2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(previousCorner, .2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(nextCorner, .2f);
    }

    public bool isRightSide(Vector3 fwd, Vector3 targetDir, Vector3 up)
    {
        Vector3 right = Vector3.Cross(up.normalized, fwd.normalized);
        float dir = Vector3.Dot(right, targetDir.normalized);
        return dir > 0f;
    }

    public void DofPostVolume(float x)
    {
        dofVolume.weight = x;
    }

    public void ZoomVolume(float x)
    {
        zoomVolume.weight = x;
    }

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
        if (merge == true && impulseSource != null)
            s.AppendCallback(() => impulseSource.GenerateImpulse());
        if (merge == false)
        {
            s.AppendCallback(() => playerMovement.enabled = true);
            s.AppendCallback(() => playerController.enabled = true);
        }
        s.AppendCallback(() => decalMovement.isActive = merge);
        s.Append(frameRenderer.material.DOColor(Color.clear, "_UnlitColor", 1));

        return s;
    }
}