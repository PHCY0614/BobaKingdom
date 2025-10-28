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
        private Vector3 startPosition; 
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

            // UIManager 
            uiManager = FindFirstObjectByType<UIManager>();
            // Store original position
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
                    nextInput = Vector3.zero; 
                }
                // keep current direction
                else if (currentDirection != Vector3.zero && CanMoveInDirection(currentDirection))
                {
                    chosenDir = currentDirection;
                }
                else
                {
                    // cant move then stop
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
                GhostController ghost = other.GetComponent<GhostController>();

                if (ghost != null)
                {
                    if (ghost.currentState == GhostState.Normal)
                    {
                        // Touch normal ghost：PacStudent dead
                        HandleDeath();
                    }
                    else if (ghost.currentState == GhostState.Scared || ghost.currentState == GhostState.Recovering)
                    {
                        // touch Scared/Recovering ghost: eat ghost (TBC)
                        Debug.Log("PacStudent ate the ghost! (Logic TBC)");
                        HandleGhostEaten(ghost);
                    }
                }
            }
        }
        private void HandleGhostEaten(GhostController ghost)
        {
            if (ghost.currentState == GhostState.Dead) return;
            
            // 2. PacStudentController 
            if (uiManager != null)
            {
                const int GHOST_EAT_SCORE = 300;
                uiManager.AddScore(GHOST_EAT_SCORE);
            }
            
            // 3. Eat sound
            if (audioSource != null && pacEatClip != null)
            {
                audioSource.PlayOneShot(pacEatClip, 0.5f);
            }

            // 4. bgm to Dead 
            if (uiManager != null && uiManager.AudioController != null) 
            {
                uiManager.AudioController.PlayDeadMusic();
            }
    
            // 5. ghost handle its own death
            ghost.HandleEaten(uiManager);
        }
        private IEnumerator ResetTeleporterCooldownAfterDelay()
        {
            teleporterCooldown = true;
            yield return new WaitForSeconds(teleporterCooldownTime);
            teleporterCooldown = false;
        }

        public void ResetPosition()
        {
            // reset position
            transform.position = startPosition;
            currentPosition = startPosition;
            targetPosition = startPosition;

            // reset moving state
            isMoving = false;
            currentDirection = Vector3.zero;
            lastDirection = Vector3.right;
            nextInput = Vector3.zero;

            // reset death state
            isDead = false;

            // rest to Idle
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                // make sure Trigger is reset when dead
                animator.ResetTrigger("Death"); 
            }
    
            // stop all audio
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            Collider2D myCollider = GetComponent<Collider2D>();
            if (myCollider != null)
            {
                myCollider.enabled = true;
            }

            Debug.Log("PacStudent Reset to Start Position.");
        }
        private void HandleDeath()
        {
            if (isDead) return; 
            isDead = true;
    
            // 1. stop PacStudent moving
            StopAllCoroutines(); 
            isMoving = false;
            
            // stop Collider and moving
            Collider2D myCollider = GetComponent<Collider2D>();
            if (myCollider != null)
            {
                myCollider.enabled = false;
            }
            // play dead audio
            if (audioSource != null && pacDeathClip != null)
            {
                audioSource.PlayOneShot(pacDeathClip);
            }
    
            // deduct life
            if (uiManager != null)
            {
                uiManager.LoseLife();
            }
        }
        public void PlayDeathEffect()
        {
            // play dead audio
            if (audioSource != null && pacDeathClip != null)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                audioSource.PlayOneShot(pacDeathClip);
            }

            // 1. play dead animator
            if (animator != null) 
            {
                animator.SetTrigger("Death"); 
                animator.SetBool("IsMoving", false); 
            }
        }

        public void PlayDeathParticle()
        {
            if (deathParticlePrefab != null)
            {
                Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
            }
        }
    }


           