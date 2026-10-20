using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scaredTimerText;
    [SerializeField] private GameObject powerPanel;
    [SerializeField] private int initialLives = 3;
    public GameObject[] lifeIcons;
    [SerializeField] private float deathParticleWaitTime = 2f;
    [SerializeField] private TextMeshProUGUI roundMessage;
    [SerializeField] private TextMeshProUGUI roundDetails;
    [SerializeField] private TextMeshProUGUI roundStats, roundReturn;
    [SerializeField] private GameObject blockingImage;

    public enum RoundState { Menu, Countdown, Running, Death, GameOver }
    public RoundState State { get; private set; }
    public bool IsRoundRunning => State == RoundState.Running;
    public int CurrentScore => currentScore;
    public int CurrentLives => currentLives;
    public int RemainingPellets => remainingPellets;
    public float ElapsedTime => totalTime;
    public AudioController AudioController => audioController;
    public InnovationController Innovation { get; private set; }
    private int currentScore, currentLives, remainingPellets;
    private float totalTime, scaredTimeRemaining;
    private bool isRecovering;
    private PacStudentController pacStudentController;
    private GhostManager ghostManager;
    private AudioController audioController;

    void Awake()
    {
        Time.timeScale = 1f;
        State = RoundState.Menu;
        pacStudentController = FindFirstObjectByType<PacStudentController>();
        ghostManager = FindFirstObjectByType<GhostManager>();
        audioController = FindFirstObjectByType<AudioController>();
        Innovation = GetComponent<InnovationController>();
        if (SceneManager.GetActiveScene().name != "StartScene") State = RoundState.Countdown;
    }

    void Start()
    {
        if (State == RoundState.Menu) { LoadMenuRecords(); return; }
        currentLives = initialLives;
        remainingPellets = FindObjectsByType<PelletController>(FindObjectsSortMode.None).Length
            + FindObjectsByType<PowerPillController>(FindObjectsSortMode.None).Length;
        UpdateScoreDisplay(); UpdateLivesDisplay(); UpdateTimerDisplay(); StopScaredTimer();
        if (roundMessage == null || blockingImage == null)
        {
            Debug.LogError("HUD countdown and blocking image must be assigned.", this);
            enabled = false; return;
        }
        StartCoroutine(StartRound());
    }

    void Update()
    {
        if (!IsRoundRunning) return;
        totalTime += Time.deltaTime;
        UpdateTimerDisplay();
        if (scaredTimeRemaining <= 0f) return;
        scaredTimeRemaining = Mathf.Max(0f, scaredTimeRemaining - Time.deltaTime);
        if (scaredTimeRemaining <= 0f)
        {
            isRecovering = false;
            if (ghostManager != null) ghostManager.ChangeStateToNormal();
            if (audioController != null) audioController.RefreshGhostMusic();
        }
        else if (scaredTimeRemaining <= 3f && !isRecovering)
        {
            isRecovering = true;
            if (ghostManager != null) ghostManager.ChangeStateToRecovering();
        }
        UpdateScaredTimerDisplay();
    }

    private IEnumerator StartRound()
    {
        if (roundDetails != null) roundDetails.text = Innovation != null
            ? "Get ready\n<size=18>WASD to move | SPACE to shoot</size>"
            : "Get ready";
        ShowMessage("3");
        if (audioController != null) audioController.PlayIntro();
        foreach (string message in new[] { "3", "2", "1", "GO!" })
        {
            ShowMessage(message);
            yield return new WaitForSecondsRealtime(1f);
        }
        HideMessage(); State = RoundState.Running;
        if (audioController != null) audioController.RefreshGhostMusic();
    }

    public void LoadLevel1() { LoadScene("Level1Scene"); }
    public void LoadLevel2() { LoadScene("InnovationScene"); }
    public void StartScene() { LoadScene("StartScene"); }
    private void LoadScene(string scene)
    {
        if (State == RoundState.GameOver) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    public void AddScore(int points)
    {
        if (!IsRoundRunning) return;
        currentScore += points; UpdateScoreDisplay();
    }
    public void CollectPellet(int points)
    {
        if (!IsRoundRunning) return;
        AddScore(points);
        remainingPellets = Mathf.Max(0, remainingPellets - 1);
        if (Innovation != null)
        {
            Innovation.CollectPearl(points);
            if (remainingPellets == 0) CompleteInnovation();
        }
        else if (remainingPellets == 0) StartCoroutine(EndRound(true));
    }
    public void CompleteInnovation()
    {
        if (Innovation != null && IsRoundRunning && remainingPellets == 0
            && Innovation.TotalTargets > 0 && Innovation.DefeatedCount >= Innovation.TotalTargets)
            StartCoroutine(EndRound(true));
    }
    private void UpdateScoreDisplay() { if (scoreText != null) scoreText.text = "SCORE\n<size=26>" + currentScore.ToString("D6") + "</size>"; }
    public static string FormatTime(float seconds)
    {
        int hundredths = Mathf.FloorToInt(Mathf.Max(0, seconds) * 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", hundredths / 6000, hundredths / 100 % 60, hundredths % 100);
    }
    private void UpdateTimerDisplay() { if (timerText != null) timerText.text = "TIME\n<size=26>" + FormatTime(totalTime) + "</size>"; }
    private void UpdateScaredTimerDisplay()
    {
        if (Innovation != null) return;
        bool visible = scaredTimeRemaining > 0f;
        if (scaredTimerText != null)
        {
            scaredTimerText.gameObject.SetActive(visible);
            scaredTimerText.text = "Power: " + Mathf.CeilToInt(scaredTimeRemaining) + "s";
        }
        if (powerPanel != null) powerPanel.SetActive(visible);
    }
    public void StartScaredTimer(float duration)
    {
        if (Innovation != null) return;
        if (!IsRoundRunning) return;
        scaredTimeRemaining = duration; isRecovering = false;
        if (ghostManager != null) ghostManager.ChangeStateToScared();
        if (audioController != null) audioController.RefreshGhostMusic();
        UpdateScaredTimerDisplay();
    }
    public void StopScaredTimer() { scaredTimeRemaining = 0f; isRecovering = false; UpdateScaredTimerDisplay(); }
    public float GetScaredTimeRemaining() => scaredTimeRemaining;
    public void SetGamePaused(bool isPaused) { Time.timeScale = isPaused ? 0f : 1f; }

    public void LoseLife()
    {
        if (!IsRoundRunning || currentLives <= 0) return;
        currentLives--; UpdateLivesDisplay();
        if (currentLives == 0) StartCoroutine(EndRound(false));
        else StartCoroutine(DeathSequence());
    }
    private IEnumerator DeathSequence()
    {
        Innovation?.ClearShots();
        State = RoundState.Death;
        pacStudentController.PlayDeathEffect(); pacStudentController.PlayDeathParticle();
        SetGamePaused(true);
        yield return new WaitForSecondsRealtime(deathParticleWaitTime);
        StopScaredTimer();
        if (ghostManager != null) ghostManager.ResetGhosts();
        pacStudentController.ResetPosition();
        Innovation?.ResetAfterDeath();
        SetGamePaused(false); State = RoundState.Running;
        if (audioController != null) audioController.RefreshGhostMusic();
    }
    private IEnumerator EndRound(bool won)
    {
        Innovation?.ClearShots();
        State = RoundState.GameOver;
        if (pacStudentController != null)
        {
            pacStudentController.StopMovement();
            if (currentLives == 0) { pacStudentController.PlayDeathEffect(); pacStudentController.PlayDeathParticle(); }
        }
        SetGamePaused(true); ShowMessage(won ? "Victory!" : "Game Over");
        if (roundDetails != null)
            roundDetails.text = won ? (Innovation != null ? "All pearls collected. All four popped." : "All pearls collected") : "Out of lives";
        if (roundStats != null)
            roundStats.text = "SCORE<pos=54%>" + currentScore.ToString("D6") + "\nTIME<pos=54%>" + FormatTime(totalTime);
        if (roundReturn != null) roundReturn.text = "Returning to menu...";
        foreach (var button in blockingImage.transform.root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) button.interactable = false;
        SaveRecord(SceneManager.GetActiveScene().name, currentScore, totalTime);
        yield return new WaitForSecondsRealtime(3f);
        Time.timeScale = 1f; SceneManager.LoadScene("StartScene");
    }
    private void UpdateLivesDisplay()
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++) if (lifeIcons[i] != null) lifeIcons[i].SetActive(i < currentLives);
    }
    private void ShowMessage(string message)
    {
        bool ended = State == RoundState.GameOver;
        if (roundDetails != null)
        {
            ((RectTransform)roundMessage.transform.parent).sizeDelta = ended
                ? new Vector2(440, 360) : Innovation != null ? new Vector2(400, 230) : new Vector2(320, 210);
            if (ended)
            {
                RectTransform details = roundDetails.rectTransform;
                details.anchorMin = details.anchorMax = details.pivot = new Vector2(0.5f, 1f);
                details.anchoredPosition = new Vector2(0, -114);
                details.sizeDelta = new Vector2(400, 36);
            }
        }
        if (roundStats != null) roundStats.transform.parent.gameObject.SetActive(ended);
        if (roundReturn != null) roundReturn.gameObject.SetActive(ended);
        blockingImage.SetActive(true); roundMessage.text = message;
    }
    private void HideMessage() { blockingImage.SetActive(false); }

    private static string ScoreKey(string level) => "PacStudent." + level + ".HighScore";
    private static string TimeKey(string level) => "PacStudent." + level + ".BestTime";
    public static void SaveRecord(string level, int score, float seconds)
    {
        int previous = PlayerPrefs.GetInt(ScoreKey(level), 0);
        float previousTime = PlayerPrefs.GetFloat(TimeKey(level), float.MaxValue);
        if (score < previous || (score == previous && seconds >= previousTime)) return;
        PlayerPrefs.SetInt(ScoreKey(level), score); PlayerPrefs.SetFloat(TimeKey(level), seconds); PlayerPrefs.Save();
    }
    private void LoadMenuRecords()
    {
        foreach (var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            string level = label.name.StartsWith("Level 1 -") ? "Level1Scene"
                : label.name.StartsWith("Level 2 -") ? "InnovationScene" : null;
            if (level == null) continue;
            string scoreKey = ScoreKey(level);
            if (!PlayerPrefs.HasKey(scoreKey))
            {
                if (label.name.EndsWith("HighScoreText")) label.text = "No record yet";
                else if (label.name.EndsWith("TimeText")) label.text = "Play to set your first score";
                continue;
            }
            if (label.name.EndsWith("HighScoreText")) label.text = "HIGH SCORE<pos=62%>" + PlayerPrefs.GetInt(scoreKey).ToString("D6");
            else if (label.name.EndsWith("TimeText")) label.text = "RUN TIME<pos=62%>" + FormatTime(PlayerPrefs.GetFloat(TimeKey(level), 0f));
        }
    }
    void OnDestroy() { Time.timeScale = 1f; }
}
