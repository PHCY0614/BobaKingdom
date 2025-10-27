using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerPillController : MonoBehaviour
{
    private const int POWER_PILL_SCORE = 50;
    
    // 幽靈進入 Scared 狀態的總時長 (10 秒)
    private const float SCARED_DURATION = 10.0f; 
    
    // 進入 Recovering 狀態的剩餘時間 (10 - 3 = 7 秒時切換)
    private const float RECOVERING_THRESHOLD = 3.0f; 
    
    private UIManager uiManager;
    // 🚨 H1 新增：GhostManager 引用 (稍後會用到)
    private GhostManager ghostManager; 

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        // 假設你未來會新增一個 GhostManager
        ghostManager = FindFirstObjectByType<GhostManager>();

        if (uiManager == null) Debug.LogError("UIManager not found!");
        if (ghostManager == null) Debug.LogWarning("GhostManager not found! Ghost states cannot be changed yet.");
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PacStudent"))
        {
            // 1. 計分 (Add 50 points)
            if (uiManager != null)
            {
                uiManager.AddScore(POWER_PILL_SCORE);
            }
        
            // 2. 啟動 Scared 狀態計時器並更新 UI
            // UIManager 已經有 StartScaredTimer 方法
            if (uiManager != null)
            {
                uiManager.StartScaredTimer(SCARED_DURATION);
            }
        
            // 3. 改變 Ghost 狀態與音樂 (假設我們有一個 GhostManager)
            if (ghostManager != null)
            {
                // 這是未來步驟，但我們先預留程式碼
                // ghostManager.SetGhostState(GhostState.Scared, RECOVERING_THRESHOLD);
                // ghostManager.ChangeBackgroundMusic(MusicState.Scared);
            }

            // 4. 銷毀 Power Pill
            Destroy(gameObject);

            // 5. 🚨 H2 新增：處理 Recovering 和 Normal 狀態的計時（透過 UIManager 的計時來觸發）
            // 由於 UIManager 已經在 Update() 中獨立處理 scaredTimeRemaining，
            // 我們讓 UIManager 負責在時間到達時發出信號，但目前我們還沒有 GhostManager，
            // 所以我們先讓 UIManager 專注於 UI 邏輯。
        
            // 由於你需要一個在 SCARED_DURATION 結束時執行的動作，我們使用 Coroutine 或 Invoke。
            // 我們決定讓 UIManager 處理計時和 UI，並讓 PowerPill 負責開始計時。
        }
    }
}
