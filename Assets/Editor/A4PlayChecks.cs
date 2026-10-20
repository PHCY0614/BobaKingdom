using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class A4PlayChecks
{
    private const string Flag = "PacStudent.A4PlayChecks";
    private const string ScoreKey = "PacStudent.Level1Scene.HighScore";
    private const string TimeKey = "PacStudent.Level1Scene.BestTime";
    private static readonly List<string> passed = new List<string>();
    private static UIManager ui;
    private static PacStudentController player;
    private static GhostController[] ghosts;
    private static Vector3[] ghostPositions;
    private static GhostController eatenGhost;
    private static CherryController cherryController;
    private static Vector3 cherryDeathPosition;
    private static bool cherryPresentAtDeath;
    private static double stageStarted, started;
    private static int stage;
    private static bool queuedTurn;
    private static float stoppedTimer;

    static A4PlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("PacStudent/Run A4 Play Mode checks")]
    public static void Start()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Level1Scene")
            throw new InvalidOperationException("Open Level1Scene outside Play Mode before running checks.");
        SessionState.SetBool(Flag + ".HadScore", PlayerPrefs.HasKey(ScoreKey));
        SessionState.SetBool(Flag + ".HadTime", PlayerPrefs.HasKey(TimeKey));
        SessionState.SetInt(Flag + ".Score", PlayerPrefs.GetInt(ScoreKey));
        SessionState.SetFloat(Flag + ".Time", PlayerPrefs.GetFloat(TimeKey));
        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear(); stage = 0; queuedTurn = false;
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
            if (stage == 0 && elapsed > 1)
            {
                ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None);
                Check(ui.State == UIManager.RoundState.Countdown && ui.ElapsedTime == 0f, "Countdown holds timer at zero");
                Check(player.transform.position == new Vector3(1, -1, 0), "Countdown holds player at spawn");
                var map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
                Check(ghosts.Length == 4 && ghosts.All(g => map.IsGhostSpawn(map.WorldToGrid(g.transform.position))), "All ghosts remain in the house during countdown");
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
                Check(ghosts.Where((g, i) => Vector3.Distance(g.transform.position, ghostPositions[i]) > 0.5f).Any(), "Ghosts leave spawn and move");
                Check(cherryController.transform.childCount == 1, "First Cherry appears by five and a half seconds");
                var cherry = cherryController.transform.GetChild(0).gameObject;
                var sprite = cherry.GetComponent<SpriteRenderer>();
                Check(sprite.sortingOrder == 100 && sprite.maskInteraction != SpriteMaskInteraction.None, "Cherry uses top sorting order and sprite mask");
                Capture("running.png"); cherryController.DestroyCherry(cherry);
                player.StopMovement(); player.enabled = false; player.GetComponent<Collider2D>().enabled = false;
                ui.StartScaredTimer(10f);
                Check(ghosts.All(g => g.currentState == GhostState.Scared), "Power pill changes all living ghosts to Scared");
                Next(now);
            }
            else if (stage == 3 && elapsed > 7.2)
            {
                Check(ui.GetScaredTimeRemaining() > 0 && ui.GetScaredTimeRemaining() <= 3f, "Scared countdown reaches final three seconds");
                Check(cherryController.transform.childCount == 1, "Cherry respawns after removal");
                Check(ghosts.All(g => g.currentState == GhostState.Recovering), "All ghosts enter Recovering");
                eatenGhost = ghosts.OrderByDescending(g => Vector3.Distance(g.transform.position, new Vector3(13.5f, -14, 0))).First();
                eatenGhost.HandleEaten(ui);
                Check(eatenGhost.currentState == GhostState.Dead && !eatenGhost.GetComponent<Collider2D>().enabled, "Eaten ghost is Dead and cannot collide");
                ui.StartScaredTimer(10f);
                Check(eatenGhost.currentState == GhostState.Dead, "A second power pill cannot overwrite Dead state");
                Next(now);
            }
            else if (stage == 4 && eatenGhost.currentState != GhostState.Dead)
            {
                Check(eatenGhost.GetComponent<Collider2D>().enabled, "Returned ghost restores collisions");
                float remaining = ui.GetScaredTimeRemaining();
                var expected = remaining <= 0 ? GhostState.Normal : remaining <= 3 ? GhostState.Recovering : GhostState.Scared;
                Check(eatenGhost.currentState == expected, "Returned ghost matches current scared timer");
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
                int remaining = ui.RemainingPellets;
                for (int i = 0; i < remaining; i++) ui.CollectPellet(10);
                Check(ui.State == UIManager.RoundState.GameOver, "Eating the final pellet ends the round");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).All(b => !b.interactable), "Game Over disables HUD buttons");
                stoppedTimer = ui.ElapsedTime; Next(now);
            }
            else if (stage == 7 && elapsed > 0.7)
            {
                Check(ui.ElapsedTime == stoppedTimer, "Game Over freezes the game timer"); Capture("game-over.png"); Next(now);
            }
            else if (stage == 8 && SceneManager.GetActiveScene().name == "StartScene")
            {
                Check(UnityEngine.Object.FindFirstObjectByType<UIManager>().State == UIManager.RoundState.Menu, "Game Over returns to main menu");
                Capture("menu.png"); Next(now);
            }
            else if (stage == 9 && elapsed > 0.5) Finish("PASSED");
        }
        catch (Exception error) { Finish("FAILED: " + error.Message); }
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
        PlayerPrefs.Save();
    }
}
