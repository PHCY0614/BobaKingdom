using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class A4Checks
{
    [MenuItem("PacStudent/Run A4 rule checks")]
    public static void Run()
    {
        var temporary = new GameObject("A4 rule checks");
        try
        {
            var map = temporary.AddComponent<LevelGenerator>();
            Check(map.Rows == 29 && map.Columns == 28, "Mirrored map dimensions");
            Check(map.IsWalkable(1, 1) && !map.IsWalkable(0, 0), "Wall and corridor classification");
            Check(!map.IsWalkable(-1, 0) && !map.IsWalkable(map.Columns, 0), "Out-of-map movement blocked");
            Check(map.LevelBounds.center == new Vector3(13.5f, -14f, 0), "Map centre");
            Check(map.WorldToGrid(map.GridToWorld(new Vector2Int(6, 9))) == new Vector2Int(6, 9), "Grid/world round trip");
            Check(map.IsGhostSpawn(new Vector2Int(12, 14)), "Ghost house interior");
            Check(!map.IsGhostStepAllowed(new Vector2Int(13, 11), new Vector2Int(13, 12), false), "Ghost house cannot be entered from outside");
            Check(map.IsGhostStepAllowed(new Vector2Int(13, 13), new Vector2Int(13, 12), true), "Ghost house can be exited through gate");
            Check(!map.IsGhostStepAllowed(new Vector2Int(1, 14), new Vector2Int(0, 14), false), "Ghost cannot enter teleporter endpoint");
            var queue = new Queue<Vector2Int>(); var visited = new HashSet<Vector2Int>();
            queue.Enqueue(map.GetGhostExit(true));
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (!visited.Add(cell)) continue;
                int exits = 0;
                foreach (var direction in directions)
                    if (map.IsGhostStepAllowed(cell, cell + direction, false)) { exits++; queue.Enqueue(cell + direction); }
                Check(exits > 0, "Reachable ghost corridor has an exit: " + cell);
            }
            Check(visited.Count > 100, "Ghost corridor coverage");
            Check(UIManager.FormatTime(0f) == "00:00:00", "Initial timer format");
            Check(UIManager.FormatTime(92.25f) == "01:32:25", "Timer hundredths format");
            Check(UIManager.FormatTime(60f) == "01:00:00", "Timer minute rollover");
            CheckRecordRules();
            CheckClockwiseRoute(map);
            Debug.Log("A4 rule checks passed; ghost corridors visited: " + visited.Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
    }
    private static void CheckClockwiseRoute(LevelGenerator map)
    {
        var ghostObject = new GameObject("Clockwise route check");
        var playerObject = new GameObject("Ghost target check");
        try
        {
            var ghost = ghostObject.AddComponent<GhostController>();
            var player = playerObject.AddComponent<PacStudentController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GhostController).GetField("ghostNumber", flags).SetValue(ghost, 4);
            typeof(GhostController).GetField("levelGenerator", flags).SetValue(ghost, map);
            typeof(GhostController).GetField("pacStudent", flags).SetValue(ghost, player);
            var directionField = typeof(GhostController).GetField("currentDirection", flags);
            var choose = typeof(GhostController).GetMethod("ChooseNextGridPosition", flags);
            var cell = map.GetGhostExit(false);
            var direction = Vector3.down;
            var checkpoints = new[] { new Vector2Int(1, 1), new Vector2Int(map.Columns - 2, 1),
                new Vector2Int(map.Columns - 2, map.Rows - 2), new Vector2Int(1, map.Rows - 2), new Vector2Int(1, 1) };
            int checkpoint = 0;
            for (int step = 0; step < 500 && checkpoint < checkpoints.Length; step++)
            {
                directionField.SetValue(ghost, direction);
                ghost.transform.position = map.GridToWorld(cell);
                var next = (Vector2Int)choose.Invoke(ghost, new object[] { cell });
                Check(next != cell && map.IsGhostStepAllowed(cell, next, false), "Ghost 4 chooses a legal moving step");
                direction = map.GridToWorld(next) - map.GridToWorld(cell);
                cell = next;
                if (cell == checkpoints[checkpoint]) checkpoint++;
            }
            Check(checkpoint == checkpoints.Length, "Ghost 4 follows all outer corners clockwise");
        }
        finally { UnityEngine.Object.DestroyImmediate(ghostObject); UnityEngine.Object.DestroyImmediate(playerObject); }
    }

    private static void CheckRecordRules()
    {
        string level = "A4Validation." + Guid.NewGuid().ToString("N");
        string scoreKey = "PacStudent." + level + ".HighScore", timeKey = "PacStudent." + level + ".BestTime";
        try
        {
            UIManager.SaveRecord(level, 100, 30f);
            UIManager.SaveRecord(level, 90, 10f);
            Check(PlayerPrefs.GetInt(scoreKey) == 100 && PlayerPrefs.GetFloat(timeKey) == 30f, "Lower score cannot replace record");
            UIManager.SaveRecord(level, 100, 40f);
            Check(PlayerPrefs.GetFloat(timeKey) == 30f, "Slower tie cannot replace record");
            UIManager.SaveRecord(level, 100, 20f);
            Check(PlayerPrefs.GetFloat(timeKey) == 20f, "Faster tie replaces record");
            UIManager.SaveRecord(level, 200, 50f);
            Check(PlayerPrefs.GetInt(scoreKey) == 200 && PlayerPrefs.GetFloat(timeKey) == 50f, "Higher score replaces record and associated time");
        }
        finally { PlayerPrefs.DeleteKey(scoreKey); PlayerPrefs.DeleteKey(timeKey); PlayerPrefs.Save(); }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("A4 check failed: " + message);
    }
}
