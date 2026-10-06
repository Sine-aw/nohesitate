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
    [Header("발판 설정")]
    [SerializeField] private PlateType plateType;
    [SerializeField] private Boss targetBoss;

    [Header("수치 설정")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float playerShieldAmount = 20f;
    [SerializeField] private float greenDamageMultiplier = 2f;
    [SerializeField] private float greenDebuffDuration = 5f;
    [SerializeField] private float stunDuration = 3f;

    [Header("소멸 연출 설정")]
    [SerializeField] private float destroyDelay = 0.5f;

    private Renderer plateRenderer;
    private Material plateMaterial;
    private Color activeColor;
    private bool isActivated = false;

    private void Start()
    {
        plateRenderer = GetComponent<Renderer>();
        if (plateRenderer != null)
        {
            plateMaterial = plateRenderer.material;
        }

        InitPlateColor();
    }

    private void InitPlateColor()
    {
        switch (plateType)
        {
            case PlateType.Red_AttackBoss:
                activeColor = Color.red * 4f;
                break;
            case PlateType.Blue_ShieldPlayer:
                activeColor = Color.blue * 4f;
                break;
            case PlateType.Green_DebuffBoss:
                activeColor = Color.green * 4f;
                break;
            case PlateType.Black_StunBoss:
                activeColor = Color.white * 2f;
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;
        if (!other.CompareTag("Player")) return;

        isActivated = true;

        SetEmission(activeColor);

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

        StartCoroutine(DisappearRoutine());
    }

    private IEnumerator DisappearRoutine()
    {
        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    private void SetEmission(Color color)
    {
        if (plateMaterial != null && plateMaterial.HasProperty("_EmissionColor"))
        {
            plateMaterial.SetColor("_EmissionColor", color);
            plateMaterial.EnableKeyword("_EMISSION");
        }
    }
}