using UnityEngine;

public class GlassPlate : MonoBehaviour
{
    public enum PlateType { Real, Fake }
    [Header("발판 종류 선택")]
    [SerializeField] private PlateType plateType;

    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        if (!other.CompareTag("Player")) return;

        isTriggered = true;

        if (plateType == PlateType.Fake)
        {
            gameObject.SetActive(false);
        }
    }
}