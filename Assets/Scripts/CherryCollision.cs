using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CherryCollision : MonoBehaviour
{
    private CherryController controller;
    private const int cherryScore = 100;
    private UIManager uiManager;

    void Start()
    {
        // 取得 CherryController 的引用
        controller = FindFirstObjectByType<CherryController>();
        if (controller == null)
        {
            Debug.LogError("CherryController not found in scene!");
            Destroy(gameObject);
        }
        
        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("UIManager not found in scene! Score will not be updated.");
        }
    }
    
    // 額外功能：處理碰撞 (假設 Cherry 有一個 Trigger Collider)
    void OnTriggerEnter2D(Collider2D other)
    {
        // 假設 PacStudent 有一個 Tag 叫做 "PacStudent"
        if (other.CompareTag("PacStudent"))
        {
            if (uiManager != null)
            {
                uiManager.AddScore(cherryScore);
                Debug.Log($"PacStudent ate the cherry! Bonus points added: {cherryScore}");
            }
            if (controller != null)
            {
                controller.DestroyCherry(gameObject);
            }
        }
    }
}
