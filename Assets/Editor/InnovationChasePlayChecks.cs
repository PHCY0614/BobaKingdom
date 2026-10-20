using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InnovationChasePlayChecks
{
    private const string Flag = "PacStudent.InnovationChasePlayChecks";
    private const double TimeoutSeconds = 30d;
    private const double ObservationSeconds = 2d;
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> passed = new List<string>();
    private static UIManager ui;
    private static PacStudentController player;
    private static LevelGenerator map;
    private static GhostController[] ghosts;
    private static Vector3[] firstPositions;
    private static Vector3[] secondPositions;
    private static int[] firstDistances;
    private static int[] secondDistances;
    private static double started;
    private static double stageStarted;
    private static int stage;

    static InnovationChasePlayChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Boba Kingdom/Run Innovation chase Play Mode checks")]
    public static void Start()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InnovationScene")
        {
            throw new InvalidOperationException("Open InnovationScene outside Play Mode before running chase checks.");
        }

        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear();
            ui = null;
            player = null;
            map = null;
            ghosts = null;
            firstPositions = null;
            secondPositions = null;
            firstDistances = null;
            secondDistances = null;
            stage = 0;
            started = stageStarted = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            string reportPath = ReportPath();
            if (File.Exists(reportPath) && File.ReadAllText(reportPath).StartsWith("RUNNING"))
            {
                WriteReport(File.ReadAllText(reportPath).Replace("RUNNING", "INTERRUPTED: Play Mode stopped before completion"));
            }
            SessionState.SetBool(Flag, false);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > TimeoutSeconds)
            {
                throw new Exception("Innovation chase checks exceeded 30 seconds.");
            }

            CacheObjects();
            if (stage == 0 && ui != null && ui.State == UIManager.RoundState.Running)
            {
                SetupChallenge();
                stageStarted = now;
                stage = 1;
                WriteReport("RUNNING\nCountdown complete; observing player A.");
            }
            else if (stage == 1 && now - stageStarted >= ObservationSeconds)
            {
                CheckObservation(firstPositions, firstDistances, "player A");
                Capture();
                SetPlayerPosition(new Vector2Int(20, 23));
                secondPositions = ghosts.Select(ghost => ghost.transform.position).ToArray();
                secondDistances = GhostDistances(new Vector2Int(20, 23));
                // A teleported target may require a corridor loop before turning without reversal.
                stageStarted = now;
                stage = 2;
                WriteReport("RUNNING\nPlayer A observation passed; observing player B.");
            }
            else if (stage == 2 && now - stageStarted >= 8d)
            {
                CheckObservation(secondPositions, secondDistances, "player B");
                Capture();
                stageStarted = now;
                stage = 3;
            }
            else if (stage == 3 && now - stageStarted >= 0.1d)
            {
                Finish("PASSED");
            }
        }
        catch (Exception error)
        {
            Finish("FAILED: " + error.Message);
        }
    }

    private static void CacheObjects()
    {
        if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (player == null) player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
        if (map == null) map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
        if (ghosts == null || ghosts.Length < 4)
        {
            ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)
                .OrderBy(GhostNumber).ToArray();
        }
    }

    private static void SetupChallenge()
    {
        Check(ui != null && player != null && map != null && ghosts != null && ghosts.Length == 4,
            "Innovation scene exposes player, map, UI and four ghosts");
        Vector2Int playerCell = new Vector2Int(6, 5);
        Vector2Int[] ghostCells =
        {
            new Vector2Int(1, 5), new Vector2Int(26, 5), new Vector2Int(20, 5), new Vector2Int(6, 27)
        };
        Check(IsGhostCell(playerCell), "Player A fixture is a legal corridor cell");
        for (int i = 0; i < ghosts.Length; i++)
        {
            Check(IsGhostCell(ghostCells[i]), "Ghost fixture is a legal corridor cell: " + (i + 1));
            ResetGhostForObservation(ghosts[i], ghostCells[i]);
        }

        player.StopMovement();
        player.enabled = false;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null) playerCollider.enabled = false;
        SetPlayerPosition(playerCell);
        firstPositions = ghosts.Select(ghost => ghost.transform.position).ToArray();
        firstDistances = GhostDistances(playerCell);
        Check(firstDistances[1] > 0 && firstDistances[3] > 0, "Ghost 2 and Ghost 4 start away from player A");
    }

    private static void ResetGhostForObservation(GhostController ghost, Vector2Int cell)
    {
        Vector3 position = map.GridToWorld(cell);
        ghost.StopAllCoroutines();
        ghost.enabled = true;
        ghost.currentState = GhostState.Normal;
        SetField(ghost, "movementRoutine", null);
        SetField(ghost, "deadRoutine", null);
        SetField(ghost, "currentPosition", position);
        SetField(ghost, "targetPosition", position);
        SetField(ghost, "currentDirection", Vector3.zero);
        SetField(ghost, "ghostFourOnOuterLoop", false);
        ghost.transform.position = position;
        ghost.SetState(GhostState.Normal);
    }

    private static void SetPlayerPosition(Vector2Int cell)
    {
        player.transform.position = map.GridToWorld(cell);
    }

    private static void CheckObservation(Vector3[] startPositions, int[] startDistances, string label)
    {
        Check(startPositions != null && startDistances != null, "Observation state exists for " + label);
        for (int i = 0; i < ghosts.Length; i++)
        {
            GhostController ghost = ghosts[i];
            Check(ghost != null && ghost.gameObject.activeSelf && ghost.enabled,
                "Ghost " + (i + 1) + " remains enabled during " + label);
            Check(!ghost.IsPermanentlyDefeated, "Ghost " + (i + 1) + " is not defeated during " + label);
            Check(IsGhostCell(map.WorldToGrid(ghost.transform.position)),
                "Ghost " + (i + 1) + " stays on legal corridors during " + label);
            Check(Vector3.Distance(startPositions[i], ghost.transform.position) > 0.05f,
                "Ghost " + (i + 1) + " moved through its real Update during " + label);
        }

        int[] distances = GhostDistances(map.WorldToGrid(player.transform.position));
        Check(distances[1] < startDistances[1], "Ghost 2 approaches " + label + ": " + startDistances[1] + " -> " + distances[1]);
        Check(distances[3] < startDistances[3], "Ghost 4 approaches " + label + ": " + startDistances[3] + " -> " + distances[3]);
    }

    private static int[] GhostDistances(Vector2Int target)
    {
        Dictionary<Vector2Int, int> distances = BuildDistances(target);
        var result = new int[ghosts.Length];
        for (int i = 0; i < ghosts.Length; i++)
        {
            Vector2Int cell = map.WorldToGrid(ghosts[i].transform.position);
            Check(distances.ContainsKey(cell), "Ghost " + (i + 1) + " is connected to the chase target");
            result[i] = distances[cell];
        }
        return result;
    }

    private static Dictionary<Vector2Int, int> BuildDistances(Vector2Int target)
    {
        var distances = new Dictionary<Vector2Int, int>();
        var pending = new Queue<Vector2Int>();
        distances[target] = 0;
        pending.Enqueue(target);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (pending.Count > 0)
        {
            Vector2Int cell = pending.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = cell + direction;
                if (!map.IsGhostStepAllowed(cell, next, false) || distances.ContainsKey(next)) continue;
                distances[next] = distances[cell] + 1;
                pending.Enqueue(next);
            }
        }
        return distances;
    }

    private static bool IsGhostCell(Vector2Int cell)
    {
        return map.InBounds(cell) && map.IsWalkable(cell.x, cell.y) && !map.IsGhostSpawn(cell)
            && !(cell.y == map.Rows / 2 && (cell.x == 0 || cell.x == map.Columns - 1));
    }

    private static int GhostNumber(GhostController ghost)
    {
        return new SerializedObject(ghost).FindProperty("ghostNumber").intValue;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Check(field != null, "Reflection field exists: " + fieldName);
        field.SetValue(target, value);
    }

    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        WriteReport(status + "\n" + string.Join("\n", passed));
        Debug.Log("Innovation chase Play checks: " + status);
        SessionState.SetBool(Flag, false);
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed.Add(message);
    }

    private static void Capture()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath()));
        ScreenCapture.CaptureScreenshot("Temp/GoldenRushChecks/chase-play.png");
    }

    private static string ReportPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GoldenRushChecks/chase-play.txt"));
    }

    private static string ScreenshotPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GoldenRushChecks/chase-play.png"));
    }

    private static void WriteReport(string report)
    {
        string path = ReportPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report);
    }
}
