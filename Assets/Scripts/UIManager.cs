using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    public TextMeshProUGUI timerText;

    // Score
    private int currentScore = 0;

    private const string SCORE_FORMAT = "D6";

    // Time
    private float totalTime = 0f;
    private bool isTiming = true;

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

        // 分數增加後，必須更新 UI 顯示
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
    
    void Start()
    {
        // 確保遊戲開始時，分數顯示為 "000000"
        UpdateScoreDisplay();
    }

    void Update()
    {
        if (timerText != null && isTiming)
        {
            // 1. 累積時間：使用 Time.deltaTime 確保時間與幀率無關
            totalTime += Time.deltaTime;
        
            // 2. 更新 UI 顯示
            UpdateTimerDisplay(totalTime);
        }
    }

}
