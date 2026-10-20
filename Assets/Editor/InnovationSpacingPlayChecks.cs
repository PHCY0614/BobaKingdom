using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InnovationSpacingPlayChecks
{
    private const string Flag = "PacStudent.InnovationSpacingPlayChecks";
    private const double TimeoutSeconds = 35d;
    private const double ObservationSeconds = 8d;
    private const float OverlapDistance = 0.2f;
    private const float MaxAllowedContinuousOverlap = 0.3f;
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> passed = new List<string>();
    private static readonly List<string> phaseReports = new List<string>();
    private static UIManager ui;
    private static PacStudentController player;
    private static GhostManager ghostManager;
    private static LevelGenerator map;
    private static GhostController[] ghosts;
    private static FieldInfo directionField;
    private static double started;
    private static double phaseStarted;
    private static float lastRuntimeTime;
    private static int lastFrame = -1;
    private static int phase;
    private static float continuousOverlap;
    private static float maxContinuousOverlap;
    private static float minimumDistance = float.MaxValue;
    private static Vector3 maxGreenPosition;
    private static Vector3 maxOrangePosition;
    private static Vector3 maxGreenDirection;
    private static Vector3 maxOrangeDirection;
    private static bool overlapScreenshotTaken;
    private static readonly bool[] moved = new bool[4];
    private static readonly int[] movementSamples = new int[4];
    private static Vector3[] lastPositions;
    private static Vector3[] phaseStartPositions;
    private static bool finishing;

    static InnovationSpacingPlayChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Boba Kingdom/Run Innovation spacing Play Mode checks")]
    public static void Start()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InnovationScene")
        {
            throw new InvalidOperationException("Open InnovationScene outside Play Mode before running spacing checks.");
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
            phaseReports.Clear();
            ui = null;
            player = null;
            ghostManager = null;
            map = null;
            ghosts = null;
            directionField = null;
            phase = 0;
            started = phaseStarted = EditorApplication.timeSinceStartup;
            finishing = false;
            overlapScreenshotTaken = false;
            lastPositions = null;
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
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || finishing) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > TimeoutSeconds)
            {
                throw new Exception("Innovation spacing checks exceeded 35 seconds.");
            }

            CacheObjects();
            if (phase == 0 && ui != null && ui.State == UIManager.RoundState.Running)
            {
                StopPlayerForObservation();
                BeginPhase("natural", now);
                phase = 1;
            }
            else if (phase == 1)
            {
                ObserveFrame();
                if (now - phaseStarted >= ObservationSeconds)
                {
                    CompletePhase("natural");
                    ghostManager.ResetGhosts();
                    BeginPhase("reset", now);
                    phase = 2;
                    WriteReport("RUNNING\nNatural opening completed; ResetGhosts phase started.");
                }
            }
            else if (phase == 2)
            {
                ObserveFrame();
                if (now - phaseStarted >= ObservationSeconds)
                {
                    CompletePhase("reset");
                    Finish("PASSED");
                }
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
        if (ghostManager == null) ghostManager = UnityEngine.Object.FindFirstObjectByType<GhostManager>();
        if (map == null) map = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
        if (ghosts == null || ghosts.Length < 4)
        {
            ghosts = UnityEngine.Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)
                .OrderBy(GhostNumber).ToArray();
        }
        if (directionField == null)
        {
            directionField = typeof(GhostController).GetField("currentDirection", InstancePrivate);
        }
    }

    private static void StopPlayerForObservation()
    {
        Check(ui != null && player != null && ghostManager != null && map != null && ghosts != null && ghosts.Length == 4,
            "Innovation scene exposes UI, player, GhostManager, map and four ghosts");
        Check(directionField != null, "Ghost movement direction field is available for overlap reporting");
        player.StopMovement();
        player.enabled = false;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null) playerCollider.enabled = false;
    }

    private static void BeginPhase(string name, double now)
    {
        phaseStarted = now;
        lastRuntimeTime = Time.time;
        lastFrame = Time.frameCount;
        continuousOverlap = 0f;
        maxContinuousOverlap = 0f;
        minimumDistance = float.MaxValue;
        phaseStartPositions = ghosts.Select(ghost => ghost.transform.position).ToArray();
        lastPositions = phaseStartPositions.ToArray();
        for (int i = 0; i < moved.Length; i++)
        {
            moved[i] = false;
            movementSamples[i] = 0;
        }

        Check(ghosts.All(ghost => ghost != null && ghost.gameObject.activeSelf && ghost.enabled),
            "All ghosts are enabled at " + name + " phase start");
        maxGreenPosition = phaseStartPositions[0];
        maxOrangePosition = phaseStartPositions[2];
        maxGreenDirection = (Vector3)directionField.GetValue(ghosts[0]);
        maxOrangeDirection = (Vector3)directionField.GetValue(ghosts[2]);
        phaseReports.Add(name + " phase started at player position " + player.transform.position);
    }

    private static void ObserveFrame()
    {
        if (Time.frameCount == lastFrame) return;
        float now = Time.time;
        float delta = Mathf.Max(0f, now - lastRuntimeTime);
        lastRuntimeTime = now;
        lastFrame = Time.frameCount;

        for (int i = 0; i < ghosts.Length; i++)
        {
            GhostController ghost = ghosts[i];
            AssertLive(ghost != null && ghost.gameObject.activeSelf && ghost.enabled && !ghost.IsPermanentlyDefeated,
                "Ghost " + (i + 1) + " remains active and undefeated");
            Vector3 position = ghost.transform.position;
            if (!IsGhostCell(map.WorldToGrid(position)))
            {
                throw new InvalidOperationException("Ghost " + (i + 1) + " left legal corridors at " + position);
            }
            if (Vector3.Distance(phaseStartPositions[i], position) > 0.05f) moved[i] = true;
            Vector3 previous = lastPositions[i];
            lastPositions[i] = position;
            if (Vector3.Distance(previous, position) > 0.0001f) movementSamples[i]++;
        }

        float distance = Vector3.Distance(ghosts[0].transform.position, ghosts[2].transform.position);
        minimumDistance = Mathf.Min(minimumDistance, distance);
        if (distance < OverlapDistance)
        {
            continuousOverlap += delta;
            if (continuousOverlap > maxContinuousOverlap)
            {
                maxContinuousOverlap = continuousOverlap;
                maxGreenPosition = ghosts[0].transform.position;
                maxOrangePosition = ghosts[2].transform.position;
                maxGreenDirection = (Vector3)directionField.GetValue(ghosts[0]);
                maxOrangeDirection = (Vector3)directionField.GetValue(ghosts[2]);
            }
            if (continuousOverlap > MaxAllowedContinuousOverlap && !overlapScreenshotTaken)
            {
                CaptureOverlap();
                overlapScreenshotTaken = true;
            }
        }
        else
        {
            continuousOverlap = 0f;
        }
    }

    private static void CompletePhase(string name)
    {
        phaseReports.Add(name + " phase: maxOverlap=" + maxContinuousOverlap.ToString("F3")
            + "s, minDistance=" + minimumDistance.ToString("F3")
            + ", maxGreenPosition=" + maxGreenPosition.ToString("F3")
            + ", maxOrangePosition=" + maxOrangePosition.ToString("F3")
            + ", maxGreenDirection=" + maxGreenDirection.ToString("F3")
            + ", maxOrangeDirection=" + maxOrangeDirection.ToString("F3"));
        Check(maxContinuousOverlap < MaxAllowedContinuousOverlap,
            name + " maximum continuous Green 1 / Orange 3 overlap is below 0.3 seconds");
        Check(moved.All(value => value), "All four ghosts moved during " + name);
        Check(movementSamples.All(value => value >= 3), "All four ghosts continuously updated during " + name);
        Check(ghosts.All(ghost => !map.IsGhostSpawn(map.WorldToGrid(ghost.transform.position))),
            "All four ghosts left the house during " + name);
    }

    private static bool IsGhostCell(Vector2Int cell)
    {
        if (!map.InBounds(cell) || (cell.y == map.Rows / 2 && (cell.x == 0 || cell.x == map.Columns - 1)))
        {
            return false;
        }
        return map.IsGhostStepAllowed(cell, cell, true);
    }

    private static int GhostNumber(GhostController ghost)
    {
        return new SerializedObject(ghost).FindProperty("ghostNumber").intValue;
    }

    private static void Finish(string status)
    {
        if (finishing) return;
        finishing = true;
        EditorApplication.update -= Tick;
        CaptureFinal();
        WriteReport(status + "\n" + string.Join("\n", passed.Concat(phaseReports)));
        Debug.Log("Innovation spacing Play checks: " + status);
        EditorApplication.delayCall += StopAfterFinalFrame;
    }

    private static void StopAfterFinalFrame()
    {
        EditorApplication.delayCall -= StopAfterFinalFrame;
        Time.timeScale = 1f;
        SessionState.SetBool(Flag, false);
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed.Add(message);
    }

    private static void AssertLive(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CaptureOverlap()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath()));
        ScreenCapture.CaptureScreenshot("Temp/GoldenRushChecks/spacing-overlap.png");
    }

    private static void CaptureFinal()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath()));
        ScreenCapture.CaptureScreenshot("Temp/GoldenRushChecks/spacing-final.png");
    }

    private static string ReportPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GoldenRushChecks/spacing-play.txt"));
    }

    private static void WriteReport(string report)
    {
        string path = ReportPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report);
    }
}
