using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InnovationController : MonoBehaviour
{
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Material goldenPearlMaterial;
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
    private readonly Dictionary<GhostController, int> ghostDefeats = new Dictionary<GhostController, int>();
    private readonly Dictionary<GhostController, float> respawnTimers = new Dictionary<GhostController, float>();
    private readonly Dictionary<GhostController, Color> originalGhostColours = new Dictionary<GhostController, Color>();
    private float cooldownRemaining, goldenRushRemaining;
    private int shotCharges = 1;
    private Color shotTextColour;

    public int TotalTargets => targets == null ? 0 : targets.Length;
    public int ActiveShotCount => activeShots.Count;
    public float CooldownRemaining => cooldownRemaining;
    public int ShotCharges => Mathf.Min(shotCharges, ShotCapacity);
    public int ShotCapacity => IsGoldenRushActive ? 2 : 1;
    public float GoldenRushRemaining => uiManager != null && uiManager.IsRoundRunning ? goldenRushRemaining : 0f;
    public bool IsGoldenRushActive => GoldenRushRemaining > 0f;

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
        if (shotStatusText != null) shotTextColour = shotStatusText.color;
        pacStudent = FindFirstObjectByType<PacStudentController>();
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        targets = FindObjectsByType<GhostController>(FindObjectsSortMode.None);
        foreach (GhostController ghost in targets)
        {
            if (ghost == null) continue;
            ghostHits[ghost] = 0;
            ghost.SetHealth(2);
        }
    }

    private void Update()
    {
        bool running = uiManager != null && uiManager.IsRoundRunning;
        if (running)
        {
            UpdateRespawns(Time.deltaTime);
            UpdateShotCharge(Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) TryShoot();
        }
        else
        {
            goldenRushRemaining = 0f;
            shotCharges = Mathf.Min(shotCharges, 1);
            if (activeShots.Count > 0) ClearShots();
        }

        if (shotPanel != null) shotPanel.SetActive(running);
        if (shotStatusText != null)
        {
            shotStatusText.color = IsGoldenRushActive ? new Color32(141, 81, 0, 255) : shotTextColour;
            string charges = "SHOTS " + ShotCharges + " / " + ShotCapacity;
            string recharge = ShotCharges < ShotCapacity ? "RECHARGE " + cooldownRemaining.ToString("0.0") + "s" : "READY";
            shotStatusText.text = IsGoldenRushActive
                ? "GOLDEN RUSH\n<size=12>" + goldenRushRemaining.ToString("0.0") + "s | " + charges
                    + "\nSPACE | " + (ShotCharges < ShotCapacity ? "+1: " + cooldownRemaining.ToString("0.0") + "s" : "READY") + "</size>"
                : charges + "\n<size=12>" + recharge + "\nSPACE to shoot</size>";
        }
        if (targetStatusText != null)
        {
            float nextRespawn = 0f;
            foreach (float remaining in respawnTimers.Values)
                if (nextRespawn == 0f || remaining < nextRespawn) nextRespawn = remaining;
            targetStatusText.text = "PEARLS LEFT\n<size=26>" + uiManager.RemainingPellets
                + "</size>\n<size=15>Two hits to pop</size>\n<size=12>"
                + (nextRespawn > 0f ? "NEXT RESPAWN " + Mathf.CeilToInt(nextRespawn) + "s" : "Respawn: 10s") + "</size>";
        }
    }

    public float GetRespawnRemaining(GhostController ghost)
    {
        float remaining;
        return ghost != null && respawnTimers.TryGetValue(ghost, out remaining) ? remaining : 0f;
    }

    private void UpdateRespawns(float seconds)
    {
        if (uiManager == null || !uiManager.IsRoundRunning || targets == null) return;
        foreach (GhostController ghost in targets)
        {
            float remaining;
            if (ghost == null || !respawnTimers.TryGetValue(ghost, out remaining)) continue;
            remaining = Mathf.Max(0f, remaining - seconds);
            if (remaining > 0f)
            {
                respawnTimers[ghost] = remaining;
                continue;
            }

            respawnTimers.Remove(ghost);
            ghostHits[ghost] = 0;
            injuredGhosts.Remove(ghost);
            ghost.ReviveAtSpawn();
            SpriteRenderer renderer = ghost.GetComponent<SpriteRenderer>();
            Color original;
            if (renderer != null && originalGhostColours.TryGetValue(ghost, out original)) renderer.color = original;
            originalGhostColours.Remove(ghost);
            ghost.SetHealth(2);
        }
    }

    private void UpdateShotCharge(float seconds)
    {
        goldenRushRemaining = Mathf.Max(0f, goldenRushRemaining - seconds);
        shotCharges = Mathf.Min(shotCharges, ShotCapacity);
        if (shotCharges >= ShotCapacity)
        {
            cooldownRemaining = 0f;
            return;
        }

        cooldownRemaining -= seconds;
        while (cooldownRemaining <= 0f && shotCharges < ShotCapacity)
        {
            shotCharges++;
            cooldownRemaining += 3f;
        }
        if (shotCharges >= ShotCapacity) cooldownRemaining = 0f;
    }

    public bool TryShoot()
    {
        if (uiManager == null || !uiManager.IsRoundRunning || ShotCharges <= 0 || pacStudent == null)
            return false;
        if (projectileSprite == null || goldenPearlMaterial == null)
        {
            Debug.LogError("InnovationController requires a projectile sprite and golden material.", this);
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
        shot.ConfigureAppearance(goldenPearlMaterial, IsGoldenRushActive);
        RegisterShot(shot);
        shotCharges--;
        if (cooldownRemaining <= 0f) cooldownRemaining = 3f;
        if (audioSource != null && shotClip != null) audioSource.PlayOneShot(shotClip);
        return true;
    }

    public void CollectPearl(int points)
    {
        if (uiManager == null || !uiManager.IsRoundRunning) return;
        if (points >= 50)
        {
            goldenRushRemaining = 6f;
            shotCharges = 2;
            cooldownRemaining = 0f;
            if (pacStudent != null) PearlShot.PlaySparkles(transform, pacStudent.transform.position, goldenPearlMaterial, true);
        }
        else
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - 0.25f);
            UpdateShotCharge(0f);
        }
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
        goldenRushRemaining = 0f;
        shotCharges = 1;
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
        ghost.SetHealth(2 - newHitCount);
        int defeats;
        ghostDefeats.TryGetValue(ghost, out defeats);
        int points = defeats switch
        {
            0 => newHitCount == 1 ? 50 : 250,
            1 => newHitCount == 1 ? 25 : 125,
            2 => newHitCount == 2 ? 60 : 0,
            _ => 0
        };
        uiManager.AddScoreAtPosition(points, ghost.transform.position);
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, ghost.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        PearlShot.PlaySparkles(transform, ghost.transform.position, goldenPearlMaterial, false);
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
            ghostDefeats[ghost] = Mathf.Min(defeats + 1, 3);
            ghost.DefeatPermanently();
            respawnTimers[ghost] = 10f;
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
