using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEndingTrigger : MonoBehaviour
{
    [Header("씬 설정")]
    [SerializeField] private string endingSceneName = "EndingScene";
    [SerializeField] private float delayBeforeFade = 1.5f;
    [SerializeField] private float fallbackLoadDelay = 0f;

    private bool isTriggered = false;

    public void GoToEndingScene()
    {
        if (isTriggered) return;
        isTriggered = true;
        StartCoroutine(LoadEndingRoutine());
    }

    private IEnumerator LoadEndingRoutine()
    {
        float elapsed = 0f;
        while (elapsed < delayBeforeFade)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;

        if (fallbackLoadDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(fallbackLoadDelay);
        }

        SceneManager.LoadScene(endingSceneName);
    }
}