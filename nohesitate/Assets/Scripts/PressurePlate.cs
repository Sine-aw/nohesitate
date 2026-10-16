using System.Collections;
using UnityEngine;

public enum PlateType
{
    Red_AttackBoss,     // 빨간색: 보스에게 피해 (attack)
    Blue_ShieldPlayer,  // 파란색: 플레이어에게 쉴드 (shield)
    Green_DebuffBoss,   // 녹색: 보스가 받는 피해 증가 (posion/green)
    Black_StunBoss      // 검은색: 보스 스턴 (stun)
}

public class PressurePlate : MonoBehaviour
{
    [Header("발판 설정")]
    [SerializeField] private PlateType plateType;
    [SerializeField] private Boss targetBoss;

    [Header("아트 모델링 연결")]
    [SerializeField] private GameObject offModel; // 기본 모델링 (0번: attack0, shield0 등)
    [SerializeField] private GameObject onModel;  // 밟았을 때 모델링 (1번: attack1, shield1 등)

    [Header("수치 설정")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float playerShieldAmount = 20f;
    [SerializeField] private float greenDamageMultiplier = 2f;
    [SerializeField] private float greenDebuffDuration = 5f;
    [SerializeField] private float stunDuration = 3f;

    private void Start()
    {
        // 시작할 때 0번 모델링은 켜고, 1번 모델링은 꺼둡니다.
        SetModelActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 1. 모델링 교체 (기본 끄고, 밟은 상태 모델링 켜기)
        SetModelActive(true);

        // 2. 기존 기믹 기능 실행
        if (plateType == PlateType.Blue_ShieldPlayer)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.AddShield(playerShieldAmount);
            }
        }
        else if (targetBoss != null)
        {
            switch (plateType)
            {
                case PlateType.Red_AttackBoss:
                    targetBoss.TakeDamage(attackDamage);
                    break;

                case PlateType.Green_DebuffBoss:
                    targetBoss.ApplyDamageMultiplierForDuration(greenDamageMultiplier, greenDebuffDuration);
                    break;

                case PlateType.Black_StunBoss:
                    targetBoss.StunForDuration(stunDuration);
                    break;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 발판에서 발을 떼면 다시 기본 모델링으로 원복
        SetModelActive(false);
    }

    private void SetModelActive(bool isStepped)
    {
        if (offModel != null) offModel.SetActive(!isStepped);
        if (onModel != null) onModel.SetActive(isStepped);
    }
}