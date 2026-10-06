using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Normal,
    PickUp,
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    [Header("이동 설정")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("점프 및 중력 설정")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;
    private PlayerState currentState = PlayerState.Normal;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (currentState == PlayerState.Normal)
        {
            HandleJump(keyboard);
            HandleMovement(keyboard);
        }

        ApplyGravity();
    }

    private void HandleJump(Keyboard keyboard)
    {
        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);

            if (animator != null)
            {
                animator.SetTrigger("jump");
            }
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        if (controller != null)
        {
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }

    private void HandleMovement(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;

        if (keyboard.aKey.isPressed)
            input.x -= 1f;
        if (keyboard.dKey.isPressed)
            input.x += 1f;
        if (keyboard.sKey.isPressed)
            input.y -= 1f;
        if (keyboard.wKey.isPressed)
            input.y += 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : Vector3.right;

        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * input.y + cameraRight * input.x;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        bool isRunning = keyboard.leftShiftKey.isPressed;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        if (controller != null)
        {
            controller.Move(moveDirection * currentSpeed * Time.deltaTime);

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (animator != null)
            {
                float moveMagnitude = moveDirection.magnitude * (isRunning ? 2f : 1f);
                animator.SetFloat("speed", moveMagnitude);
            }
        }
    }

    public void ChangeState(PlayerState newState)
    {
        currentState = newState;

        if (currentState != PlayerState.Normal && animator != null)
        {
            animator.SetFloat("speed", 0);
        }

        Debug.Log("현재 상태 : " + currentState);
    }
}