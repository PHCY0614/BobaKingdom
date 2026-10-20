using UnityEngine;

public class PearlShot : MonoBehaviour
{
    private const float Speed = 10f;
    private const float MaxTravel = 32f;
    private InnovationController controller;
    private LevelGenerator levelGenerator;
    private UIManager uiManager;
    private GhostController[] targets;
    private Collider2D[] targetColliders;
    private Vector3 direction;
    private float travelled;
    private bool disposed;

    public void Initialize(InnovationController owner, Vector3 shotDirection, LevelGenerator map,
        UIManager ui, GhostController[] cachedTargets)
    {
        controller = owner;
        direction = shotDirection == Vector3.zero ? Vector3.right : shotDirection.normalized;
        levelGenerator = map;
        uiManager = ui;
        targets = cachedTargets;
        targetColliders = new Collider2D[targets == null ? 0 : targets.Length];
        for (int i = 0; i < targetColliders.Length; i++)
            targetColliders[i] = targets[i] == null ? null : targets[i].GetComponent<Collider2D>();
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    public void Advance(float seconds)
    {
        if (disposed || seconds <= 0f || uiManager == null || !uiManager.IsRoundRunning) return;
        if (levelGenerator == null)
        {
            Dispose();
            return;
        }

        Physics2D.SyncTransforms();
        float distance = Speed * seconds;
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
        Vector3 step = direction * (distance / stepCount);
        for (int i = 0; i < stepCount; i++)
        {
            Vector3 next = transform.position + step;
            Vector2Int cell = levelGenerator.WorldToGrid(next);
            if (!levelGenerator.InBounds(cell) || !levelGenerator.IsWalkable(cell.x, cell.y)
                || levelGenerator.IsGhostSpawn(cell))
            {
                Dispose();
                return;
            }

            for (int targetIndex = 0; targetIndex < targetColliders.Length; targetIndex++)
            {
                GhostController ghost = targets[targetIndex];
                Collider2D collider = targetColliders[targetIndex];
                if (ghost == null || collider == null || !collider.enabled || ghost.IsPermanentlyDefeated
                    || ghost.currentState == GhostState.Dead || !collider.OverlapPoint(next)) continue;
                if (controller != null && controller.HitGhost(ghost))
                {
                    Dispose();
                    return;
                }
            }

            transform.position = next;
            travelled += step.magnitude;
            if (travelled >= MaxTravel)
            {
                Dispose();
                return;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (controller != null) controller.UnregisterShot(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (controller != null) controller.UnregisterShot(this);
    }
}
