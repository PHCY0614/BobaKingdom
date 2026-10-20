using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class A4PlayChecks
{
    private const string Flag = "PacStudent.A4PlayChecks";
    private const string ScoreKey = "PacStudent.Level1Scene.HighScore";
    private const string TimeKey = "PacStudent.Level1Scene.BestTime";
    private const string InnovationScoreKey = "PacStudent.InnovationScene.HighScore";
    private const string InnovationTimeKey = "PacStudent.InnovationScene.BestTime";
    private static readonly List<string> passed = new List<string>();
    private static UIManager ui;
    private static PacStudentController player;
    private static GhostController[] ghosts;
    private static Vector3[] ghostPositions;
    private static readonly Dictionary<GhostController, HashSet<string>> deathSprites = new Dictionary<GhostController, HashSet<string>>();
    private static CherryController cherryController;
    private static Vector3 cherryDeathPosition;
    private static bool cherryPresentAtDeath;
    private static double stageStarted, started;
    private static int stage;
    private static bool queuedTurn;
    private static float stoppedTimer;
    private static readonly float[] audioSamples = new float[8192];

    static A4PlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("PacStudent/Run A4 Play Mode checks")]
    public static void Start() { Begin(false); }

    [MenuItem("PacStudent/Run A4 defeat Play Mode checks")]
    public static void StartDefeatCheck() { Begin(true); }

    private static void Begin(bool defeat)
    {
        SessionState.SetBool(Flag + ".Defeat", defeat);
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Level1Scene")
            throw new InvalidOperationException("Open Level1Scene outside Play Mode before running checks.");
        SessionState.SetBool(Flag + ".HadScore", PlayerPrefs.HasKey(ScoreKey));
        SessionState.SetBool(Flag + ".HadTime", PlayerPrefs.HasKey(TimeKey));
        SessionState.SetInt(Flag + ".Score", PlayerPrefs.GetInt(ScoreKey));
        SessionState.SetFloat(Flag + ".Time", PlayerPrefs.GetFloat(TimeKey));
        SessionState.SetBool(Flag + ".HadInnovationScore", PlayerPrefs.HasKey(InnovationScoreKey));
        SessionState.SetBool(Flag + ".HadInnovationTime", PlayerPrefs.HasKey(InnovationTimeKey));
        SessionState.SetInt(Flag + ".InnovationScore", PlayerPrefs.GetInt(InnovationScoreKey));
        SessionState.SetFloat(Flag + ".InnovationTime", PlayerPrefs.GetFloat(InnovationTimeKey));
        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear(); deathSprites.Clear(); stage = 0; queuedTurn = false;
            started = stageStarted = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CodexChecks/play-checks.txt"));
            if (File.Exists(reportPath) && File.ReadAllText(reportPath).StartsWith("RUNNING"))
                WriteReport(File.ReadAllText(reportPath).Replace("RUNNING", "INTERRUPTED: Play Mode stopped before completion"));
            RestoreRecords(); SessionState.SetBool(Flag, false);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            double now = EditorApplication.timeSinceStartup, elapsed = now - stageStarted;
            if (now - started > 60) throw new Exception("Play checks exceeded 60 seconds.");
            if (ui == null && stage < 8) ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
            if (player == null && stage < 8) player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
            // Unity fills output history after sampling begins; prime it across frames before assertions.
            if (ui != null && ui.AudioController != null && ui.AudioController.audioSource != null)
                ui.AudioController.audioSource.GetOutputData(audioSamples, 0);
            if (player != null && player.GetComponent<AudioSource>() != null)
                player.GetComponent<AudioSource>().GetOutputData(audioSamples, 0);
            AudioListener.GetOutputData(audioSamples, 0);
            if (stage == 4)
                foreach (var ghost in ghosts)
                    if (ghost.currentState == GhostState.Dead)
                    {
                        var sprite = ghost.GetComponent<SpriteRenderer>().sprite;
                        if (sprite != null && sprite.name.StartsWith("Popping_dead_")) deathSprites[ghost].Add(sprite.name);
                    }
            if (stage == 0 && elapsed > 1)
            {
                ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)
                    .OrderBy(g => new SerializedObject(g).FindProperty("ghostNumber").intValue).ToArray();
                Check(ui.State == UIManager.RoundState.Countdown && ui.ElapsedTime == 0f, "Countdown holds timer at zero");
                Check(player.transform.position == new Vector3(1, -1, 0), "Countdown holds player at spawn");
                var map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
                Check(ghosts.Length == 4 && ghosts.All(g => map.IsGhostSpawn(map.WorldToGrid(g.transform.position))), "All ghosts remain in the house during countdown");
                var countdownDetails = (TMPro.TextMeshProUGUI)new SerializedObject(ui).FindProperty("roundDetails").objectReferenceValue;
                Check(countdownDetails != null && countdownDetails.text == "Get ready", "Countdown displays Get ready");
                CheckAudio(ui.AudioController.audioSource, ui.AudioController.bgmIntro, "Countdown music");
                CheckListenerOutput();
                Capture("countdown.png"); Next(now);
            }
            else if (stage == 1 && ui.IsRoundRunning)
            {
                cherryController = UnityEngine.Object.FindFirstObjectByType<CherryController>();
                Check(cherryController.transform.childCount == 0, "Cherry does not spawn before five seconds");
                ghostPositions = ghosts.Select(g => g.transform.position).ToArray();
                player.SetInput(Vector3.right); Next(now);
            }
            else if (stage == 2)
            {
                if (elapsed > 0.05 && !queuedTurn) { player.SetInput(Vector3.down); queuedTurn = true; }
                if (elapsed < 1.5) return;
                Check(Mathf.Abs(player.transform.position.x - 6f) < 0.05f && player.transform.position.y < -1.1f, "Buffered turn survives blocked intersections and turns at column 6");
                Check(ui.CurrentScore > 0, "Moving over pellets updates score");
                CheckAudio(ui.AudioController.audioSource, ui.AudioController.bgmGhostNormal, "Normal background music");
                var playerAudio = player.GetComponent<AudioSource>();
                CheckAudio(playerAudio, playerAudio.clip, "Movement or eating sound");
                Check(ghosts.Where((g, i) => Vector3.Distance(g.transform.position, ghostPositions[i]) > 0.5f).Any(), "Ghosts leave spawn and move");
                Check(cherryController.transform.childCount == 1, "First Cherry appears by five and a half seconds");
                var cherry = cherryController.transform.GetChild(0).gameObject;
                var sprite = cherry.GetComponent<SpriteRenderer>();
                Check(sprite.sortingOrder == 100 && sprite.maskInteraction != SpriteMaskInteraction.None, "Cherry uses top sorting order and sprite mask");
                CheckHudLayout();
                Capture("running.png"); cherryController.DestroyCherry(cherry);
                player.StopMovement(); player.enabled = false; player.GetComponent<Collider2D>().enabled = false;
                ui.StartScaredTimer(10f);
                Check(ghosts.All(g => g.currentState == GhostState.Scared), "Power pill changes all living ghosts to Scared");
                Check(ui.scaredTimerText != null && ui.scaredTimerText.gameObject.activeSelf
                    && ui.scaredTimerText.text == "Power: 10s", "Power timer displays seconds");
                var powerPanel = (GameObject)new SerializedObject(ui).FindProperty("powerPanel").objectReferenceValue;
                if (powerPanel != null) Check(powerPanel.activeSelf, "Power panel is visible while powered");
                Next(now);
            }
            else if (stage == 3 && elapsed > 7.2)
            {
                Check(ui.GetScaredTimeRemaining() > 0 && ui.GetScaredTimeRemaining() <= 3f, "Scared countdown reaches final three seconds");
                Check(cherryController.transform.childCount == 1, "Cherry respawns after removal");
                Check(ghosts.All(g => g.currentState == GhostState.Recovering), "All ghosts enter Recovering");
                CheckAudio(ui.AudioController.audioSource, ui.AudioController.bgmGhostScared, "Scared background music");
                var corners = new[] { new Vector3(1, -1), new Vector3(26, -1), new Vector3(26, -27), new Vector3(1, -27) };
                for (int i = 0; i < ghosts.Length; i++)
                {
                    ghosts[i].transform.position = corners[i];
                    deathSprites[ghosts[i]] = new HashSet<string>();
                    ghosts[i].HandleEaten(ui);
                }
                Check(ghosts.All(g => g.currentState == GhostState.Dead && !g.GetComponent<Collider2D>().enabled), "All four eaten ghosts are Dead and cannot collide");
                ui.StartScaredTimer(10f);
                Check(ghosts.All(g => g.currentState == GhostState.Dead), "A second power pill cannot overwrite any Dead state");
                Check(ui.AudioController.audioSource.clip == ui.AudioController.bgmGhostDead && ui.AudioController.audioSource.isPlaying, "Dead background music takes priority");
                Next(now);
            }
            else if (stage == 4 && ghosts.All(g => g.currentState != GhostState.Dead))
            {
                for (int i = 0; i < ghosts.Length; i++)
                    Check(deathSprites[ghosts[i]].Count == 4, "Ghost " + (i + 1) + " plays all four death sprites: " + string.Join(",", deathSprites[ghosts[i]]));
                Check(ghosts.All(g => g.GetComponent<Collider2D>().enabled), "All returned ghosts restore collisions");
                float remaining = ui.GetScaredTimeRemaining();
                var expected = remaining <= 0 ? GhostState.Normal : remaining <= 3 ? GhostState.Recovering : GhostState.Scared;
                Check(ghosts.All(g => g.currentState == expected), "All returned ghosts match current scared timer");
                cherryPresentAtDeath = cherryController.transform.childCount > 0;
                if (cherryPresentAtDeath) cherryDeathPosition = cherryController.transform.GetChild(0).position;
                ui.LoseLife(); ghostPositions = ghosts.Select(g => g.transform.position).ToArray();
                Check(ui.State == UIManager.RoundState.Death && ui.CurrentLives == 2, "Normal ghost death removes one life and pauses the round");
                Next(now);
            }
            else if (stage == 5 && elapsed > 0.5)
            {
                Check(ghosts.Where((g, i) => Vector3.Distance(g.transform.position, ghostPositions[i]) > 0.01f).Count() == 0, "Ghosts stop during player death");
                if (cherryPresentAtDeath) Check(cherryController.transform.GetChild(0).position == cherryDeathPosition, "Cherry stops during player death");
                Next(now);
            }
            else if (stage == 6 && ui.IsRoundRunning)
            {
                Check(player.transform.position == new Vector3(1, -1, 0), "Player respawns at start awaiting input");
                Check(ghosts.All(g => g.currentState == GhostState.Normal), "Player death resets ghost state");
                Check(ui.GetScaredTimeRemaining() == 0, "Player death clears scared timer");
                Check(ui.scaredTimerText == null || !ui.scaredTimerText.gameObject.activeSelf, "Power timer hides at zero");
                var powerPanel = (GameObject)new SerializedObject(ui).FindProperty("powerPanel").objectReferenceValue;
                if (powerPanel != null) Check(!powerPanel.activeSelf, "Power panel hides at zero");
                bool defeat = SessionState.GetBool(Flag + ".Defeat", false);
                if (defeat)
                {
                    ui.LoseLife();
                    if (ui.CurrentLives > 0) return;
                }
                else
                {
                    int remaining = ui.RemainingPellets;
                    for (int i = 0; i < remaining; i++) ui.CollectPellet(10);
                }
                Check(ui.State == UIManager.RoundState.GameOver, defeat ? "Zero lives ends the round" : "Eating the final pellet ends the round");
                var message = (TMPro.TextMeshProUGUI)new SerializedObject(ui).FindProperty("roundMessage").objectReferenceValue;
                Check(message.text == (defeat ? "Game Over" : "Victory!"), "Round result displays " + message.text);
                var details = (TMPro.TextMeshProUGUI)new SerializedObject(ui).FindProperty("roundDetails").objectReferenceValue;
                Check(details != null && details.text.Contains("SCORE  " + ui.CurrentScore.ToString("D6"))
                    && details.text.Contains("TIME   " + UIManager.FormatTime(ui.ElapsedTime))
                    && details.text.Contains("Returning to menu...")
                    && details.text.Contains(defeat ? "Out of lives" : "All pearls collected"),
                    "Round result displays outcome, score, time, and return message");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).All(b => !b.interactable), "Round end disables HUD buttons");
                stoppedTimer = ui.ElapsedTime; Next(now);
            }
            else if (stage == 7 && elapsed > 0.7)
            {
                Check(ui.ElapsedTime == stoppedTimer, "Round end freezes the game timer"); Capture(SessionState.GetBool(Flag + ".Defeat", false) ? "defeat.png" : "victory.png"); Next(now);
            }
            else if (stage == 8 && SceneManager.GetActiveScene().name == "StartScene")
            {
                Check(UnityEngine.Object.FindFirstObjectByType<UIManager>().State == UIManager.RoundState.Menu, "Round end returns to main menu");
                CheckMenuLayout(); Capture("menu.png"); Next(now);
            }
            else if (stage == 9 && elapsed > 0.5)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Next(now);
            }
            else if (stage == 10 && SceneManager.GetActiveScene().name == "Level1Scene" && elapsed > 0.5)
            {
                Check(UnityEngine.Object.FindFirstObjectByType<UIManager>().State == UIManager.RoundState.Countdown,
                    "Submitting the selected menu button starts Level 1");
                var exit = GameObject.Find("Button - Exit");
                Check(exit.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == "Main Menu", "Return button names its destination");
                ExecuteEvents.Execute(exit, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Next(now);
            }
            else if (stage == 11 && SceneManager.GetActiveScene().name == "StartScene" && elapsed > 0.5)
            {
                Check(EventSystem.current.currentSelectedGameObject == GameObject.Find("Button - Level 1"),
                    "Returning through Main Menu restores selection");
                Finish("PASSED");
            }
        }
        catch (Exception error) { Finish("FAILED: " + error.Message); }
    }
    private static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return new Rect(corners[0], corners[2] - corners[0]);
    }
    private static void CheckHudLayout()
    {
        Canvas.ForceUpdateCanvases();
        var hud = GameObject.Find("HUD");
        var map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>().LevelBounds;
        var minimum = Camera.main.WorldToScreenPoint(map.min);
        var maximum = Camera.main.WorldToScreenPoint(map.max);
        var maze = new Rect(minimum, maximum - minimum);
        foreach (string name in new[] { "HUD Info", "Power Panel", "Button - Exit" })
        {
            var rect = hud.GetComponentsInChildren<RectTransform>(true).First(r => r.name == name);
            var bounds = ScreenBounds(rect);
            Check(bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= Screen.width && bounds.yMax <= Screen.height
                && !bounds.Overlaps(maze), name + " stays on screen and clear of the maze");
        }
        var lives = GameObject.Find("LivesContainer");
        var info = ScreenBounds(GameObject.Find("HUD Info").GetComponent<RectTransform>());
        foreach (var image in lives.GetComponentsInChildren<UnityEngine.UI.Image>())
        {
            var bounds = ScreenBounds(image.rectTransform);
            Check(image.sprite.name == "Life" && info.Contains(bounds.min) && info.Contains(bounds.max),
                image.name + " keeps the original leaf and fits inside HUD");
        }
        foreach (var label in new[] { ui.scoreText, ui.timerText })
        {
            label.ForceMeshUpdate();
            Check(label.textInfo.lineCount == 2 && !label.isTextOverflowing, label.name + " fits its label and value");
        }
    }
    private static void CheckMenuLayout()
    {
        Canvas.ForceUpdateCanvases();
        var level1 = GameObject.Find("Button - Level 1");
        var level2 = GameObject.Find("Button - Level 2");
        var title = GameObject.Find("Text - Title").GetComponent<TMPro.TextMeshProUGUI>();
        var subtitle = GameObject.Find("Text - Subtitle").GetComponent<TMPro.TextMeshProUGUI>();
        Check(level1 != null && level1.activeInHierarchy, "Level 1 button is visible");
        Check(level2 != null && level2.activeInHierarchy
            && level2.GetComponent<UnityEngine.UI.Button>().interactable
            && level2.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == "Play Level 2",
            "Level 2 button is visible and enabled");
        Check(title.text == "Boba Kingdom", "Menu title is Boba Kingdom");
        Check(subtitle.text == "The Popping Pearl Invasion", "Menu subtitle is The Popping Pearl Invasion");
        Check(EventSystem.current != null && EventSystem.current.firstSelectedGameObject == level1,
            "Menu initial focus is Level 1");
        Check(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == level1,
            "Menu return focus is Level 1");
        var scoreLabel = GameObject.Find("Level 1 - HighScoreText").GetComponent<TMPro.TextMeshProUGUI>();
        var timeLabel = GameObject.Find("Level 1 - TimeText").GetComponent<TMPro.TextMeshProUGUI>();
        var innovationScoreLabel = GameObject.Find("Level 2 - HighScoreText").GetComponent<TMPro.TextMeshProUGUI>();
        var innovationTimeLabel = GameObject.Find("Level 2 - TimeText").GetComponent<TMPro.TextMeshProUGUI>();
        if (PlayerPrefs.HasKey(ScoreKey))
        {
            Check(scoreLabel.text == "HIGH SCORE<pos=62%>" + PlayerPrefs.GetInt(ScoreKey).ToString("D6"),
                "Level 1 high score record is aligned");
            Check(timeLabel.text == "RUN TIME<pos=62%>" + UIManager.FormatTime(PlayerPrefs.GetFloat(TimeKey)),
                "Level 1 run time record is aligned");
        }
        else
        {
            Check(scoreLabel.text == "No record yet" && timeLabel.text == "Play to set your first score",
                "Empty Level 1 records are aligned");
        }
        if (PlayerPrefs.HasKey(InnovationScoreKey))
        {
            Check(innovationScoreLabel.text == "HIGH SCORE<pos=62%>" + PlayerPrefs.GetInt(InnovationScoreKey).ToString("D6"),
                "Level 2 high score record is aligned");
            Check(innovationTimeLabel.text == "RUN TIME<pos=62%>" + UIManager.FormatTime(PlayerPrefs.GetFloat(InnovationTimeKey)),
                "Level 2 run time record is aligned");
        }
        else
        {
            Check(innovationScoreLabel.text == "No record yet" && innovationTimeLabel.text == "Play to set your first score",
                "Empty Level 2 records are aligned");
        }
        var names = new[]
        {
            "Button - Level 1", "Level 1 - HighScoreText", "Level 1 - TimeText",
            "Button - Level 2", "Level 2 - HighScoreText", "Level 2 - TimeText"
        };
        var rects = names.Select(name => GameObject.Find(name).GetComponent<RectTransform>()).ToArray();
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>().GetComponent<RectTransform>();
        var bounds = new List<Rect>();
        foreach (var rect in rects)
        {
            var label = rect.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            label.ForceMeshUpdate();
            Check(label.textInfo.lineCount == 1 && !label.isTextOverflowing, rect.name + " fits on one line");
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            bounds.Add(new Rect(corners[0], corners[2] - corners[0]));
            var canvasCorners = new Vector3[4]; canvas.GetWorldCorners(canvasCorners);
            Check(corners[0].x >= canvasCorners[0].x && corners[2].x <= canvasCorners[2].x
                && corners[0].y >= canvasCorners[0].y && corners[2].y <= canvasCorners[2].y,
                rect.name + " stays inside canvas");
        }
        for (int i = 0; i < bounds.Count; i++)
            for (int j = i + 1; j < bounds.Count; j++)
                if (bounds[i].Overlaps(bounds[j])) throw new Exception(names[i] + " overlaps " + names[j]);
        int savedScore = PlayerPrefs.GetInt(ScoreKey);
        float savedTime = PlayerPrefs.GetFloat(TimeKey);
        bool hadScore = PlayerPrefs.HasKey(ScoreKey);
        bool hadTime = PlayerPrefs.HasKey(TimeKey);
        int savedInnovationScore = PlayerPrefs.GetInt(InnovationScoreKey);
        float savedInnovationTime = PlayerPrefs.GetFloat(InnovationTimeKey);
        bool hadInnovationScore = PlayerPrefs.HasKey(InnovationScoreKey);
        bool hadInnovationTime = PlayerPrefs.HasKey(InnovationTimeKey);
        var loadRecords = typeof(UIManager).GetMethod("LoadMenuRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var menu = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        try
        {
            PlayerPrefs.DeleteKey(ScoreKey); PlayerPrefs.DeleteKey(TimeKey);
            PlayerPrefs.DeleteKey(InnovationScoreKey); PlayerPrefs.DeleteKey(InnovationTimeKey);
            loadRecords.Invoke(menu, null);
            scoreLabel.ForceMeshUpdate(); timeLabel.ForceMeshUpdate();
            innovationScoreLabel.ForceMeshUpdate(); innovationTimeLabel.ForceMeshUpdate();
            Check(scoreLabel.text == "No record yet" && timeLabel.text == "Play to set your first score"
                && !scoreLabel.isTextOverflowing && !timeLabel.isTextOverflowing, "Empty Level 1 records show a readable first-play hint");
            Check(innovationScoreLabel.text == "No record yet" && innovationTimeLabel.text == "Play to set your first score"
                && !innovationScoreLabel.isTextOverflowing && !innovationTimeLabel.isTextOverflowing, "Empty Level 2 records show a readable first-play hint");
        }
        finally
        {
            if (hadScore) PlayerPrefs.SetInt(ScoreKey, savedScore);
            else PlayerPrefs.DeleteKey(ScoreKey);
            if (hadTime) PlayerPrefs.SetFloat(TimeKey, savedTime);
            else PlayerPrefs.DeleteKey(TimeKey);
            if (hadInnovationScore) PlayerPrefs.SetInt(InnovationScoreKey, savedInnovationScore);
            else PlayerPrefs.DeleteKey(InnovationScoreKey);
            if (hadInnovationTime) PlayerPrefs.SetFloat(InnovationTimeKey, savedInnovationTime);
            else PlayerPrefs.DeleteKey(InnovationTimeKey);
            loadRecords.Invoke(menu, null);
        }
        Check(true, "Both level buttons and records have separate bounds at " + Screen.width + "x" + Screen.height);
    }

    private static void CheckAudio(AudioSource source, AudioClip expected, string label)
    {
        Check(source != null && expected != null && source.clip == expected && source.isPlaying
            && !source.mute && source.volume > 0, label + " is playing");
        var samples = new float[8192]; source.GetOutputData(samples, 0);
        float peak = samples.Max(sample => Mathf.Abs(sample));
        Check(peak > 0.00001f, label + " outputs a non-zero signal (peak=" + peak
            + ", clipTime=" + source.time + ", samples=" + source.timeSamples + ", DSP=" + AudioSettings.dspTime
            + ", virtual=" + source.isVirtual + ", listenerPause=" + AudioListener.pause
            + ", masterMute=" + EditorUtility.audioMasterMute + ", focused=" + Application.isFocused + ")");
    }
    private static void CheckListenerOutput()
    {
        var samples = new float[8192]; AudioListener.GetOutputData(samples, 0);
        Check(!EditorUtility.audioMasterMute && !AudioListener.pause && AudioListener.volume > 0
            && samples.Any(sample => Mathf.Abs(sample) > 0.00001f), "AudioListener receives a non-zero mix without Unity mute");
    }
    private static void Capture(string file) { ScreenCapture.CaptureScreenshot("Temp/CodexChecks/" + file); }
    private static void Next(double now) { stage++; stageStarted = now; WriteReport("RUNNING: stage " + stage + "\n" + string.Join("\n", passed)); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed.Add(message);
    }
    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        WriteReport(status + "\n" + string.Join("\n", passed));
        Debug.Log("A4 Play checks: " + status);
        EditorApplication.isPlaying = false;
    }
    private static void WriteReport(string report)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CodexChecks"));
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "play-checks.txt"), report);
    }
    private static void RestoreRecords()
    {
        if (SessionState.GetBool(Flag + ".HadScore", false)) PlayerPrefs.SetInt(ScoreKey, SessionState.GetInt(Flag + ".Score", 0));
        else PlayerPrefs.DeleteKey(ScoreKey);
        if (SessionState.GetBool(Flag + ".HadTime", false)) PlayerPrefs.SetFloat(TimeKey, SessionState.GetFloat(Flag + ".Time", 0));
        else PlayerPrefs.DeleteKey(TimeKey);
        if (SessionState.GetBool(Flag + ".HadInnovationScore", false)) PlayerPrefs.SetInt(InnovationScoreKey, SessionState.GetInt(Flag + ".InnovationScore", 0));
        else PlayerPrefs.DeleteKey(InnovationScoreKey);
        if (SessionState.GetBool(Flag + ".HadInnovationTime", false)) PlayerPrefs.SetFloat(InnovationTimeKey, SessionState.GetFloat(Flag + ".InnovationTime", 0));
        else PlayerPrefs.DeleteKey(InnovationTimeKey);
        PlayerPrefs.Save();
    }
}
