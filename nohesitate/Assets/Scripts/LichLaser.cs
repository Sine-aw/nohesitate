using UnityEngine;

public class LichLaser : MonoBehaviour
{
    [SerializeField] private float damage = 20f;
    [SerializeField] private float lifeTime = 1.5f;

    public void Init(float damageBonus)
    {
        damage += damageBonus;
        Destroy(gameObject, lifeTime);
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