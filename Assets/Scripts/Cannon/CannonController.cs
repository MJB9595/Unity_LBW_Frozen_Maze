using UnityEngine;

/// <summary>
/// 대포 조준/발사/탑승 제어.
/// F 탑승 → WASD 조준(좌우=YawPivot, 상하=ElevationPivot) → Space 발사 → F/Esc 하차.
/// 탑승 중에는 플레이어 이동(MovementInput)과 플레이어 카메라를 잠그고 대포 카메라로 전환한다.
/// </summary>
public class CannonController : MonoBehaviour
{
    [Header("Pivots / Muzzle (필수 연결)")]
    public Transform yawPivot;          // 좌우 선회 (로컬 Y)
    public Transform elevationPivot;    // 상하 고각 (로컬 X)
    public Transform muzzlePoint;       // 포구: 포탄 생성 위치/방향 (forward = 발사 방향)

    [Header("조준 한계 / 속도")]
    public float yawSpeed = 45f;
    public float pitchSpeed = 35f;
    public float minYaw = -75f;
    public float maxYaw = 75f;
    public float minElevation = -5f;    // 고각(시각적 올림) 최소
    public float maxElevation = 40f;    // 고각 최대

    [Header("발사")]
    public GameObject cannonballPrefab;
    public float muzzleSpeed = 70f;
    public float fireCooldown = 1.5f;
    public float projectileGravity = -9.81f;

    [Header("발사음")]
    public AudioSource audioSource;     // 발사음 재생용 (대포에 부착)
    public AudioClip fireSfx;           // 발사 효과음
    [Range(0f, 1f)] public float fireVolume = 1f;

    [Header("카메라")]
    public Camera cannonCamera;         // 대포 전용 카메라 (자식). 비탑승 시 비활성

    [Header("탑승 거치 포즈(사수)")]
    public float gunnerBackDistance = 2.8f;  // 대포(YawPivot) 뒤로 떨어진 거리
    public float gunnerSideOffset   = 0f;    // 좌우 보정(+ = 사수 기준 오른쪽)
    public float gunnerHeightOffset = 0f;    // 발 높이 보정(받침대 윗면 기준)
    public bool  gunnerFollowYaw    = true;  // 좌우 조준 시 사수도 같이 회전/이동

    [Header("예상 탄도선")]
    public TrajectoryPredictor predictor;

    [Header("상태(읽기용)")]
    public bool isMounted;

    float yawAngle;
    float pitchAngle;
    float cooldownTimer;
    float mountLock;                    // 탑승 직후 F 중복 입력 무시

    // 플레이어 측 캐시
    MovementInput playerMovement;
    Behaviour playerCameraController;   // Mario64Camera 등
    Camera playerCamera;
    Transform playerTf;                 // 사수(플레이어) 트랜스폼
    Animator playerAnim;                // 사수 애니메이터(걷기 Blend 정지용)

    void Awake()
    {
        playerMovement = FindObjectOfType<MovementInput>();
        if (cannonCamera != null) cannonCamera.enabled = false;
        if (predictor != null) predictor.Hide();
    }

    void Update()
    {
        if (mountLock > 0f) mountLock -= Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (!isMounted) return;

        HandleAim();
        UpdatePredictor();
        UpdateGunnerPose();   // 사수를 대포 뒤에 고정 + 걷기 애니메이션 정지

        if (Input.GetKeyDown(KeyCode.Space) && cooldownTimer <= 0f)
            Fire();

        if (mountLock <= 0f && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape)))
            Dismount();
    }

    void HandleAim()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        yawAngle = Mathf.Clamp(yawAngle + h * yawSpeed * Time.deltaTime, minYaw, maxYaw);
        pitchAngle = Mathf.Clamp(pitchAngle + v * pitchSpeed * Time.deltaTime, minElevation, maxElevation);

        if (yawPivot != null)
            yawPivot.localRotation = Quaternion.Euler(0f, yawAngle, 0f);
        if (elevationPivot != null)
            elevationPivot.localRotation = Quaternion.Euler(-pitchAngle, 0f, 0f); // -X = 포구 상승
    }

    void UpdatePredictor()
    {
        if (predictor == null || muzzlePoint == null) return;
        predictor.Show();
        predictor.Simulate(muzzlePoint.position, muzzlePoint.forward * muzzleSpeed,
                           new Vector3(0f, projectileGravity, 0f));
    }

    void Fire()
    {
        if (cannonballPrefab == null || muzzlePoint == null) return;
        cooldownTimer = fireCooldown;

        if (fireSfx != null && audioSource != null)
            audioSource.PlayOneShot(fireSfx, fireVolume);

        var ball = Instantiate(cannonballPrefab, muzzlePoint.position, muzzlePoint.rotation);
        var rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = muzzlePoint.forward * muzzleSpeed;
#else
            rb.velocity = muzzlePoint.forward * muzzleSpeed;
#endif
        }
    }

    public void Mount()
    {
        if (isMounted) return;
        isMounted = true;
        mountLock = 0.25f;

        if (playerMovement != null)
        {
            playerTf = playerMovement.transform;
            playerAnim = playerMovement.anim != null ? playerMovement.anim
                                                     : playerMovement.GetComponent<Animator>();
            playerMovement.enabled = false;
            playerCamera = playerMovement.cam != null ? playerMovement.cam : Camera.main;
        }
        // 걷기 애니메이션 즉시 정지 + 대포 뒤 거치 자세로 이동
        if (playerAnim != null) playerAnim.SetFloat("Blend", 0f);
        PlaceGunner();
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null)
        {
            playerCameraController = playerCamera.GetComponent("Mario64Camera") as Behaviour;
            if (playerCameraController != null) playerCameraController.enabled = false;
            playerCamera.enabled = false; // AudioListener는 남겨둠
        }
        if (cannonCamera != null) cannonCamera.enabled = true;
    }

    public void Dismount()
    {
        if (!isMounted) return;
        isMounted = false;

        if (cannonCamera != null) cannonCamera.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;
        if (playerCameraController != null) playerCameraController.enabled = true;
        if (playerMovement != null) playerMovement.enabled = true;
        if (predictor != null) predictor.Hide();
    }

    /// <summary>탑승 중 매 프레임 사수를 대포 뒤에 고정하고 걷기 애니메이션을 끈다.</summary>
    void UpdateGunnerPose()
    {
        if (playerAnim != null) playerAnim.SetFloat("Blend", 0f); // 멈춰있는 동안 idle 유지
        if (gunnerFollowYaw) PlaceGunner();                        // 좌우 조준에 맞춰 따라 회전
    }

    /// <summary>플레이어를 YawPivot(대포) 뒤쪽, 발사 방향을 바라보게 배치한다.</summary>
    void PlaceGunner()
    {
        if (playerTf == null) return;
        Transform pivot = yawPivot != null ? yawPivot : transform;

        Vector3 fwd = pivot.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) { fwd = transform.forward; fwd.y = 0f; }
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd); // 좌우 보정용 수평 오른쪽

        Vector3 stand = pivot.position
                        - fwd * gunnerBackDistance
                        + right * gunnerSideOffset;
        stand.y = pivot.position.y + gunnerHeightOffset;

        playerTf.position = stand;
        playerTf.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }
}
