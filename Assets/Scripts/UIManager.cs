using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
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
    public AudioController AudioController 
    {
        get { return audioController; }
    }
    //Lives
    private PacStudentController pacStudentController;
    [SerializeField] private int initialLives = 3;
    private int currentLives;
    [SerializeField] public GameObject[] lifeIcons;
    [SerializeField] private float deathParticleWaitTime = 2.0f;
    
    void Awake()
    {
        pacStudentController = FindFirstObjectByType<PacStudentController>();
        ghostManager = FindFirstObjectByType<GhostManager>();
        audioController = FindFirstObjectByType<AudioController>();
    }
    
    void Start()
    {
        UpdateScoreDisplay();
        if (scaredTimerText != null)
        {
            scaredTimerText.gameObject.SetActive(false); 
        }
        currentLives = initialLives;
        UpdateLivesDisplay();
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
                    ghostManager.ChangeStateToRecovering();
                }
            }

            if (scaredTimeRemaining <= 0f)
            {
                // 將時間強制設為 0f
                scaredTimeRemaining = 0f;
                isRecovering = false; // 重置 Recovering 旗標
            
                // 呼叫 GhostManager 恢復 Normal 狀態
                if (ghostManager != null)
                {
                    ghostManager.ChangeStateToNormal();
                }
            
                // 恢復 Normal BGM (這是之前步驟的成果)
                if (audioController != null)
                {
                    audioController.PlayNormalMusic();
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
        
        // --- 新增：啟動嚇跑狀態 (10 秒開始) ---
        if (audioController != null)
        {
            audioController.PlayScaredMusic(); // 播放嚇跑音樂
        }

        if (ghostManager != null)
        {
            ghostManager.ChangeStateToScared(); // 呼叫幽靈切換到 Scared 狀態
        }
        
        UpdateScaredTimerDisplay(duration);
    }

    // 新增：當計時結束或幽靈被吃掉時，GhostManager 會呼叫此方法
    public void StopScaredTimer()
    {
        scaredTimeRemaining = 0f;
        scaredTimerText.gameObject.SetActive(false);
    }
    
    
    public void SetGamePaused(bool isPaused)
    {
        // 0.0 = 暫停所有受 Time.timeScale 影響的物理和 Update 邏輯
        // 1.0 = 恢復正常時間
        Time.timeScale = isPaused ? 0.0f : 1.0f; 
    }
    public void LoseLife()
    {
        if (currentLives <= 0) return; // 如果已經是 0 命，則忽略

        // 1. 扣除生命
        currentLives--; 
        UpdateLivesDisplay();

        // 2. 判斷剩餘生命
        if (currentLives > 0)
        {
            // 還有生命：開始死亡序列 (暫停、動畫、重置)
            StartCoroutine(DeathSequenceRoutine(currentLives));
        }
        else
        {
            // 生命值歸零：遊戲結束
            SetGamePaused(true); // 暫停遊戲時間
            Debug.Log("Game Over! (TODO: Show Game Over UI)");
            // TODO: (稍後) 顯示 Game Over 畫面
        }
    }

    private IEnumerator DeathSequenceRoutine(int remainingLives)
    {
        // 1. 觸發動畫和音效 (在 Time.timeScale=1 時，立即開始播放)
        if (pacStudentController != null) pacStudentController.PlayDeathEffect();
        
        // 2. 立即暫停遊戲時間
        SetGamePaused(true); // 現在 Time.timeScale = 0.0f

        // 【✨ 新增：在等待結束後，重置前，觸發粒子效果】
        if (pacStudentController != null)
        {
            pacStudentController.PlayDeathParticle(); // 觸發粒子效果
        }
        yield return new WaitForSecondsRealtime(deathParticleWaitTime);

        // 4. 解開被註解的重置邏輯！
        if (remainingLives > 0)
        {
            Debug.Log("--- 執行 ResetPosition ---");
            // 4a. 重置角色和幽靈位置
            if (pacStudentController != null) pacStudentController.ResetPosition(); 
            // if (ghostManager != null) ghostManager.ResetGhosts(); 

            // 4b. 恢復遊戲時間
            SetGamePaused(false); 
        }
        else
        {
            // 5. game over
            SetGamePaused(true); 
        }
    }

    private void UpdateLivesDisplay()
    {
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null)
            {
                lifeIcons[i].SetActive(i < currentLives);
            }
        }
    }
    
    public float GetScaredTimeRemaining()
    {
        return scaredTimeRemaining;
    }
}
