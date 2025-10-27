    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class PacStudentController : MonoBehaviour
    {
        [SerializeField]
        private float moveDuration = 0.3f; 
        [SerializeField]
        private AudioClip pacMovingClip;
        [SerializeField]
        private AudioClip pacEatClip;
        [SerializeField] 
        private AudioClip wallCollisionClip;
        [SerializeField] 
        private GameObject wallParticlePrefab;
        // Movement
        private bool isMoving = false; 
        private Vector3 targetPosition; 
        private Vector3 currentPosition; 
        // Input
        private Vector3 lastInput = Vector3.zero;
        private Vector3 currentInput = Vector3.zero; 
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
        }

        void Update()
        {
            // Gather player input and update lastInput
            if (Input.GetKeyDown(KeyCode.W)) lastInput = Vector3.up;
            else if (Input.GetKeyDown(KeyCode.S)) lastInput = Vector3.down;
            else if (Input.GetKeyDown(KeyCode.A)) lastInput = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D)) lastInput = Vector3.right;

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
                Vector3 directionToTry = Vector3.zero;

                if (lastInput != Vector3.zero)
                    directionToTry = lastInput;
                else if (currentInput != Vector3.zero)
                    directionToTry = currentInput;

                if (directionToTry != Vector3.zero)
                {
                    if (CanMoveInDirection(directionToTry))
                    {
                        currentInput = directionToTry;
                        InitiateMovement(directionToTry);
                    }
                    else
                    {
                        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
                        {
                            if (!wallHitCooldown)
                            {
                                PlayWallCollisionEffect();
                                wallHitCooldown = true;
                            }
                        }
                        ResetMovementStates();
                    }
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
            float timeStarted = Time.time;
        
            while (true)
            {
                if (!isMoving)
                {
                    yield break;
                }
                
                float timeSinceStart = Time.time - timeStarted;
                float fractionOfJourney = Mathf.Min(timeSinceStart / moveDuration, 1.0f);
            
                transform.position = Vector3.Lerp(currentPosition, targetPosition, fractionOfJourney);

                if (fractionOfJourney >= 1.0f) break;
            
                yield return null; 
            }
            
            transform.position = targetPosition;
            currentPosition = targetPosition;
            isMoving = false;
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
        
            // Reset input
            currentInput = Vector3.zero;
        }
        private void PlayWallCollisionEffect()
        {
            if (audioSource != null && wallCollisionClip != null)
            {
                if (audioSource != null && wallCollisionClip != null)
                {
                    if (audioSource.isPlaying && !isPlayingWallSound)
                    {
                        audioSource.Stop();
                    }

                    audioSource.PlayOneShot(wallCollisionClip, 2.0f); 
        
                    isPlayingWallSound = true;
                    
                    Invoke("ResetWallSoundFlag", wallCollisionClip.length);
                }
            }
            
            if (wallParticlePrefab != null)
            {
                Instantiate(wallParticlePrefab, transform.position, Quaternion.identity);
            }
        }
        private void ResetWallSoundFlag()
        {
            isPlayingWallSound = false;
        }
    }


