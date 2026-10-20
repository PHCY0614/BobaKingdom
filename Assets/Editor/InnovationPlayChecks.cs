using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private const double TimeoutSeconds = 120d;

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
    private static bool noPopsMode;
    private static bool cooldownProbePending;
    private static int goldenRushStage;
    private static double goldenRushStartedAt;
    private static float pausedGoldenRush;
    private static float pausedRoundTimer;
    private static float pausedCooldown;
    private static float goldenRushCooldownBeforeSecond;
    private static bool goldenRushRechargeStarted;
    private static bool goldenRushCaptured;
    private static bool firstHitCaptured;
    private static bool deathRespawnFrozenChecked;
    private static float deathPausedRespawn;
    private static float pausedRespawn;
    private static Vector3 revivedSpawnPosition;
    private static double respawnStartedAt;
    private static int respawnStage;
    private static bool timerFreezeChecked;
    private static bool menuCaptured;
    private static float stoppedTimer;
    private static readonly Dictionary<int, HashSet<string>> deathSprites = new Dictionary<int, HashSet<string>>();
    private static readonly Dictionary<int, Vector3> ghostSpawns = new Dictionary<int, Vector3>();
    private static readonly Dictionary<int, Color> ghostColours = new Dictionary<int, Color>();

    static InnovationPlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("PacStudent/Run Innovation Play Mode checks")]
    public static void Start() { Begin(false); }

    [MenuItem("PacStudent/Run Innovation defeat Play Mode checks")]
    public static void StartDefeatCheck() { Begin(true); }

    [MenuItem("PacStudent/Run Innovation no-pops Play Mode checks")]
    public static void StartNoPopsCheck() { Begin(false, true); }

    private static void Begin(bool defeat) { Begin(defeat, false); }

    private static void Begin(bool defeat, bool ghostsFirst)
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InnovationScene")
            throw new InvalidOperationException("Open InnovationScene outside Play Mode before running checks.");
        defeatMode = defeat;
        noPopsMode = ghostsFirst;
        SaveRecordSnapshot("Level1", Level1ScoreKey, Level1TimeKey);
        SaveRecordSnapshot("Level2", Level2ScoreKey, Level2TimeKey);
        SessionState.SetBool(Flag + ".Defeat", defeat);
        SessionState.SetBool(Flag + ".NoPops", ghostsFirst);
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
            noPopsMode = SessionState.GetBool(Flag + ".NoPops", false);
            passed.Clear(); stage = 0; defeatLosses = 0;
            cooldownProbePending = false; goldenRushStage = 0; timerFreezeChecked = false; menuCaptured = false;
            goldenRushStartedAt = 0d; pausedGoldenRush = 0f; pausedRoundTimer = 0f; pausedCooldown = 0f;
            goldenRushCooldownBeforeSecond = 0f; goldenRushRechargeStarted = false; goldenRushCaptured = false;
            deathPausedRespawn = 0f; pausedRespawn = 0f; revivedSpawnPosition = Vector3.zero;
            respawnStartedAt = 0d; respawnStage = 0;
            firstHitCaptured = false; deathRespawnFrozenChecked = false;
            deathSprites.Clear();
            ghostSpawns.Clear(); ghostColours.Clear();
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
            if (now - started > TimeoutSeconds) throw new Exception("Innovation checks exceeded 120 seconds.");
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
            else if (noPopsMode && stage == 11) CheckNoPopsVictory(now);
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
            if (field == "targetStatusText")
                Check(label.text.ToUpperInvariant().Contains("PEARLS LEFT"),
                    "Target HUD displays the remaining pearl count");
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

    private static Material InnovationMaterial(string field)
    {
        if (innovation == null) return null;
        SerializedProperty property = new SerializedObject(innovation).FindProperty(field);
        return property == null ? null : property.objectReferenceValue as Material;
    }

    private static void CheckGoldenRushStatus(bool active, int expectedCharges = -1, int expectedCapacity = -1,
        bool requireRecharge = false)
    {
        TMP_Text status = InnovationText("shotStatusText");
        Check(status != null, "Shot status text is assigned for Golden Rush");
        status.ForceMeshUpdate();
        Check(!status.isTextOverflowing, "Golden Rush shot instructions fit without overflow");
        string text = status.text.ToUpperInvariant();
        if (active)
            Check(text.Contains("GOLDEN RUSH") && text.Contains("SHOTS") && text.Contains("SPACE")
                && !text.Contains("HOLD SPACE"),
                "Golden Rush HUD shows charges, recharge, and space instructions");
        else
            Check(!text.Contains("GOLDEN RUSH") && !text.Contains("HOLD SPACE") && text.Contains("SHOTS")
                && text.Contains("SPACE"),
                "Shot HUD returns to the normal ready or cooldown instructions");
        if (expectedCharges >= 0 && expectedCapacity >= 0)
            Check(text.Contains("SHOTS " + expectedCharges + " / " + expectedCapacity),
                "Shot HUD displays the expected available charges");
        if (requireRecharge)
            Check((text.Contains("RECHARGE") || text.Contains("+1:")), "Shot HUD displays the active recharge countdown");
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
        Check(innovation.ShotCapacity == 1 && innovation.ShotCharges == 1,
            "Countdown starts with one available normal shot");
        Check(!innovation.IsGoldenRushActive && innovation.GoldenRushRemaining == 0f,
            "Countdown starts without Golden Rush");
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
            int number = GhostNumber(ghost);
            ghostSpawns[number] = ghost.transform.position;
            SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
            if (renderer != null) ghostColours[number] = renderer.color;
        }
        Physics2D.SyncTransforms();
        ui.SetGamePaused(true);
        Check(player.transform.position == new Vector3(1, -1, 0), "Running test uses the player spawn position");
        Check(ghosts.Length == 4 && ghosts.All(ghost => innovation.GetRemainingHits(ghost) == TargetHits),
            "Running keeps all target hit counts intact");
        CheckInnovationHealth(ghosts[0], 2);
        CheckHudLayout(true);
        Capture("running.png");
        Next(now);
    }

    private static void CheckShootingAndCollectibles(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        if (goldenRushStage == 0)
        {
            if (!cooldownProbePending)
            {
                Check(innovation.TryShoot(), "Running allows a shot");
                Check(innovation.CooldownRemaining == 3f && innovation.ActiveShotCount == 1
                    && innovation.ShotCapacity == 1 && innovation.ShotCharges == 0,
                    "A normal shot starts a three second recharge with no charge left");
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
            int normalRefillSteps = 0;
            while (innovation.CooldownRemaining > 0.001f && normalRefillSteps++ < 20)
                innovation.CollectPearl(10);
            Check(innovation.CooldownRemaining == 0f && innovation.ShotCapacity == 1
                && innovation.ShotCharges == 1,
                "Normal pearl recharge reaches zero and restores the single shot");
            score = ui.CurrentScore;
            ui.StartScaredTimer(10f);
            ui.CollectPellet(50);
            Check(ui.CurrentScore == score + 50 && innovation.CooldownRemaining == 0f
                && innovation.ShotCapacity == 2 && innovation.ShotCharges == 2,
                "A large pearl scores fifty and refills two Golden Rush shots");
            Check(ui.GetScaredTimeRemaining() == 0f && ghosts.All(ghost => ghost.currentState == GhostState.Normal),
                "Innovation pearls never activate Scared ghosts");
            Check(ui.State == UIManager.RoundState.Running && ui.RemainingPellets > 0,
                noPopsMode ? "No-pops mode keeps pearls for the final victory" : "Pearls remain for the final victory");
        }
        if (!CheckGoldenRush(now)) return;
        if (noPopsMode)
        {
            stage = 11; stageStarted = now;
            return;
        }
        if (defeatMode)
        {
            Next(now);
            stage = 10; stageStarted = now;
            return;
        }
        Next(now);
    }

    private static bool CheckGoldenRush(double now)
    {
        if (goldenRushStage == 0)
        {
            CheckChargeBoundaries();
            innovation.ClearShots();
            int score = ui.CurrentScore;
            innovation.CollectPearl(50);
            Check(ui.CurrentScore == score && innovation.IsGoldenRushActive
                && innovation.GoldenRushRemaining > 5.8f && innovation.GoldenRushRemaining <= 6.05f
                && innovation.ShotCapacity == 2 && innovation.ShotCharges == 2
                && innovation.CooldownRemaining == 0f,
                "A large pearl starts a six second Golden Rush without extra score");
            Material goldenMaterial = InnovationMaterial("goldenPearlMaterial");
            Check(goldenMaterial != null && goldenMaterial.shader != null && goldenMaterial.shader.isSupported,
                "Golden pearl material is assigned with a supported shader");
            Check(innovation.TryShoot(), "Golden Rush allows the first empowered shot");
            Check(innovation.ShotCharges == 1 && innovation.ShotCapacity == 2
                && Mathf.Abs(innovation.CooldownRemaining - 3f) < 0.02f,
                "The first full Golden Rush shot starts a three second recharge");
            PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
            Check(shot != null, "Golden Rush creates a PearlShot");
            SpriteRenderer renderer = shot.GetComponent<SpriteRenderer>();
            TrailRenderer trail = shot.GetComponent<TrailRenderer>();
            Check(renderer != null && renderer.sharedMaterial == goldenMaterial,
                "Golden Rush shot uses the golden material");
            Check(renderer != null && renderer.transform.localScale.x > 0.75f,
                "Golden Rush shot uses the larger projectile scale");
            Check(trail != null && trail.enabled && trail.emitting && trail.sharedMaterial == goldenMaterial
                && trail.startWidth > 0.08f, "Golden Rush shot uses an active larger golden trail");
            shot.Advance(0.1f); Physics2D.SyncTransforms();
            float cooldownAfterFirst = innovation.CooldownRemaining;
            Check(innovation.TryShoot() && innovation.ActiveShotCount == 2 && innovation.ShotCharges == 0,
                "The second Golden Rush charge fires immediately");
            Check(Mathf.Abs(innovation.CooldownRemaining - cooldownAfterFirst) < 0.02f,
                "Using the second charge does not reset the existing recharge");
            Check(!innovation.TryShoot() && innovation.ActiveShotCount == 2,
                "An empty Golden Rush charge pool blocks a third shot");
            Check(ui.CurrentScore == score, "Two empty Golden Rush shots do not add score");
            goldenRushStartedAt = now; goldenRushStage = 1;
            return false;
        }
        if (goldenRushStage == 1)
        {
            if (now - goldenRushStartedAt < 0.05) return false;
            if (!goldenRushCaptured)
            {
                PearlShot shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
                Check(shot != null && shot.gameObject.activeInHierarchy,
                    "Golden Rush screenshot keeps the empowered shot visible after a render frame");
                Check(UnityEngine.Object.FindObjectsByType<PearlShot>(FindObjectsSortMode.None).Length == 2,
                    "Golden Rush screenshot retains both fired charges");
                CheckGoldenRushStatus(true, 0, 2, true);
                float beforeRefresh = innovation.GoldenRushRemaining;
                int score = ui.CurrentScore;
                innovation.CollectPearl(50);
                Check(ui.CurrentScore == score && innovation.GoldenRushRemaining <= 6.05f
                    && innovation.GoldenRushRemaining >= beforeRefresh - 0.01f,
                    "A second large pearl refreshes Golden Rush without stacking its timer");
                Check(innovation.ShotCapacity == 2 && innovation.ShotCharges == 2
                    && innovation.CooldownRemaining == 0f,
                    "Refreshing with a large pearl restores both charges and clears recharge");
                Capture("golden-rush.png");
                goldenRushCaptured = true;
                return false;
            }
            innovation.ClearShots();
            ui.SetGamePaused(false);
            goldenRushRechargeStarted = false;
            goldenRushStartedAt = now; goldenRushStage = 2;
            return false;
        }
        if (goldenRushStage == 2)
        {
            if (!goldenRushRechargeStarted)
            {
                Check(innovation.IsGoldenRushActive, "Golden Rush remains active before its expiry window");
                CheckGoldenRushStatus(true, 2, 2);
                Check(innovation.TryShoot() && innovation.ShotCharges == 1,
                    "A remaining Golden Rush charge can fire during recharge");
                goldenRushCooldownBeforeSecond = innovation.CooldownRemaining;
                Check(Mathf.Abs(goldenRushCooldownBeforeSecond - 3f) < 0.02f,
                    "A full recharge starts at three seconds");
                goldenRushRechargeStarted = true;
                goldenRushStartedAt = now;
                return false;
            }
            if (now - goldenRushStartedAt < 0.65) return false;
            int score = ui.CurrentScore;
            Check(innovation.TryShoot() && innovation.ShotCharges == 0,
                "The remaining charge fires after 0.65 seconds");
            Check(innovation.CooldownRemaining < goldenRushCooldownBeforeSecond - 0.2f
                && innovation.CooldownRemaining > 2f,
                "Using a charge during recharge does not reset its timer");
            Check(ui.CurrentScore == score, "An empty Golden Rush charge shot does not add score");
            innovation.ClearShots();
            ui.SetGamePaused(true);
            pausedGoldenRush = innovation.GoldenRushRemaining;
            pausedRoundTimer = ui.ElapsedTime;
            pausedCooldown = innovation.CooldownRemaining;
            goldenRushStartedAt = now; goldenRushStage = 3;
            return false;
        }
        if (goldenRushStage == 3)
        {
            if (now - goldenRushStartedAt < 0.5) return false;
            Check(Mathf.Abs(innovation.GoldenRushRemaining - pausedGoldenRush) < 0.001f
                && Mathf.Abs(innovation.CooldownRemaining - pausedCooldown) < 0.001f
                && Mathf.Abs(ui.ElapsedTime - pausedRoundTimer) < 0.001f,
                "Pausing the running round freezes Golden Rush, recharge, and the round timer");
            CheckGoldenRushStatus(true, 0, 2, true);
            Capture("charge-empty.png");
            ui.SetGamePaused(false);
            goldenRushStartedAt = now; goldenRushStage = 4;
            return false;
        }
        if (goldenRushStage == 4)
        {
            if (innovation.ShotCharges < 1) return false;
            Check(innovation.IsGoldenRushActive && innovation.ShotCapacity == 2 && innovation.ShotCharges == 1,
                "One Golden Rush charge returns after three seconds");
            Check(innovation.CooldownRemaining > 2.5f,
                "The next Golden Rush charge is already recharging while one shot is available");
            CheckGoldenRushStatus(true, 1, 2, true);
            float cooldownBeforeFinal = innovation.CooldownRemaining;
            Check(innovation.TryShoot() && innovation.ShotCharges == 0,
                "The final Golden Rush charge can be consumed");
            Check(Mathf.Abs(innovation.CooldownRemaining - cooldownBeforeFinal) < 0.001f,
                "Consuming the restored charge preserves the next recharge timer");
            innovation.ClearShots();
            ui.SetGamePaused(true);
            pausedGoldenRush = innovation.GoldenRushRemaining;
            pausedRoundTimer = ui.ElapsedTime;
            pausedCooldown = innovation.CooldownRemaining;
            goldenRushStartedAt = now; goldenRushStage = 5;
            return false;
        }
        if (goldenRushStage == 5)
        {
            if (now - goldenRushStartedAt < 0.5) return false;
            Check(Mathf.Abs(innovation.GoldenRushRemaining - pausedGoldenRush) < 0.001f
                && Mathf.Abs(innovation.CooldownRemaining - pausedCooldown) < 0.001f
                && Mathf.Abs(ui.ElapsedTime - pausedRoundTimer) < 0.001f,
                "Pausing after the final charge freezes Golden Rush and recharge");
            ui.SetGamePaused(false);
            goldenRushStartedAt = now; goldenRushStage = 6;
            return false;
        }
        if (goldenRushStage == 6)
        {
            if (innovation.IsGoldenRushActive) return false;
            Check(innovation.GoldenRushRemaining <= 0.001f && innovation.ShotCapacity == 1
                && innovation.ShotCharges <= 1 && innovation.CooldownRemaining < pausedCooldown - 0.1f,
                "Golden Rush expires at one charge without granting or resetting recharge");
            CheckGoldenRushStatus(false, innovation.ShotCharges, 1, innovation.CooldownRemaining > 0.001f);
            if (innovation.ShotCharges < 1) return false;
            Check(innovation.ShotCharges == 1 && innovation.ShotCapacity == 1,
                "The normal single charge returns after its existing recharge");
            CheckGoldenRushStatus(false, 1, 1);
            int score = ui.CurrentScore;
            float cooldownBeforeNormal = innovation.CooldownRemaining;
            Check(innovation.TryShoot() && innovation.ShotCharges == 0,
                "Normal mode returns to one available shot");
            Check(cooldownBeforeNormal > 0.001f
                ? innovation.CooldownRemaining <= cooldownBeforeNormal + 0.02f
                : Mathf.Abs(innovation.CooldownRemaining - 3f) < 0.02f,
                "Normal mode preserves or starts the single shot recharge");
            Check(ui.CurrentScore == score, "The normal post-rush shot does not add score");
            innovation.ClearShots();
            goldenRushStartedAt = now; goldenRushStage = 7;
            return false;
        }
        if (now - goldenRushStartedAt < 3.1) return false;
        Check(innovation.CooldownRemaining <= 0.001f && innovation.ShotCharges == 1
            && innovation.ShotCapacity == 1 && !innovation.IsGoldenRushActive,
            "The normal single shot is ready before the wall test");
        ui.SetGamePaused(true);
        return true;
    }

    private static void CheckChargeBoundaries()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var advance = typeof(InnovationController).GetMethod("UpdateShotCharge", flags);
        var rushTime = typeof(InnovationController).GetField("goldenRushRemaining", flags);
        Check(advance != null && rushTime != null, "Charge timer boundary hooks are available");
        innovation.CollectPearl(50);
        advance.Invoke(innovation, new object[] { 6.1f });
        Check(innovation.ShotCapacity == 1 && innovation.ShotCharges == 1 && innovation.CooldownRemaining == 0f,
            "Expiring a full two-charge pool keeps only one shot");
        innovation.CollectPearl(50);
        Check(innovation.TryShoot() && innovation.TryShoot(), "Boundary probe consumes both charges");
        innovation.ClearShots();
        advance.Invoke(innovation, new object[] { 3.25f });
        Check(innovation.ShotCharges == 1 && Mathf.Abs(innovation.CooldownRemaining - 2.75f) < 0.001f,
            "Recharge restores one shot and carries elapsed time into the next charge");
        Check(innovation.TryShoot() && Mathf.Abs(innovation.CooldownRemaining - 2.75f) < 0.001f,
            "Using a restored charge preserves partial recharge progress");
        innovation.ClearShots();
        rushTime.SetValue(innovation, 0.1f);
        advance.Invoke(innovation, new object[] { 0.2f });
        Check(!innovation.IsGoldenRushActive && innovation.ShotCapacity == 1 && innovation.ShotCharges == 0
            && Mathf.Abs(innovation.CooldownRemaining - 2.55f) < 0.001f && !innovation.TryShoot(),
            "An empty pool stays empty at expiry and keeps its recharge progress");
        innovation.ResetAfterDeath();
        Check(innovation.ShotCharges == 1 && innovation.ShotCapacity == 1 && innovation.CooldownRemaining == 0f,
            "Reset restores one normal ready shot after timer boundary probes");
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
        PearlShot shot;
        if (!firstHitCaptured)
        {
            PositionGhost(ghosts[0], new Vector3(4, -1, 0));
            innovation.CollectPearl(50);
            Check(innovation.TryShoot(), "A refilled shot can target a ghost");
            shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
            Check(shot != null, "Hit test creates a PearlShot");
            shot.Advance(0.4f); Physics2D.SyncTransforms();
            Check(innovation.GetRemainingHits(ghosts[0]) == 1 && !ghosts[0].IsPermanentlyDefeated,
                "The first ghost hit injures without popping");
            CheckInnovationHealth(ghosts[0], 1);
            Capture("health-injured.png");
            firstHitCaptured = true;
            return;
        }
        int score = ui.CurrentScore;
        innovation.CollectPearl(50);
        Check(innovation.TryShoot(), "The second ghost hit can be fired after refill");
        shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Second hit creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(innovation.GetRemainingHits(ghosts[0]) == 0 && ghosts[0].IsPermanentlyDefeated
            && !ghosts[0].GetComponent<Collider2D>().enabled,
            "Two hits temporarily pop one ghost and disable its collider");
        CheckInnovationHealth(ghosts[0], 0);
        Check(innovation.GetRespawnRemaining(ghosts[0]) > 9.5f
            && innovation.GetRespawnRemaining(ghosts[0]) <= 10.05f,
            "A popped ghost starts a ten second respawn countdown");
        Check(ui.CurrentScore == score + 300, "A popped ghost awards 300 points once");
        innovation.CollectPearl(50);
        score = ui.CurrentScore;
        Check(innovation.TryShoot(), "A duplicate hit probe can be fired after refill");
        shot = UnityEngine.Object.FindFirstObjectByType<PearlShot>();
        Check(shot != null, "Duplicate hit probe creates a PearlShot");
        shot.Advance(0.4f); Physics2D.SyncTransforms();
        Check(ui.CurrentScore == score
            && innovation.GetRemainingHits(ghosts[0]) == 0,
            "Repeated hits on a popped ghost do not add score");
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
        CheckInnovationHealth(ghosts[1], 1);
        PositionGhost(ghosts[1], new Vector3(14, -1, 0));
        innovation.CollectPearl(50); innovation.TryShoot();
        Check(innovation.ActiveShotCount == 1, "An active projectile exists before player death");
        Check(innovation.IsGoldenRushActive && innovation.GoldenRushRemaining > 0f,
            "Player death is tested while Golden Rush is active");
        deathPausedRespawn = innovation.GetRespawnRemaining(ghosts[0]);
        ui.LoseLife();
        Check(ui.State == UIManager.RoundState.Death && ui.CurrentLives == 2,
            "Player death removes one life and pauses the round");
        Check(innovation.ActiveShotCount == 0 && !innovation.IsGoldenRushActive
            && innovation.GoldenRushRemaining <= 0.001f && !innovation.TryShoot()
            && Mathf.Abs(innovation.GetRespawnRemaining(ghosts[0]) - deathPausedRespawn) < 0.001f,
            "Death clears projectiles, cancels Golden Rush, and blocks shooting");
        stageStarted = now; stage++;
    }

    private static void CheckRemainingTargets(double now)
    {
        if (ui.State == UIManager.RoundState.Death)
        {
            if (!deathRespawnFrozenChecked)
            {
                Check(Mathf.Abs(innovation.GetRespawnRemaining(ghosts[0]) - deathPausedRespawn) < 0.05f,
                    "Player death keeps the popped ghost respawn countdown frozen");
                deathRespawnFrozenChecked = true;
            }
            return;
        }
        if (respawnStage == 0)
        {
            if (ui.State != UIManager.RoundState.Running) return;
            Check(ui.CurrentLives == 2 && player.transform.position == new Vector3(1, -1, 0),
                "Respawn preserves the second life and player position");
            Check(ghosts[0].IsPermanentlyDefeated && !ghosts[0].gameObject.activeSelf,
                "A popped ghost remains down during the player death countdown");
            float runningRespawn = innovation.GetRespawnRemaining(ghosts[0]);
            Check(runningRespawn <= deathPausedRespawn && runningRespawn > deathPausedRespawn - 0.2f,
                "The popped ghost respawn countdown resumes after player death");
            Check(innovation.GetRemainingHits(ghosts[1]) == 1
                && innovation.ActiveShotCount == 0 && innovation.CooldownRemaining == 0f
                && innovation.ShotCapacity == 1 && innovation.ShotCharges == 1,
                "Respawn preserves injured progress while resetting the shot");
            Check(!innovation.IsGoldenRushActive && innovation.GoldenRushRemaining <= 0.001f,
                "Respawn keeps Golden Rush cancelled");
            Check(ui.GetScaredTimeRemaining() == 0f, "Respawn keeps the Innovation scared timer inactive");
            pausedRespawn = innovation.GetRespawnRemaining(ghosts[0]);
            ui.SetGamePaused(true);
            respawnStartedAt = now; respawnStage = 1;
            return;
        }
        if (respawnStage == 1)
        {
            if (now - respawnStartedAt < 0.5) return;
            Check(Mathf.Abs(innovation.GetRespawnRemaining(ghosts[0]) - pausedRespawn) < 0.001f,
                "Pausing the round freezes the ghost respawn countdown");
            ui.SetGamePaused(false);
            respawnStartedAt = now; respawnStage = 2;
            return;
        }
        if (respawnStage == 2)
        {
            if (ghosts[0].IsPermanentlyDefeated) return;
            Check(innovation.GetRespawnRemaining(ghosts[0]) <= 0.001f,
                "A ghost revives after ten seconds of running time");
            CheckRevivedGhost(ghosts[0]);
            Capture("respawn.png");
            revivedSpawnPosition = ghosts[0].transform.position;
            respawnStartedAt = now; respawnStage = 3;
            return;
        }
        if (respawnStage == 3)
        {
            if (now - respawnStartedAt < 0.5) return;
            Check(Vector3.Distance(ghosts[0].transform.position, revivedSpawnPosition) > 0.01f,
                "A revived ghost resumes movement");
            ui.SetGamePaused(true);
            DefeatGhost(ghosts[0], true, false);
            Check(innovation.GetRespawnRemaining(ghosts[0]) > 9.5f,
                "A revived ghost starts a fresh respawn countdown when popped again");
            DefeatGhost(ghosts[1], false, true);
            DefeatGhost(ghosts[2], true, true);
            DefeatGhost(ghosts[3], true, true);
            Check(PoppedCount() == 4 && ui.State == UIManager.RoundState.Running && ui.RemainingPellets > 0,
                "All ghosts can be down while the round still waits for pearls");
            int remaining = ui.RemainingPellets;
            for (int i = 1; i < remaining; i++) ui.CollectPellet(10);
            Check(ui.State == UIManager.RoundState.Running && ui.RemainingPellets == 1,
                "The round stays running until its final pearl");
            int scoreBeforeFinalPearl = ui.CurrentScore;
            ui.CollectPellet(10);
            Check(ui.State == UIManager.RoundState.GameOver && ui.RemainingPellets == 0,
                "The final pearl completes the round");
            Check(ui.CurrentScore == scoreBeforeFinalPearl + 10, "The final pearl is scored once");
            CheckNoRespawnAfterGameOver();
            int scoreAfterCompletion = ui.CurrentScore;
            ui.CollectPellet(10);
            Check(ui.State == UIManager.RoundState.GameOver && ui.CurrentScore == scoreAfterCompletion,
                "Repeated collection after GameOver cannot score or restart the round");
            TMP_Text details = RoundDetails();
            Check(details != null && details.text == "All pearls collected",
                "Innovation victory uses the all pearls collected reason");
            CheckResultOverlay(ui, true, details.text);
            stoppedTimer = ui.ElapsedTime;
            Capture("victory.png");
            Next(now);
        }
    }

    private static void CheckNoPopsVictory(double now)
    {
        if (ui.State != UIManager.RoundState.Running) return;
        Check(PoppedCount() == 0 && ui.RemainingPellets > 0,
            "No-pops mode keeps every ghost alive before collecting pearls");
        int remaining = ui.RemainingPellets;
        for (int i = 1; i < remaining; i++) ui.CollectPellet(10);
        Check(ui.State == UIManager.RoundState.Running && ui.RemainingPellets == 1,
            "No-pops mode stays running until its final pearl");
        int scoreBeforeFinalPearl = ui.CurrentScore;
        ui.CollectPellet(10);
        Check(ui.State == UIManager.RoundState.GameOver && ui.RemainingPellets == 0
            && PoppedCount() == 0, "Only the final pearl completes the no-pops round");
        Check(ui.CurrentScore == scoreBeforeFinalPearl + 10, "No-pops final pearl is scored once");
        int scoreAfterCompletion = ui.CurrentScore;
        ui.CollectPellet(10);
        Check(ui.State == UIManager.RoundState.GameOver && ui.CurrentScore == scoreAfterCompletion,
            "No-pops collection after GameOver cannot score or restart the round");
        TMP_Text details = RoundDetails();
        Check(details != null && details.text == "All pearls collected",
            "No-pops victory uses the all pearls collected reason");
        CheckResultOverlay(ui, true, details.text);
        stoppedTimer = ui.ElapsedTime;
        Capture("victory-no-pops.png");
        stage = 6;
        Next(now);
    }

    private static int PoppedCount()
    {
        return ghosts == null ? 0 : ghosts.Count(ghost => ghost != null && ghost.IsPermanentlyDefeated);
    }

    private static void CheckRevivedGhost(GhostController ghost)
    {
        int number = GhostNumber(ghost);
        Check(!ghost.IsPermanentlyDefeated && ghost.enabled && ghost.currentState == GhostState.Normal
            && innovation.GetRemainingHits(ghost) == 2,
            "Revived ghost restores two hits and Normal state");
        Collider2D collider = ghost.GetComponent<Collider2D>();
        Check(collider != null && collider.enabled, "Revived ghost collider is enabled");
        Check(ghostSpawns.ContainsKey(number) && Vector3.Distance(ghost.transform.position, ghostSpawns[number]) < 1.1f,
            "Revived ghost returns to its world spawn");
        SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
        Check(renderer != null && ghostColours.ContainsKey(number)
            && Vector4.Distance(renderer.color, ghostColours[number]) < 0.001f,
            "Revived ghost restores its original colour");
        Canvas canvas = ghost.GetComponentInChildren<Canvas>(true);
        Check(canvas != null && canvas.gameObject.activeInHierarchy, "Revived ghost health Canvas is active");
        Animator animator = ghost.GetComponent<Animator>();
        Check(animator != null && animator.updateMode == AnimatorUpdateMode.Normal
            && animator.GetInteger("GhostState") == (int)GhostState.Normal,
            "Revived ghost animator returns to Normal mode");
        CheckInnovationHealth(ghost, 2);
    }

    private static void CheckInnovationHealth(GhostController ghost, int remainingHits)
    {
        Canvas canvas = ghost.GetComponentInChildren<Canvas>(true);
        Check(canvas != null, "Ghost health Canvas is assigned");
        Transform root = canvas.transform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == "InnovationHealth");
        Check(root != null && (remainingHits == 0 || root.gameObject.activeInHierarchy),
            remainingHits == 0 ? "Ghost health bar root is present while hidden at zero HP" : "Ghost health bar root is active");
        if (remainingHits == 0)
            Check(!root.gameObject.activeInHierarchy, "Ghost health bar is hidden at zero HP");
        Image[] cells = root.GetComponentsInChildren<Image>(true)
            .Where(image => image.transform.parent == root)
            .OrderBy(image => image.name).ToArray();
        Check(cells.Length == 2, "Ghost health bar has two cells");
        Color32 filled = new Color32(255, 236, 174, 255);
        Color32 empty = new Color32(70, 38, 24, 255);
        for (int i = 0; i < cells.Length; i++)
            Check(!cells[i].raycastTarget && cells[i].color == (i < remainingHits ? filled : empty),
                "Ghost health bar cell " + (i + 1) + " displays its hit state");
    }

    private static void CheckNoRespawnAfterGameOver()
    {
        MethodInfo updateRespawns = typeof(InnovationController).GetMethod("UpdateRespawns",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(updateRespawns != null, "Innovation respawn updater is available for the GameOver guard");
        updateRespawns.Invoke(innovation, new object[] { 11f });
        Check(ghosts.All(ghost => ghost != null && ghost.IsPermanentlyDefeated),
            "GameOver prevents respawn updates from reviving ghosts");
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
            if (!noPopsMode) CheckDeathSprites();
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
        Check(ui.CurrentLives == InitialLives && innovation != null
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

    private static void DefeatGhost(GhostController ghost, bool twoHits, bool award300 = true)
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
                    "Target enters its respawn cooldown after its final hit");
                CheckInnovationHealth(ghost, 0);
                Check(ui.CurrentScore == score + (award300 ? 300 : 0),
                    award300 ? "Each first popped target awards 300 points once" : "A revived target does not award duplicate pop points");
            }
            else CheckInnovationHealth(ghost, innovation.GetRemainingHits(ghost));
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
