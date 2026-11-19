using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingUIController : MonoBehaviour
{
    [Header("이동할 타이틀 씬 이름")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("타이틀 이동 버튼")]
    [SerializeField] private Button titleButton;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (titleButton != null)
        {
            titleButton.onClick.RemoveAllListeners();
            titleButton.onClick.AddListener(OnClickGoToTitle);
        }
    }

    public void OnClickGoToTitle()
    {
        Time.timeScale = 1f;

        SceneFader fader = FindFirstObjectByType<SceneFader>();
        if (fader != null)
        {
            fader.StartCoroutine("FadeAndLoad", titleSceneName);
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }
}