using UnityEngine;

public class PearlShot : MonoBehaviour
{
    private const float Speed = 10f;
    private const float MaxTravel = 32f;
    private InnovationController controller;
    private LevelGenerator levelGenerator;
    private UIManager uiManager;
    private GhostController[] targets;
    private Collider2D[] targetColliders;
    private Vector3 direction;
    private float travelled;
    private bool disposed;
    private TrailRenderer trail;

    public void Initialize(InnovationController owner, Vector3 shotDirection, LevelGenerator map,
        UIManager ui, GhostController[] cachedTargets)
    {
        controller = owner;
        direction = shotDirection == Vector3.zero ? Vector3.right : shotDirection.normalized;
        levelGenerator = map;
        uiManager = ui;
        targets = cachedTargets;
        targetColliders = new Collider2D[targets == null ? 0 : targets.Length];
        for (int i = 0; i < targetColliders.Length; i++)
            targetColliders[i] = targets[i] == null ? null : targets[i].GetComponent<Collider2D>();
    }

    public void ConfigureAppearance(Material material, bool empowered)
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.transform.localScale = Vector3.one * (empowered ? 0.95f : 0.75f);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Shimmer", empowered ? 1f : 0.65f);
            renderer.SetPropertyBlock(properties);
        }

        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = empowered ? 0.15f : 0.1f;
        trail.startWidth = empowered ? 0.13f : 0.08f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.textureMode = LineTextureMode.Stretch;
        trail.startColor = empowered ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(1f, 0.72f, 0.12f, 0.85f);
        trail.endColor = new Color(1f, 0.5f, 0f, 0f);
        trail.sortingOrder = 102;
        trail.emitting = true;
    }

    public static ParticleSystem PlaySparkles(Transform parent, Vector3 worldPosition, Material material, bool large)
    {
        GameObject effectObject = new GameObject(large ? "LargePearlSparkles" : "PearlSparkles");
        if (parent != null) effectObject.transform.SetParent(parent, false);
        effectObject.transform.position = worldPosition;
        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer particleRenderer = effectObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 103;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.startLifetime = large ? new ParticleSystem.MinMaxCurve(0.35f, 0.5f) : new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = large ? new ParticleSystem.MinMaxCurve(1.5f, 3f) : new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSize = large ? new ParticleSystem.MinMaxCurve(0.09f, 0.13f) : new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.15f, 1f));
        main.maxParticles = large ? 24 : 12;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(large ? 20 : 9)) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = large ? 0.16f : 0.08f;
        ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        particles.Clear();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Play();
        Object.Destroy(effectObject, 1f);
        return particles;
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    public void Advance(float seconds)
    {
        if (disposed || seconds <= 0f || uiManager == null || !uiManager.IsRoundRunning) return;
        if (levelGenerator == null)
        {
            Dispose();
            return;
        }

        Physics2D.SyncTransforms();
        float distance = Speed * seconds;
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
        Vector3 step = direction * (distance / stepCount);
        for (int i = 0; i < stepCount; i++)
        {
            Vector3 next = transform.position + step;
            Vector2Int cell = levelGenerator.WorldToGrid(next);
            if (!levelGenerator.InBounds(cell) || !levelGenerator.IsWalkable(cell.x, cell.y)
                || levelGenerator.IsGhostSpawn(cell))
            {
                Dispose();
                return;
            }

            for (int targetIndex = 0; targetIndex < targetColliders.Length; targetIndex++)
            {
                GhostController ghost = targets[targetIndex];
                Collider2D collider = targetColliders[targetIndex];
                if (ghost == null || collider == null || !collider.enabled || ghost.IsPermanentlyDefeated
                    || ghost.currentState == GhostState.Dead || !collider.OverlapPoint(next)) continue;
                if (controller != null && controller.HitGhost(ghost))
                {
                    Dispose();
                    return;
                }
            }

            transform.position = next;
            travelled += step.magnitude;
            if (travelled >= MaxTravel)
            {
                Dispose();
                return;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (controller != null) controller.UnregisterShot(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (controller != null) controller.UnregisterShot(this);
    }
}
