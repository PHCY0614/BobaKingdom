using UnityEngine;

public class PelletController : MonoBehaviour
{
    private UIManager uiManager;
    private bool collected;
    void Start() { uiManager = FindFirstObjectByType<UIManager>(); }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("PacStudent") || uiManager == null || !uiManager.IsRoundRunning) return;
        collected = true;
        uiManager.CollectPellet(10);
        Destroy(gameObject);
    }
}
