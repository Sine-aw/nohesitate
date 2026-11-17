using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [Header("UI 연결")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1.0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(FadeInRoutine());
    }

    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeOutAndLoadSceneRoutine(sceneName));
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadeImage == null) yield break;

        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;

        color.a = 1f;
        fadeImage.color = color;

        while (timer < fadeDuration)
        {
            if (fadeImage == null) yield break;

            timer += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        if (fadeImage != null)
        {
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeOutAndLoadSceneRoutine(string sceneName)
    {
        if (fadeImage == null) yield break;

        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            if (fadeImage == null) yield break;

            timer += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        if (fadeImage != null)
        {
            color.a = 1f;
            fadeImage.color = color;
        }

        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("unset");
        }
    }
}