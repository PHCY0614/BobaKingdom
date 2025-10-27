using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CherryCollision : MonoBehaviour
{
    private CherryController controller;

    void Start()
    {
        // 取得 CherryController 的引用
        controller = FindFirstObjectByType<CherryController>();
        if (controller == null)
        {
            Debug.LogError("CherryController not found in scene!");
            Destroy(gameObject);
        }
    }
    
    // 額外功能：處理碰撞 (假設 Cherry 有一個 Trigger Collider)
    void OnTriggerEnter2D(Collider2D other)
    {
        // 假設 PacStudent 有一個 Tag 叫做 "PacStudent"
        if (other.CompareTag("PacStudent"))
        {
            // 執行 PacStudent 吃櫻桃的邏輯（例如加分）
            Debug.Log("PacStudent ate the cherry! Bonus points added.");
            
            // 通知控制器銷毀櫻桃並重新計時
            controller.DestroyCherry(gameObject);
        }
    }
}
