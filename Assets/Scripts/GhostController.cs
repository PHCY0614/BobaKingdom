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
    // 確保你在 Unity Inspector 中為每個幽靈物件添加了 Animator 元件

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Animator component not found on Ghost!");
        startPosition = transform.position;
    }

    void Start()
    {
        uiManager = UIManager.Instance;
    }
    public void SetState(GhostState newState)
    {
        // 檢查：如果幽靈已經是 Dead 狀態，則無視 Normal 指令 (這是作業要求的一部分，但先不做完整判斷)
        if (currentState == GhostState.Dead && newState != GhostState.Dead) 
        {
            // 🔧 改進：Dead 狀態的鬼魂不接受其他狀態廣播
            if (currentState == GhostState.Dead && newState != GhostState.Dead) 
            {
                Debug.Log($"Ghost {gameObject.name} is Dead, ignoring state change to {newState}");
                return;
            }
        }
        currentState = newState;
        
        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)newState);

            // 非 Normal 狀態時，重置 Blend Tree 參數
            if (newState == GhostState.Scared || 
                newState == GhostState.Recovering || 
                newState == GhostState.Dead)
            {
                animator.SetFloat("MoveX", 0f); 
                animator.SetFloat("MoveY", 0f);
            }
        }
        Debug.Log($"Ghost {gameObject.name} changed to state: {newState}");
    }
    
    // 處理被吃掉的邏輯
    public void HandleEaten(UIManager uiManager)
    {
        if (currentState == GhostState.Dead)
        {
            Debug.LogWarning($"Ghost {gameObject.name} is already Dead!");
            return;
        }

        Debug.Log($"Ghost {gameObject.name} was eaten by PacStudent!");
        
        // 切換到 Dead 狀態
        SetState(GhostState.Dead);

        // 啟動復活協程
        StartCoroutine(RespawnRoutine());
    }
    
    // 復活協程
    private IEnumerator RespawnRoutine()
    {
        Debug.Log($"Ghost {gameObject.name} starting 3 second respawn timer...");

        yield return new WaitForSecondsRealtime(3.0f);

        // 重置位置 (必須在切換狀態前完成，否則動畫會影響移動)
        ResetPosition(); 

// 根據 Scared Timer 決定狀態
        if (uiManager != null)
        {
            float scaredTime = uiManager.GetScaredTimeRemaining();
            
            if (scaredTime <= 0f)
            {
                // Scared 狀態已結束 → Normal
                SetState(GhostState.Normal);
                Debug.Log($"Ghost {gameObject.name} respawned to Normal (scared timer ended)");
            }
            else if (scaredTime <= 3.0f)
            {
                // 剩餘 3 秒內 → Recovering
                SetState(GhostState.Recovering);
                Debug.Log($"Ghost {gameObject.name} respawned to Recovering ({scaredTime:F1}s remaining)");
            }
            else
            {
                // 剩餘時間較長 → Scared
                SetState(GhostState.Scared);
                Debug.Log($"Ghost {gameObject.name} respawned to Scared ({scaredTime:F1}s remaining)");
            }
        }
        else
        {
            // 找不到 UIManager，預設回 Normal
            SetState(GhostState.Normal);
            Debug.LogWarning($"Ghost {gameObject.name} respawned to Normal (UIManager not found)");
        }
    }
    
    public void ResetPosition() 
    {
        transform.position = startPosition;
        
        // TODO: 如果有 AI 移動系統，在這裡重置
        // 例如：
        // if (ghostAI != null) ghostAI.ResetTarget();
        // currentTargetWaypoint = 0;
        // isMoving = false;
        
        Debug.Log($"Ghost {gameObject.name} position reset to {startPosition}");
    }
}