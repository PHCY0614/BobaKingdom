using System.Collections.Generic;
using UnityEngine;

public class TopLeftMapGenerator : MonoBehaviour
{
    [Header("Tile Prefabs (ID order: 0=Empty, 1=OutsideCorner, ..., 8=GhostExit)")]
    public List<GameObject> tilePrefabs; // Inspector 拉入 9 個 prefab
    public object ManualLevel;
    private int[,] levelMap = {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    void Start()
    {
        // 關閉 ManualLevel
        GameObject ManualLevel = GameObject.Find("ManualLevel");
        if (ManualLevel != null) ManualLevel.SetActive(false);

        // 生成左上角地圖
        InstantiateTopLeftMap(levelMap);
    }

    #region Instantiate & Rotation

    void InstantiateTopLeftMap(int[,] map)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                int tile = map[i, j];
                if (tile < 0 || tile >= tilePrefabs.Count) continue;

                GameObject prefab = tilePrefabs[tile];
                if (prefab == null) continue;

                Vector3 pos = new Vector3(j, -i, 0);
                Quaternion rot = GetTileRotation(map, i, j);
                Instantiate(prefab, pos, rot, this.transform);
            }
        }
    }

    Quaternion GetTileRotation(int[,] map, int row, int col)
    {
        int tile = Get(map, row, col);

        int up    = Get(map, row - 1, col);
        int down  = Get(map, row + 1, col);
        int left  = Get(map, row, col - 1);
        int right = Get(map, row, col + 1);

        bool upWalk  = IsWalkable(up);
        bool downWalk  = IsWalkable(down);
        bool leftWalk  = IsWalkable(left);
        bool rightWalk = IsWalkable(right);

        bool upWall  = IsWall(up);
        bool downWall  = IsWall(down);
        bool leftWall  = IsWall(left);
        bool rightWall = IsWall(right);

        switch (tile)
        {
            case 1: // Outside Corner (右 + 下 = 0°)
                if (rightWall && downWall)  return Quaternion.Euler(0,0,0);
                if (downWall && leftWall)   return Quaternion.Euler(0,0,90);
                if (leftWall && upWall)     return Quaternion.Euler(0,0,180);
                if (upWall && rightWall)    return Quaternion.Euler(0,0,270);
                break;

            case 2: // Outside Wall
                if (upWall && downWall) return Quaternion.Euler(0,0,90);
                if (leftWall && rightWall) return Quaternion.Euler(0,0,0);
                break;

            case 3: // Inside Corner
                int rightTile = Get(map, row, col + 1);
                int downTile = Get(map, row + 1, col);
                int leftTile = Get(map, row, col - 1);
                int upTile = Get(map, row - 1, col);

                // 多格內角矩形組合
                if (rightTile == 3 && downTile == 3) return Quaternion.Euler(0,0,90);   // 左上角
                if (leftTile == 3 && downTile == 3)  return Quaternion.Euler(0,0,270);  // 右上角
                if (rightTile == 3 && upTile == 3)   return Quaternion.Euler(0,0,0);    // 左下角
                if (leftTile == 3 && upTile == 3)    return Quaternion.Euler(0,0,180);  // 右下角

                // 單一內角
                if (rightWall && downWall) return Quaternion.Euler(0,0,0);
                if (downWall && leftWall)  return Quaternion.Euler(0,0,90);
                if (leftWall && upWall)    return Quaternion.Euler(0,0,180);
                if (upWall && rightWall)   return Quaternion.Euler(0,0,270);
                break;

            case 4: // Inside Wall
                if (leftWalk && rightWalk) return Quaternion.Euler(0,0,90);
                if (upWalk && downWalk)   return Quaternion.Euler(0,0,0);
                break;

            case 7: // T Junction
                if (leftWalk && rightWalk && downWalk) return Quaternion.Euler(0,0,0);
                if (upWalk && leftWalk && downWalk)    return Quaternion.Euler(0,0,90);
                if (leftWalk && rightWalk && upWalk)   return Quaternion.Euler(0,0,180);
                if (upWalk && rightWalk && downWalk)   return Quaternion.Euler(0,0,270);
                break;

            case 8: // Ghost Exit
                return Quaternion.identity;
        }

        // fallback
        if (rightWall && downWall) return Quaternion.Euler(0,0,0);
        if (downWall && leftWall)  return Quaternion.Euler(0,0,90);
        if (leftWall && upWall)    return Quaternion.Euler(0,0,180);
        if (upWall && rightWall)   return Quaternion.Euler(0,0,270);

        return Quaternion.identity;
    }

    int Get(int[,] map, int row, int col)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        if (row < 0 || row >= rows || col < 0 || col >= cols) return -1;
        return map[row, col];
    }

    bool IsWalkable(int t)
    {
        return t == 0 || t == 5 || t == 6;
    }

    bool IsWall(int t)
    {
        if (t == -1) return true;
        return !IsWalkable(t);
    }

    #endregion
}
