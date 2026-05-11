using UnityEngine;

public class CrumblingSegment : MonoBehaviour
{
    [Header("Shatter Settings")]
    public int shardCount = 15;
    public float explosionForce = 20f;
    public float shardScale = 1.2f; // 기존 0.4에서 대폭 상향
    public float destroyDelay = 3f;

    private bool isShattered = false;
    private Renderer mainRenderer;
    private Collider mainCollider;

    void Start()
    {
        mainRenderer = GetComponent<Renderer>();
        if (mainRenderer == null) mainRenderer = GetComponentInChildren<Renderer>();
        mainCollider = GetComponent<Collider>();
    }

    public void StartCrumbling()
    {
        if (isShattered) return;
        isShattered = true;

        Shatter();
    }

    private void Shatter()
    {
        // 1. Get original material for shards
        Material mat = null;
        if (mainRenderer != null) mat = mainRenderer.sharedMaterial;

        // 2. Hide the original object
        if (mainRenderer != null) mainRenderer.enabled = false;
        if (mainCollider != null) mainCollider.isTrigger = true; 

        // 3. Create shards
        Vector3 center = transform.position;
        if (mainRenderer != null) center = mainRenderer.bounds.center;

        for (int i = 0; i < shardCount; i++)
        {
            // 다양한 모양의 파편 생성을 위해 Cube와 Sphere를 섞어서 사용 가능 (혹은 임의의 스케일 조정)
            GameObject shard = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            shard.name = "Shard_" + i;
            
            Vector3 randomOffset = Random.insideUnitSphere * 1.5f; // 오프셋 범위 확장
            shard.transform.position = center + randomOffset;
            
            // 파편 크기를 훨씬 크게 설정하고 랜덤성 부여
            float size = Random.Range(shardScale * 0.7f, shardScale * 1.8f);
            shard.transform.localScale = new Vector3(size, size * 0.5f, size * 1.2f); // 불규칙한 모양
            shard.transform.rotation = Random.rotation;

            if (mat != null)
            {
                shard.GetComponent<Renderer>().material = mat;
            }

            Rigidbody rb = shard.GetComponent<Rigidbody>();
            if (rb == null) rb = shard.AddComponent<Rigidbody>();
            
            Vector3 forceDir = (shard.transform.position - center).normalized;
            // 위쪽으로 솟구치는 힘을 더해 더 역동적으로 표현
            rb.AddForce((forceDir + Vector3.up * 0.5f) * explosionForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * explosionForce * 2f, ForceMode.Impulse);

            Destroy(shard, destroyDelay + Random.Range(0, 1.5f));
        }

        // 4. Clean up original object
        Destroy(gameObject, destroyDelay + 2f);
    }

    /*
    private void OnCollisionEnter(Collision collision)
    {
        // 골렘(Boss)과 충돌 시 즉시 파편화
        if (collision.gameObject.name.Contains("Boss") || collision.gameObject.GetComponent<BossFollow>() != null)
        {
            StartCrumbling();
        }
    }
    */
    }

    // 먼지 효과를 위한 간단한 헬퍼 클래스
    public class DustEffect : MonoBehaviour
    {
    private float timer = 0f;
    private float duration = 1.5f;
    private Material mat;
    private Vector3 initialScale;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        initialScale = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        // 크기 확장
        transform.localScale = initialScale * (1f + progress * 4f);

        // 투명도 감소 (페이드 아웃)
        if (mat != null)
        {
            Color c = mat.color;
            c.a = Mathf.Lerp(0.5f, 0f, progress);
            mat.color = c;
        }
    }
    }

