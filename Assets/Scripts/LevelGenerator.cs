using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    private int[,] _quadrantMap =
    {
        { 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 7 },
        { 2, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4 },
        { 2, 5, 3, 4, 4, 3, 5, 3, 4, 4, 4, 3, 5, 4 },
        { 2, 6, 4, 0, 0, 4, 5, 4, 0, 0, 0, 4, 5, 4 },
        { 2, 5, 3, 4, 4, 3, 5, 3, 4, 4, 4, 3, 5, 3 },
        { 2, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 },
        { 2, 5, 3, 4, 4, 3, 5, 3, 3, 5, 3, 4, 4, 4 },
        { 2, 5, 3, 4, 4, 3, 5, 4, 4, 5, 3, 4, 4, 3 },
        { 2, 5, 5, 5, 5, 5, 5, 4, 4, 5, 5, 5, 5, 4 },
        { 1, 2, 2, 2, 2, 1, 5, 4, 3, 4, 4, 3, 0, 4 },
        { 0, 0, 0, 0, 0, 2, 5, 4, 3, 4, 4, 3, 0, 3 },
        { 0, 0, 0, 0, 0, 2, 5, 4, 4, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 2, 5, 4, 4, 0, 3, 4, 4, 8 },
        { 2, 2, 2, 2, 2, 1, 5, 3, 3, 0, 4, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 4, 0, 0, 0 },
    };

    private int[,] _levelMap;
    private readonly HashSet<Vector2Int> spawnCells = new HashSet<Vector2Int>();
    private Vector2Int topExit;
    private Vector2Int bottomExit;
    public int Rows { get { EnsureMap(); return _levelMap.GetLength(0); } }
    public int Columns { get { EnsureMap(); return _levelMap.GetLength(1); } }
    public Bounds LevelBounds => new Bounds(new Vector3((Columns - 1) / 2f, -(Rows - 1) / 2f, 0), new Vector3(Columns, Rows, 1));
    public Vector2Int GhostSpawnCentre { get; private set; }

    void Awake() { EnsureMap(); }

    private void EnsureMap()
    {
        if (_levelMap != null) return;
        int rows = _quadrantMap.GetLength(0), cols = _quadrantMap.GetLength(1);
        _levelMap = new int[rows * 2 - 1, cols * 2];
        for (int y = 0; y < rows * 2 - 1; y++)
            for (int x = 0; x < cols * 2; x++)
                _levelMap[y, x] = _quadrantMap[y < rows ? y : 2 * rows - 2 - y, x < cols ? x : 2 * cols - 1 - x];

        int minY = int.MaxValue, maxY = -1, gateX = cols - 1;
        for (int y = 0; y < _levelMap.GetLength(0); y++)
            for (int x = 0; x < _levelMap.GetLength(1); x++)
                if (_levelMap[y, x] == 8) { minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); gateX = Mathf.Min(gateX, x); }
        if (maxY < 0) return;
        topExit = new Vector2Int(gateX, minY - 1);
        bottomExit = new Vector2Int(gateX, maxY + 1);
        GhostSpawnCentre = new Vector2Int(gateX, (minY + maxY) / 2);
        var pending = new Queue<Vector2Int>();
        pending.Enqueue(GhostSpawnCentre);
        while (pending.Count > 0)
        {
            var cell = pending.Dequeue();
            if (!InBounds(cell) || _levelMap[cell.y, cell.x] != 0 || !spawnCells.Add(cell)) continue;
            pending.Enqueue(cell + Vector2Int.up); pending.Enqueue(cell + Vector2Int.down);
            pending.Enqueue(cell + Vector2Int.left); pending.Enqueue(cell + Vector2Int.right);
        }
    }

    public Vector2Int WorldToGrid(Vector3 world) => new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.y));
    public Vector3 GridToWorld(Vector2Int cell) => new Vector3(cell.x, -cell.y, 0);
    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows;
    public bool IsWalkable(int x, int y)
    {
        EnsureMap();
        var cell = new Vector2Int(x, y);
        if (!InBounds(cell)) return false;
        int tile = _levelMap[y, x];
        return tile == 0 || tile == 5 || tile == 6;
    }
    public bool IsGhostSpawn(Vector2Int cell) { EnsureMap(); return spawnCells.Contains(cell); }
    public Vector2Int GetGhostExit(bool top) { EnsureMap(); return top ? topExit : bottomExit; }
    public bool IsGhostStepAllowed(Vector2Int from, Vector2Int to, bool leavingSpawn)
    {
        if (!InBounds(to)) return false;
        if (leavingSpawn && (_levelMap[to.y, to.x] == 8 || IsGhostSpawn(to))) return true;
        if (IsGhostSpawn(to) || _levelMap[to.y, to.x] == 8) return false;
        // Tunnel endpoints belong to PacStudent; ghosts turn before reaching them.
        if ((to.x == 0 || to.x == Columns - 1) && to.y == Rows / 2) return false;
        return IsWalkable(to.x, to.y);
    }
}
