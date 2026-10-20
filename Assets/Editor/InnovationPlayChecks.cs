using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class InnovationPlayChecks
{
    private const string Flag = "PacStudent.InnovationPlayChecks";
    private const string Level1ScoreKey = "PacStudent.Level1Scene.HighScore";
    private const string Level1TimeKey = "PacStudent.Level1Scene.BestTime";
    private const string Level2ScoreKey = "PacStudent.InnovationScene.HighScore";
    private const string Level2TimeKey = "PacStudent.InnovationScene.BestTime";
    private const int InitialLives = 3;
    private const int TargetHits = 2;
    private const double TimeoutSeconds = 90d;

    private static readonly List<string> passed = new List<string>();
    private static UIManager ui;
    private static InnovationController innovation;
    private static PacStudentController player;
    private static LevelGenerator map;
    private static GhostController[] ghosts;
    private static double started, stageStarted;
    private static int stage;
    private static int defeatLosses;
    private static bool defeatMode;
    private static bool cooldownProbePending;
    private static bool timerFreezeChecked;
    private static bool menuCaptured;
    private static float stoppedTimer;
    private static readonly Dictionary<int, HashSet<string>> deathSprites = new Dictionary<int, HashSet<string>>();

    static InnovationPlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("PacStudent/Run Innovation Play Mode checks")]
    public static void Start() { Begin(false); }

    [MenuItem("PacStudent/Run Innovation defeat Play Mode checks")]
    public static void StartDefeatCheck() { Begin(true); }

    private static void Begin(bool defeat)
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InnovationScene")
            throw new InvalidOperationException("Open InnovationScene outside Play Mode before running checks.");
        defeatMode = defeat;
        SaveRecordSnapshot("Level1", Level1ScoreKey, Level1TimeKey);
        SaveRecordSnapshot("Level2", Level2ScoreKey, Level2TimeKey);
        SessionState.SetBool(Flag + ".Defeat", defeat);
        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void SaveRecordSnapshot(string name, string scoreKey, string timeKey)
    {
        SessionState.SetBool(Flag + "." + name + ".HadScore", PlayerPrefs.HasKey(scoreKey));
        SessionState.SetBool(Flag + "." + name + ".HadTime", PlayerPrefs.HasKey(timeKey));
        SessionState.SetInt(Flag + "." + name + ".Score", PlayerPrefs.GetInt(scoreKey));
        SessionState.SetFloat(Flag + "." + name + ".Time", PlayerPrefs.GetFloat(timeKey));
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            defeatMode = SessionState.GetBool(Flag + ".Defeat", false);
            passed.Clear(); stage = 0; defeatLosses = 0;
            cooldownProbePending = false; timerFreezeChecked = false; menuCaptured = false;
            deathSprites.Clear();
            started = stageStarted = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            string reportPath = ReportPath();
            if (File.Exists(reportPath) && File.ReadAllText(reportPath).StartsWith("RUNNING"))
                WriteReport(File.ReadAllText(reportPath).Replace("RUNNING", "INTERRUPTED: Play Mode stopped before completion"));
            RestoreRecordSnapshot("Level1", Level1ScoreKey, Level1TimeKey);
            RestoreRecordSnapshot("Level2", Level2ScoreKey, Level2TimeKey);
            SessionState.SetBool(Flag, false);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > TimeoutSeconds) throw new Exception("Innovation checks exceeded 90 seconds.");
            CacheObjects();
            ObserveDeathSprites();
            if (stage == 0) CheckCountdown(now);
            else if (stage == 1) CheckRunning(now);
            else if (stage == 2) CheckShootingAndCollectibles(now);
            else if (stage == 3) CheckWallBlock(now);
            else if (stage == 4) CheckFirstHit(now);
            else if (stage == 5) CheckDeathReset(now);
            else if (stage == 6) CheckRemainingTargets(now);
            else if (stage == 7) CheckMenuAfterRound(now);
            else if (stage == 8) CheckLevel2Restart(now);
            else if (stage == 9) CheckMainMenuReturn(now);
            else if (defeatMode && stage == 10) CheckDefeatRound(now);
            else if (defeatMode && stage == 11) CheckDefeatMenu(now);
        }
        catch (Exception error) { Finish("FAILED: " + error.Message); }
    }

    private static void CacheObjects()
    {
        if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (innovation == null && ui != null) innovation = ui.Innovation;
        if (player == null) player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
        if (map == null) map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
        if (ghosts == null || ghosts.Length < 4)
            ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)
                .OrderBy(GhostNumber).ToArray();
    }

    private static void ObserveDeathSprites()
    {
        if (ghosts == null) return;
        foreach (GhostController ghost in ghosts)
        {
            if (ghost == null || !ghost.IsPermanentlyDefeated || !ghost.gameObject.activeSelf) continue;
            int number = GhostNumber(ghost);
            if (!deathSprites.ContainsKey(number)) deathSprites[number] = new HashSet<string>();
            SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null && renderer.sprite.name.StartsWith("Popping_dead_"))
                deathSprites[number].Add(renderer.sprite.name);
        }
    }

    private static void CheckHudLayout(bool running)
    {
        Canvas.ForceUpdateCanvases();
        Canvas canvas = ui.scoreText.GetComponentInParent<Canvas>().rootCanvas;
        Check(canvas != null, "Innovation HUD has a Canvas");
        Rect canvasBounds = ScreenBounds(canvas.GetComponent<RectTransform>());
        foreach (string field in new[] { "shotStatusText", "targetStatusText" })
        {
            TMP_Text label = InnovationText(field);
            Check(label != null, field + " is assigned");
            label.ForceMeshUpdate();
            Check(!label.isTextOverflowing, field + " fits without text overflow");
            Rect bounds = ScreenBounds(label.rectTransform);
            Check(canvasBounds.Contains(bounds.min) && canvasBounds.Contains(bounds.max),
                field + " stays inside the canvas");
        }
        if (!running)
        {
            TMP_Text details = RoundDetails();
            Check(details != null, "Countdown details text is assigned");
            details.ForceMeshUpdate();
            Check(!details.isTextOverflowing, "Countdown details fit without text overflow");
            TMP_Text message = UIText("roundMessage");
            Check(message != null, "Countdown message text is assigned");
            message.ForceMeshUpdate();
            Check(!message.isTextOverflowing, "Countdown message fits without text overflow");
        }
        CheckResultOverlay(ui, false, RoundDetails() == null ? null : RoundDetails().text);
    }

    private static TMP_Text InnovationText(string field)
    {
        if (innovation == null) return null;
        SerializedProperty property = new SerializedObject(innovation).FindProperty(field);
        return property == null ? null : property.objectReferenceValue as TMP_Text;
    }

    private static TMP_Text UIText(string field)
    {
        if (ui == null) return null;
        SerializedProperty property = new SerializedObject(ui).FindProperty(field);
        return property == null ? null : property.objectReferenceValue as TMP_Text;
    }

    private static Rect ScreenBounds(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return new Rect(corners[0], corners[2] - corners[0]);
    }

    private static void CheckResultOverlay(UIManager manager, bool gameOver, string reason)
    {
        TMP_Text details = UIText("roundDetails");
        TMP_Text stats = UIText("roundStats");
        TMP_Text footer = UIText("roundReturn");
        Check(details != null && stats != null && footer != null, "Result overlay text references are assigned");
        RectTransform statsFrame = stats == null ? null : stats.transform.parent as RectTransform;
        RectTransform panel = details == null ? null : details.transform.parent as RectTransform;
        Check(statsFrame != null && panel != null, "Result Stats and Round Panel are present");
        UnityEngine.UI.Image statsImage = statsFrame == null ? null : statsFrame.GetComponent<UnityEngine.UI.Image>();
        Check(statsFrame == null || statsFrame.name == "Result Stats", "Result Stats uses the named cream frame");
        Check(statsImage != null && statsImage.color == new Color32(250, 239, 222, 255),
            "Result Stats uses the cream FA EF DE background");
        Check(stats.color == new Color32(59, 30, 20, 255), "Result stats use the dark brown text");
        Check(footer.color == new Color32(250, 239, 222, 255), "Result footer uses the cream text");
        Check(stats.gameObject.activeInHierarchy == gameOver && footer.gameObject.activeInHierarchy == gameOver
            && statsFrame.gameObject.activeInHierarchy == gameOver, "Result stats and footer visibility follows GameOver");
        details.ForceMeshUpdate(); stats.ForceMeshUpdate(); footer.ForceMeshUpdate();
        Check(!details.isTextOverflowing && !stats.isTextOverflowing && !footer.isTextOverflowing,
            "Result reason, stats, and footer fit without text overflow");
        if (reason != null) Check(details.text == reason, "Result reason text is aligned");
        if (!gameOver) return;
        Check(stats.text == "SCORE<pos=54%>" + manager.CurrentScore.ToString("D6")
            + "\nTIME<pos=54%>" + UIManager.FormatTime(manager.ElapsedTime),
            "Result stats display score and time");
        Check(footer.text == "Returning to menu...", "Result footer displays the return message");
        Rect panelBounds = ScreenBounds(panel);
        Rect statsBounds = ScreenBounds(statsFrame);
        Check(panelBounds.Contains(statsBounds.min) && panelBounds.Contains(statsBounds.max),
            "Result Stats stays inside Round Panel");
        Check(!statsBounds.Overlaps(ScreenBounds(details.rectTransform))
            && !statsBounds.Overlaps(ScreenBounds(footer.rectTransform)),
            "Result Stats does not overlap the reason or footer");
        Transform divider = panel.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name.IndexOf("Divider", StringComparison.OrdinalIgnoreCase) >= 0);
        Check(divider != null && divider.gameObject.activeInHierarchy, "Result panel includes a visible divider");
    }

    private static void CheckCountdown(double now)
    {
        if (now - started < 0.5) return;
        if (ui == null || innovation == null || player == null || ghosts == null || ghosts.Length < 4
            || innovation.TotalTargets != 4 || ui.State != UIManager.RoundState.Countdown) return;
        Check(ui.CurrentLives == InitialLives, "Countdown starts with three lives");
        Check(ghosts.Length == 4 && ghosts.All(ghost => innovation.GetRemainingHits(ghost) == TargetHits),
            "Countdown gives all four ghosts two hits");
        Check(innovation.CooldownRemaining == 0f && innovation.ActiveShotCount == 0,
            "Countdown starts with a ready shot and no active projectiles");
        Check(!innovation.TryShoot() && innovation.ActiveShotCount == 0,
            "Shooting is blocked during countdown");
        CheckHudLayout(false);
        Capture("countdown.png");
        Next(now);
    }

    private static void CheckRunning(double now)
    {
        if (ui == null || innovation == null || player == null || ui.State != UIManager.RoundState.Running) return;
        player.StopMovement(); player.ResetPosition();
        player.transform.position = new Vector3(1, -1, 0);
        player.enabled = false;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null) playerCollider.enabled = false;
        foreach (GhostController ghost in ghosts)
        {
            ghost.ResetPosition();
            ghost.enabled = false;
        }
        Physics2D.SyncTransforms();
        ui.SetGamePaused(true);
        Check(player.transform.position == new Vector3(1, -1, 0), "Running test uses the player spawn position");
        Check(ghosts.Length == 4 && ghosts.All(ghost => innovation.GetRemainingHits(ghost) == TargetHits),
            "Running keeps all target hit counts intact");
        CheckHudLayout(true);
        Capture("running.png");
        Next(now);
    }

    private static void CheckShootingAndCollectibles(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        if (!cooldownProbePending)
        {
            Check(innovation.TryShoot(), "Running allows a shot");
            Check(innovation.CooldownRemaining == 3f && innovation.ActiveShotCount == 1,
                "A shot starts a three second cooldown");
            Check(!innovation.TryShoot() && innovation.ActiveShotCount == 1,
                "A second shot is blocked during cooldown");
            ui.SetGamePaused(false);
            cooldownProbePending = true;
            stageStarted = now;
            return;
        }
        if (now - stageStarted < 0.5) return;
        Check(innovation.CooldownRemaining < 2.7f && innovation.CooldownRemaining > 2.1f,
            "The running cooldown decreases with real elapsed time");
        ui.SetGamePaused(true);
        cooldownProbePending = false;
        innovation.ClearShots();
        int score = ui.CurrentScore;
        float cooldownBeforePearl = innovation.CooldownRemaining;
        ui.CollectPellet(10);
        Check(ui.CurrentScore == score + 10 && Mathf.Abs(innovation.CooldownRemaining - (cooldownBeforePearl - 0.25f)) < 0.001f,
            "A normal pearl scores ten and shortens cooldown by 0.25 seconds");
        score = ui.CurrentScore;
        ui.StartScaredTimer(10f);
        ui.CollectPellet(50);
        Check(ui.CurrentScore == score + 50 && innovation.CooldownRemaining == 0f,
            "A large pearl scores fifty and refills the shot");
        Check(ui.GetScaredTimeRemaining() == 0f && ghosts.All(ghost => ghost.currentState == GhostState.Normal),
            "Innovation pearls never activate Scared ghosts");
        int remaining = ui.RemainingPellets;
        for (int i = 0; i < remaining; i++) ui.CollectPellet(10);
        Check(ui.State == UIManager.RoundState.Running && ui.RemainingPellets == 0,
            "Collecting every pellet does not win Innovation mode");
        if (defeatMode)
        {
            Next(now);
            stage = 10; stageStarted = now;
            return;
        }
        Next(now);
    }

    private static void CheckWallBlock(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        PositionGhost(ghosts[0], new Vector3(14, -1, 0));
        for (int i = 1; i < ghosts.Length; i++) PositionGhost(ghosts[i], new Vector3(20 + i, -20, 0));
        int hits = innovation.GetRemainingHits(ghosts[0]);
        Check(innovation.TryShoot(), "A ready shot can be fired at the wall test");
        PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Wall test creates a PearlShot");
        shot.Advance(2f); Physics2D.SyncTransforms();
        Check(innovation.ActiveShotCount == 0 && innovation.GetRemainingHits(ghosts[0]) == hits,
            "PearlShot stops at the real wall before a distant ghost");
        Next(now);
    }

    private static void CheckFirstHit(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        PositionGhost(ghosts[0], new Vector3(4, -1, 0));
        innovation.CollectPearl(50);
        Check(innovation.TryShoot(), "A refilled shot can target a ghost");
        PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Hit test creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(innovation.GetRemainingHits(ghosts[0]) == 1 && !ghosts[0].IsPermanentlyDefeated,
            "The first ghost hit injures without popping");
        int score = ui.CurrentScore;
        innovation.CollectPearl(50);
        Check(innovation.TryShoot(), "The second ghost hit can be fired after refill");
        shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Second hit creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(innovation.GetRemainingHits(ghosts[0]) == 0 && ghosts[0].IsPermanentlyDefeated
            && !ghosts[0].GetComponent<Collider2D>().enabled && innovation.DefeatedCount == 1,
            "Two hits permanently pop one ghost and disable its collider");
        Check(ui.CurrentScore == score + 300, "A popped ghost awards 300 points once");
        innovation.CollectPearl(50);
        score = ui.CurrentScore;
        Check(innovation.TryShoot(), "A duplicate hit probe can be fired after refill");
        shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Duplicate hit probe creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(ui.CurrentScore == score && innovation.DefeatedCount == 1
            && innovation.GetRemainingHits(ghosts[0]) == 0,
            "Repeated hits on a permanently popped ghost do not add score");
        innovation.ClearShots();
        Next(now);
    }

    private static void CheckDeathReset(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        PositionGhost(ghosts[1], new Vector3(4, -1, 0));
        innovation.CollectPearl(50);
        Check(innovation.TryShoot(), "A second ghost can be injured before death");
        PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Injury shot creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(innovation.GetRemainingHits(ghosts[1]) == 1, "The second ghost keeps one hit after injury");
        PositionGhost(ghosts[1], new Vector3(14, -1, 0));
        innovation.CollectPearl(50); innovation.TryShoot();
        Check(innovation.ActiveShotCount == 1, "An active projectile exists before player death");
        ui.LoseLife();
        Check(ui.State == UIManager.RoundState.Death && ui.CurrentLives == 2,
            "Player death removes one life and pauses the round");
        Check(innovation.ActiveShotCount == 0 && !innovation.TryShoot(),
            "Death clears projectiles and blocks shooting");
        stageStarted = now; stage++;
    }

    private static void CheckRemainingTargets(double now)
    {
        if (ui.State != UIManager.RoundState.Running || stageStarted + 2.1 > EditorApplication.timeSinceStartup) return;
        Check(ui.CurrentLives == 2 && player.transform.position == new Vector3(1, -1, 0),
            "Respawn preserves the second life and player position");
        Check(ghosts[0].IsPermanentlyDefeated && !ghosts[0].gameObject.activeSelf,
            "A permanently popped ghost does not revive after death");
        Check(innovation.DefeatedCount == 1 && innovation.GetRemainingHits(ghosts[1]) == 1
            && innovation.ActiveShotCount == 0 && innovation.CooldownRemaining == 0f,
            "Respawn preserves defeated and injured progress while resetting the shot");
        Check(ui.GetScaredTimeRemaining() == 0f, "Respawn keeps the Innovation scared timer inactive");
        ui.SetGamePaused(true);
        DefeatGhost(ghosts[1], false);
        DefeatGhost(ghosts[2], true);
        DefeatGhost(ghosts[3], true);
        Check(ui.State == UIManager.RoundState.GameOver, "All four popped ghosts end the round");
        TMP_Text details = RoundDetails();
        Check(details != null && details.text.Contains("All popping pearls defeated"),
            "Innovation victory uses the popping pearl reason");
        CheckResultOverlay(ui, true, details.text);
        stoppedTimer = ui.ElapsedTime;
        Capture("victory.png");
        Next(now);
    }

    private static void CheckMenuAfterRound(double now)
    {
        if (!timerFreezeChecked && SceneManager.GetActiveScene().name == "InnovationScene" && now - stageStarted > 0.5)
        {
            Check(ui.ElapsedTime == stoppedTimer, "The three second return delay freezes the round timer");
            timerFreezeChecked = true;
        }
        if (SceneManager.GetActiveScene().name != "StartScene" || EditorApplication.timeSinceStartup - stageStarted < 3.2) return;
        if (!menuCaptured)
        {
            CheckDeathSprites();
            Check(PlayerPrefs.HasKey(Level2ScoreKey), "Innovation round saves an independent Level 2 record");
            bool level1HadScore = SessionState.GetBool(Flag + ".Level1.HadScore", false);
            bool level1HadTime = SessionState.GetBool(Flag + ".Level1.HadTime", false);
            if (level1HadScore)
                Check(PlayerPrefs.GetInt(Level1ScoreKey) == SessionState.GetInt(Flag + ".Level1.Score", 0), "Level 1 score record is unchanged");
            else Check(!PlayerPrefs.HasKey(Level1ScoreKey), "Level 1 score record remains absent");
            if (level1HadTime)
                Check(Mathf.Approximately(PlayerPrefs.GetFloat(Level1TimeKey), SessionState.GetFloat(Flag + ".Level1.Time", 0)), "Level 1 time record is unchanged");
            else Check(!PlayerPrefs.HasKey(Level1TimeKey), "Level 1 time record remains absent");
            CheckMenuRecord("Level 1", Level1ScoreKey, Level1TimeKey, level1HadScore);
            CheckMenuRecord("Level 2", Level2ScoreKey, Level2TimeKey, true);
            GameObject menuLevel2 = GameObject.Find("Button - Level 2");
            Check(menuLevel2 != null && menuLevel2.activeInHierarchy && menuLevel2.GetComponent<Button>().interactable,
                "Menu Level 2 button is available after the innovation round");
            Capture("menu.png");
            menuCaptured = true;
            return;
        }
        GameObject level2 = GameObject.Find("Button - Level 2");
        Check(level2 != null && level2.activeInHierarchy && level2.GetComponent<Button>().interactable,
            "Menu Level 2 button remains available for submit");
        EventSystem.current.SetSelectedGameObject(level2);
        ExecuteEvents.Execute(level2, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        ui = null; innovation = null; player = null; map = null; ghosts = null;
        Next(now);
    }

    private static void CheckLevel2Restart(double now)
    {
        if (SceneManager.GetActiveScene().name != "InnovationScene" || ui == null || ui.State != UIManager.RoundState.Countdown) return;
        Check(ui.CurrentLives == InitialLives && innovation != null && innovation.DefeatedCount == 0
            && innovation.TotalTargets == 4 && ghosts != null && ghosts.Length == 4
            && ghosts.All(ghost => innovation.GetRemainingHits(ghost) == TargetHits),
            "Submitting Level 2 starts a fresh round with full lives and targets");
        GameObject exit = GameObject.Find("Button - Exit");
        Check(exit != null, "Innovation round exposes a Main Menu button");
        ExecuteEvents.Execute(exit, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        Next(now);
    }

    private static void CheckMainMenuReturn(double now)
    {
        if (SceneManager.GetActiveScene().name != "StartScene") return;
        Check(ui != null && ui.State == UIManager.RoundState.Menu, "Main Menu return completes in menu state");
        GameObject level1 = GameObject.Find("Button - Level 1");
        Check(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == level1,
            "Main Menu return restores Level 1 selection");
        Finish("PASSED");
    }

    private static void CheckDefeatRound(double now)
    {
        if (ui.State == UIManager.RoundState.Running)
        {
            ui.LoseLife(); defeatLosses++; stageStarted = now; return;
        }
        if (ui.State == UIManager.RoundState.GameOver)
        {
            Check(defeatLosses == InitialLives && ui.CurrentLives == 0, "Defeat check reaches zero lives");
            Check(RoundDetails() != null && RoundDetails().text.Contains("Out of lives"), "Zero lives displays Game Over reason");
            CheckResultOverlay(ui, true, RoundDetails().text);
            Capture("defeat.png");
            Next(now); return;
        }
        if (ui.State == UIManager.RoundState.Death && now - stageStarted > 2.2)
        {
            Check(ui.CurrentLives == InitialLives - defeatLosses, "Defeat respawn preserves the remaining lives");
            stageStarted = double.MaxValue;
        }
    }

    private static void CheckDefeatMenu(double now)
    {
        if (SceneManager.GetActiveScene().name == "StartScene" && now - stageStarted > 3.2)
            Finish("PASSED");
    }

    private static void DefeatGhost(GhostController ghost, bool twoHits)
    {
        PositionGhost(ghost, new Vector3(4, -1, 0));
        int hits = twoHits ? 2 : 1;
        for (int i = 0; i < hits; i++)
        {
            innovation.CollectPearl(50);
            int score = ui.CurrentScore;
            Check(innovation.TryShoot(), "A target shot is ready");
            PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
            Check(shot != null, "Target shot exists");
            shot.Advance(0.4f); Physics2D.SyncTransforms();
            if (i == hits - 1)
            {
                Check(ghost.IsPermanentlyDefeated && innovation.GetRemainingHits(ghost) == 0,
                    "Target reaches permanent defeat after its final hit");
                Check(ui.CurrentScore == score + 300, "Each popped target awards 300 points once");
            }
        }
    }

    private static void PositionGhost(GhostController ghost, Vector3 position)
    {
        ghost.ResetPosition(); ghost.transform.position = position; ghost.enabled = false;
        Collider2D collider = ghost.GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
        Physics2D.SyncTransforms();
    }

    private static int GhostNumber(GhostController ghost)
    {
        SerializedProperty property = new SerializedObject(ghost).FindProperty("ghostNumber");
        return property == null ? int.MaxValue : property.intValue;
    }

    private static TMP_Text RoundDetails()
    {
        return UIText("roundDetails");
    }

    private static void CheckDeathSprites()
    {
        foreach (int number in new[] { 1, 2, 3, 4 })
        {
            Check(deathSprites.ContainsKey(number) && deathSprites[number].Count == 4,
                "Ghost " + number + " plays all four death sprites");
        }
    }

    private static void CheckMenuRecord(string labelPrefix, string scoreKey, string timeKey, bool hasRecord)
    {
        TMP_Text score = GameObject.Find(labelPrefix + " - HighScoreText").GetComponent<TMP_Text>();
        TMP_Text time = GameObject.Find(labelPrefix + " - TimeText").GetComponent<TMP_Text>();
        if (hasRecord)
        {
            Check(score.text == "HIGH SCORE<pos=62%>" + PlayerPrefs.GetInt(scoreKey).ToString("D6"), labelPrefix + " score record is aligned");
            Check(time.text == "RUN TIME<pos=62%>" + UIManager.FormatTime(PlayerPrefs.GetFloat(timeKey)), labelPrefix + " time record is aligned");
        }
        else
        {
            Check(score.text == "No record yet" && time.text == "Play to set your first score", labelPrefix + " empty records are aligned");
        }
    }

    private static void Next(double now) { stage++; stageStarted = now; WriteReport("RUNNING: stage " + stage + "\n" + string.Join("\n", passed)); }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed.Add(message);
    }

    private static void Capture(string file)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/InnovationChecks"));
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, file));
    }

    private static string ReportPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/InnovationChecks/play-checks.txt")); }

    private static void WriteReport(string report)
    {
        string directory = Path.GetDirectoryName(ReportPath());
        Directory.CreateDirectory(directory); File.WriteAllText(ReportPath(), report);
    }

    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        WriteReport(status + "\n" + string.Join("\n", passed));
        Debug.Log("Innovation Play checks: " + status);
        EditorApplication.isPlaying = false;
    }

    private static void RestoreRecordSnapshot(string name, string scoreKey, string timeKey)
    {
        if (SessionState.GetBool(Flag + "." + name + ".HadScore", false)) PlayerPrefs.SetInt(scoreKey, SessionState.GetInt(Flag + "." + name + ".Score", 0));
        else PlayerPrefs.DeleteKey(scoreKey);
        if (SessionState.GetBool(Flag + "." + name + ".HadTime", false)) PlayerPrefs.SetFloat(timeKey, SessionState.GetFloat(Flag + "." + name + ".Time", 0));
        else PlayerPrefs.DeleteKey(timeKey);
        PlayerPrefs.Save();
    }
}
