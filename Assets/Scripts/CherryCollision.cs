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
        controller = FindFirstObjectByType<CherryController>();
        uiManager = FindFirstObjectByType<UIManager>();
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
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
