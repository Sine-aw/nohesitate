using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEndingTrigger : MonoBehaviour
{
    [Header("씬 설정")]
    [SerializeField] private string endingSceneName = "EndingScene";
    [SerializeField] private float delayBeforeFade = 1.5f;
    [SerializeField] private float fallbackLoadDelay = 1.0f;

    private bool isTriggered = false;

    public void GoToEndingScene()
    {
        if (isTriggered) return;
        isTriggered = true;

        StartCoroutine(LoadEndingRoutine());
    }

    private IEnumerator LoadEndingRoutine()
    {
        if (delayBeforeFade > 0f)
        {
            float elapsed = 0f;
            while (elapsed < delayBeforeFade)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Time.timeScale = 1f;

        SceneFader fader = FindFirstObjectByType<SceneFader>();
        if (fader != null)
        {
            fader.StartCoroutine("FadeAndLoad", endingSceneName);
            yield break;
        }

        yield return new WaitForSecondsRealtime(fallbackLoadDelay);
        SceneManager.LoadScene(endingSceneName);
    }
}