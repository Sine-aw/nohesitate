using UnityEngine;

public class LichOrb : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float lifeTime = 5f;

    private Vector3 moveDirection;

    public void Init(Vector3 direction, float damageBonus)
    {
        moveDirection = direction.normalized;
        damage += damageBonus;
        Destroy(gameObject, lifeTime); // 일정 시간 후 자동 소멸
    }

    private void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
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