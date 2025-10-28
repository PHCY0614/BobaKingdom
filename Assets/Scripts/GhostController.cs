using UnityEngine;
using System.Collections;

public enum GhostState 
{ 
    Normal = 0, 
    Scared = 1, 
    Recovering = 2, 
    Dead = 3 
}
public class GhostController : MonoBehaviour
{
    private Animator animator;
    private UIManager uiManager;
    public GhostState currentState = GhostState.Normal;
    private Vector3 startPosition;

    void Awake()
    {
        animator = GetComponent<Animator>();
        startPosition = transform.position;
    }

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        
    }
    public void SetState(GhostState newState)
    {
        // If Ghost is dead, ignore normal 
        if (currentState == GhostState.Dead && newState != GhostState.Dead) 
        {
            if (currentState == GhostState.Dead && newState != GhostState.Dead) 
            {
                return;
            }
        }
        currentState = newState;
        
        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)newState);

            // Reset blend tree when not in Normal
            if (newState == GhostState.Scared || 
                newState == GhostState.Recovering || 
                newState == GhostState.Dead)
            {
                animator.SetFloat("MoveX", 0f); 
                animator.SetFloat("MoveY", 0f);
            }
        }
    }
    
    public void HandleEaten(UIManager uiManager)
    {
        if (currentState == GhostState.Dead)
        {
            return;
        }
        
        SetState(GhostState.Dead);
        StartCoroutine(RespawnRoutine());
    }
    
    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSecondsRealtime(3.0f);
        ResetPosition(); 

        if (uiManager != null)
        {
            float scaredTime = uiManager.GetScaredTimeRemaining();
            
            if (scaredTime <= 0f)
            {
                // Scared → Normal
                SetState(GhostState.Normal);
            }
            else if (scaredTime <= 3.0f)
            {
                // in 3 seconds → Recovering
                SetState(GhostState.Recovering);
            }
            else
            {
                // longer time → Scared
                SetState(GhostState.Scared);
            }
        }
    }
    
    public void ResetPosition() 
    {
        transform.position = startPosition;
    }
}