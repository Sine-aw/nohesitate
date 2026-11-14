using UnityEngine;
using TMPro;

public class StageTimer : MonoBehaviour
{
    [Header("타이머 설정")]
    [SerializeField] private float timeLimit = 60f;
    [SerializeField] private TextMeshProUGUI timerText;

    private float currentTime;
    private bool isTimerRunning = true;

    private void Start()
    {
        currentTime = timeLimit;
    }

    private void Update()
    {
        if (!isTimerRunning) return;

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;

            if (currentTime < 0) currentTime = 0;

            UpdateTimerUI();

            if (currentTime <= 0)
            {
                TimeOut();
            }
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private void TimeOut()
    {
        isTimerRunning = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }
}