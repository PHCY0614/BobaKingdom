using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostManager : MonoBehaviour
{
    private List<GhostController> allGhosts = new List<GhostController>();
    void Awake()
    {
        allGhosts.AddRange(FindObjectsByType<GhostController>(FindObjectsSortMode.None)); 
    }
    
    public void ChangeStateToScared()
    {
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
        foreach (var ghost in allGhosts)
        {
            if (ghost != null)
            {
                ghost.SetState(GhostState.Normal);
            }
        }
    }
}