using System.Collections;
using UnityEngine;

public class CherryController : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private float moveDuration = 15f;
    private const float SpawnOutsideMargin = 1f;

    private static CherryController instance;
    private LevelGenerator levelGenerator;
    private UIManager uiManager;
    private Coroutine spawnCoroutine;
    private Coroutine moveCoroutine;
    private GameObject currentCherry;
    private Bounds mapBounds;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        uiManager = FindFirstObjectByType<UIManager>();

        if (levelGenerator == null)
        {
            Debug.LogError("CherryController requires a LevelGenerator with valid LevelBounds.");
            enabled = false;
            return;
        }

        if (uiManager == null)
        {
            Debug.LogError("CherryController requires a UIManager with IsRoundRunning.");
            enabled = false;
            return;
        }

        if (cherryPrefab == null)
        {
            Debug.LogError("CherryController requires a cherry prefab.");
            enabled = false;
            return;
        }

        mapBounds = levelGenerator.LevelBounds;
        if (mapBounds.size.x <= 0f || mapBounds.size.y <= 0f)
        {
            Debug.LogError("CherryController requires LevelGenerator.LevelBounds with a positive area.");
            enabled = false;
            return;
        }

        StartSpawnTimer(respawnDelay, true);
    }

    private void OnDisable()
    {
        StopSpawnTimer();
        StopMoveTimer();
    }

    private void OnDestroy()
    {
        StopSpawnTimer();
        StopMoveTimer();

        if (instance == this)
        {
            instance = null;
        }
    }

    // Kept for CherryCollision and existing callers.
    public static void StartRespawnTimer()
    {
        if (instance == null || !instance.isActiveAndEnabled)
        {
            return;
        }

        instance.CleanupCurrentCherry();
        instance.StartSpawnTimer(instance.respawnDelay, false);
    }

    private bool IsRoundRunning()
    {
        return uiManager != null && uiManager.IsRoundRunning;
    }

    private void StopSpawnTimer()
    {
        if (spawnCoroutine == null)
        {
            return;
        }

        StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;
    }

    private void StopMoveTimer()
    {
        if (moveCoroutine == null)
        {
            return;
        }

        StopCoroutine(moveCoroutine);
        moveCoroutine = null;
    }

    private void CleanupCurrentCherry()
    {
        StopMoveTimer();

        if (currentCherry != null)
        {
            Destroy(currentCherry);
            currentCherry = null;
        }
    }

    private void StartSpawnTimer(float delay, bool firstSpawn)
    {
        StopSpawnTimer();
        spawnCoroutine = StartCoroutine(SpawnCherryRoutine(Mathf.Max(0f, delay), firstSpawn));
    }

    private IEnumerator SpawnCherryRoutine(float delay, bool firstSpawn)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (firstSpawn ? IsFirstSpawnTimerActive() : IsRoundRunning())
            {
                elapsed += firstSpawn ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            yield return null;
        }

        spawnCoroutine = null;

        if ((!firstSpawn && !IsRoundRunning()) || (firstSpawn && !IsFirstSpawnTimerActive())
            || currentCherry != null || moveCoroutine != null)
        {
            yield break;
        }

        SpawnCherry();
    }

    private bool IsFirstSpawnTimerActive()
    {
        return uiManager != null && (uiManager.State == UIManager.RoundState.Countdown || IsRoundRunning());
    }

    private void SpawnCherry()
    {
        Vector3 startPosition;
        Vector3 endPosition;
        GetPathPositions(out startPosition, out endPosition);

        currentCherry = Instantiate(cherryPrefab, startPosition, Quaternion.identity, transform);

        SpriteRenderer spriteRenderer = currentCherry.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 100;
        }

        moveCoroutine = StartCoroutine(MoveCherryRoutine(currentCherry.transform, startPosition, endPosition));
    }

    private void GetPathPositions(out Vector3 startPosition, out Vector3 endPosition)
    {
        Vector3 center = mapBounds.center;
        float horizontalDistance = Mathf.Max(Mathf.Abs(mapBounds.min.x - center.x), Mathf.Abs(mapBounds.max.x - center.x));
        float verticalDistance = Mathf.Max(Mathf.Abs(mapBounds.min.y - center.y), Mathf.Abs(mapBounds.max.y - center.y));

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.orthographic)
        {
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            float cameraMinX = mainCamera.transform.position.x - halfWidth;
            float cameraMaxX = mainCamera.transform.position.x + halfWidth;
            float cameraMinY = mainCamera.transform.position.y - halfHeight;
            float cameraMaxY = mainCamera.transform.position.y + halfHeight;
            horizontalDistance = Mathf.Max(horizontalDistance, Mathf.Abs(cameraMinX - center.x), Mathf.Abs(cameraMaxX - center.x));
            verticalDistance = Mathf.Max(verticalDistance, Mathf.Abs(cameraMinY - center.y), Mathf.Abs(cameraMaxY - center.y));
        }

        if (Random.value > 0.5f)
        {
            bool fromLeft = Random.value > 0.5f;
            float startX = center.x + (fromLeft ? -1f : 1f) * (horizontalDistance + SpawnOutsideMargin);
            float startY = Random.Range(mapBounds.min.y, mapBounds.max.y);
            startPosition = new Vector3(startX, startY, center.z);
        }
        else
        {
            bool fromBottom = Random.value > 0.5f;
            float startY = center.y + (fromBottom ? -1f : 1f) * (verticalDistance + SpawnOutsideMargin);
            float startX = Random.Range(mapBounds.min.x, mapBounds.max.x);
            startPosition = new Vector3(startX, startY, center.z);
        }

        endPosition = (2f * center) - startPosition;
    }

    private IEnumerator MoveCherryRoutine(Transform cherryTransform, Vector3 startPosition, Vector3 endPosition)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveDuration);

        while (elapsed < duration)
        {
            if (cherryTransform == null)
            {
                moveCoroutine = null;
                yield break;
            }

            if (IsRoundRunning())
            {
                elapsed += Time.deltaTime;
                cherryTransform.position = Vector3.Lerp(startPosition, endPosition, Mathf.Clamp01(elapsed / duration));
            }

            yield return null;
        }

        moveCoroutine = null;
        if (cherryTransform != null && currentCherry == cherryTransform.gameObject)
        {
            cherryTransform.position = endPosition;
            DestroyCherry(cherryTransform.gameObject);
        }
    }

    // Manage cherry removal when eaten or destroyed.
    public void DestroyCherry(GameObject cherryObject)
    {
        if (cherryObject == null || cherryObject != currentCherry)
        {
            return;
        }

        CleanupCurrentCherry();
        StartSpawnTimer(respawnDelay, false);
        Debug.Log("Cherry destroyed. Respawn timer started.");
    }
}
