using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [Header("설정 UI")]
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (settingsPanel == null || !settingsPanel.activeSelf)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    public void OnClickStart()
    {
        SceneManager.LoadScene("Stage 1");
    }

    public void OnClickOpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OnClickExit()
    {
        Debug.Log("게임 종료 버튼 클릭됨");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}