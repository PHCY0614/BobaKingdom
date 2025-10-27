using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PelletController : MonoBehaviour
{
    private const int pelletScore = 10;
    private UIManager uiManager;

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("UIManager not found in scene! Score will not be updated.");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PacStudent"))
        {
            if (uiManager != null)
            {
                uiManager.AddScore(pelletScore);
            }
            Destroy(gameObject);
        }
    }
}
