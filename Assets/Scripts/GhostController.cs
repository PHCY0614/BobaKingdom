using UnityEngine;

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
    private GhostState currentState = GhostState.Normal;
    // 確保你在 Unity Inspector 中為每個幽靈物件添加了 Animator 元件

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Animator component not found on Ghost!");
    }
    
    public void SetState(GhostState newState)
    {
        // 檢查：如果幽靈已經是 Dead 狀態，則無視 Normal 指令 (這是作業要求的一部分，但先不做完整判斷)
        // if (currentState == GhostState.Dead && newState == GhostState.Normal) return; 

        currentState = newState;
        
        if (animator != null)
        {
            // 1. 設定主狀態
            animator.SetInteger("GhostState", (int)newState);

            // 2. 在切換到非 Normal 狀態時，強制將 Blend Tree 參數歸零
            if (newState == GhostState.Scared || newState == GhostState.Recovering || newState == GhostState.Dead)
            {
                // 確保這些 float 參數的名稱與你的 Blend Tree 參數名稱完全一致！
                // 如果你的 Blend Tree 只使用一個參數，請只重設那個
                animator.SetFloat("MoveX", 0f); 
                animator.SetFloat("MoveY", 0f);
            
                // 你的問題藍線停頓，就是因為 MoveX/Y 的非零值在影響動畫播放。
            }
        }

        // 待辦：在這裡實作移動速度、AI 邏輯的切換
    }
}