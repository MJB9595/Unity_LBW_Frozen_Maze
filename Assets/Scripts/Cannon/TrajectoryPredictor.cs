using UnityEngine;

/// <summary>
/// 발사 전 예상 포물선을 LineRenderer 로 반투명하게 그린다(수치적분).
/// 지면/장애물에 닿으면 그 지점에서 곡선을 끊고 착탄 마커를 표시한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TrajectoryPredictor : MonoBehaviour
{
    public int steps = 60;
    public float timeStep = 0.08f;
    public float hitCheckRadius = 0.3f;
    public LayerMask collisionMask = ~0;
    public Transform impactMarker;      // 선택: 착탄 지점 표시 오브젝트

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        Hide();
    }

    public void Show()
    {
        if (line != null) line.enabled = true;
    }

    public void Hide()
    {
        if (line != null) line.enabled = false;
        if (impactMarker != null) impactMarker.gameObject.SetActive(false);
    }

    public void Simulate(Vector3 origin, Vector3 velocity, Vector3 gravity)
    {
        if (line == null) return;

        Vector3 pos = origin;
        Vector3 vel = velocity;
        int count = 0;
        bool hit = false;
        Vector3 hitPoint = origin;

        line.positionCount = steps;
        for (int i = 0; i < steps; i++)
        {
            line.SetPosition(i, pos);
            count = i + 1;

            Vector3 next = pos + vel * timeStep + 0.5f * gravity * timeStep * timeStep;
            Vector3 seg = next - pos;
            if (Physics.SphereCast(pos, hitCheckRadius, seg.normalized, out RaycastHit rh,
                                   seg.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = rh.point;
                line.SetPosition(i, hitPoint);
                hit = true;
                break;
            }

            vel += gravity * timeStep;
            pos = next;
        }

        line.positionCount = count;

        if (impactMarker != null)
        {
            impactMarker.gameObject.SetActive(hit);
            if (hit) impactMarker.position = hitPoint;
        }
    }
}
