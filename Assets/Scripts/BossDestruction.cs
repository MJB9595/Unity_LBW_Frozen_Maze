using UnityEngine;
using System.Collections.Generic;

public class BossDestruction : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float triggerRadius = 6f;
    [SerializeField] private LayerMask destructionMask = -1;

    [Header("OpenFracture Settings")]
    [SerializeField] [Range(5, 100)] private int fragmentCount = 40;
    [Tooltip("골렘 붕괴 마커가 붙은 '타워'만 이 개수로 더 잘게 부순다(내부 타워 한정)")]
    [SerializeField] [Range(5, 200)] private int towerFragmentCount = 90;
    [SerializeField] private Material insideMaterial;
    [SerializeField] private float explosionForce = 600f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float upwardModifier = 0.5f;
[SerializeField] [Range(1, 600)] private float fragmentDestroyDelay = 600f;

    [Header("Decal Settings")]
    [SerializeField] private GameObject crackDecalPrefab;
    [SerializeField] private float decalLifetime = 600f;

    private Unity.Cinemachine.CinemachineImpulseSource impulseSource;
    private HashSet<GameObject> recentlyFractured = new HashSet<GameObject>();
    private Collider[] bossColliders;

    private void Start()
    {
        impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        bossColliders = GetComponents<Collider>();

        // Setup Trigger
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = triggerRadius;

        // Setup Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        GameObject obj = other.gameObject;

        if (recentlyFractured.Contains(obj)) return;
        if (obj.name.StartsWith("Fragment")) return; 
        if (obj.transform.parent != null && obj.transform.parent.name.EndsWith("Fragments")) return;

        if (((1 << obj.layer) & destructionMask) == 0) return;
        if (obj == gameObject || obj.CompareTag("Player")) return;

        string lowerName = obj.name.ToLower();
        if (lowerName.Contains("floor") || lowerName.Contains("terrain") ||
            lowerName.Contains("ground") || lowerName.Contains("grass") ||
            lowerName.Contains("navmesh") || lowerName.Contains("plane"))
            return;

        // Handle Architectural Objects with Decals
        if (IsStaticCrackObject(obj))
        {
            ApplyCrackDecal(obj);
            return;
        }

        // Handle Props with Fracture
        if (obj.GetComponent<MeshFilter>() == null || obj.GetComponent<MeshRenderer>() == null)
            return;

        FractureObject(obj);
    }

    private void ApplyCrackDecal(GameObject obj)
    {
        recentlyFractured.Add(obj);

        // Find precise hit point using Raycast from Boss center to target
        Vector3 direction = (obj.transform.position - transform.position).normalized;
        RaycastHit hit;
        
        // Raycast from boss towards the object to find surface
        if (Physics.Raycast(transform.position, direction, out hit, triggerRadius + 5f))
        {
            if (crackDecalPrefab != null)
            {
                GameObject decal = Instantiate(crackDecalPrefab, hit.point, Quaternion.LookRotation(-hit.normal));
                decal.transform.SetParent(obj.transform); // Stick to the wall
                
                // Randomize rotation for variety
                decal.transform.Rotate(Vector3.forward, Random.Range(0, 360));
                
                Destroy(decal, decalLifetime);
            }
        }

        if (impulseSource != null) impulseSource.GenerateImpulse();
    }

    private bool IsStaticCrackObject(GameObject obj)
    {
        // GolemCollapsible 마커가 붙은 구조물은 "집"처럼 실제 붕괴(fracture)시킨다 → 데칼 처리 제외
        if (obj.GetComponentInParent<GolemCollapsible>() != null)
            return false;

        string lowerName = obj.name.ToLower();
        bool isArchitectural = lowerName.Contains("wall") || lowerName.Contains("tower") || lowerName.Contains("castle");
        
        if (!isArchitectural) return false;

        // Check if it belongs to the Medieval_Castle_Set
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.name.Contains("Medieval_Castle_Set"))
                return true;
            current = current.parent;
        }

        return false;
    }

    private void FractureObject(GameObject obj)
    {
        recentlyFractured.Add(obj);
        bool shouldStaticCrack = IsStaticCrackObject(obj);

        MeshRenderer targetRenderer = obj.GetComponent<MeshRenderer>();
        // Use the first material as a fallback for the inside faces
        Material fallbackMaterial = (targetRenderer != null && targetRenderer.sharedMaterials.Length > 0) 
            ? targetRenderer.sharedMaterials[0] 
            : null;

        // Ensure mesh is readable
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && !mf.sharedMesh.isReadable)
        {
            Mesh readableMesh = Object.Instantiate(mf.sharedMesh);
            readableMesh.MarkDynamic();
            mf.sharedMesh = readableMesh;
        }

        // Pre-flight check: OpenFracture's ConstrainedTriangulator can fail on degenerate or
        // very small meshes. Skip these gracefully instead of throwing in the middle of slicing.
        if (mf == null || mf.sharedMesh == null)
            return;

        var sharedMesh = mf.sharedMesh;
        if (sharedMesh.vertexCount < 12 || sharedMesh.triangles.Length < 12)
        {
            // Too few verts/tris — bail out quietly. Use a Decal instead if visual feedback is needed.
            return;
        }

        // Reject zero/near-zero scale meshes
        Vector3 lossy = obj.transform.lossyScale;
        if (Mathf.Abs(lossy.x) < 0.01f || Mathf.Abs(lossy.y) < 0.01f || Mathf.Abs(lossy.z) < 0.01f)
            return;

        // 골렘 붕괴 마커가 붙은 '타워'(내부 타워)만 더 잘게 부순다. 외곽 대포 타워/다리/성은 기본값 유지.
        bool isCollapsibleTower = obj.name.ToLower().Contains("tower")
                                  && obj.GetComponentInParent<GolemCollapsible>() != null;
        int countToUse = isCollapsibleTower ? towerFragmentCount : fragmentCount;

        Fracture fracture = obj.GetComponent<Fracture>();
        if (fracture == null)
        {
            fracture = obj.AddComponent<Fracture>();
            fracture.triggerOptions = new TriggerOptions { triggerType = TriggerType.Collision };

            // Priority: 1. Manually assigned insideMaterial, 2. Original material of the object
            Material materialToUse = insideMaterial != null ? insideMaterial : fallbackMaterial;

            // If we still don't have a material, it might stay purple.
            // In HDRP, we must ensure the material is compatible.
            fracture.fractureOptions = new FractureOptions
            {
                fragmentCount = countToUse,
                asynchronous = false,
                xAxis = true, yAxis = true, zAxis = true,
                insideMaterial = materialToUse
            };
            fracture.refractureOptions = new RefractureOptions();
            fracture.callbackOptions = new CallbackOptions();
        }

        // OpenFracture의 ConstrainedTriangulator가 일부 메시(non-manifold, 자가 교차, T-junction 등)에서
        // "Failed to find final triangle" 같은 워닝과 함께 실패할 수 있다. 이게 게임 로직을 막진 않지만
        // 콘솔이 시끄러워지므로 try/catch로 감싸 안전하게 처리한다.
        try
        {
            fracture.CauseFracture();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BossDestruction] Fracture failed on '{obj.name}': {e.Message}. Falling back to crack decal.");
            ApplyCrackDecal(obj);
            return;
        }

        // fragmentRoot가 null이거나 자식이 없으면 OpenFracture 내부에서 부분 실패한 것.
        // 이런 경우 데칼만이라도 남기고 원본 오브젝트는 비활성화한다.
        if (fracture.fragmentRoot == null || fracture.fragmentRoot.transform.childCount == 0)
        {
            Debug.LogWarning($"[BossDestruction] Fracture produced no fragments on '{obj.name}'. Falling back to crack decal.");
            ApplyCrackDecal(obj);
            if (impulseSource != null) impulseSource.GenerateImpulse();
            return;
        }

        if (fracture.fragmentRoot != null)
        {
            // Priority: 1. Manually assigned insideMaterial, 2. Fallback to original material
            Material materialForInside = insideMaterial != null ? insideMaterial : fallbackMaterial;

            foreach (Transform fragment in fracture.fragmentRoot.transform)
            {
                // Force material assignment to ensure HDRP compatibility and fix purple issue
                MeshRenderer fragRenderer = fragment.GetComponent<MeshRenderer>();
                if (fragRenderer != null)
                {
                    // OpenFracture fragments have 2 submeshes: 0 = Outside, 1 = Inside (Cut face)
                    // We ensure both slots are filled.
                    Material[] mats = new Material[2];
                    mats[0] = fallbackMaterial; // Outside faces
                    mats[1] = materialForInside; // Inside/Cut faces
                    fragRenderer.sharedMaterials = mats;
                }

                Rigidbody frb = fragment.GetComponent<Rigidbody>();
                Collider frCol = fragment.GetComponent<Collider>();

                if (frb != null)
                {
                    if (shouldStaticCrack)
                    {
                        // Lock strictly for walls
                        frb.isKinematic = true;
                        frb.useGravity = false;
                        
                        // Ignore collision with boss so pieces aren't pushed out of position
                        if (frCol != null && bossColliders != null)
                        {
                            foreach (var bCol in bossColliders)
                            {
                                if (bCol != null) Physics.IgnoreCollision(bCol, frCol);
                            }
                        }
                    }
                    else
                    {
                        frb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        frb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardModifier);
                        frb.angularVelocity = Random.insideUnitSphere * 20f;
                    }
                }
                
                float finalDelay = fragmentDestroyDelay * Random.Range(0.8f, 1.2f);
                Destroy(fragment.gameObject, finalDelay);
            }
            Destroy(fracture.fragmentRoot, fragmentDestroyDelay + 1f);
        }

        if (impulseSource != null) impulseSource.GenerateImpulse();
    }
}
