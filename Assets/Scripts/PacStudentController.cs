    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class PacStudentController : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.2f; 
        [SerializeField] private AudioClip pacMovingClip;
        [SerializeField] private AudioClip pacEatClip;
        [SerializeField] private AudioClip wallCollisionClip;
        [SerializeField] private GameObject wallParticlePrefab;
        [SerializeField] private GameObject deathParticlePrefab;
        // Movement
        private bool isMoving = false; 
        private Vector3 targetPosition; 
        private Vector3 currentPosition; 
        private Vector3 nextInput = Vector3.zero;
        private Vector3 currentDirection = Vector3.zero;
        private Vector3 lastDirection = Vector3.right;
        
        // Component references
        private LevelGenerator levelGenerator; 
        private Animator animator;
        private AudioSource audioSource;
        private ParticleSystem dustParticles;
        
        // Hit wall
        private bool wallHitCooldown = false;
        private float wallHitCooldownTime = 0.3f; 
        private float wallHitTimer = 0f;
        private bool isPlayingWallSound = false;
        
        // Transporter
        private bool teleporterCooldown = false;
        private const float teleporterCooldownTime = 0.1f;
        
        // Game State
        private Vector3 startPosition; // 儲存 PacStudent 的起始位置
        private UIManager uiManager;
        [SerializeField] private AudioClip pacDeathClip; 
        private bool isDead = false;
        
        void Start()
        {    

            levelGenerator = FindFirstObjectByType<LevelGenerator>();
            animator = GetComponent<Animator>();
            dustParticles = GetComponentInChildren<ParticleSystem>();
            audioSource = GetComponent<AudioSource>();
            
            // Snap to grid position
            transform.position = new Vector3(
                Mathf.Round(transform.position.x), 
                Mathf.Round(transform.position.y), 
                transform.position.z);
            
            currentPosition = transform.position;
            targetPosition = transform.position;
            
            if (dustParticles != null)
            {
                dustParticles.Stop();
            }

            // UIManager 引用 (新增)
            uiManager = UIManager.Instance;
            if (uiManager == null) Debug.LogError("UIManager Instance not found!");    
            // 儲存起始位置 (新增)
            startPosition = transform.position;
            currentPosition = startPosition;
            targetPosition = startPosition;
        }

        void Update()
        {
            if (isDead) return;
            
            // Gather player input and update lastInput
            if (Input.GetKeyDown(KeyCode.W)) nextInput = Vector3.up;
            else if (Input.GetKeyDown(KeyCode.S)) nextInput = Vector3.down;
            else if (Input.GetKeyDown(KeyCode.A)) nextInput = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D)) nextInput = Vector3.right;
            
            if (wallHitCooldown)
            {
                wallHitTimer += Time.deltaTime;
                if (wallHitTimer >= wallHitCooldownTime)
                {
                    wallHitCooldown = false;
                    wallHitTimer = 0f;
                }
            }
            
            // Only process movement when not currently moving
            if (!isMoving)
            {
                Vector3 chosenDir = Vector3.zero;
                
                // Check new input
                if (nextInput != Vector3.zero && CanMoveInDirection(nextInput))
                {
                    currentDirection = nextInput;
                    chosenDir = nextInput;
                    nextInput = Vector3.zero; 
                }
                
                else if (nextInput != Vector3.zero && !CanMoveInDirection(nextInput))
                {
                    if (!wallHitCooldown)
                    {
                        PlayWallCollisionEffect();
                        wallHitCooldown = true;
                    }
                    nextInput = Vector3.zero; // 清除無效輸入
                }
                // 繼續當前方向
                else if (currentDirection != Vector3.zero && CanMoveInDirection(currentDirection))
                {
                    chosenDir = currentDirection;
                }
                else
                {
                    // 無法移動，停下
                    ResetMovementStates();
                    return;
                }
            
                if (chosenDir != Vector3.zero)
                {
                    InitiateMovement(chosenDir);
                }
            }
        }

        // Check if movement is possible in the given direction
        private bool CanMoveInDirection(Vector3 direction)
        {
            Vector3 potentialTarget = currentPosition + direction;
            Vector2Int gridPos = WorldToGrid(potentialTarget);
            return levelGenerator.IsWalkable(gridPos.x, gridPos.y);
        }
        
        // Start movement in the specified direction
        private void InitiateMovement(Vector3 direction)
        {
            targetPosition = currentPosition + direction;
            lastDirection = direction;
        
            // Update animation
            if (animator != null)
            {
                animator.SetFloat("MoveX", direction.x);
                animator.SetFloat("MoveY", direction.y);
                animator.SetBool("IsMoving", true);
            }
        
            // Play movement audio
            if (audioSource != null && !audioSource.isPlaying && pacMovingClip != null)
            {
                audioSource.clip = pacMovingClip;
                audioSource.volume = 1f;
                audioSource.loop = true;
                audioSource.Play();            
            }
        
            // Start dust particle effect
            if (dustParticles != null && !dustParticles.isPlaying)
            {
                dustParticles.Play();
            }
        
            StartCoroutine(MoveToTarget());
        }
        
        // Convert world position to grid coordinates
        private Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int gridX = Mathf.RoundToInt(worldPos.x);
            int gridY = Mathf.RoundToInt(-worldPos.y); 
            return new Vector2Int(gridX, gridY); 
        }
        
        // Movement toward the target from current position
        IEnumerator MoveToTarget()
        {
            isMoving = true; 
            float elapsed = 0f;
        
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
        
                transform.position = Vector3.Lerp(currentPosition, targetPosition, t);
                yield return null; 
            }
            
            transform.position = targetPosition;
            currentPosition = targetPosition;
            isMoving = false;
            
            if (nextInput != Vector3.zero && CanMoveInDirection(nextInput))
            {
                currentDirection = nextInput;
                nextInput = Vector3.zero;
                InitiateMovement(currentDirection);
            }
            else if (currentDirection != Vector3.zero && CanMoveInDirection(currentDirection))
            {
                InitiateMovement(currentDirection);
            }
            else
            {
                currentDirection = Vector3.zero;
                ResetMovementStates();
            }
        }
        
        // Stop all movement effects when blocked
        private void ResetMovementStates()
        {
            // Update animation to idle state
            if (animator != null)
            {
                animator.SetFloat("MoveX", lastDirection.x);
                animator.SetFloat("MoveY", lastDirection.y);
                animator.SetBool("IsMoving", false);
            }

            // Stop audio
            if (audioSource != null && audioSource.isPlaying && !isPlayingWallSound)
            {
                audioSource.Stop();
            }

            // Stop particle effects
            if (dustParticles != null && dustParticles.isPlaying)
            {
                dustParticles.Stop();
            }
        }
        private void PlayWallCollisionEffect()
        {
            if (audioSource != null && wallCollisionClip != null)
            {
                if (audioSource.isPlaying && !isPlayingWallSound)
                {
                    audioSource.Stop();
                }

                audioSource.PlayOneShot(wallCollisionClip, 2.0f); 
                isPlayingWallSound = true;
                
                StartCoroutine(ResetWallSoundFlagAfterDelay(wallCollisionClip.length));
            }
            
            if (wallParticlePrefab != null)
            {
                Instantiate(wallParticlePrefab, transform.position, Quaternion.identity);
            }
        }
        private IEnumerator ResetWallSoundFlagAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            isPlayingWallSound = false;
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Teleporter") && !teleporterCooldown)
            {
                GameObject targetTeleporter = null;
    
                if (other.name == "TeleporterLeft")
                {
                    targetTeleporter = GameObject.Find("TeleporterRight");
                }
                else if (other.name == "TeleporterRight")
                {
                    targetTeleporter = GameObject.Find("TeleporterLeft");
                }
            
                if (targetTeleporter != null)
                {
                    Vector3 newPos = targetTeleporter.transform.position;
                    newPos.z = transform.position.z;

                    transform.position = newPos;
                    currentPosition = newPos;
                    targetPosition = newPos;
                
                    StartCoroutine(ResetTeleporterCooldownAfterDelay());
                }
            }
            else if (other.CompareTag("Pellet"))
            {
                if (audioSource != null && pacEatClip != null)
                {
                    audioSource.PlayOneShot(pacEatClip, 0.5f);
                }
            }
            else if (other.CompareTag("Ghost"))
            {
                // 取得幽靈控制器
                GhostController ghost = other.GetComponent<GhostController>();

                if (ghost != null)
                {
                    // 檢查幽靈的狀態 - 修正點：移除 GhostController.
                    if (ghost.currentState == GhostState.Normal)
                    {
                        // 碰到 Normal 幽靈：PacStudent 死亡
                        HandleDeath();
                    }
                    else if (ghost.currentState == GhostState.Scared || ghost.currentState == GhostState.Recovering)
                    {
                        // 碰到 Scared/Recovering 幽靈：吃掉幽靈 (稍後實作)
                        Debug.Log("PacStudent ate the ghost! (Logic TBD)");
                        // TODO: 實作吃幽靈邏輯 (Score, Ghost Respawn)
                    }
                }
            }
        }
        private IEnumerator ResetTeleporterCooldownAfterDelay()
        {
            teleporterCooldown = true;
            yield return new WaitForSeconds(teleporterCooldownTime);
            teleporterCooldown = false;
        }

        public void ResetPosition()
        {
            // 重置位置
            transform.position = startPosition;
            currentPosition = startPosition;
            targetPosition = startPosition;

            // 重置移動狀態
            isMoving = false;
            currentDirection = Vector3.zero;
            lastDirection = Vector3.right;
            nextInput = Vector3.zero;

            // 重置死亡狀態
            isDead = false;

            // 重置動畫狀態為 Idle
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                // 確保死亡 Trigger 被清空
                animator.ResetTrigger("Death"); 
            }
    
            // 停止所有音效
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
    
            // 啟用 Collider (防止在死亡動畫中被重複判定)
            Collider2D myCollider = GetComponent<Collider2D>();
            if (myCollider != null)
            {
                myCollider.enabled = true;
            }

            Debug.Log("PacStudent Reset to Start Position.");
        }
        private void HandleDeath()
        {
            if (isDead) return; // 避免重複死亡
            isDead = true;
    
            // 1. 立即停止所有 PacStudent 的移動協程
            StopAllCoroutines(); 
            isMoving = false;
            
            // 禁用 Collider 和移動
            Collider2D myCollider = GetComponent<Collider2D>();
            // 禁用輸入和移動邏輯
            if (myCollider != null)
            {
                myCollider.enabled = false;
            }
            // 播放死亡音效
            if (audioSource != null && pacDeathClip != null)
            {
                audioSource.PlayOneShot(pacDeathClip);
            }
    
            // 扣除生命值並觸發重置流程
            if (uiManager != null)
            {
                uiManager.LoseLife();
            }
        }
        public void PlayDeathEffect()
        {
            // 播放死亡音效 
            if (audioSource != null && pacDeathClip != null)
            {
                // 如果移動音效還在播放，先停止
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                audioSource.PlayOneShot(pacDeathClip);
            }

            // 1. 播放死亡動畫 (使用之前在 Animator 中設定的 Trigger)
            if (animator != null) 
            {
                // "Death" 必須與你在 Animator 中設定的 Trigger 名稱完全一致
                animator.SetTrigger("Death"); 
                // 確保 PacStudent 的動畫狀態機立即停止移動動畫
                animator.SetBool("IsMoving", false); 
            }

            /*// 2. 產生死亡粒子效果
            if (deathParticlePrefab != null)
            {
                // 在 PacStudent 的位置產生粒子效果
                // 因為 Time.timeScale=0，所以這個 Instantiation 必須立即發生。
                // 粒子系統的 StopAction 設為 Destroy 才能在播放結束後移除自身。
                Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
            }*/
        }

        public void PlayDeathParticle()
        {
            // deathParticlePrefab 在 PacStudentController 內部，所以可以存取
            if (deathParticlePrefab != null)
            {
                // 在 PacStudent 的位置產生粒子效果
                Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
            }
        }
    }


           