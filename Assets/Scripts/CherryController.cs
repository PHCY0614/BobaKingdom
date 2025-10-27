using System.Collections;
using UnityEngine;
public class CherryController : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private Vector3 levelCenter = new Vector3(14f, -15f, 0f);
    [SerializeField] private float levelBounds = 35f;
    [SerializeField] private float moveDuration = 15f;
        
    private static CherryController instance; 
    private Coroutine spawnCoroutine;
    private Coroutine moveCoroutine;
    private GameObject currentCherry;
    
    void Awake()
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

    void Start()
    {
        StartSpawnTimer(respawnDelay);
    }
    
    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    
    // Public
    public static void StartRespawnTimer()
    {
        if (instance != null)
        {
            if (instance != null)
            {
                instance.CleanupCurrentCherry();
                instance.StartSpawnTimer(instance.respawnDelay);
            }
        }
    }

    // Private
    private void CleanupCurrentCherry()
    {
        // Stop coroutine
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        // Destroy cherry
        if (currentCherry != null)
        {
            Destroy(currentCherry);
            currentCherry = null;
        }
    }

    // Start a timer to create cherries
    private void StartSpawnTimer(float delay)
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnCherryRoutine(delay));
    }

    // Generate cherries after waiting
    IEnumerator SpawnCherryRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 1. Random start and end 
        Vector3 startPosition;
        Vector3 endPosition;
        
        // Random outside
        bool spawnOnXAxis = Random.value > 0.5f; 

        if (spawnOnXAxis)
        {
            // Random left or right
            float startX = Random.value > 0.5f ? levelBounds : -levelBounds;
            float startY = Random.Range(-levelBounds, levelBounds);
            startPosition = new Vector3(startX, startY, 0f);
            
            // Ensure the end point mirrors the start point across the center
            float endX = -startX; 
            float endY = 2 * levelCenter.y - startY;
            endPosition = new Vector3(endX, endY, 0f);
        }
        else
        {
            // Random up or down
            float startY = Random.value > 0.5f ? levelBounds : -levelBounds;
            float startX = Random.Range(-levelBounds, levelBounds); 
            startPosition = new Vector3(startX, startY, 0f);
            
            // Ensure the end point mirrors the start point across the center
            float endY = -startY; 
            float endX = 2 * levelCenter.x - startX;
            endPosition = new Vector3(endX, endY, 0f);
        }
        
        // 2. Instantiate cherries
        currentCherry = Instantiate(cherryPrefab, startPosition, Quaternion.identity, transform); 
        
        // Ensure cherries are at the top
        SpriteRenderer sr = currentCherry.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 100;
        }
        
        // 3. Start Coroutine
        StartCoroutine(MoveCherryRoutine(currentCherry.transform, startPosition, endPosition, moveDuration));
    }
    
    IEnumerator MoveCherryRoutine(Transform cherryTransform, Vector3 startPos, Vector3 endPos, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Check if the cherry exist
            if (cherryTransform == null)
            {
                yield break; 
            }

            cherryTransform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (cherryTransform != null)
        {
            cherryTransform.position = endPos;
            DestroyCherry(cherryTransform.gameObject);
        }
    }
    
    // Manage cherry removal when eaten or destroyed
    public void DestroyCherry(GameObject cherryObject)
    {
        if (cherryObject == currentCherry)
        {
            CleanupCurrentCherry(); // 
            StartSpawnTimer(respawnDelay);
            Debug.Log("Cherry destroyed. Respawn timer started.");
        }
    }
}