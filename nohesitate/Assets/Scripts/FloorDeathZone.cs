using UnityEngine;

public class FloorDeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }
}