using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class InnovationAIChecks
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("PacStudent/Run Innovation AI checks")]
    public static void Run()
    {
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        var temporaryObjects = new List<GameObject>();
        try
        {
            GameObject mapObject = TemporaryObject("Innovation AI map");
            temporaryObjects.Add(mapObject);
            LevelGenerator map = mapObject.AddComponent<LevelGenerator>();

            GameObject uiObject = TemporaryObject("Innovation AI UI");
            temporaryObjects.Add(uiObject);
            UIManager ui = uiObject.AddComponent<UIManager>();
            InnovationController innovation = uiObject.AddComponent<InnovationController>();
            SetField(ui, "<Innovation>k__BackingField", innovation);

            GameObject playerObject = TemporaryObject("Innovation AI player");
            temporaryObjects.Add(playerObject);
            PacStudentController player = playerObject.AddComponent<PacStudentController>();
            SetField(player, "facing", Vector3.right);

            GameObject ghostObject = TemporaryObject("Innovation AI ghost");
            temporaryObjects.Add(ghostObject);
            GhostController ghost = ghostObject.AddComponent<GhostController>();
            SetField(ghost, "levelGenerator", map);
            SetField(ghost, "pacStudent", player);
            SetField(ghost, "uiManager", ui);
            MethodInfo choose = typeof(GhostController).GetMethod("ChooseNextGridPosition", InstancePrivate);
            MethodInfo target = typeof(GhostController).GetMethod("ChooseInnovationInterceptTarget", InstancePrivate);
            MethodInfo speed = typeof(GhostController).GetMethod("GetMoveSpeed", InstancePrivate);
            Check(choose != null && target != null && speed != null, "Innovation AI reflection hooks exist");

            CheckInnovationChase(map, ghost, player, choose);
            CheckNoReverse(map, ghost, player, choose);
            CheckInterceptTargets(map, ghost, player, choose, target);
            CheckClosedRoomInterceptFallback(map, ghost, player, choose, target);
            CheckRedSpeed(map, ghost, speed, ui, innovation);
            CheckGhostSpacing(map, ui, innovation, player, temporaryObjects);
            CheckLevelOneBranch(map, ghost, player, speed, ui);
            Debug.Log("Innovation AI checks passed.");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            foreach (GameObject temporary in temporaryObjects)
            {
                if (temporary != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }
    }

    private static void CheckInnovationChase(LevelGenerator map, GhostController ghost,
        PacStudentController player, MethodInfo choose)
    {
        SetField(ghost, "ghostNumber", 2);
        Vector2Int[] starts = { new Vector2Int(1, 1), new Vector2Int(26, 1), new Vector2Int(1, 27), new Vector2Int(26, 27) };
        Vector2Int[] targets = { new Vector2Int(13, 5), new Vector2Int(14, 5), new Vector2Int(13, 23), new Vector2Int(14, 23) };
        int checkedPositions = 0;
        for (int i = 0; i < starts.Length; i++)
        {
            if (!IsTargetCell(map, starts[i]) || !IsTargetCell(map, targets[i])) continue;
            player.transform.position = map.GridToWorld(targets[i]);
            ghost.currentState = GhostState.Normal;
            SetField(ghost, "currentDirection", Vector3.zero);
            Vector2Int next = InvokeNext(choose, ghost, starts[i]);
            Dictionary<Vector2Int, int> distances = Distances(map, targets[i]);
            Check(map.IsGhostStepAllowed(starts[i], next, false), "White 2 chooses a legal corridor step");
            Check(distances.ContainsKey(starts[i]) && distances.ContainsKey(next)
                && distances[next] < distances[starts[i]], "White 2 shortens route distance around walls");
            checkedPositions++;
        }

        Check(checkedPositions >= 3, "White 2 chase checks cover multiple maze positions");
    }

    private static void CheckNoReverse(LevelGenerator map, GhostController ghost,
        PacStudentController player, MethodInfo choose)
    {
        SetField(ghost, "ghostNumber", 2);
        Vector2Int from = new Vector2Int(6, 5);
        Vector2Int playerCell = new Vector2Int(20, 5);
        Check(IsTargetCell(map, from) && IsTargetCell(map, playerCell), "No-reversal fixture uses corridors");
        player.transform.position = map.GridToWorld(playerCell);
        ghost.currentState = GhostState.Normal;
        SetField(ghost, "currentDirection", Vector3.right);
        Vector2Int next = InvokeNext(choose, ghost, from);
        Check(map.IsGhostStepAllowed(from, next, false), "Innovation AI avoids walls");
        Check(next - from != Vector2Int.left, "Innovation AI keeps the non-reverse constraint");
    }

    private static void CheckInterceptTargets(LevelGenerator map, GhostController ghost,
        PacStudentController player, MethodInfo choose, MethodInfo target)
    {
        Vector2Int playerCell = new Vector2Int(10, 5);
        Check(IsTargetCell(map, playerCell), "Intercept fixture uses a corridor");
        player.transform.position = map.GridToWorld(playerCell);
        SetField(player, "facing", Vector3.right);
        ghost.currentState = GhostState.Normal;
        SetField(ghost, "currentDirection", Vector3.zero);

        SetField(ghost, "ghostNumber", 1);
        Vector2Int front = (Vector2Int)target.Invoke(ghost, new object[] { playerCell });
        Check(front == new Vector2Int(13, 5), "Green 1 targets three legal cells ahead");
        Vector2Int frontNext = InvokeNext(choose, ghost, playerCell);
        Check(map.IsGhostStepAllowed(playerCell, frontNext, false), "Green 1 uses a legal intercept step");

        SetField(ghost, "ghostNumber", 3);
        Vector2Int back = (Vector2Int)target.Invoke(ghost, new object[] { playerCell });
        Check(back == new Vector2Int(7, 5), "Orange 3 targets three legal cells behind");
        Vector2Int backNext = InvokeNext(choose, ghost, playerCell);
        Check(map.IsGhostStepAllowed(playerCell, backNext, false), "Orange 3 uses a legal intercept step");

        SetField(ghost, "ghostNumber", 1);
        Vector2Int close = InvokeNext(choose, ghost, front);
        Dictionary<Vector2Int, int> chaseDistances = Distances(map, playerCell);
        Check(chaseDistances[close] < chaseDistances[front], "Intercept ghosts chase the player after reaching the target");
    }

    private static void CheckRedSpeed(LevelGenerator map, GhostController ghost, MethodInfo speed,
        UIManager ui, InnovationController innovation)
    {
        SetField(ghost, "ghostNumber", 4);
        ghost.currentState = GhostState.Normal;
        float innovationSpeed = (float)speed.Invoke(ghost, null);
        Check(Mathf.Abs(innovationSpeed - 3.75f) < 0.001f, "Red 4 Innovation speed is 75 percent");
    }

    private static void CheckClosedRoomInterceptFallback(LevelGenerator map, GhostController ghost,
        PacStudentController player, MethodInfo choose, MethodInfo target)
    {
        Vector2Int playerCell = new Vector2Int(6, 3);
        Vector2Int ghostCell = new Vector2Int(6, 5);
        Vector2Int roomCell = new Vector2Int(3, 3);
        Check(IsTargetCell(map, playerCell) && IsTargetCell(map, ghostCell) && IsTargetCell(map, roomCell),
            "Closed-room intercept fixture uses valid cells");

        player.transform.position = map.GridToWorld(playerCell);
        ghost.currentState = GhostState.Normal;
        SetField(ghost, "currentDirection", Vector3.zero);
        foreach (int ghostNumber in new[] { 1, 3 })
        {
            SetField(ghost, "ghostNumber", ghostNumber);
            SetField(player, "facing", ghostNumber == 1 ? Vector3.left : Vector3.right);
            Vector2Int predicted = (Vector2Int)target.Invoke(ghost, new object[] { playerCell });
            Check(predicted == roomCell, "Green/Orange prediction can enter the enclosed room fixture");
            Vector2Int next = InvokeNext(choose, ghost, ghostCell);
            Check(map.IsGhostStepAllowed(ghostCell, next, false), "Closed-room fallback keeps the step legal");
            Dictionary<Vector2Int, int> chaseDistances = Distances(map, playerCell);
            Check(chaseDistances[next] < chaseDistances[ghostCell],
                "Closed-room intercept fallback still chases the player");
        }
    }

    private static void CheckLevelOneBranch(LevelGenerator map, GhostController ghost,
        PacStudentController player, MethodInfo speed, UIManager ui)
    {
        SetField(ui, "<Innovation>k__BackingField", null);
        SetField(ghost, "ghostNumber", 4);
        ghost.currentState = GhostState.Normal;
        float levelOneSpeed = (float)speed.Invoke(ghost, null);
        Check(Mathf.Abs(levelOneSpeed - 4.5f) < 0.001f, "Level 1 keeps the original 90 percent speed");
        player.transform.position = map.GridToWorld(new Vector2Int(20, 5));
    }

    private static void CheckGhostSpacing(LevelGenerator map, UIManager ui,
        InnovationController innovation, PacStudentController player, List<GameObject> temporaryObjects)
    {
        GameObject leaderObject = TemporaryObject("Innovation spacing leader");
        GameObject followerObject = TemporaryObject("Innovation spacing follower");
        temporaryObjects.Add(leaderObject);
        temporaryObjects.Add(followerObject);
        GhostController leader = leaderObject.AddComponent<GhostController>();
        GhostController follower = followerObject.AddComponent<GhostController>();
        SetField(leader, "uiManager", ui);
        SetField(leader, "levelGenerator", map);
        SetField(leader, "pacStudent", player);
        SetField(leader, "ghostNumber", 1);
        SetField(follower, "uiManager", ui);
        SetField(follower, "levelGenerator", map);
        SetField(follower, "pacStudent", player);
        SetField(follower, "ghostNumber", 3);
        MethodInfo spacedDistance = typeof(GhostController).GetMethod("GetSpacedMoveDistance", InstancePrivate);
        Check(spacedDistance != null, "Ghost spacing reflection hook exists");
        GhostController[] cache = { leader, follower };
        SetField(leader, "innovationGhosts", cache);
        SetField(follower, "innovationGhosts", cache);
        leader.currentState = follower.currentState = GhostState.Normal;
        SetField(leader, "<IsPermanentlyDefeated>k__BackingField", false);
        SetField(follower, "<IsPermanentlyDefeated>k__BackingField", false);

        SetMotion(map, leader, new Vector2Int(6, 5), Vector2Int.right);
        SetMotion(map, follower, new Vector2Int(5, 5), Vector2Int.right);
        float following = InvokeSpaced(spacedDistance, follower, 1f);
        Check(following < 0.99f, "Same-lane follower respects the minimum gap");
        Check(following <= 0.15f, "Same-lane follower preserves approximately 0.85 cells");

        SetMotion(map, leader, new Vector2Int(6, 5), Vector2Int.right);
        SetMotion(map, follower, new Vector2Int(6, 5), Vector2Int.right);
        float leaderPriority = InvokeSpaced(spacedDistance, leader, 0.2f);
        float followerPriority = InvokeSpaced(spacedDistance, follower, 0.2f);
        Check(leaderPriority > followerPriority && Mathf.Abs(leaderPriority - 0.2f) < 0.001f,
            "Exact overlap gives movement priority to the smaller ghost number");

        SetMotion(map, leader, new Vector2Int(5, 5), Vector2Int.right);
        SetMotion(map, follower, new Vector2Int(6, 5), Vector2Int.left);
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, leader, 1f) - 1f) < 0.001f,
            "Head-on ghosts do not block each other");

        SetMotion(map, leader, new Vector2Int(5, 5), Vector2Int.right);
        SetMotion(map, follower, new Vector2Int(5, 8), Vector2Int.right);
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Parallel ghosts on different corridors do not block each other");

        SetMotion(map, leader, new Vector2Int(6, 5), Vector2Int.right);
        SetMotion(map, follower, new Vector2Int(5, 5), Vector2Int.right);
        leader.gameObject.SetActive(false);
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Inactive spacing ghosts are ignored");
        leader.gameObject.SetActive(true);
        leader.enabled = false;
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Disabled spacing ghosts are ignored");
        leader.enabled = true;
        leader.currentState = GhostState.Dead;
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Dead spacing ghosts are ignored");
        leader.currentState = GhostState.Normal;
        SetField(leader, "<IsPermanentlyDefeated>k__BackingField", true);
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Permanently defeated spacing ghosts are ignored");
        SetField(leader, "<IsPermanentlyDefeated>k__BackingField", false);

        SetField(ui, "<Innovation>k__BackingField", null);
        Check(Mathf.Abs(InvokeSpaced(spacedDistance, follower, 1f) - 1f) < 0.001f,
            "Level 1 spacing returns the original requested distance");
        SetField(ui, "<Innovation>k__BackingField", innovation);
    }

    private static void SetMotion(LevelGenerator map, GhostController ghost,
        Vector2Int cell, Vector2Int gridDirection)
    {
        Vector3 position = map.GridToWorld(cell);
        Vector3 target = map.GridToWorld(cell + gridDirection);
        Vector3 direction = new Vector3(gridDirection.x, -gridDirection.y, 0f).normalized;
        ghost.transform.position = position;
        SetField(ghost, "currentPosition", position);
        SetField(ghost, "targetPosition", target);
        SetField(ghost, "currentDirection", direction);
        SetField(ghost, "movementRoutine", null);
        SetField(ghost, "deadRoutine", null);
    }

    private static float InvokeSpaced(MethodInfo spacedDistance, GhostController ghost, float requestedDistance)
    {
        return (float)spacedDistance.Invoke(ghost, new object[] { requestedDistance });
    }

    private static Vector2Int InvokeNext(MethodInfo choose, GhostController ghost, Vector2Int from)
    {
        return (Vector2Int)choose.Invoke(ghost, new object[] { from });
    }

    private static Dictionary<Vector2Int, int> Distances(LevelGenerator map, Vector2Int target)
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

    private static bool IsTargetCell(LevelGenerator map, Vector2Int cell)
    {
        return map.InBounds(cell) && map.IsWalkable(cell.x, cell.y) && !map.IsGhostSpawn(cell)
            && !(cell.y == map.Rows / 2 && (cell.x == 0 || cell.x == map.Columns - 1));
    }

    private static GameObject TemporaryObject(string name)
    {
        var result = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        return result;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Check(field != null, "Reflection field exists: " + fieldName);
        field.SetValue(target, value);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Innovation AI check failed: " + message);
    }
}
