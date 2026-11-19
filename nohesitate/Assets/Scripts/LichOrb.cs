using UnityEngine;

public class LichOrb : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private float baseDamage = 15f;
    [SerializeField] private float lifeTime = 8f;

    private Rigidbody rb;
    private float finalDamage;
    private bool hasHit = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        finalDamage = baseDamage;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Init(Vector3 dir, float damageBonus, bool isParabola = false)
    {
        finalDamage = Mathf.Max(0f, baseDamage + damageBonus);

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
        finalDamage = Mathf.Max(0f, baseDamage + damageBonus);

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

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDealDamage(collision.gameObject);
    }

    private void TryDealDamage(GameObject hitObject)
    {
        if (hasHit) return;

        if (hitObject.CompareTag("Player"))
        {
            hasHit = true;
            PlayerHealth playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(finalDamage);
            }
            Destroy(gameObject);
        }
    }
}