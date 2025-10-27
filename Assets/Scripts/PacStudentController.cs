    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class PacStudentController : MonoBehaviour
    {
        [SerializeField]
        private float moveDuration = 0.5f; 
        [SerializeField]
        private AudioClip pacMovingClip;
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
        
        void Start()
        { 
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
            animator = GetComponent<Animator>();
            dustParticles = GetComponentInChildren<ParticleSystem>();
            audioSource = GetComponent<AudioSource>();

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
            if (Input.GetKeyDown(KeyCode.W)) lastInput = Vector3.up;
            else if (Input.GetKeyDown(KeyCode.S)) lastInput = Vector3.down;
            else if (Input.GetKeyDown(KeyCode.A)) lastInput = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D)) lastInput = Vector3.right;
            
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
                        // 1. 設定 Blend Tree 參數 (MoveX 和 MoveY)
                        // 這將控制移動動畫的方向，並作為靜止時面向的方向
                        animator.SetFloat("MoveX", directionToTry.x);
                        animator.SetFloat("MoveY", directionToTry.y);

                        // 2. 啟動移動 Blend Tree 狀態
                        animator.SetBool("IsMoving", true);
                    }
                    if (audioSource != null)
                    {
                        // 只有在音源沒有播放時，才啟動。避免每一幀都重啟。
                        if (!audioSource.isPlaying)
                        {
                            audioSource.clip = pacMovingClip;
                            audioSource.volume = 1f;
                            audioSource.Play();            
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
                // 確保 Idle Blend Tree 使用最後的方向（lastDirection）來顯示靜止圖片
                // 這是將 lastDirection 儲存為靜止時的方向，供 Idle Blend Tree 使用
                animator.SetFloat("MoveX", lastDirection.x);
                animator.SetFloat("MoveY", lastDirection.y);
            
                // 關閉 IsMoving，讓 Animator 轉換到 Idle Blend Tree (即靜止圖片)
                animator.SetBool("IsMoving", false);
            }

            // 2. 停止音效
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
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


