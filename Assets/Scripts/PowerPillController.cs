using UnityEngine;

public class PowerPillController : MonoBehaviour
{
    private UIManager uiManager;
    private bool collected;
    void Start() { uiManager = FindFirstObjectByType<UIManager>(); }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("PacStudent") || uiManager == null || !uiManager.IsRoundRunning) return;
        collected = true;
        uiManager.StartScaredTimer(10f);
        uiManager.CollectPellet(50);
        Destroy(gameObject);
    }
}
