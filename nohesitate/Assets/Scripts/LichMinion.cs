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
        // 만약 Init에서 playerTarget을 넘겨받지 못했다면 직접 Player 태그 검색
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
        // 여전히 타겟이 없으면 이동하지 않음
        if (playerTarget == null) return;

        // 플레이어 방향 계산 (Y축 높이는 고정하여 평지 추적)
        Vector3 targetPosition = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
        Vector3 direction = (targetPosition - transform.position).normalized;

        // 플레이어 쪽으로 이동 및 회전
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