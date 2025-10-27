using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scaredTimerText;
    
    // Score
    private int currentScore = 0;
    private const string SCORE_FORMAT = "D6";

    // Time
    private float totalTime = 0f;
    private bool isTiming = true;
    private float scaredTimeRemaining = 0f;
    
    // Ghost
    private bool isRecovering = false;
    private GhostManager ghostManager;
    private AudioController audioController;

    void Awake()
    {
        ghostManager = FindFirstObjectByType<GhostManager>();
        audioController = FindFirstObjectByType<AudioController>();
        if (ghostManager == null) Debug.LogWarning("GhostManager not found. Ghost states cannot be changed yet.");
        if (audioController == null) Debug.LogError("AudioController not found!");
    }
    void Start()
    {
        UpdateScoreDisplay();
        if (scaredTimerText != null)
        {
            scaredTimerText.gameObject.SetActive(false); 
        }
    }

    void Update()
    {
        if (timerText != null && isTiming)
        {
            totalTime += Time.deltaTime;
            UpdateTimerDisplay(totalTime);
        }

        //Ghost Scared Timer
        if (scaredTimeRemaining > 0f)
        {
            scaredTimeRemaining -= Time.deltaTime;
            
            if (scaredTimeRemaining <= 3.0f && !isRecovering)
            {
                isRecovering = true;
                // 呼叫 GhostManager 進入 Recovering 狀態
            }

            else if (scaredTimeRemaining <= 0f && isRecovering) // 計時結束，狀態重置
            {
                scaredTimeRemaining = 0f;
                isRecovering = false;

                // --- ADDED: 步驟五 (恢復正常音樂) ---
                if (audioController != null)
                {
                    // Set the Ghosts back to their Normal states and change BGM
                    audioController.PlayNormalMusic();
                }

                // 恢復幽靈狀態（待實作）
                if (ghostManager != null)
                {
                    // ghostManager.ChangeStateToNormal(); 
                }
            }
            UpdateScaredTimerDisplay(scaredTimeRemaining);
        }
    }

    public void LoadLevel1()
    {
        SceneManager.LoadScene("Level1Scene");
    }

    public void LoadLevel2()
    {
        SceneManager.LoadScene("InnovationScene");
    }

    public void StartScene()
    {
        SceneManager.LoadScene("StartScene");
    }

    public void AddScore(int points)
    {
        currentScore += points;
        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText == null) return;

        // 使用 String.Format() 或 ToString() 的 "D6" 格式化字串
        // D6: Decimal, 最小長度為 6，不足則補零。
        string formattedScore = currentScore.ToString(SCORE_FORMAT);

        // 更新 TextMeshPro 的內容
        scoreText.text = "SCORE: " + formattedScore;
    }

    private void UpdateTimerDisplay(float timeToDisplay)
    {
        // 1. 計算分鐘、秒和百分秒
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
    
        // 取得毫秒 (百分秒) 部份，乘以 100 後取整數，再取最後兩位數
        float milliseconds = (timeToDisplay * 100) % 100;

        // 2. 格式化字串：關鍵在於使用 "D2" 進行零填充
        // mm:ss:msms
        string timeFormat = string.Format("{0:00}:{1:00}:{2:00}", 
            minutes, 
            seconds, 
            milliseconds);
    
        // 更新 TextMeshPro 的內容
        timerText.text = "TIME: " + timeFormat;
    }
        
    private void UpdateScaredTimerDisplay(float time)
    {
        if (scaredTimerText == null) return;

        // 取得時間的整數秒數，使用 Mathf.CeilToInt 確保倒數時看起來更順暢 (例如 2.9s 顯示為 3)
        int seconds = Mathf.CeilToInt(time); 

        if (seconds <= 0)
        {
            // 當時間歸零，隱藏 UI
            scaredTimerText.gameObject.SetActive(false);
        }
        else
        {
            // 時間大於零，顯示 UI 和數值
            scaredTimerText.gameObject.SetActive(true);
            scaredTimerText.text = "Ghost Scared: " + seconds.ToString(); // 顯示為整數
        }
    }

    // 新增：當 PacStudent 吃下大力丸時，GhostManager 會呼叫此方法
    public void StartScaredTimer(float duration)
    {
        
        if (scaredTimerText == null) return;

        scaredTimeRemaining = duration;
        // 立即讓 UI 顯示，並開始倒數
        isRecovering = false;
        scaredTimerText.gameObject.SetActive(true); 
        
        if (audioController != null)
        {
            // Change the background music to the Scared state
            audioController.PlayScaredMusic(); 
        }
    
        // 由於 GhostManager 尚未實作，我們先留下這個區塊作為待辦事項
        if (ghostManager != null)
        {
            // Change the Ghost animator state to "Scared"
        }
    }

    // 新增：當計時結束或幽靈被吃掉時，GhostManager 會呼叫此方法
    public void StopScaredTimer()
    {
        scaredTimeRemaining = 0f;
        scaredTimerText.gameObject.SetActive(false);
    }
}
