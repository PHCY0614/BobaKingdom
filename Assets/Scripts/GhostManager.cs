using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostManager : MonoBehaviour
{
    private List<GhostController> allGhosts = new List<GhostController>();
    void Awake()
    {
        // 查找所有單個幽靈並儲存引用
        // FindObjectsByType 是查找所有場景中該類型元件的最佳方法
        // 必須確保你的幽靈遊戲物件上都掛載了 GhostController.cs 腳本
        allGhosts.AddRange(FindObjectsByType<GhostController>(FindObjectsSortMode.None)); 
        Debug.Log("GhostManager: Found " + allGhosts.Count + " GhostControllers in the scene.");
    }
    
    public void ChangeStateToScared()
    {
        Debug.Log("GhostManager: Received Scared command. Broadcasting to all ghosts.");
        // 待辦：遍歷 allGhosts，並呼叫 ghost.SetState(GhostState.Scared)
        foreach (var ghost in allGhosts)
        {
            if (ghost != null)
            {
                ghost.SetState(GhostState.Scared);
            }
        }
    }

    public void ChangeStateToRecovering()
    {
        Debug.Log("GhostManager: Received Recovering command. Broadcasting to all ghosts.");
        // 待辦：遍歷 allGhosts，並呼叫 ghost.SetState(GhostState.Recovering)
        foreach (var ghost in allGhosts)
        {
            if (ghost != null)
            {
                ghost.SetState(GhostState.Recovering);
            }
        }
    }

    public void ChangeStateToNormal()
    {
        Debug.Log("GhostManager: Received Normal command. Broadcasting to all ghosts.");
        // 待辦：遍歷 allGhosts，並呼叫 ghost.SetState(GhostState.Normal)
        foreach (var ghost in allGhosts)
        {
            if (ghost != null)
            {
                // GhostController 內部會判斷 Dead 狀態的幽靈是否要切回 Normal (這是下一個步驟要做的)
                ghost.SetState(GhostState.Normal);
            }
        }
    }
    // ... 後續我們會在這裡加入 PacStudent 死亡後的 ResetGhosts()
}