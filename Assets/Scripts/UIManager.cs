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
                if (ghostManager != null)
                {
                    // ghostManager.SetGhostState(GhostState.Recovering); 
                    Debug.Log("SCARED Timer: Entering Recovering State.");
                }
                // 💡 未來：在這裡改變音樂/動畫為閃爍狀態
            }

            if (scaredTimeRemaining <= 0f)
            {
                scaredTimeRemaining = 0f;
                StopScaredTimer(); 
                // *** (未來 GhostManager 會在這裡被通知切換狀態)
                if (ghostManager != null)
                {
                    // ghostManager.SetGhostState(GhostState.Normal); 
                    Debug.Log("SCARED Timer: Timer expired, setting Ghosts to Normal.");
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
    
    public void QuitGame()
    {
        // Purpose: Exit the game (useful for the main menu, though optional)
        Debug.Log("Quitting Game...");
        Application.Quit();
        // NOTE: Application.Quit() only works in a built game (not in Unity Editor).
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
    }

    // 新增：當計時結束或幽靈被吃掉時，GhostManager 會呼叫此方法
    public void StopScaredTimer()
    {
        scaredTimeRemaining = 0f;
        scaredTimerText.gameObject.SetActive(false);
    }
}
