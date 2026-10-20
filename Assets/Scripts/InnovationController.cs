using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InnovationController : MonoBehaviour
{
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip shotClip, hitClip;
    [SerializeField] private TMP_Text shotStatusText, targetStatusText;
    [SerializeField] private GameObject shotPanel;

    private UIManager uiManager;
    private PacStudentController pacStudent;
    private LevelGenerator levelGenerator;
    private GhostController[] targets;
    private AudioSource audioSource;
    private readonly List<PearlShot> activeShots = new List<PearlShot>();
    private readonly Dictionary<GhostController, int> ghostHits = new Dictionary<GhostController, int>();
    private readonly HashSet<GhostController> injuredGhosts = new HashSet<GhostController>();
    private readonly Dictionary<GhostController, Color> originalGhostColours = new Dictionary<GhostController, Color>();
    private float cooldownRemaining;
    private int defeatedCount;
    private bool completionRequested;

    public int DefeatedCount => defeatedCount;
    public int TotalTargets => targets == null ? 0 : targets.Length;
    public int ActiveShotCount => activeShots.Count;
    public float CooldownRemaining => cooldownRemaining;

    private void Awake()
    {
        uiManager = GetComponent<UIManager>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.5f;
    }

    private void Start()
    {
        pacStudent = FindFirstObjectByType<PacStudentController>();
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        targets = FindObjectsByType<GhostController>(FindObjectsSortMode.None);
        foreach (GhostController ghost in targets)
        {
            if (ghost != null) ghostHits[ghost] = 0;
        }
    }

    private void Update()
    {
        bool running = uiManager != null && uiManager.IsRoundRunning;
        if (running)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) TryShoot();
        }
        else if (activeShots.Count > 0)
        {
            ClearShots();
        }

        if (shotPanel != null) shotPanel.SetActive(running);
        if (shotStatusText != null)
        {
            shotStatusText.text = cooldownRemaining <= 0f
                ? "SHOT READY\n<size=15>SPACE to shoot</size>"
                : "SHOT " + cooldownRemaining.ToString("0.0") + "s\n<size=15>SPACE to shoot</size>";
        }
        if (targetStatusText != null)
        {
            targetStatusText.text = "POPPING PEARLS\n<size=26>" + defeatedCount + " / " + TotalTargets
                + "</size>\n<size=15>Two hits to pop</size>";
        }
    }

    public bool TryShoot()
    {
        if (uiManager == null || !uiManager.IsRoundRunning || cooldownRemaining > 0f || pacStudent == null)
            return false;
        if (projectileSprite == null)
        {
            Debug.LogError("InnovationController requires a projectileSprite.", this);
            return false;
        }

        GameObject shotObject = new GameObject("PearlShot");
        SpriteRenderer renderer = shotObject.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.sortingOrder = 101;
        shotObject.transform.position = pacStudent.transform.position;
        shotObject.transform.localScale = Vector3.one * 0.75f;
        PearlShot shot = shotObject.AddComponent<PearlShot>();
        shot.Initialize(this, pacStudent.FacingDirection, levelGenerator, uiManager, targets);
        RegisterShot(shot);
        cooldownRemaining = 3f;
        if (audioSource != null && shotClip != null) audioSource.PlayOneShot(shotClip);
        return true;
    }

    public void CollectPearl(int points)
    {
        if (uiManager == null || !uiManager.IsRoundRunning) return;
        cooldownRemaining = points >= 50 ? 0f : Mathf.Max(0f, cooldownRemaining - 0.25f);
    }

    public void ClearShots()
    {
        PearlShot[] shots = activeShots.ToArray();
        foreach (PearlShot shot in shots)
        {
            if (shot != null) shot.Dispose();
        }
        activeShots.Clear();
    }

    public void ResetAfterDeath()
    {
        ClearShots();
        cooldownRemaining = 0f;
        ApplyInjuryTints();
    }

    public bool HitGhost(GhostController ghost)
    {
        if (uiManager == null || !uiManager.IsRoundRunning || ghost == null || ghost.IsPermanentlyDefeated)
            return false;
        if (targets == null || System.Array.IndexOf(targets, ghost) < 0) return false;
        Collider2D collider = ghost.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled || ghost.currentState == GhostState.Dead) return false;

        int hits = GetRemainingHits(ghost);
        if (hits <= 0) return false;
        int newHitCount = ghostHits[ghost] + 1;
        ghostHits[ghost] = newHitCount;
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, ghost.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip);

        if (newHitCount == 1)
        {
            SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
            if (renderer != null && injuredGhosts.Add(ghost))
            {
                originalGhostColours[ghost] = renderer.color;
                renderer.color = new Color(renderer.color.r * 0.7f, renderer.color.g * 0.7f, renderer.color.b * 0.7f, renderer.color.a);
            }
        }
        else
        {
            defeatedCount++;
            uiManager.AddScore(300);
            ghost.DefeatPermanently();
            if (!completionRequested && defeatedCount >= TotalTargets)
            {
                completionRequested = true;
                uiManager.CompleteInnovation();
            }
        }
        return true;
    }

    public int GetRemainingHits(GhostController ghost)
    {
        if (ghost == null || ghost.IsPermanentlyDefeated) return 0;
        int hitCount;
        if (!ghostHits.TryGetValue(ghost, out hitCount)) return 0;
        return Mathf.Max(0, 2 - hitCount);
    }

    internal void RegisterShot(PearlShot shot)
    {
        if (shot != null && !activeShots.Contains(shot)) activeShots.Add(shot);
    }

    internal void UnregisterShot(PearlShot shot)
    {
        if (shot != null) activeShots.Remove(shot);
    }

    private void ApplyInjuryTints()
    {
        foreach (GhostController ghost in injuredGhosts)
        {
            if (ghost == null) continue;
            SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
            Color original;
            if (renderer != null && originalGhostColours.TryGetValue(ghost, out original))
                renderer.color = new Color(original.r * 0.7f, original.g * 0.7f, original.b * 0.7f, original.a);
        }
    }
}
