using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Boss : MonoBehaviour
{
    [Header("기본 스탯")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("타겟 플레이어")]
    [SerializeField] private Transform playerTransform;

    [Header("스킬 프리팹")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private Transform spawnPoint; // 구체/쫄 소환 위치 (없을 시 보스 위치 사용)

    [Header("2페이즈 및 이펙트 설정")]
    [SerializeField] private GameObject phase2EnterEffect; // 2페이즈 진입 시 퍼지는 이펙트
    [SerializeField] private GameObject phase2AuraEffect;  // 2페이즈 동안 켜지는 지속 오라 이펙트
    [SerializeField] private float phase2DamageBonus = 10f;

    [Header("UI 연결")]
    [SerializeField] private Slider healthSlider;

    private float damageMultiplier = 1.0f;
    private bool isStunned = false;
    private bool isPhase2 = false;

    private float minionSkillTimer = 0f;
    private float orbSkillTimer = 0f;

    [Header("스킬 쿨타임(초)")]
    [SerializeField] private float minionSkillCooldown = 8f;
    [SerializeField] private float orbSkillCooldown = 4f;

    private Coroutine stunCoroutine;
    private Coroutine debuffCoroutine;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (phase2AuraEffect != null) phase2AuraEffect.SetActive(false);
    }

    private void Update()
    {
        if (currentHealth <= 0 || isStunned) return;

        // 스킬 쿨타임 계산
        minionSkillTimer += Time.deltaTime;
        orbSkillTimer += Time.deltaTime;

        if (orbSkillTimer >= orbSkillCooldown)
        {
            CastOrbSkill();
            orbSkillTimer = 0f;
        }

        if (minionSkillTimer >= minionSkillCooldown)
        {
            CastMinionSkill();
            minionSkillTimer = 0f;
        }
    }

    // 1. 구체 발사 스킬
    private void CastOrbSkill()
    {
        if (orbPrefab == null) return;

        // playerTransform이 비어있으면 재생성/재검색
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
            else return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

        // Y축 높이를 맞춘 상태에서 플레이어 방향 계산
        Vector3 targetPos = new Vector3(playerTransform.position.x, spawnPos.y, playerTransform.position.z);
        Vector3 dir = (targetPos - spawnPos).normalized;

        if (dir == Vector3.zero) dir = transform.forward; // 방향을 못 잡을 경우 보스 정면으로 발사

        GameObject orbObj = Instantiate(orbPrefab, spawnPos, Quaternion.LookRotation(dir));
        if (orbObj.TryGetComponent<LichOrb>(out var orb))
        {
            orb.Init(dir, isPhase2 ? phase2DamageBonus : 0f);
        }
    }

    // 2. 쫄다구 3마리 소환 스킬
    private void CastMinionSkill()
    {
        if (minionPrefab == null || playerTransform == null) return;

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

        for (int i = 0; i < 3; i++)
        {
            // 약간의 렌덤 위치 오프셋 추가
            Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            GameObject minionObj = Instantiate(minionPrefab, spawnPos + offset, Quaternion.identity);

            if (minionObj.TryGetComponent<LichMinion>(out var minion))
            {
                minion.Init(playerTransform, isPhase2 ? phase2DamageBonus : 0f);
            }
        }
    }

    // 보스 피격
    public void TakeDamage(float amount)
    {
        float finalDamage = amount * damageMultiplier;
        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateUI();

        // 체력 50% 이하 및 2페이즈 미진입 시 전환
        if (!isPhase2 && (currentHealth / maxHealth) <= 0.5f)
        {
            EnterPhase2();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void EnterPhase2()
    {
        isPhase2 = true;
        Debug.Log("[BOSS] 2페이즈 진입!");

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

        // 2페이즈 진입 1회성 이펙트
        if (phase2EnterEffect != null)
        {
            Instantiate(phase2EnterEffect, spawnPos, Quaternion.identity);
        }

        // 2페이즈 지속 오라 이펙트 켜기
        if (phase2AuraEffect != null)
        {
            phase2AuraEffect.SetActive(true);
        }
    }

    public void ApplyDamageMultiplierForDuration(float multiplier, float duration)
    {
        if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
        debuffCoroutine = StartCoroutine(DamageMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator DamageMultiplierRoutine(float multiplier, float duration)
    {
        damageMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        damageMultiplier = 1.0f;
    }

    public void StunForDuration(float duration)
    {
        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    private void UpdateUI()
    {
        if (healthSlider != null) healthSlider.value = currentHealth / maxHealth;
    }

    private void Die()
    {
        gameObject.SetActive(false);
    }
}