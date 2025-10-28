using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerPillController : MonoBehaviour
{
    private const int POWER_PILL_SCORE = 50;
    private const float SCARED_DURATION = 10.0f; 
    private const float RECOVERING_THRESHOLD = 3.0f; 
    
    private UIManager uiManager;
    private GhostManager ghostManager; 

    void Start()
    {
        uiManager = UIManager.Instance;
        ghostManager = FindFirstObjectByType<GhostManager>();
        if (ghostManager == null) Debug.LogWarning("GhostManager not found! Ghost states cannot be changed yet.");
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
