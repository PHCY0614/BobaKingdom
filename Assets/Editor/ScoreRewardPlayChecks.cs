using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ScoreRewardPlayChecks
{
    private const string Flag = "PacStudent.ScoreRewardPlayChecks";
    private const string Level1ScoreKey = "PacStudent.Level1Scene.HighScore";
    private const string Level1TimeKey = "PacStudent.Level1Scene.BestTime";
    private const string Level2ScoreKey = "PacStudent.InnovationScene.HighScore";
    private const string Level2TimeKey = "PacStudent.InnovationScene.BestTime";
    private const double TimeoutSeconds = 90d;

    private static readonly List<string> passed = new List<string>();
    private static UIManager ui;
    private static InnovationController innovation;
    private static GhostController[] ghosts;
    private static int stage;
    private static double started, stageStarted;
    private static bool deathStarted;
    private static bool popupMotionChecked;
    private static bool sceneUnloadedCheck;
    private static bool level1CollisionChecked;
    private static bool level1MotionChecked;
    private static bool roundResetChecked;
    private static GameObject observedPopup;
    private static Vector3 observedPopupPosition;
    private static float observedPopupAlpha;

    static ScoreRewardPlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("Bubble Kingdom/Run score reward Play Mode checks")]
    public static void Start()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InnovationScene")
            throw new InvalidOperationException("Open InnovationScene outside Play Mode before running score checks.");
        SaveRecordSnapshot("Level1", Level1ScoreKey, Level1TimeKey);
        SaveRecordSnapshot("Level2", Level2ScoreKey, Level2TimeKey);
        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear(); stage = 0; deathStarted = false; popupMotionChecked = false; sceneUnloadedCheck = false;
            level1CollisionChecked = false; level1MotionChecked = false; roundResetChecked = false;
            observedPopup = null; observedPopupPosition = Vector3.zero; observedPopupAlpha = 0f;
            started = stageStarted = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            if (File.Exists(ReportPath()) && File.ReadAllText(ReportPath()).StartsWith("RUNNING"))
                WriteReport(File.ReadAllText(ReportPath()).Replace("RUNNING", "INTERRUPTED: Play Mode stopped before completion"));
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
            if (now - started > TimeoutSeconds) throw new Exception("Score reward checks exceeded 90 seconds.");
            if (stage == 9)
            {
                if (SceneManager.GetActiveScene().name == "Level1Scene") CheckLevel1Round(now);
                return;
            }
            if (stage == 10 && SceneManager.GetActiveScene().name != "InnovationScene")
                return;
            CacheObjects();
            if (ui == null || innovation == null || ghosts == null || ghosts.Length < 2) return;
            if (stage == 0) CheckReady(now);
            else if (stage == 1) CheckFirstLifePop(now);
            else if (stage == 2) CheckSecondLife(now);
            else if (stage == 3) CheckThirdLife(now);
            else if (stage == 4) CheckFourthLife(now);
            else if (stage == 5) CheckThirdLifeFinal(now);
            else if (stage == 6) CheckFourthLifeFirst(now);
            else if (stage == 7) CheckFourthLifeFinal(now);
            else if (stage == 8) CheckDeathPreservesStage(now);
            else if (stage == 10) CheckNewRoundReset(now);
        }
        catch (Exception error) { Finish("FAILED: " + error.Message); }
    }

    private static void CacheObjects()
    {
        if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (innovation == null && ui != null) innovation = ui.Innovation;
        if (ghosts == null || ghosts.Length < 4)
            ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)
                .OrderBy(ghost => ghost.name).ToArray();
    }

    private static void CheckReady(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        ui.SetGamePaused(true);
        Check(ghosts.All(ghost => innovation.GetRemainingHits(ghost) == 2), "A new round starts every target at two hits");
        PositionGhost(ghosts[0]);
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A first-life injury can be registered");
        Check(ui.CurrentScore == score + 50, "First-life injury awards 50 points");
        CheckFloatingScore(50, ghosts[0].transform.position);
        Capture("initial.png");
        stageStarted = now; stage = 1;
    }

    private static void CheckFirstLifePop(double now)
    {
        if (!popupMotionChecked && now - stageStarted >= 0.2)
        {
            CheckFloatingMotion();
            Capture("mid.png");
            popupMotionChecked = true;
        }
        if (now - stageStarted < 0.7) return;
        Check(observedPopup == null || !observedPopup.activeInHierarchy, "FloatingScore is destroyed after 0.6 seconds");
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A first-life final hit can be registered");
        Check(ui.CurrentScore == score + 250, "First-life final hit awards 250 points");
        CheckFloatingScore(250, ghosts[0].transform.position);
        InvokeRespawn(ghosts[0]);
        stageStarted = now; stage = 2;
    }

    private static void CheckSecondLife(double now)
    {
        if (now - stageStarted < 0.2) return;
        PositionGhost(ghosts[0]);
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A first-respawn injury can be registered");
        Check(ui.CurrentScore == score + 25, "First-respawn injury awards 25 points");
        stageStarted = now; stage = 3;
    }

    private static void CheckThirdLife(double now)
    {
        if (now - stageStarted < 0.7) return;
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A first-respawn final hit can be registered");
        Check(ui.CurrentScore == score + 125, "First-respawn final hit awards 125 points");
        InvokeRespawn(ghosts[0]);
        stageStarted = now; stage = 4;
    }

    private static void CheckFourthLife(double now)
    {
        if (now - stageStarted < 0.7) return;
        PositionGhost(ghosts[0]);
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A second-respawn injury can be registered");
        Check(ui.CurrentScore == score, "Second-respawn injury awards zero points");
        Check(FindFloatingScore() == null, "Zero-point injuries do not create a floating score");
        stageStarted = now; stage = 5;
    }

    private static void CheckThirdLifeFinal(double now)
    {
        if (now - stageStarted < 0.7) return;
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A second-respawn final hit can be registered");
        Check(ui.CurrentScore == score + 60, "Second-respawn final hit awards 60 points");
        InvokeRespawn(ghosts[0]);
        stageStarted = now; stage = 6;
    }

    private static void CheckFourthLifeFirst(double now)
    {
        if (now - stageStarted < 0.7) return;
        PositionGhost(ghosts[0]);
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A third-respawn injury can be registered");
        Check(ui.CurrentScore == score, "Third-respawn injury awards zero points");
        Check(FindFloatingScore() == null, "Third-respawn zero injury creates no floating score");
        stageStarted = now; stage = 7;
    }

    private static void CheckFourthLifeFinal(double now)
    {
        if (now - stageStarted < 0.7) return;
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A third-respawn final hit can be registered");
        Check(ui.CurrentScore == score, "Third-respawn final hit awards zero points");
        Check(FindFloatingScore() == null, "Zero-point final hits create no floating score");
        stageStarted = now; stage = 8;
    }

    private static void CheckDeathPreservesStage(double now)
    {
        if (!deathStarted)
        {
            PositionGhost(ghosts[1]);
            int score = ui.CurrentScore;
            Check(innovation.HitGhost(ghosts[1]), "An independent target can be injured");
            Check(ui.CurrentScore == score + 50, "Independent target starts at first-life reward");
            Check(innovation.GetRespawnRemaining(ghosts[0]) > 0f, "The third-respawn target remains in its zero-reward cooldown before death");
            ui.LoseLife();
            deathStarted = true; stageStarted = now;
            return;
        }
        if (ui.State == UIManager.RoundState.Death || now - stageStarted < 2.2) return;
        if (ui.State != UIManager.RoundState.Running) return;
        Check(innovation.GetRemainingHits(ghosts[1]) == 1, "Player death preserves the target injury stage");
        ui.SetGamePaused(true);

        Check(ghosts[0].IsPermanentlyDefeated, "The third-respawn zero-reward target remains defeated after player death");
        InvokeRespawn(ghosts[0]);
        PositionGhost(ghosts[0]);
        int zeroStageScore = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "The post-death third-respawn target can be hit");
        Check(ui.CurrentScore == zeroStageScore && innovation.GetRemainingHits(ghosts[0]) == 1,
            "The third-respawn target still awards zero on its first hit after death");
        Check(innovation.HitGhost(ghosts[0]), "The post-death third-respawn target can be defeated");
        Check(ui.CurrentScore == zeroStageScore && FindFloatingScore() == null,
            "The third-respawn target still awards zero on its final hit after death");

        int scoreAfterDeath = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[1]), "The preserved target can receive its final hit");
        Check(ui.CurrentScore == scoreAfterDeath + 250, "Player death does not reduce the target's current-life reward");
        SceneManager.LoadScene("Level1Scene");
        ui = null; innovation = null; ghosts = null;
        stageStarted = now; stage = 9;
    }

    private static void CheckLevel1Round(double now)
    {
        if (level1CollisionChecked && now - stageStarted < 0.2) return;
        if (level1CollisionChecked)
        {
            if (!level1MotionChecked)
            {
                CheckFloatingMotion();
                level1MotionChecked = true;
            }
            SceneManager.LoadScene("InnovationScene");
            ui = null; innovation = null; ghosts = null;
            stageStarted = now; stage = 10;
            return;
        }
        UIManager level1Ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        PacStudentController level1Player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
        GhostController level1Ghost = UnityEngine.Object.FindFirstObjectByType<GhostController>();
        if (level1Ui == null || level1Player == null || level1Ghost == null || level1Ui.State != UIManager.RoundState.Running) return;
        level1Ui.SetGamePaused(true);
        level1Player.ResetPosition();
        level1Ghost.ResetPosition();
        level1Ghost.SetState(GhostState.Scared);
        Collider2D ghostCollider = level1Ghost.GetComponent<Collider2D>();
        if (ghostCollider != null) ghostCollider.enabled = true;
        MethodInfo trigger = typeof(PacStudentController).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(trigger != null, "PacStudent collision callback is available for Level 1 scoring");
        int score = level1Ui.CurrentScore;
        trigger.Invoke(level1Player, new object[] { ghostCollider });
        Check(level1Ui.CurrentScore == score + 300, "Level 1 scared-ghost collision awards 300 points");
        CheckFloatingScore(300, level1Ghost.transform.position);
        Capture("level1.png");
        level1CollisionChecked = true;
        stageStarted = now;
    }

    private static void CheckNewRoundReset(double now)
    {
        if (ui == null || innovation == null || ghosts == null || ghosts.Length < 4) return;
        if (!sceneUnloadedCheck)
        {
            Check(FindFloatingScore() == null, "Loading another scene cleans up the Level 1 floating score");
            sceneUnloadedCheck = true;
        }
        if (ui.State == UIManager.RoundState.Countdown)
        {
            if (!roundResetChecked)
            {
                Check(ghosts.All(ghost => innovation.GetRemainingHits(ghost) == 2), "Loading a new round resets every target reward stage");
                roundResetChecked = true;
            }
            return;
        }
        if (ui.State != UIManager.RoundState.Running) return;
        ui.SetGamePaused(true); PositionGhost(ghosts[0]);
        int score = ui.CurrentScore;
        Check(innovation.HitGhost(ghosts[0]), "A new round accepts the first target hit");
        Check(ui.CurrentScore == score + 50, "A new round restores the 50-point first injury reward");
        Finish("PASSED");
    }

    private static void CheckFloatingScore(int expected)
    {
        GameObject popup = FindFloatingScore();
        Check(popup != null, "Positive score creates a FloatingScore object");
        TMP_Text text = popup.GetComponentInChildren<TMP_Text>(true);
        Check(text != null && text.text == "+" + expected, "FloatingScore displays the exact awarded points");
        Check(text.color.a > 0f, "FloatingScore starts visible");
        Check(text.transform.localScale == Vector3.one, "FloatingScore keeps the requested small scale");
        Check(!text.raycastTarget, "FloatingScore does not block gameplay input");
        observedPopup = popup;
        observedPopupPosition = popup.transform.position;
        observedPopupAlpha = text.color.a;
    }

    private static void CheckFloatingScore(int expected, Vector3 worldPosition)
    {
        CheckFloatingScore(expected);
        Camera camera = Camera.main;
        if (camera != null)
        {
            Canvas canvas = observedPopup.GetComponentInParent<Canvas>();
            Vector3 expectedPosition = camera.WorldToScreenPoint(worldPosition + Vector3.up * 1.15f);
            Vector3 actualPosition = observedPopup.transform.position;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
                actualPosition = canvas.worldCamera.WorldToScreenPoint(actualPosition);
            Check(Vector2.Distance(new Vector2(actualPosition.x, actualPosition.y),
                    new Vector2(expectedPosition.x, expectedPosition.y)) < 1f,
                "FloatingScore starts above the hit position and clears the ghost label");
        }
    }

    private static void CheckFloatingMotion()
    {
        Check(observedPopup != null && observedPopup.activeInHierarchy, "FloatingScore remains alive during its fade interval");
        TMP_Text text = observedPopup == null ? null : observedPopup.GetComponentInChildren<TMP_Text>(true);
        Check(text != null && observedPopup.transform.position.y > observedPopupPosition.y,
            "FloatingScore moves upward during its fade interval");
        Check(text != null && text.color.a < observedPopupAlpha,
            "FloatingScore fades during its fade interval");
        Check(text != null && text.transform.localScale == Vector3.one, "FloatingScore does not scale up while fading");
        Check(text == null || !text.raycastTarget, "FloatingScore remains non-interactive while fading");
    }

    private static GameObject FindFloatingScore()
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform => transform.name == "FloatingScore" && transform.gameObject.activeInHierarchy)
            .Select(transform => transform.gameObject)
            .OrderByDescending(gameObject => gameObject.GetInstanceID())
            .FirstOrDefault();
    }

    private static void PositionGhost(GhostController ghost)
    {
        ghost.ResetPosition(); ghost.transform.position = new Vector3(4, -1, 0); ghost.enabled = false;
        Collider2D collider = ghost.GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
        Physics2D.SyncTransforms();
    }

    private static void InvokeRespawn(GhostController ghost)
    {
        MethodInfo update = typeof(InnovationController).GetMethod("UpdateRespawns", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(update != null, "Respawn hook is available for reward stage testing");
        update.Invoke(innovation, new object[] { 10.1f });
    }

    private static void Capture(string file)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ScoreChecks"));
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, file));
    }

    private static void SaveRecordSnapshot(string name, string scoreKey, string timeKey)
    {
        SessionState.SetBool(Flag + "." + name + ".HadScore", PlayerPrefs.HasKey(scoreKey));
        SessionState.SetBool(Flag + "." + name + ".HadTime", PlayerPrefs.HasKey(timeKey));
        SessionState.SetInt(Flag + "." + name + ".Score", PlayerPrefs.GetInt(scoreKey));
        SessionState.SetFloat(Flag + "." + name + ".Time", PlayerPrefs.GetFloat(timeKey));
    }

    private static void RestoreRecordSnapshot(string name, string scoreKey, string timeKey)
    {
        if (SessionState.GetBool(Flag + "." + name + ".HadScore", false)) PlayerPrefs.SetInt(scoreKey, SessionState.GetInt(Flag + "." + name + ".Score", 0));
        else PlayerPrefs.DeleteKey(scoreKey);
        if (SessionState.GetBool(Flag + "." + name + ".HadTime", false)) PlayerPrefs.SetFloat(timeKey, SessionState.GetFloat(Flag + "." + name + ".Time", 0));
        else PlayerPrefs.DeleteKey(timeKey);
        PlayerPrefs.Save();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed.Add(message);
    }

    private static string ReportPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ScoreChecks/report.txt")); }
    private static void WriteReport(string report)
    {
        string directory = Path.GetDirectoryName(ReportPath());
        Directory.CreateDirectory(directory); File.WriteAllText(ReportPath(), report);
    }

    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        WriteReport(status + "\n" + string.Join("\n", passed));
        Debug.Log("Score reward Play checks: " + status);
        EditorApplication.isPlaying = false;
    }
}
