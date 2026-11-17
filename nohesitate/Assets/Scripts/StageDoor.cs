using UnityEngine;
using UnityEngine.SceneManagement;

public class StageDoor : MonoBehaviour
{
    public enum DoorType { Stage1_Next, Stage2_Real, Stage2_Fake }

    [Header("문의 종류 선택")]
    [SerializeField] private DoorType doorType;

    [Header("이동할 씬 이름")]
    [SerializeField] private string targetSceneName;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        switch (doorType)
        {
            case DoorType.Stage1_Next:
                if (!string.IsNullOrEmpty(targetSceneName))
                {
                    DisablePlayerControl(other.gameObject);

                    if (SceneFader.Instance != null)
                    {
                        SceneFader.Instance.FadeToScene(targetSceneName);
                    }
                    else
                    {
                        SceneManager.LoadScene(targetSceneName);
                    }
                }
                break;

            case DoorType.Stage2_Real:
                if (!string.IsNullOrEmpty(targetSceneName))
                {
                    DisablePlayerControl(other.gameObject);

                    if (SceneFader.Instance != null)
                    {
                        SceneFader.Instance.FadeToScene(targetSceneName);
                    }
                    else
                    {
                        SceneManager.LoadScene(targetSceneName);
                    }
                }
                break;

            case DoorType.Stage2_Fake:
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.TriggerGameOver();
                }
                break;
        }
    }

    private void DisablePlayerControl(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }
    }
}