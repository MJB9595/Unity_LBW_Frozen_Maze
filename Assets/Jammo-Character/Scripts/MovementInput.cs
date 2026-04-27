// MovementInput.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovementInput : MonoBehaviour
{
    private PlayerKnockback knockbackScript;
    public float Velocity;
    [Space]

    public float InputX;
    public float InputZ;
    public Vector3 desiredMoveDirection;
    public bool blockRotationPlayer;
    public float desiredRotationSpeed = 0.1f;
    public Animator anim;
    public float Speed;
    public float allowPlayerRotation = 0.1f;
    public Camera cam;
    public CharacterController controller;
    public bool isGrounded;

    [Header("Animation Smoothing")]
    [Range(0, 1f)] public float HorizontalAnimSmoothTime = 0.2f;
    [Range(0, 1f)] public float VerticalAnimTime = 0.2f;
    [Range(0, 1f)] public float StartAnimTime = 0.3f;
    [Range(0, 1f)] public float StopAnimTime = 0.15f;

    public float verticalVel;
    private Vector3 moveVector;

    void Awake()
    {
        knockbackScript = GetComponent<PlayerKnockback>();
    }

    void Start()
    {
        anim = this.GetComponent<Animator>();
        cam = Camera.main;
        controller = this.GetComponent<CharacterController>();
    }

    void Update()
    {
        // ✅ 중력은 넉백 중에도 항상 처리
        ApplyGravity();

        // 넉백 중이면 이동/회전 입력만 차단
        if (knockbackScript != null && knockbackScript.knockbackTimer > 0)
        {
            // 애니메이션 블렌드 값을 0으로 줄여서 뛰는 애니메이션을 멈춤
            anim.SetFloat("Blend", 0f, StopAnimTime, Time.deltaTime);
            return;
        }

        InputMagnitude();
    }

    // ✅ 중력 처리를 별도 메서드로 분리
    void ApplyGravity()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVel < 0)
        {
            // ✅ 착지 시 중력 누적값 리셋
            verticalVel = -1f; // 0이 아닌 약한 음수로 바닥 밀착 유지
        }
        else if (!isGrounded)
        {
            verticalVel -= 20f * Time.deltaTime; // 중력 가속
        }

        moveVector = new Vector3(0, verticalVel * Time.deltaTime, 0);
        controller.Move(moveVector);
    }

    void PlayerMoveAndRotation()
    {
        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");

        var forward = cam.transform.forward;
        var right = cam.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        desiredMoveDirection = forward * InputZ + right * InputX;

        if (!blockRotationPlayer)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed);
            controller.Move(desiredMoveDirection * Time.deltaTime * Velocity);
        }
    }

    public void LookAt(Vector3 pos)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(pos), desiredRotationSpeed);
    }

    public void RotateToCamera(Transform t)
    {
        var forward = cam.transform.forward;
        desiredMoveDirection = forward;
        t.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed);
    }

    void InputMagnitude()
    {
        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");

        Speed = new Vector2(InputX, InputZ).sqrMagnitude;

        if (Speed > allowPlayerRotation)
        {
            anim.SetFloat("Blend", Speed, StartAnimTime, Time.deltaTime);
            PlayerMoveAndRotation();
        }
        else if (Speed < allowPlayerRotation)
        {
            anim.SetFloat("Blend", Speed, StopAnimTime, Time.deltaTime);
        }
    }
}