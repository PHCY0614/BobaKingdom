using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public GameObject manualLevel;
    public GameObject[] tilePrefabs;

    private int[,] _levelMap =
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

    bool IsWall(int tile) => tile == 2 || tile == 4;
    bool IsEmpty(int tile) => tile == 0 || tile == 5 || tile == 6;
    bool IsCorner(int tile) => tile == 1 || tile == 3;

    void Start()
    {
        if (manualLevel != null) manualLevel.SetActive(false);

        int rows = _levelMap.GetLength(0);
        int cols = _levelMap.GetLength(1);
        int mirrorRows = rows - 1; // ignore the bottom row
        int fullRows = rows + mirrorRows;
        int fullCols = cols * 2;

        int[,] fullMap = new int[fullRows, fullCols];

        // top left：original
        CopyTo(fullMap, _levelMap, 0, 0, false, false, rows);

        // bottom left：
        CopyTo(fullMap, _levelMap, rows, 0, false, true, mirrorRows);

        // top right
        CopyTo(fullMap, _levelMap, 0, cols, true, false, rows);

        // bottom right
        CopyTo(fullMap, _levelMap, rows, cols, true, true, mirrorRows);

        GenerateLevel(fullMap);
    }

    // ------Main Function------
    void GenerateLevel(int[,] map)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        Vector3 offset = new Vector3(-cols / 2f + 9f, rows / 2f + 0.5f, 0);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int tile = map[y, x];

                int rotation ;
                if (IsWall(tile)) rotation = CalcWallRotation(x, y, map);
                else if (IsCorner(tile)) rotation = CalcCornerRotation(x, y, map);
                else if (tile == 7) rotation = CalcTJunctionRotation(x, y, map);
                else rotation = 0;

                // Generate tiles
                if (tile >= 0 && tile < tilePrefabs.Length && tilePrefabs[tile] != null)
                {
                    Quaternion q = Quaternion.Euler(0, 0, rotation);
                    Vector3 position = new Vector3(x, -y, 0) + offset;
                    GameObject newTile = Instantiate(tilePrefabs[tile], position, q);
                    newTile.transform.SetParent(this.transform);
                }
            }
        }
    }
    // ------Rotation Methods------
    int CalcWallRotation(int x, int y, int[,] map) // 2 and 4
    {
        int cols = map.GetLength(1);

        bool leftEmpty = (x > 0) && IsEmpty(map[y, x - 1]);
        bool rightEmpty = (x < cols - 1) && IsEmpty(map[y, x + 1]);

        return (leftEmpty || rightEmpty) ? 90 : 0;
    }

    int CalcCornerRotation(int x, int y, int[,] map) // 1 and 3
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        bool rightWall = (x < cols - 1) && IsWall(map[y, x + 1]);
        bool leftWall = (x > 0) && IsWall(map[y, x - 1]);
        bool topWall = (y > 0) && IsWall(map[y - 1, x]);
        bool bottomWall = (y < rows - 1) && IsWall(map[y + 1, x]);

        bool rightEmpty = (x < cols - 1) && IsEmpty(map[y, x + 1]);
        bool leftEmpty = (x > 0) && IsEmpty(map[y, x - 1]);
        bool topEmpty = (y > 0) && IsEmpty(map[y - 1, x]);
        bool bottomEmpty = (y < rows - 1) && IsEmpty(map[y + 1, x]);
        
        // Special rule: when 3 in a row + 0/5
        if (map[y, x] == 3)
        {
            // Right edge exception
            if (x == cols - 1 && bottomEmpty && leftEmpty) return 90;
            
            if (rightEmpty && topEmpty) return 270;
            if (rightEmpty && bottomEmpty) return 180;
            if (bottomEmpty && leftEmpty) return 90;
            if (leftEmpty && topEmpty) return 0;
        }

        // Usual rule
        if (map[y, x] == 1 || map[y, x] == 3)
        {
            int rightWallRotation = (rightWall) ? CalcWallRotation(x + 1, y, map) : -1;
            int leftWallRotation = (leftWall) ? CalcWallRotation(x - 1, y, map) : -1;
            int topWallRotation = (topWall) ? CalcWallRotation(x, y - 1, map) : -1;
            int bottomWallRotation = (bottomWall) ? CalcWallRotation(x, y + 1, map) : -1;

            if (rightWall && rightWallRotation == 0 && bottomWall && bottomWallRotation == 90) return 0;
            if (rightWall && rightWallRotation == 0 && topWall && topWallRotation == 90) return 90;
            if (leftWall && leftWallRotation == 0 && bottomWall && bottomWallRotation == 90) return 270;
            if (leftWall && leftWallRotation == 0 && topWall && topWallRotation == 90) return 180;
            
            if (rightWall && bottomWall) return 0;
            if (rightWall && topWall) return 270;
            if (leftWall && bottomWall) return 90;
            if (leftWall && topWall) return 180;
        }

        return 0;
        
    }
            
    int CalcTJunctionRotation(int x, int y, int[,] map) // 7
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // Check if the four directions are walls
        bool leftWall = (x > 0) && (map[y, x - 1] == 2);
        bool rightWall = (x < cols - 1) && (map[y, x + 1] == 2);
        bool topWall = (y > 0) && IsWall(map[y - 1, x]);
        bool bottomWall = (y < rows - 1) && IsWall(map[y + 1, x]);

        if (leftWall && rightWall && bottomWall) return 0;
        if (leftWall && rightWall && topWall) return 180;

        if (topWall && bottomWall && rightWall) return 90; // ⊣
        if (topWall && bottomWall && leftWall) return 270; // ⊢

        return 0;
    }
    void CopyTo(int[,] target, int[,] source, int startY, int startX, bool mirrorX, bool mirrorY, int copyRows)
    {
        int cols = source.GetLength(1);

        for (int y = 0; y < copyRows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int srcY = mirrorY ? copyRows - 1 - y : y;
                int srcX = mirrorX ? cols - 1 - x : x;

                target[startY + y, startX + x] = source[srcY, srcX];
            }
        }
    }
}    