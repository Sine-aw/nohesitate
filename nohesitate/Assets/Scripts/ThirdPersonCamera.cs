using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("따라갈 대상")]
    [SerializeField] private Transform target;

    [Header("카메라 위치")]
    [SerializeField] private float distance = 7f;
    [SerializeField] private float height = 1.5f;

    [Header("벽 충돌 방지")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float cameraRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.15f;

    [Header("마우스 회전")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("마우스 스크롤")]
    [SerializeField] private float zoomSpeed = 0.01f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Time.timeScale == 0f || (GameManager.Instance != null && GameManager.Instance.IsInputBlocked))
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        float scroll = mouse.scroll.ReadValue().y;

        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 lookPoint = target.position + Vector3.up * height;
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 castDirection = (orbitRotation * Vector3.back).normalized;

        float currentDistance = distance;

        if (Physics.SphereCast(
            lookPoint,
            cameraRadius,
            castDirection,
            out RaycastHit hit,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            currentDistance = Mathf.Clamp(hit.distance - collisionBuffer, 0.2f, distance);
        }

        transform.position = lookPoint + castDirection * currentDistance;
        transform.LookAt(lookPoint);
    }
}