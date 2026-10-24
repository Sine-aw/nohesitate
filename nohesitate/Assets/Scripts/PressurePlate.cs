using System.Collections;
using UnityEngine;

public enum PlateType
{
    Red_AttackBoss,
    Blue_ShieldPlayer,
    Green_DebuffBoss,
    Black_StunBoss
}

public class PressurePlate : MonoBehaviour
{
    private static PlateType? lastSteppedPlateType = null;

    [Header("발판 설정")]
    [SerializeField] private PlateType plateType;
    [SerializeField] private Boss targetBoss;

    [Header("아트 모델링 연결")]
    [SerializeField] private GameObject offModel;
    [SerializeField] private GameObject onModel;

    [Header("수치 설정")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float playerShieldAmount = 20f;
    [SerializeField] private float greenDamageMultiplier = 2f;
    [SerializeField] private float greenDebuffDuration = 5f;
    [SerializeField] private float stunDuration = 3f;

    private bool isPlayerStepping = false;

    private void Start()
    {
        SetModelActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (lastSteppedPlateType.HasValue && lastSteppedPlateType.Value == plateType)
        {
            Debug.Log($"[발판] {plateType} 발판은 연속으로 밟을 수 없습니다! 다른 발판을 먼저 밟으세요.");
            return;
        }

        isPlayerStepping = true;
        lastSteppedPlateType = plateType;
        SetModelActive(true);

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

        isPlayerStepping = false;
        SetModelActive(false);
    }

    private void SetModelActive(bool isStepped)
    {
        if (offModel != null) offModel.SetActive(!isStepped);
        if (onModel != null) onModel.SetActive(isStepped);
    }
}