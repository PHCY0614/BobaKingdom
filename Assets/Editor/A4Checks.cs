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
            CheckGhostRules(map);
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

    private static void CheckGhostRules(LevelGenerator map)
    {
        var ghostObject = new GameObject("Ghost rules check");
        var playerObject = new GameObject("Ghost rules target");
        var randomState = UnityEngine.Random.state;
        try
        {
            var ghost = ghostObject.AddComponent<GhostController>();
            var player = playerObject.AddComponent<PacStudentController>();
            player.transform.position = map.GridToWorld(new Vector2Int(6, 4));
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(GhostController);
            type.GetField("levelGenerator", flags).SetValue(ghost, map);
            type.GetField("pacStudent", flags).SetValue(ghost, player);
            var number = type.GetField("ghostNumber", flags);
            var directionField = type.GetField("currentDirection", flags);
            var choose = type.GetMethod("ChooseNextGridPosition", flags);
            var speed = type.GetMethod("GetMoveSpeed", flags);
            foreach (GhostState state in Enum.GetValues(typeof(GhostState)))
            {
                ghost.currentState = state;
                float expected = player.MoveSpeed * (state == GhostState.Normal ? 0.9f : 0.45f);
                Check(Mathf.Abs((float)speed.Invoke(ghost, null) - expected) < 0.001f, "Ghost speed for " + state);
            }
            ghost.currentState = GhostState.Normal;
            for (int id = 1; id <= 4; id++)
            {
                number.SetValue(ghost, id);
                var cell = new Vector2Int(11 + id, 14);
                directionField.SetValue(ghost, Vector3.zero);
                var exit = map.GetGhostExit(id == 1 || id == 3);
                for (int step = 0; step < 20 && cell != exit; step++)
                {
                    var next = (Vector2Int)choose.Invoke(ghost, new object[] { cell });
                    Check(next != cell && map.IsGhostStepAllowed(cell, next, true), "Ghost " + id + " exits house legally");
                    directionField.SetValue(ghost, map.GridToWorld(next) - map.GridToWorld(cell));
                    cell = next;
                }
                Check(cell == exit, "Ghost " + id + " uses assigned exit");
            }
            UnityEngine.Random.InitState(4020);
            var junction = new Vector2Int(6, 5);
            float currentDistance = (map.GridToWorld(junction) - player.transform.position).sqrMagnitude;
            foreach (GhostState state in new[] { GhostState.Normal, GhostState.Scared, GhostState.Recovering })
            {
                ghost.currentState = state;
                for (int id = 1; id <= 4; id++)
                {
                    if (state == GhostState.Normal && id > 2) continue;
                    number.SetValue(ghost, id);
                    directionField.SetValue(ghost, Vector3.right);
                    for (int attempt = 0; attempt < 24; attempt++)
                    {
                        var next = (Vector2Int)choose.Invoke(ghost, new object[] { junction });
                        Check(map.IsGhostStepAllowed(junction, next, false) && next != junction + Vector2Int.left,
                            "Ghost selects a legal step without unnecessary reversal");
                        float distance = (map.GridToWorld(next) - player.transform.position).sqrMagnitude;
                        Check(state == GhostState.Normal && id == 2 ? distance <= currentDistance : distance >= currentDistance,
                            "Ghost " + id + " distance rule for " + state);
                    }
                }
            }
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(ghostObject); UnityEngine.Object.DestroyImmediate(playerObject);
        }
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
