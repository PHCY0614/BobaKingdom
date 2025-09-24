using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public GameObject ManualLevel;
    public GameObject[] tilePrefabs;
    
    private int[,] levelMap =
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

    private int[,] rotationMap;

    void Start()
    {
        // Hide ManualLevel
        if (ManualLevel != null) ManualLevel.SetActive(false);

        rotationMap = new int[levelMap.GetLength(0), levelMap.GetLength(1)];
        GenerateLevel(levelMap, rotationMap);
    }

    // ------Main Function------
    void GenerateLevel(int[,] map, int[,] rotations)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int tile = map[y, x];
                
                int rotation = 0;

                switch (tile)
                {
                    case 2: // Outside wall
                    case 4: // Inside wall
                        rotation = CalcWallRotation(x, y, map);
                        break;

                    case 1: // Outside corner
                    case 3: // Inside corner
                        // Check tile 3 on the right border
                        if (tile == 3 && x == cols - 1) 
                        {
                            bool bottomEmpty = (y < rows - 1) && (map[y + 1, x] == 0 || map[y + 1, x] == 5);
                            bool leftEmpty = (x > 0) && (map[y, x - 1] == 0 || map[y, x - 1] == 5);
                            
                            if (bottomEmpty && leftEmpty) 
                            {
                                rotation = 90;
                                break;
                            }
                        }
                        
                        // normal tile 3
                        if (tile == 3 && HasAdjacent3(map, x, y))
                            rotation = CalcInsideCornerParallelWithEmpty(x, y, map);
                        else
                            rotation = CalcCornerRotationByWall(x, y, map);
                        break;

                    case 7: // T junction
                        rotation = CalcTJunctionRotation(x, y, map);
                        break;

                    default: // Other tile (5, 6, 8)
                        rotation = 0;
                        break;
                }

                rotations[y, x] = rotation;

                // Generate tiles
                if (tile >= 0 && tile < tilePrefabs.Length && tilePrefabs[tile] != null)
                {
                    Quaternion q = Quaternion.Euler(0, 0, rotation);
                    GameObject newTile = Instantiate(tilePrefabs[tile], new Vector3(x, -y, 0), q);
                    newTile.transform.SetParent(this.transform); 
                }
            }
        }
    }

    // ------Utility Function------
    bool IsWall(int tile) => tile == 1 || tile == 2 || tile == 3 || tile == 4;
    bool IsEmpty(int tile) => tile == 0 || tile == 5 || tile == 6;

    bool HasAdjacent3(int[,] map, int x, int y)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        return (x > 0 && map[y, x - 1] == 3) ||
               (x < cols - 1 && map[y, x + 1] == 3) ||
               (y > 0 && map[y - 1, x] == 3) ||
               (y < rows - 1 && map[y + 1, x] == 3);
    }

    // ------Rotation Methods------
    int CalcWallRotation(int x, int y, int[,] map) // 2 and 4
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // Check if the four directions are empty (0, 5, 6). boundaries are not considered empty
        bool topEmpty = (y > 0) && IsEmpty(map[y - 1, x]);
        bool bottomEmpty = (y < rows - 1) && IsEmpty(map[y + 1, x]);
        bool leftEmpty = (x > 0) && IsEmpty(map[y, x - 1]);
        bool rightEmpty = (x < cols - 1) && IsEmpty(map[y, x + 1]);
        
        if (leftEmpty || rightEmpty)
        {
            return 90; 
        }
        else if (topEmpty || bottomEmpty)
        {
            return 0; 
        }
        
        return 0; 
    }

    int CalcCornerRotationByWall(int x, int y, int[,] map) // 1 and 3
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // Check if the four directions are walls
        bool rightWall = (x < cols - 1) && IsWall(map[y, x + 1]);
        bool leftWall = (x > 0) && IsWall(map[y, x - 1]);
        bool topWall = (y > 0) && IsWall(map[y - 1, x]);
        bool bottomWall = (y < rows - 1) && IsWall(map[y + 1, x]);

        // Check the rotation of walls
        int rightWallRotation = GetWallRotation(x + 1, y, map);
        int leftWallRotation = GetWallRotation(x - 1, y, map);
        int topWallRotation = GetWallRotation(x, y - 1, map);
        int bottomWallRotation = GetWallRotation(x, y + 1, map);
        
        if (rightWall && rightWallRotation == 0 && bottomWall && bottomWallRotation == 90)
            return 0;
        
        if (rightWall && rightWallRotation == 0 && topWall && topWallRotation == 90)
            return 90;
        
        if (leftWall && leftWallRotation == 0 && bottomWall && bottomWallRotation == 90)
            return 270;
        
        if (leftWall && leftWallRotation == 0 && topWall && topWallRotation == 90)
            return 180;

        // Use a simplified version if the above conditions do not apply
        if (rightWall && bottomWall) return 0;   // ┌
        if (rightWall && topWall) return 270;    // └
        if (leftWall && bottomWall) return 90;   // ┐
        if (leftWall && topWall) return 180;     // ┘

        return 0;
    }

    int GetWallRotation(int x, int y, int[,] map)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        
        if (x < 0 || y < 0 || x >= cols || y >= rows) return 0;
        
        int tile = map[y, x];
        if (tile != 2 && tile != 4) return 0;
        
        return CalcWallRotation(x, y, map);
    }

    int CalcInsideCornerParallelWithEmpty(int x, int y, int[,] map)  // 3
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // Check if the four directions are empty
        bool topEmpty = (y > 0) ? (map[y - 1, x] == 0 || map[y - 1, x] == 5) : false;
        bool bottomEmpty = (y < rows - 1) ? (map[y + 1, x] == 0 || map[y + 1, x] == 5) : false;
        bool leftEmpty = (x > 0) ? (map[y, x - 1] == 0 || map[y, x - 1] == 5) : false;
        bool rightEmpty = (x < cols - 1) ? (map[y, x + 1] == 0 || map[y, x + 1] == 5) : false;
        
        // Special case: at right edge, rotate 90° if bottom and left tiles are 0 or 5
        if (x == cols - 1 && bottomEmpty && leftEmpty) 
        {
            return 90;
        }
        
        if (rightEmpty && topEmpty) return 270;
        if (rightEmpty && bottomEmpty) return 180;
        if (bottomEmpty && leftEmpty) return 90;
        if (leftEmpty && topEmpty) return 0;
        
        return CalcCornerRotationByWall(x, y, map);
    }

    int CalcTJunctionRotation(int x, int y, int[,] map)  // 7
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
        
        if (topWall && bottomWall && rightWall) return 90;  // ⊣
        if (topWall && bottomWall && leftWall) return 270;  // ⊢

        return 0; 
    }
}