using UnityEngine;

public class LichOrb : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Init(Vector3 dir, float damageBonus, bool isParabola = false)
    {
        if (rb != null)
        {
            if (isParabola)
            {
                rb.useGravity = true;
                rb.linearVelocity = (dir + Vector3.up * 0.8f).normalized * 15f;
            }
            else
            {
                rb.useGravity = false;
                rb.linearVelocity = dir * 15f;
            }
        }
    }

    public void InitParabolaToTarget(Vector3 targetPos, float damageBonus)
    {
        if (rb == null) return;

        rb.useGravity = true;

        Vector3 startPos = transform.position;
        float gravity = Physics.gravity.y;

        float heightOffset = 6f;

        float displacementY = targetPos.y - startPos.y;
        Vector3 displacementXZ = new Vector3(targetPos.x - startPos.x, 0, targetPos.z - startPos.z);

        float time = Mathf.Sqrt(-2f * heightOffset / gravity) + Mathf.Sqrt(2f * (displacementY - heightOffset) / gravity);
        if (float.IsNaN(time) || time <= 0f) time = 1.5f;

        Vector3 velocityXZ = displacementXZ / time;
        float velocityY = (displacementY - 0.5f * gravity * time * time) / time;

        Vector3 launchVelocity = velocityXZ + Vector3.up * velocityY;

        rb.linearVelocity = launchVelocity;
    }
}