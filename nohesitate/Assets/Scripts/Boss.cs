using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Boss : MonoBehaviour
{
    [Header("기본 스탯 (최대 체력 100)")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("타겟 플레이어")]
    [SerializeField] private Transform playerTransform;

    [Header("스킬 프리팹 (1페이즈 / 공용)")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("스킬 프리팹 (2페이즈 전용)")]
    [SerializeField] private GameObject laserWarningPrefab;
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private GameObject giantOrbPrefab;

    [Header("2페이즈 및 이펙트 설정")]
    [SerializeField] private GameObject phase2EnterEffect;
    [SerializeField] private GameObject phase2AuraEffect;
    [SerializeField] private float phase2DamageBonus = 10f;

    [Header("UI 연결")]
    [SerializeField] private Slider healthSlider;

    private float damageMultiplier = 1.0f;
    private bool isStunned = false;
    private bool isPhase2 = false;

    private float minionSkillTimer = 0f;
    private float orbSkillTimer = 0f;
    private float phase2SkillTimer = 0f;
    private int orbCastCount = 0;

    [Header("스킬 쿨타임(초)")]
    [SerializeField] private float minionRespawnDelay = 15f;
    [SerializeField] private float orbSkillCooldown = 4f;
    [SerializeField] private float phase2SpecialCooldown = 6f;

    private bool activeMinionsExist = false;
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
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
        {
            TakeDamage(10f);
        }

        if (currentHealth <= 0 || isStunned) return;

        orbSkillTimer += Time.deltaTime;
        if (orbSkillTimer >= orbSkillCooldown)
        {
            orbCastCount++;

            if (isPhase2 && orbCastCount % 3 == 0)
            {
                CastGiantOrbSkill();
            }
            else
            {
                CastOrbSkill();
            }

            orbSkillTimer = 0f;
        }

        if (!activeMinionsExist)
        {
            minionSkillTimer += Time.deltaTime;
            if (minionSkillTimer >= minionRespawnDelay)
            {
                CastMinionSkill();
                minionSkillTimer = 0f;
            }
        }

        if (isPhase2)
        {
            phase2SkillTimer += Time.deltaTime;
            if (phase2SkillTimer >= phase2SpecialCooldown)
            {
                if (Random.value > 0.5f)
                {
                    CastLaserSkill();
                }
                else
                {
                    TeleportAttack();
                }
                phase2SkillTimer = 0f;
            }
        }
    }

    private void CastOrbSkill()
    {
        if (orbPrefab == null) return;
        CheckPlayer();
        if (playerTransform == null) return;

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 targetPos = new Vector3(playerTransform.position.x, spawnPos.y, playerTransform.position.z);
        Vector3 dir = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        GameObject orbObj = Instantiate(orbPrefab, spawnPos, Quaternion.LookRotation(dir));
        if (orbObj.TryGetComponent<LichOrb>(out var orb))
        {
            orb.Init(dir, isPhase2 ? phase2DamageBonus : 0f);
        }
    }

    private void CastGiantOrbSkill()
    {
        if (giantOrbPrefab == null) return;
        CheckPlayer();
        if (playerTransform == null) return;

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 targetPos = new Vector3(playerTransform.position.x, spawnPos.y, playerTransform.position.z);
        Vector3 dir = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        GameObject giantOrb = Instantiate(giantOrbPrefab, spawnPos, Quaternion.LookRotation(dir));
        if (giantOrb.TryGetComponent<LichOrb>(out var orb))
        {
            orb.Init(dir, phase2DamageBonus + 15f);
        }
    }

    private void CastMinionSkill()
    {
        if (minionPrefab == null) return;
        CheckPlayer();
        if (playerTransform == null) return;

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        activeMinionsExist = true;

        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            GameObject minionObj = Instantiate(minionPrefab, spawnPos + offset, Quaternion.identity);

            if (minionObj.TryGetComponent<LichMinion>(out var minion))
            {
                minion.Init(playerTransform, isPhase2 ? phase2DamageBonus : 0f);
            }
        }

        StartCoroutine(CheckMinionsAliveRoutine());
    }

    private IEnumerator CheckMinionsAliveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.5f);
            if (GameObject.FindGameObjectsWithTag("Minion").Length == 0)
            {
                minionSkillTimer = 0f;
                activeMinionsExist = false;
                break;
            }
        }
    }

    private void CastLaserSkill()
    {
        if (laserWarningPrefab == null || laserPrefab == null) return;
        CheckPlayer();
        if (playerTransform == null) return;

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 targetPos = new Vector3(playerTransform.position.x, spawnPos.y, playerTransform.position.z);
        Vector3 dir = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        float laserHalfLength = 7.5f;
        Vector3 adjustedSpawnPos = spawnPos + (dir * laserHalfLength);

        GameObject warningObj = Instantiate(laserWarningPrefab, adjustedSpawnPos, Quaternion.LookRotation(dir));
        if (warningObj.TryGetComponent<LaserWarningLine>(out var warningLine))
        {
            warningLine.Init(laserPrefab, phase2DamageBonus);
        }
    }

    private void TeleportAttack()
    {
        CheckPlayer();
        if (playerTransform == null) return;

        Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
        Vector3 teleportPos = playerTransform.position + randomOffset;

        transform.position = teleportPos;

        float meleeDamage = 25f + phase2DamageBonus;
        if (Vector3.Distance(transform.position, playerTransform.position) <= 3.5f)
        {
            PlayerHealth ph = playerTransform.GetComponentInParent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(meleeDamage);
        }
    }

    private void CheckPlayer()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
    }

    public void TakeDamage(float amount)
    {
        float finalDamage = amount * damageMultiplier;
        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateUI();

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
        Debug.Log("2페이즈 진입");

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

        if (phase2EnterEffect != null)
        {
            Instantiate(phase2EnterEffect, spawnPos, Quaternion.identity);
        }

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