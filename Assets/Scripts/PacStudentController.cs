using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [SerializeField]
    private float moveDuration = 0.5f; 
    [SerializeField]
    private AudioClip pacMovingClip; // The sound for moving without eating a pellet
    [SerializeField]
    private AudioClip pacEatClip;
    // Movement
    private bool isMoving = false; 
    private Vector3 targetPosition; 
    private Vector3 currentPosition; 
    // Input
    private Vector3 lastInput = Vector3.zero;
    private Vector3 currentInput = Vector3.zero; 
    private Vector3 lastDirection = Vector3.right;
    
    private LevelGenerator levelGenerator; 
    private Animator animator;
    private AudioSource audioSource;
    private ParticleSystem dustParticles;
    
    void Awake()
    {
        // 在 Awake() 中取得元件，確保在 Start() 之前就可以使用
        audioSource = GetComponent<AudioSource>();
    
        // 將音效初始化邏輯移到這裡，確保比 Start() 早執行
        if (audioSource != null)
        {
            audioSource.loop = true; 
            audioSource.playOnAwake = false; 
            audioSource.clip = null;
            audioSource.volume = 0f; // 確保音量歸零
            audioSource.Stop(); 
        }
    }
    void Start()
    {
        // Find the LevelGenerator object in the scene and get its script
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        animator = GetComponent<Animator>();
        dustParticles = GetComponentInChildren<ParticleSystem>();
        if (audioSource != null && pacMovingClip != null)
        if (levelGenerator == null)
        {
            Debug.LogError("LevelGenerator not found in the scene! Movement will not work.");
        }
        if (dustParticles != null)
        {
            dustParticles.Stop(); // Ensure it is stopped at the start
        }
        transform.position = new Vector3(
            Mathf.Round(transform.position.x), 
            Mathf.Round(transform.position.y), 
            transform.position.z);
        
        currentPosition = transform.position;
        targetPosition = transform.position;
        
        Vector2Int startGrid = WorldToGrid(currentPosition);
    }
    
    void Update()
    {
        // 1. Gather player input and update lastInput
        if (Input.GetKeyDown(KeyCode.W))
        {
            lastInput = Vector3.up; 
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            lastInput = Vector3.down; 
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            lastInput = Vector3.left; 
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            lastInput = Vector3.right; 
        }
        
        Vector3 potentialTarget;
        Vector2Int gridPos;
        // 2. Wall Check and Movement Initiation 
        if (!isMoving)
        {
            Vector3 directionToTry = Vector3.zero;
            bool canMove = false; 

            // --- A. Priority 1: Check the last pressed input (lastInput) ---
            // Purpose: Try to turn in the desired direction (W, A, S, D).
            if (lastInput != Vector3.zero) 
            {
                potentialTarget = currentPosition + lastInput;
                gridPos = WorldToGrid(potentialTarget); 

                // 🛠 修正 2：修正 IsWalkable 參數錯誤 (傳入 x 和 y)
                if (levelGenerator.IsWalkable(gridPos.x, gridPos.y)) 
                {
                    currentInput = lastInput; // The new direction becomes the current direction
                    directionToTry = lastInput;
                    canMove = true;
                }
            }
            // --- B. Priority 2: If lastInput failed, try the current movement input (currentInput) ---
            // Purpose: If we hit a wall while trying to turn, try to continue moving straight.
            if (!canMove && currentInput != Vector3.zero) 
            {
                potentialTarget = currentPosition + currentInput;
                gridPos = WorldToGrid(potentialTarget); 
            
                // 🛠 修正 2：修正 IsWalkable 參數錯誤 (傳入 x 和 y)
                if (levelGenerator.IsWalkable(gridPos.x, gridPos.y)) 
                {
                    directionToTry = currentInput; 
                    canMove = true;
                }
            }
            if (!canMove) 
            {
                ResetMovementStates(); 
            }

            // --- C. Initiate Movement ---
            if (canMove)
            {
                // Calculate the target position and start the smooth movement.
                targetPosition = currentPosition + directionToTry;
                lastDirection = directionToTry;
                if (animator != null)
                {
                    // 1. 重置所有 Bool 參數為 false (確保只有一個動畫播放)
                    animator.SetBool("IsMovingRight", false);
                    animator.SetBool("IsMovingLeft", false);
                    animator.SetBool("IsMovingUp", false);
                    animator.SetBool("IsMovingDown", false);

                    // 2. 根據方向向量設定正確的 Bool 參數為 true
                    if (directionToTry == Vector3.right)
                    {
                        animator.SetBool("IsMovingRight", true);
                    }
                    else if (directionToTry == Vector3.left)
                    {
                        animator.SetBool("IsMovingLeft", true);
                    }
                    else if (directionToTry == Vector3.up)
                    {
                        // 注意：Unity 2D 的 Y 軸是正上方
                        animator.SetBool("IsMovingUp", true);
                    }
                    else if (directionToTry == Vector3.down)
                    {
                        // 注意：向下是 Vector3.down 或 (0, -1, 0)
                        animator.SetBool("IsMovingDown", true);
                    }
                }
                if (audioSource != null)
                {
                    // 只有在音源沒有播放時，才啟動。避免每一幀都重啟。
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = pacMovingClip; // 確保設置為走路音效
                        audioSource.volume = 1f;
                        audioSource.Play();               // 啟動播放
                    }
                }
                if (dustParticles != null && !dustParticles.isPlaying)
                {
                    dustParticles.Play(); // Start playing dust when movement starts
                }
                StartCoroutine(MoveToTarget());
            }
        }
    }
    
    // Purpose: Helper function to convert Unity World Position (Vector3) to Map Grid Coordinates (Vector2Int).
    // Required for LevelGenerator.IsWalkable(int x, int y).
    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        // Assuming 1 unit in world space equals 1 cell in the grid (e.g., cell at (5, 3) is at world pos (5, 3, 0))
        // We use RoundToInt to find the nearest integer grid coordinate.
        int gridX = Mathf.RoundToInt(worldPos.x);
        int gridY = Mathf.RoundToInt(-worldPos.y); 

        return new Vector2Int(gridX, gridY); 
    }
    
    // Purpose: Coroutine to handle the actual movement (Tweening/Lerping) over time.
    // This ensures frame-rate independence (Week 6 & 7 content).
    IEnumerator MoveToTarget()
    {
        isMoving = true; // Set flag: PacStudent is now moving
    
        float timeStarted = Time.time; // Record the global time movement started
        float timeSinceStart = 0f;
        float fractionOfJourney = 0f; // The 't' value for Lerp, from 0 (start) to 1 (end)

        // Loop until the fraction of journey reaches 1 (i.e., we are at the target)
        while (fractionOfJourney < 1)
        {
            // Calculate the elapsed time since the movement started
            timeSinceStart = Time.time - timeStarted;

            // Calculate the 't' value: (Time Elapsed / Total Duration). This ensures frame-rate independence.
            fractionOfJourney = timeSinceStart / moveDuration;
        
            // Ensure 't' value does not exceed 1 (though the loop condition should handle this)
            fractionOfJourney = Mathf.Min(fractionOfJourney, 1.0f); 

            // Linear Interpolation (Lerp): Move the position based on the time fraction
            // Vector3.Lerp(start, end, t) -> t should be between 0 and 1
            transform.position = Vector3.Lerp(currentPosition, targetPosition, fractionOfJourney);

            // Wait until the next frame before continuing the loop (Week 6 Coroutines)
            yield return null; 
        }

        // Movement finished: Lock the final position to the target 
        transform.position = targetPosition;
        currentPosition = targetPosition; // Update current grid position for the next move check
        isMoving = false; // Reset flag: PacStudent has stopped moving (at a grid center)
    }
    private void ResetMovementStates()
    {
        // 1. 停止動畫
        if (animator != null)
        {
            animator.SetBool("IsMovingRight", false);
            animator.SetBool("IsMovingLeft", false);
            animator.SetBool("IsMovingUp", false);
            animator.SetBool("IsMovingDown", false);
            
            animator.SetBool("IsFacingRight", false);
            animator.SetBool("IsFacingLeft", false);
            animator.SetBool("IsFacingUp", false);
            animator.SetBool("IsFacingDown", false);
            
            if (lastDirection == Vector3.right)
            {
                animator.SetBool("IsFacingRight", true);
            }
            else if (lastDirection == Vector3.left)
            {
                animator.SetBool("IsFacingLeft", true);
            }
            else if (lastDirection == Vector3.up)
            {
                animator.SetBool("IsFacingUp", true);
            }
            else if (lastDirection == Vector3.down)
            {
                animator.SetBool("IsFacingDown", true);
            }
        }

        // 2. 停止音效
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.volume = 0f;
        }

        // 3. 停止粒子特效
        if (dustParticles != null && dustParticles.isPlaying)
        {
            dustParticles.Stop();
        }
    
        // 4. 重設輸入，確保下次 Update() 會進入 if (!canMove) 區塊
        currentInput = Vector3.zero;
    }
}


