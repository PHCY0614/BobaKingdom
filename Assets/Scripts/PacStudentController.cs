using System.Collections;
using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private AudioClip pacMovingClip, pacEatClip, wallCollisionClip, pacDeathClip;
    [SerializeField] private GameObject wallParticlePrefab, deathParticlePrefab;
    private Vector3 lastInput, currentInput, facing = Vector3.right;
    private Vector3 currentPosition, targetPosition, startPosition;
    private bool isMoving, isDead, awaitingInput = true, wallContact;
    private LevelGenerator levelGenerator;
    private UIManager uiManager;
    private Animator animator;
    private AudioSource audioSource;
    private ParticleSystem dustParticles;
    private Coroutine movementRoutine;
    public float MoveSpeed => 1f / Mathf.Max(0.01f, moveDuration);
    public Vector3 FacingDirection => facing;

    void Awake()
    {
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        uiManager = FindFirstObjectByType<UIManager>();
        animator = GetComponent<Animator>(); audioSource = GetComponent<AudioSource>();
        dustParticles = GetComponentInChildren<ParticleSystem>();
        startPosition = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), transform.position.z);
        currentPosition = targetPosition = transform.position = startPosition;
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }
    void Start()
    {
        if (levelGenerator == null || uiManager == null)
        {
            Debug.LogError("PacStudent needs a LevelGenerator and UIManager.", this); enabled = false; return;
        }
        ResetPosition();
    }
    void Update()
    {
        if (isDead || !uiManager.IsRoundRunning) return;
        if (Input.GetKeyDown(KeyCode.W)) SetInput(Vector3.up);
        else if (Input.GetKeyDown(KeyCode.S)) SetInput(Vector3.down);
        else if (Input.GetKeyDown(KeyCode.A)) SetInput(Vector3.left);
        else if (Input.GetKeyDown(KeyCode.D)) SetInput(Vector3.right);
        if (isMoving || awaitingInput) return;
        Vector3 direction = CanMove(lastInput) ? lastInput : CanMove(currentInput) ? currentInput : Vector3.zero;
        if (direction == Vector3.zero)
        {
            StopEffects();
            if (!wallContact && (lastInput != Vector3.zero || currentInput != Vector3.zero))
            {
                wallContact = true; PlayWallEffect();
            }
            return;
        }
        currentInput = facing = direction; wallContact = false;
        targetPosition = currentPosition + direction;
        movementRoutine = StartCoroutine(MoveToTarget());
    }
    public void SetInput(Vector3 direction)
    {
        // Input also has a single entry point for deterministic movement checks.
        if (isDead || uiManager == null || !uiManager.IsRoundRunning) return;
        if (direction != Vector3.up && direction != Vector3.down && direction != Vector3.left && direction != Vector3.right) return;
        lastInput = direction; awaitingInput = false; wallContact = false;
    }
    private bool CanMove(Vector3 direction)
    {
        if (direction == Vector3.zero) return false;
        Vector2Int cell = levelGenerator.WorldToGrid(currentPosition + direction);
        return levelGenerator.IsWalkable(cell.x, cell.y) && !levelGenerator.IsGhostSpawn(cell);
    }
    private IEnumerator MoveToTarget()
    {
        isMoving = true; UpdateAnimation(true);
        PlayMovementAudio();
        if (dustParticles != null) dustParticles.Play();
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            if (uiManager.IsRoundRunning) elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(currentPosition, targetPosition, Mathf.Clamp01(elapsed / moveDuration));
            yield return null;
        }
        currentPosition = transform.position = targetPosition;
        var cell = levelGenerator.WorldToGrid(currentPosition);
        if (cell.y == levelGenerator.Rows / 2 && (cell.x == 0 || cell.x == levelGenerator.Columns - 1))
        {
            cell.x = cell.x == 0 ? levelGenerator.Columns - 1 : 0;
            currentPosition = targetPosition = transform.position = levelGenerator.GridToWorld(cell);
        }
        isMoving = false; movementRoutine = null;
    }
    private void UpdateAnimation(bool moving)
    {
        if (animator == null) return;
        animator.SetFloat("MoveX", facing.x); animator.SetFloat("MoveY", facing.y); animator.SetBool("IsMoving", moving);
    }
    private void PlayMovementAudio()
    {
        if (audioSource == null) return;
        bool pelletAhead = false;
        foreach (var hit in Physics2D.OverlapPointAll(targetPosition))
            if (hit.GetComponent<PelletController>() != null || hit.GetComponent<PowerPillController>() != null) { pelletAhead = true; break; }
        AudioClip clip = pelletAhead ? pacEatClip : pacMovingClip;
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        audioSource.clip = clip; audioSource.loop = true;
        if (clip != null) audioSource.Play();
    }
    private void StopEffects()
    {
        UpdateAnimation(false);
        if (audioSource != null && audioSource.loop) { audioSource.Stop(); audioSource.loop = false; }
        if (dustParticles != null) dustParticles.Stop();
    }
    public void StopMovement()
    {
        if (movementRoutine != null) StopCoroutine(movementRoutine);
        movementRoutine = null; isMoving = false; StopEffects();
    }
    private void PlayWallEffect()
    {
        if (audioSource != null && wallCollisionClip != null) audioSource.PlayOneShot(wallCollisionClip);
        if (wallParticlePrefab != null)
        {
            var effect = Instantiate(wallParticlePrefab, transform.position + facing * 0.5f, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || uiManager == null || !uiManager.IsRoundRunning) return;
        var ghost = other.GetComponent<GhostController>();
        if (ghost == null || ghost.currentState == GhostState.Dead) return;
        if (ghost.currentState == GhostState.Normal)
        {
            isDead = true; StopMovement();
            var collider = GetComponent<Collider2D>(); if (collider != null) collider.enabled = false;
            uiManager.LoseLife();
        }
        else
        {
            uiManager.AddScore(300); ghost.HandleEaten(uiManager);
            if (audioSource != null && pacEatClip != null) audioSource.PlayOneShot(pacEatClip);
        }
    }
    public void ResetPosition()
    {
        StopMovement();
        currentPosition = targetPosition = transform.position = startPosition;
        isDead = false; awaitingInput = true; wallContact = false; currentInput = Vector3.zero; facing = Vector3.right;
        var collider = GetComponent<Collider2D>(); if (collider != null) collider.enabled = true;
        if (animator != null)
        {
            animator.Rebind(); animator.Update(0f); animator.ResetTrigger("Death"); UpdateAnimation(false);
        }
    }
    public void PlayDeathEffect()
    {
        StopMovement();
        if (audioSource != null && pacDeathClip != null) audioSource.PlayOneShot(pacDeathClip);
        if (animator != null) animator.SetTrigger("Death");
    }
    public void PlayDeathParticle()
    {
        if (deathParticlePrefab == null) return;
        var effect = Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
        foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = particles.main; main.useUnscaledTime = true;
        }
        StartCoroutine(RemoveDeathParticle(effect));
    }
    private IEnumerator RemoveDeathParticle(GameObject effect)
    {
        yield return new WaitForSecondsRealtime(3f);
        if (effect != null) Destroy(effect);
    }
}
