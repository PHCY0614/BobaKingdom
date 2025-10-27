using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CherryController : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("櫻桃被摧毀後，等待多久才重新生成 (秒)")]
    public float respawnDelay = 5f;
    [Tooltip("櫻桃 prefab")]
    public GameObject cherryPrefab;
    [Tooltip("關卡中心點 (用於計算移動路徑)")]
    public Vector3 levelCenter = new Vector3(13f, -17f, 0f); 
    [Tooltip("關卡邊界 (用於生成位置)")]
    public float levelBounds = 35f; // 假設關卡寬度約 18-20 單位

    [Header("Movement")]
    [Tooltip("櫻桃穿越螢幕所需的總時間 (秒)")]
    public float moveDuration = 4f; 

    // 私有成員變數
    private static CherryController instance; 
    private Coroutine spawnCoroutine;
    // 當前實例化的櫻桃物件
    private GameObject currentCherry;
    // --- Unity Life Cycle ---

    void Awake()
    {
        // 確保場景中只有一個 CherryController 實例（Singleton 模式）
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
        // 遊戲開始 5 秒後，第一次生成櫻桃
        StartSpawnTimer(respawnDelay);
    }
    
    // --- Public Spawning API ---

    // 呼叫這個方法來啟動計時器，等待櫻桃重新生成
    public static void StartRespawnTimer()
    {
        if (instance != null)
        {
            // 如果當前的櫻桃還存在（這通常不應該發生，但作為保護）
            if (instance.currentCherry != null)
            {
                Destroy(instance.currentCherry);
            }
            instance.StartSpawnTimer(instance.respawnDelay);
        }
    }

    // --- Private Methods ---

    // 啟動一個計時器來生成櫻桃
    private void StartSpawnTimer(float delay)
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnCherryRoutine(delay));
    }

    // 協程：等待 delay 時間後生成櫻桃
    IEnumerator SpawnCherryRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 1. 隨機生成起點和終點
        Vector3 startPosition;
        Vector3 endPosition;
        
        // 隨機選擇生成在哪個軸（X 或 Y）的邊界外
        bool spawnOnXAxis = Random.value > 0.5f; 

        if (spawnOnXAxis)
        {
            // 隨機從左或右生成
            float startX = Random.value > 0.5f ? levelBounds : -levelBounds;
            float startY = Random.Range(-levelBounds, levelBounds); // Y 軸隨機
            startPosition = new Vector3(startX, startY, 0f);
            
            // 確保終點在另一側
            float endX = startX > 0 ? -levelBounds : levelBounds;
            // 終點 Y 座標必須經過中心點，所以計算與中心點的直線反射
            float endY = 2 * levelCenter.y - startY;
            endPosition = new Vector3(endX, endY, 0f);
        }
        else
        {
            // 隨機從上或下生成
            float startY = Random.value > 0.5f ? levelBounds : -levelBounds;
            float startX = Random.Range(-levelBounds, levelBounds); // X 軸隨機
            startPosition = new Vector3(startX, startY, 0f);
            
            // 確保終點在另一側
            float endY = startY > 0 ? -levelBounds : levelBounds;
            // 終點 X 座標必須經過中心點
            float endX = 2 * levelCenter.x - startX;
            
            // 🚨 關鍵修正：將 Vector3(endX, endX, 0f) 改為 Vector3(endX, endY, 0f)
            endPosition = new Vector3(endX, endY, 0f);
        }
        
        // 2. 實例化櫻桃並啟動移動
        currentCherry = Instantiate(cherryPrefab, startPosition, Quaternion.identity);
        currentCherry.transform.SetParent(transform); 
        
        // 確保櫻桃繪製在最上層
        SpriteRenderer sr = currentCherry.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 100; // 確保在其他所有 Sprite 上方
        }
        // 3. 啟動移動協程
        StartCoroutine(MoveCherryRoutine(currentCherry.transform, startPosition, endPosition, moveDuration));
        
        Debug.Log($"Cherry spawned at {startPosition} and moving to {endPosition}");
    }
    
    IEnumerator MoveCherryRoutine(Transform cherryTransform, Vector3 startPos, Vector3 endPos, float duration)
    {
        float timeStarted = Time.time;
        float fractionOfJourney = 0f;

        while (fractionOfJourney < 1)
        {
            float timeSinceStart = Time.time - timeStarted;
            fractionOfJourney = timeSinceStart / duration;
            fractionOfJourney = Mathf.Min(fractionOfJourney, 1.0f); 

            // 線性插值 (Lerp) 移動
            cherryTransform.position = Vector3.Lerp(startPos, endPos, fractionOfJourney);

            yield return null;
        }

        // 櫻桃到達終點 (Level 外)
        cherryTransform.position = endPos;
        
        // 銷毀櫻桃並啟動下一次生成計時器
        DestroyCherry(cherryTransform.gameObject);
    }
    
    // 處理櫻桃被銷毀或被吃掉的邏輯
    public void DestroyCherry(GameObject cherryObject)
    {
        // 檢查是否是當前管理的櫻桃
        if (cherryObject == currentCherry)
        {
            // 銷毀自身
            Destroy(cherryObject);
            currentCherry = null;
            
            // 啟動下一輪生成計時器
            StartSpawnTimer(respawnDelay);
            Debug.Log("Cherry destroyed. Respawn timer started.");
        }
    }
}