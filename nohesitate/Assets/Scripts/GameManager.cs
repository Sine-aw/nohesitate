using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Panels")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Audio Sliders (Settings)")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider vfxSlider;

    private bool isPaused = false;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (bgmSlider != null) bgmSlider.value = AudioListener.volume;
        if (vfxSlider != null) vfxSlider.value = 1.0f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if ((gameOverPanel != null && gameOverPanel.activeSelf) || (playerHealth != null && playerHealth.IsDead))
            {
                return;
            }

            if (isPaused)
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    settingsPanel.SetActive(false);
                }
                else
                {
                    ResumeGame();
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void TriggerGameOver()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    public void OnClickResume()
    {
        ResumeGame();
    }

    public void OpenSettings(bool isOpen)
    {
        if (settingsPanel) settingsPanel.SetActive(isOpen);
    }

    public void OnBgmSliderChanged(float value)
    {
        AudioListener.volume = value;
    }

    public void OnVfxSliderChanged(float value)
    {
        Debug.Log("VFX 강도: " + value);
    }

    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickExitToTitle()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Title");
    }
}