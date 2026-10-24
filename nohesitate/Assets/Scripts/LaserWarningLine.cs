using UnityEngine;
using System.Collections;

public class LaserWarningLine : MonoBehaviour
{
    [Header("발사될 실제 레이저 프리팹")]
    [SerializeField] private GameObject realLaserPrefab;
    [SerializeField] private float warningDuration = 1.0f;
    private float damageBonus;

    public void Init(GameObject laserPrefab, float bonus)
    {
        realLaserPrefab = laserPrefab;
        damageBonus = bonus;
        StartCoroutine(WarningRoutine());
    }

    private IEnumerator WarningRoutine()
    {
        yield return new WaitForSeconds(warningDuration);

        if (realLaserPrefab != null)
        {
            GameObject laser = Instantiate(realLaserPrefab, transform.position, transform.rotation);
            if (laser.TryGetComponent<LichLaser>(out var lichLaser))
            {
                lichLaser.Init(damageBonus);
            }
        }

        Destroy(gameObject);
    }
}