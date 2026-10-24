using UnityEngine;

public class LichMinion : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float damage = 10f;

    private Transform playerTarget;

    public void Init(Transform target, float damageBonus)
    {
        playerTarget = target;
        damage += damageBonus;
    }

    private void Start()
    {
        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTarget == null) return;

        Vector3 targetPosition = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
        Vector3 direction = (targetPosition - transform.position).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}