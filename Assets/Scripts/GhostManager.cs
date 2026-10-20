using System.Collections.Generic;
using UnityEngine;

public class GhostManager : MonoBehaviour
{
    private readonly List<GhostController> allGhosts = new List<GhostController>();

    public bool HasDeadGhost
    {
        get
        {
            foreach (GhostController ghost in allGhosts)
            {
                if (ghost != null && ghost.currentState == GhostState.Dead)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void Awake()
    {
        RefreshGhostList();
    }

    public void ChangeStateToScared()
    {
        ChangeState(GhostState.Scared);
    }

    public void ChangeStateToRecovering()
    {
        ChangeState(GhostState.Recovering);
    }

    public void ChangeStateToNormal()
    {
        ChangeState(GhostState.Normal);
    }

    public void ResetGhosts()
    {
        RefreshGhostList();
        foreach (GhostController ghost in allGhosts)
        {
            if (ghost != null)
            {
                ghost.ResetPosition();
            }
        }
        RefreshGhostMusic();
    }

    private void ChangeState(GhostState state)
    {
        RefreshGhostList();
        foreach (GhostController ghost in allGhosts)
        {
            if (ghost != null)
            {
                ghost.SetState(state);
            }
        }
        RefreshGhostMusic();
    }

    private void RefreshGhostList()
    {
        allGhosts.Clear();
        allGhosts.AddRange(FindObjectsByType<GhostController>(FindObjectsSortMode.None));
    }

    private void RefreshGhostMusic()
    {
        AudioController audio = FindFirstObjectByType<AudioController>();
        if (audio != null)
        {
            audio.RefreshGhostMusic();
        }
    }
}
