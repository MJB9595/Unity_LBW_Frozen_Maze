using System.Collections.Generic;
using UnityEngine;

// Code collaboration with Freya Holmér
// Unity 6 Compatible Version

public class RaySearch : MonoBehaviour
{
    public float stepSize = 0.1f;
    public float offsetMargin = 0.01f;
    public int checkCountMax = 100;
    private bool cornerCheck = false;
    public List<MeshPoint> meshPoints = new List<MeshPoint>();
    public List<MeshPoint> cornerPoints = new List<MeshPoint>();

    private Vector3 lastNormalForCorner;

    List<Vector3[]> debugTangentCheck = new List<Vector3[]>();
    List<Vector3[]> debugNegativeCheck = new List<Vector3[]>();
    List<Vector3[]> debugBehindCheck = new List<Vector3[]>();

    void OnDrawGizmos()
    {
        DrawLinePairs(debugTangentCheck, Color.red);
        DrawLinePairs(debugNegativeCheck, Color.blue);
        DrawLinePairs(debugBehindCheck, Color.cyan);

        if (cornerPoints == null) return;

        foreach (MeshPoint p in cornerPoints)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(p.position, .15f);
        }
    }

    void DrawLinePairs(List<Vector3[]> list, Color color)
    {
        Gizmos.color = color;
        foreach (Vector3[] pair in list)
            if (pair.Length >= 2)
                Gizmos.DrawLine(pair[0], pair[1]);
    }

    void FindNext(Vector3 pt, Vector3 normal)
    {
        MeshPoint mp = new MeshPoint(); mp.position = pt; mp.normal = normal;
        MeshPoint mpnew = new MeshPoint(); mpnew.position = pt; mpnew.normal = normal;

        if (meshPoints.Count == 0)
        {
            cornerPoints.Add(mpnew);
            lastNormalForCorner = normal;
        }
        else if (!cornerCheck)
        {
            if (cornerPoints.Count > 0)
                if (Vector3.Distance(cornerPoints[0].position, mpnew.position) < .3f && Vector3.Dot(cornerPoints[0].normal, normal) > .99f)
                    cornerCheck = true;

            if (Vector3.Dot(lastNormalForCorner, normal) < .98f && !cornerCheck)
            {
                cornerPoints.Add(mpnew);
                lastNormalForCorner = normal;
            }
        }

        meshPoints.Add(mp);

        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        Vector3 offsetPt = pt + normal * offsetMargin;
        Vector3 tangentCheckPoint = offsetPt + tangent * stepSize;
        Vector3 negativeCheckPoint = tangentCheckPoint - normal * (offsetMargin * 2);
        Vector3 behindCheckPoint = negativeCheckPoint - tangent * (stepSize * 0.75f);

        bool foundThing = false;
        RaycastHit hit;

        if (Physics.Raycast(offsetPt, tangent, out hit, stepSize))
        {
            debugTangentCheck.Add(new[] { offsetPt, hit.point });
            foundThing = true;
        }
        else
        {
            debugTangentCheck.Add(new[] { offsetPt, tangentCheckPoint });
            if (Physics.Raycast(tangentCheckPoint, -normal, out hit, offsetMargin * 2))
            {
                debugNegativeCheck.Add(new[] { tangentCheckPoint, hit.point });
                foundThing = true;
            }
            else
            {
                debugNegativeCheck.Add(new[] { tangentCheckPoint, negativeCheckPoint });
                if (Physics.Raycast(negativeCheckPoint, -tangent, out hit, stepSize * 2))
                {
                    foundThing = true;
                    debugBehindCheck.Add(new[] { negativeCheckPoint, hit.point });
                }
                else
                {
                    debugBehindCheck.Add(new[] { negativeCheckPoint, behindCheckPoint });
                }
            }
        }

        if (foundThing && meshPoints.Count < checkCountMax)
            FindNext(hit.point, hit.normal);
    }

    void ResetLists()
    {
        cornerCheck = false;
        lastNormalForCorner = Vector3.zero;
        meshPoints.Clear();
        cornerPoints.Clear();
        debugTangentCheck.Clear();
        debugNegativeCheck.Clear();
        debugBehindCheck.Clear();
    }

    [ContextMenu("Find Points")]
    public void DoPoints()
    {
        ResetLists();
        if (Physics.Raycast(transform.position, Vector3.forward, out RaycastHit hit))
            FindNext(hit.point, hit.normal);
    }

    // WallMerge에서 호출 - 플레이어 위치/방향 기준으로 탐색
    // EdgeSearch 오브젝트를 움직이지 않고 직접 rayOrigin, rayDirection을 받음
    public void DoPointsFromPlayer(Vector3 rayOrigin, Vector3 rayDirection)
    {
        ResetLists();
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit))
            FindNext(hit.point, hit.normal);
    }
}

[System.Serializable]
public struct MeshPoint
{
    public Vector3 position;
    public Vector3 normal;
}