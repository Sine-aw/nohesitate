using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Boss : MonoBehaviour
{
    [Header("기본 스탯")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("디버프 상태")]
    [SerializeField] private float damageMultiplier = 1.0f;
    [SerializeField] private bool isStunned = false;

    [Header("UI 연결")]
    [SerializeField] private Slider healthSlider;

    private Coroutine stunCoroutine;
    private Coroutine debuffCoroutine;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float amount)
    {
        float finalDamage = amount * damageMultiplier;

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"피격 피해: {finalDamage} (배율 x{damageMultiplier}) / 남은 체력: {currentHealth}");
        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyDamageMultiplierForDuration(float multiplier, float duration)
    {
        if (debuffCoroutine != null)
        {
            StopCoroutine(debuffCoroutine);
        }
        debuffCoroutine = StartCoroutine(DamageMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator DamageMultiplierRoutine(float multiplier, float duration)
    {
        damageMultiplier = multiplier;
        Debug.Log($"{duration}초 동안 피격 데미지 증폭 x{damageMultiplier} 적용");

        yield return new WaitForSeconds(duration);

        damageMultiplier = 1.0f;
        Debug.Log("피격 데미지 증폭 디버프 해제");
    }

    public void StunForDuration(float duration)
    {
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }
        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        Debug.Log($"보스 {duration}초 동안 스턴");

        yield return new WaitForSeconds(duration);

        isStunned = false;
        Debug.Log("보스 스턴 해제");
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
        Debug.Log("보스 처치");
        gameObject.SetActive(false);
    }

    public bool IsStunned => isStunned;
}