using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Stage_4 씬의 모든 외곽 타워(TowerB 1~9)에 Cannon 프리팹을 배치합니다.
/// 각 대포는 성 내부(타워들의 중심점)를 향하도록 자동 회전됩니다.
///
/// 실행 방법: Unity Editor 메뉴 → Tools → Place Cannons On All Towers
/// </summary>
public class CannonPlacementTool : EditorWindow
{
    [MenuItem("Tools/Place Cannons On All Towers")]
    public static void PlaceCannonsOnAllTowers()
    {
        // 1. Cannon 프리팹 로드
        GameObject cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cannon.prefab");
        if (cannonPrefab == null)
        {
            Debug.LogError("[CannonPlacement] Cannon.prefab을 찾을 수 없습니다. Assets/Prefabs/Cannon.prefab");
            return;
        }

        // 2. 씬에서 모든 TowerB 오브젝트 찾기 (이름이 "TowerB (숫자)" 패턴)
        List<GameObject> towerBObjects = new List<GameObject>();
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            if (go.name.StartsWith("TowerB"))
            {
                towerBObjects.Add(go);
                Debug.Log($"[CannonPlacement] 타워 발견: {go.name} → 위치: {go.transform.position}");
            }
        }

        if (towerBObjects.Count == 0)
        {
            Debug.LogError("[CannonPlacement] 씬에서 TowerB 오브젝트를 찾을 수 없습니다.");
            return;
        }

        Debug.Log($"[CannonPlacement] 총 {towerBObjects.Count}개의 TowerB 타워를 찾았습니다.");

        // 3. Castle 중심점 계산 (모든 TowerB 위치의 평균)
        Vector3 castleCenter = Vector3.zero;
        foreach (GameObject tower in towerBObjects)
        {
            castleCenter += tower.transform.position;
        }
        castleCenter /= towerBObjects.Count;
        Debug.Log($"[CannonPlacement] Castle 중심점(추정): {castleCenter}");

        // 4. 기존 Cannon 제거 (중복 배치 방지) - CannonEmplacement, Cannon (1) 등
        //    단, 원본 프리팹 자체는 건드리지 않음
        int removedCount = 0;
        foreach (GameObject go in allObjects)
        {
            if (go.name.Contains("Cannon") && go.scene.IsValid() && go.name != "Cannon")
            {
                // 씬에 있는 Cannon 인스턴스들만 제거 (Cannon, Cannon (1), CannonEmplacement 등)
                // PrefabStage에 있는 원본은 건드리지 않음
                if (go.GetComponent<CannonController>() != null || go.name.StartsWith("Cannon"))
                {
                    Undo.DestroyObjectImmediate(go);
                    removedCount++;
                }
            }
        }
        // CannonEmplacement 오브젝트도 제거
        foreach (GameObject go in allObjects)
        {
            if (go.name.StartsWith("CannonEmplacement") && go.scene.IsValid())
            {
                Undo.DestroyObjectImmediate(go);
                removedCount++;
            }
        }

        if (removedCount > 0)
            Debug.Log($"[CannonPlacement] 기존 Cannon/CannonEmplacement {removedCount}개를 제거했습니다.");

        // 5. 각 타워 위에 Cannon 배치
        int placedCount = 0;
        foreach (GameObject tower in towerBObjects)
        {
            // 타워 꼭대기 Y 좌표 찾기: 타워의 Renderer 또는 Collider 최상단
            float towerTopY = GetTowerTopY(tower);

            // 타워 위치에서 중심을 향하는 방향 계산 (Y축 무시, 수평 방향만)
            Vector3 towerPos = tower.transform.position;
            Vector3 toCenter = castleCenter - towerPos;
            toCenter.y = 0f; // 수평 방향만 사용

            if (toCenter.sqrMagnitude < 0.01f)
            {
                Debug.LogWarning($"[CannonPlacement] {tower.name}은(는) 중심에 너무 가까워 기본 방향을 사용합니다.");
                toCenter = Vector3.forward;
            }

            Quaternion lookRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);

            // 배치 위치: 타워 꼭대기
            Vector3 placePosition = new Vector3(towerPos.x, towerTopY, towerPos.z);

            // Cannon 인스턴스 생성
            GameObject cannon = (GameObject)PrefabUtility.InstantiatePrefab(cannonPrefab);
            cannon.transform.position = placePosition;
            cannon.transform.rotation = lookRotation;

            // CannonEmplacement 더미(빈 GameObject) 생성 - 타워와 대포 사이의 받침대 역할
            GameObject emplacement = new GameObject($"CannonEmplacement_{tower.name}");
            emplacement.transform.position = new Vector3(towerPos.x, towerTopY - 0.1f, towerPos.z);
            emplacement.transform.rotation = lookRotation;

            // 휴지통(Undo) 지원 등록
            Undo.RegisterCreatedObjectUndo(cannon, $"Place Cannon on {tower.name}");
            Undo.RegisterCreatedObjectUndo(emplacement, $"Place Emplacement on {tower.name}");

            placedCount++;
            Debug.Log($"[CannonPlacement] {tower.name}: Cannon 배치 완료 → 위치: {placePosition}, 방향: {toCenter.normalized} (중심 방향)");
        }

        // 6. 씬 저장 마킹
        EditorUtility.SetDirty(GameObject.FindObjectOfType<Transform>());
        AssetDatabase.SaveAssets();

        Debug.Log($"[CannonPlacement] 완료! {placedCount}개의 타워에 Cannon을 배치했습니다. (중심: {castleCenter})");
        EditorUtility.DisplayDialog("Cannon 배치 완료",
            $"총 {placedCount}개의 TowerB 타워에 Cannon을 배치했습니다.\n\n" +
            $"Castle 중심: {castleCenter}\n" +
            $"모든 대포는 성 내부(중심 방향)를 향합니다.\n\n" +
            "씬을 저장해주세요 (Ctrl+S / Cmd+S).",
            "확인");
    }

    /// <summary>
    /// 타워 GameObject의 꼭대기 Y 좌표를 찾습니다.
    /// Renderer.bounds.max.y 또는 Collider.bounds.max.y 중 가장 높은 값을 사용합니다.
    /// </summary>
    private static float GetTowerTopY(GameObject tower)
    {
        float topY = tower.transform.position.y;

        // Renderer 기반 최상단
        Renderer[] renderers = tower.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            float maxY = r.bounds.max.y;
            if (maxY > topY) topY = maxY;
        }

        // Collider 기반 최상단 (Renderer가 없을 경우 대비)
        if (topY <= tower.transform.position.y + 0.5f)
        {
            Collider[] colliders = tower.GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                float maxY = c.bounds.max.y;
                if (maxY > topY) topY = maxY;
            }
        }

        // Fallback: 타워 높이를 알 수 없으면 기본 3m 위
        if (topY <= tower.transform.position.y + 0.5f)
        {
            topY = tower.transform.position.y + 3f;
            Debug.LogWarning($"[CannonPlacement] {tower.name}의 꼭대기를 정확히 찾을 수 없어 기본값({topY})을 사용합니다.");
        }

        return topY;
    }
}
