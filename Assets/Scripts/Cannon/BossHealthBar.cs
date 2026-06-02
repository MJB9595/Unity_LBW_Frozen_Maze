using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 보스 머리 위 월드 스페이스 HP 바.
/// Health.OnHealthChanged 를 구독해 fill 을 DOTween 으로 부드럽게 감소시키고,
/// 항상 카메라를 향하도록 빌보드 처리한다.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("연결")]
    public Health health;               // 보스 Health
    public Image fillImage;             // Image Type=Filled (Horizontal)
    public Transform followTarget;      // 보통 보스 루트
    public CanvasGroup canvasGroup;     // 선택: 페이드용

    [Header("배치 / 연출")]
    public float worldHeight = 28f;     // followTarget 기준 머리 위 높이(월드 오프셋 y)
    public float tweenDuration = 0.3f;
    public bool hideWhenFull = false;

    Camera viewCam;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDeath += HandleDeath;
            SetImmediate(Normalized(health.CurrentHealth));
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDeath -= HandleDeath;
        }
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + Vector3.up * worldHeight;

        var cam = ActiveCamera();
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    float Normalized(float current)
    {
        float max = health != null ? Mathf.Max(health.MaxHealth, 0.0001f) : 1f;
        return Mathf.Clamp01(current / max);
    }

    void SetImmediate(float n)
    {
        if (fillImage != null) fillImage.fillAmount = n;
        if (hideWhenFull && canvasGroup != null) canvasGroup.alpha = n >= 0.999f ? 0f : 1f;
    }

    void HandleHealthChanged(float current)
    {
        float n = Normalized(current);
        if (fillImage != null)
        {
            DOTween.Kill(fillImage);
            DOTween.To(() => fillImage.fillAmount, x => fillImage.fillAmount = x, n, tweenDuration)
                   .SetEase(Ease.OutQuad).SetTarget(fillImage);
        }
        if (canvasGroup != null && hideWhenFull)
            canvasGroup.alpha = n >= 0.999f ? 0f : 1f;
    }

    void HandleDeath()
    {
        if (canvasGroup != null)
            canvasGroup.DOFade(0f, 0.5f);
    }

    Camera ActiveCamera()
    {
        if (viewCam != null && viewCam.isActiveAndEnabled) return viewCam;
        if (Camera.main != null) { viewCam = Camera.main; return viewCam; }
        foreach (var c in Camera.allCameras)
            if (c.isActiveAndEnabled) { viewCam = c; return viewCam; }
        return null;
    }
}
