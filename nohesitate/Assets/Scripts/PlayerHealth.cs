using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("체력 설정")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("쉴드")]
    [SerializeField] private float shield = 0f;

    [Header("UI 연결")]
    [SerializeField] private Slider healthSlider;

    private PlayerController playerController;
    private bool isDead = false;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        float finalDamage = amount;

        // 쉴드가 먼저 피해를 흡수[cite: 8]
        if (shield > 0)
        {
            if (shield >= finalDamage)
            {
                shield -= finalDamage;
                finalDamage = 0;
            }
            else
            {
                finalDamage -= shield;
                shield = 0;
            }
        }

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"남은 체력: {currentHealth} / 쉴드: {shield}");
        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void AddShield(float amount)
    {
        if (isDead) return;
        shield += amount;
        Debug.Log($"현재 쉴드: {shield}");
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
    }

    private void Die()
    {
        isDead = true;

        if (playerController != null)
        {
            playerController.enabled = false;
        }
    }

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Shield => shield;
    public bool IsDead => isDead;
}