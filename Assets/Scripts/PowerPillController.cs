using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerPillController : MonoBehaviour
{
    private const int POWER_PILL_SCORE = 50;
    private const float SCARED_DURATION = 10.0f; 
    private UIManager uiManager;

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PacStudent"))
        {
            if (uiManager != null)
            {
                uiManager.AddScore(POWER_PILL_SCORE);
            }

            if (uiManager != null)
            {
                uiManager.StartScaredTimer(SCARED_DURATION);
            }

            Destroy(gameObject);
        }
    }
}
