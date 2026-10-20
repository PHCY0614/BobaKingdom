using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GhostState
{
    Normal = 0,
    Scared = 1,
    Recovering = 2,
    Dead = 3
}

public class GhostController : MonoBehaviour
{
    [SerializeField] private int ghostNumber = 1;

    private Animator animator;
    private UIManager uiManager;
    private LevelGenerator levelGenerator;
    private PacStudentController pacStudent;
    private Collider2D ghostCollider;
    private GhostController[] innovationGhosts;
    private Coroutine movementRoutine;
    private Coroutine deadRoutine;
    private GameObject innovationHealthRoot;
    private UnityEngine.UI.Image[] innovationHealthCells;
    private bool ghostFourOnOuterLoop;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private Vector3 currentDirection;
    private Vector3 targetPosition;

    public GhostState currentState = GhostState.Normal;
    [field: System.NonSerialized] public bool IsPermanentlyDefeated { get; private set; }

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };
    private static readonly Vector2Int OuterLoopCorner = new Vector2Int(1, 1);
    // Grid Y increases downward, so world-up entry from (1,2) to (1,1) is Vector2Int.down.
    private static readonly Vector2Int OuterLoopEntryDirection = Vector2Int.down;

    private struct GhostPathState : System.IEquatable<GhostPathState>
    {
        public readonly Vector2Int cell;
        public readonly Vector2Int direction;

        public GhostPathState(Vector2Int cell, Vector2Int direction)
        {
            this.cell = cell;
            this.direction = direction;
        }

        public bool Equals(GhostPathState other)
        {
            return cell == other.cell && direction == other.direction;
        }

        public override bool Equals(object obj)
        {
            return obj is GhostPathState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (cell.GetHashCode() * 397) ^ direction.GetHashCode();
            }
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ghostCollider = GetComponent<Collider2D>();
        startPosition = transform.position;
        currentPosition = transform.position;
        targetPosition = transform.position;
    }

    private void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        levelGenerator = FindFirstObjectByType<LevelGenerator>();
        pacStudent = FindFirstObjectByType<PacStudentController>();
        if (uiManager == null || levelGenerator == null || pacStudent == null)
        {
            Debug.LogError("GhostController needs a UIManager, LevelGenerator and PacStudentController.", this);
            enabled = false;
            return;
        }
        if (uiManager.Innovation != null)
            innovationGhosts = FindObjectsByType<GhostController>(FindObjectsSortMode.None);
        ApplyState(currentState);
    }

    private void Update()
    {
        if (IsPermanentlyDefeated || currentState == GhostState.Dead || movementRoutine != null || !IsRoundRunning())
        {
            return;
        }

        BeginNextMove();
    }

    public void SetState(GhostState newState)
    {
        if (IsPermanentlyDefeated)
        {
            return;
        }

        if (currentState == GhostState.Dead && newState != GhostState.Dead)
        {
            return;
        }

        ApplyState(newState);
    }

    public void HandleEaten(UIManager eatenByUiManager)
    {
        if (IsPermanentlyDefeated || currentState == GhostState.Dead)
        {
            return;
        }

        if (eatenByUiManager != null)
        {
            uiManager = eatenByUiManager;
        }

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
            movementRoutine = null;
        }

        ApplyState(GhostState.Dead);
        if (deadRoutine != null)
        {
            StopCoroutine(deadRoutine);
        }

        deadRoutine = StartCoroutine(ReturnToHomeRoutine());
    }

    public void DefeatPermanently()
    {
        if (IsPermanentlyDefeated)
        {
            return;
        }

        IsPermanentlyDefeated = true;
        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
            movementRoutine = null;
        }
        if (deadRoutine != null)
        {
            StopCoroutine(deadRoutine);
            deadRoutine = null;
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        ApplyState(GhostState.Dead);
        Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
        if (worldCanvas != null)
        {
            worldCanvas.gameObject.SetActive(false);
        }
        StartCoroutine(PermanentlyDefeatedRoutine());
    }

    public void ReviveAtSpawn()
    {
        StopAllCoroutines();
        movementRoutine = null;
        deadRoutine = null;
        // Innovation revives this shared popped state after its cooldown.
        IsPermanentlyDefeated = false;
        enabled = true;
        gameObject.SetActive(true);

        Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
        if (worldCanvas != null)
        {
            worldCanvas.gameObject.SetActive(true);
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Rebind();
        }

        ResetPosition();
        ApplyState(GhostState.Normal);
        if (animator != null)
        {
            animator.Update(0f);
        }
    }

    public void SetHealth(int remainingHits)
    {
        Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
        if (worldCanvas == null)
        {
            return;
        }

        if (innovationHealthRoot == null || innovationHealthCells == null)
        {
            CreateInnovationHealth(worldCanvas);
        }
        if (innovationHealthRoot == null || innovationHealthCells == null) return;

        int health = Mathf.Clamp(remainingHits, 0, 2);
        innovationHealthRoot.SetActive(true);
        Color32 cream = new Color32(255, 236, 174, 255);
        Color32 darkBrown = new Color32(70, 38, 24, 255);
        for (int i = 0; i < innovationHealthCells.Length; i++)
        {
            innovationHealthCells[i].color = i < health ? cream : darkBrown;
        }
    }

    private void CreateInnovationHealth(Canvas worldCanvas)
    {
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        innovationHealthRoot = new GameObject("InnovationHealth", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        innovationHealthRoot.layer = worldCanvas.gameObject.layer;
        RectTransform rootRect = innovationHealthRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvasRect, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(14f, 6f);
        TMPro.TextMeshProUGUI label = worldCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        Vector2 labelPosition = label != null ? label.rectTransform.anchoredPosition : Vector2.zero;
        rootRect.anchoredPosition = labelPosition + new Vector2(0f, -11f);

        UnityEngine.UI.Image background = innovationHealthRoot.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color32(70, 38, 24, 255);
        background.raycastTarget = false;

        innovationHealthCells = new UnityEngine.UI.Image[2];
        for (int i = 0; i < innovationHealthCells.Length; i++)
        {
            GameObject cellObject = new GameObject("HealthCell" + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.Image));
            cellObject.layer = worldCanvas.gameObject.layer;
            RectTransform cellRect = cellObject.GetComponent<RectTransform>();
            cellRect.SetParent(rootRect, false);
            cellRect.anchorMin = new Vector2(0.5f, 0.5f);
            cellRect.anchorMax = new Vector2(0.5f, 0.5f);
            cellRect.pivot = new Vector2(0.5f, 0.5f);
            cellRect.sizeDelta = new Vector2(5.5f, 5f);
            cellRect.anchoredPosition = new Vector2(i == 0 ? -4f : 4f, 0f);
            innovationHealthCells[i] = cellObject.GetComponent<UnityEngine.UI.Image>();
            innovationHealthCells[i].raycastTarget = false;
        }

        rootRect.SetAsLastSibling();
    }

    private IEnumerator PermanentlyDefeatedRoutine()
    {
        yield return new WaitForSecondsRealtime(0.85f);
        gameObject.SetActive(false);
    }

    public void ResetPosition()
    {
        if (IsPermanentlyDefeated)
        {
            return;
        }

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
            movementRoutine = null;
        }
        if (deadRoutine != null)
        {
            StopCoroutine(deadRoutine);
            deadRoutine = null;
        }

        transform.position = startPosition;
        currentPosition = startPosition;
        targetPosition = startPosition;
        currentDirection = Vector3.zero;
        ghostFourOnOuterLoop = false;
        ApplyState(StateForScaredTimer());
    }

    private void ApplyState(GhostState newState)
    {
        GhostState previousState = currentState;
        currentState = newState;
        if (newState != GhostState.Normal || previousState != GhostState.Normal)
        {
            ghostFourOnOuterLoop = false;
        }

        if (ghostCollider != null)
        {
            ghostCollider.enabled = newState != GhostState.Dead;
        }

        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)newState);
            animator.SetFloat("MoveX", currentDirection.x);
            animator.SetFloat("MoveY", currentDirection.y);
        }

        RefreshGhostMusic();
    }

    private void BeginNextMove()
    {
        Vector2Int from = levelGenerator.WorldToGrid(currentPosition);
        Vector2Int next = ChooseNextGridPosition(from);
        if (next == from)
        {
            next = FindFallbackNeighbour(from);
        }

        if (next == from)
        {
            return;
        }

        Vector2Int step = next - from;
        currentDirection = new Vector3(step.x, -step.y, 0f).normalized;
        targetPosition = levelGenerator.GridToWorld(next);
        targetPosition.z = currentPosition.z;
        movementRoutine = StartCoroutine(MoveOneGridRoutine());
    }

    private IEnumerator MoveOneGridRoutine()
    {
        Vector3 from = currentPosition;
        Vector3 to = targetPosition;
        float distance = Vector3.Distance(from, to);
        float travelled = 0f;

        SetDirectionAnimation();
        while (travelled < distance)
        {
            if (IsRoundRunning())
            {
                travelled = Mathf.Min(distance, travelled + GetSpacedMoveDistance(GetMoveSpeed() * Time.deltaTime));
            }
            transform.position = Vector3.Lerp(from, to, distance <= 0f ? 1f : travelled / distance);
            yield return null;
        }

        transform.position = to;
        currentPosition = to;
        movementRoutine = null;
        SetDirectionAnimation();
    }

    private float GetSpacedMoveDistance(float requestedDistance)
    {
        if (!IsInnovationNormalMode() || innovationGhosts == null) return requestedDistance;

        // Keep same-speed ghosts apart after their routes merge, without blocking head-on crossings.
        foreach (GhostController other in innovationGhosts)
        {
            if (other == null || other == this || !other.isActiveAndEnabled || other.IsPermanentlyDefeated
                || other.uiManager != uiManager || other.currentState != GhostState.Normal
                || Vector3.Dot(currentDirection, other.currentDirection) < 0.99f) continue;

            Vector3 offset = other.transform.position - transform.position;
            float ahead = Vector3.Dot(offset, currentDirection);
            if (ahead < -0.2f || (offset - currentDirection * ahead).sqrMagnitude > 0.0225f) continue;
            if (ahead <= 0.2f && other.ghostNumber > ghostNumber) continue;
            requestedDistance = Mathf.Min(requestedDistance, Mathf.Max(0f, ahead - 0.85f));
        }
        return requestedDistance;
    }

    private IEnumerator ReturnToHomeRoutine()
    {
        Vector3 from = transform.position;
        Vector3 to = startPosition;
        float distance = Vector3.Distance(from, to);
        float travelled = 0f;

        while (travelled < distance)
        {
            if (IsRoundRunning())
            {
                travelled = Mathf.Min(distance, travelled + GetMoveSpeed() * Time.deltaTime);
            }
            transform.position = Vector3.Lerp(from, to, distance <= 0f ? 1f : travelled / distance);
            yield return null;
        }

        transform.position = to;
        currentPosition = to;
        targetPosition = to;
        currentDirection = Vector3.zero;
        deadRoutine = null;
        ApplyState(StateForScaredTimer());
    }

    private Vector2Int ChooseNextGridPosition(Vector2Int from)
    {
        bool inSpawn = levelGenerator.IsGhostSpawn(from);
        if (inSpawn)
        {
            Vector2Int exit = levelGenerator.GetGhostExit(ghostNumber == 1 || ghostNumber == 3);
            List<Vector2Int> exits = GetLegalNeighbours(from, true);
            exits.Sort((a, b) => (a - exit).sqrMagnitude.CompareTo((b - exit).sqrMagnitude));
            if (exits.Count > 0)
            {
                return exits[0];
            }
        }

        List<Vector2Int> candidates = GetLegalNeighbours(from, false);
        if (candidates.Count == 0)
        {
            return from;
        }

        List<Vector2Int> nonReverse = candidates.FindAll(candidate => !IsReverse(from, candidate));
        List<Vector2Int> preferred = nonReverse.Count > 0 ? nonReverse : candidates;

        if (IsInnovationNormalMode())
        {
            return ChooseInnovationNextGridPosition(from, preferred, candidates);
        }

        int behaviourNumber = currentState == GhostState.Scared || currentState == GhostState.Recovering ? 1 : ghostNumber;
        if (behaviourNumber == 4)
        {
            if (!ghostFourOnOuterLoop)
            {
                Vector2Int incomingDirection = CurrentGridDirection();
                if (from == OuterLoopCorner && incomingDirection == OuterLoopEntryDirection)
                {
                    ghostFourOnOuterLoop = true;
                }
                else if (TryGetOuterLoopStep(from, incomingDirection, out Vector2Int outerStep))
                {
                    return outerStep;
                }
            }
            return ChooseClockwise(from, preferred, candidates);
        }

        if (behaviourNumber == 1 || behaviourNumber == 2)
        {
            Vector3 playerWorld = pacStudent.transform.position;
            float currentDistance = (levelGenerator.GridToWorld(from) - playerWorld).sqrMagnitude;
            bool chase = behaviourNumber == 2;
            List<Vector2Int> distanceMatches = preferred.FindAll(candidate =>
            {
                float distance = (levelGenerator.GridToWorld(candidate) - playerWorld).sqrMagnitude;
                return chase ? distance <= currentDistance : distance >= currentDistance;
            });

            if (distanceMatches.Count > 0)
            {
                preferred = distanceMatches;
            }
        }

        return preferred[UnityEngine.Random.Range(0, preferred.Count)];
    }

    private bool IsInnovationNormalMode()
    {
        return uiManager != null && uiManager.Innovation != null && currentState == GhostState.Normal;
    }

    private Vector2Int ChooseInnovationNextGridPosition(Vector2Int from, List<Vector2Int> preferred, List<Vector2Int> all)
    {
        Vector2Int playerCell = levelGenerator.WorldToGrid(pacStudent.transform.position);
        Vector2Int target = playerCell;
        if (ghostNumber == 1 || ghostNumber == 3)
        {
            target = ChooseInnovationInterceptTarget(playerCell);
            if (target != playerCell && (target == from || GridDistance(from, playerCell) <= 3))
            {
                target = playerCell;
            }
        }

        List<Vector2Int> choices = preferred.Count > 0 ? preferred : all;
        Vector2Int best = FindInnovationStep(choices, from, BuildInnovationDistanceField(target));
        if (best == from && target != playerCell)
        {
            best = FindInnovationStep(choices, from, BuildInnovationDistanceField(playerCell));
        }

        if (best != from)
        {
            return best;
        }

        return choices[UnityEngine.Random.Range(0, choices.Count)];
    }

    private Vector2Int FindInnovationStep(List<Vector2Int> choices, Vector2Int from,
        Dictionary<Vector2Int, int> distances)
    {
        Vector2Int best = from;
        int bestDistance = int.MaxValue;
        foreach (Vector2Int candidate in choices)
        {
            int distance;
            if (!distances.TryGetValue(candidate, out distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private Vector2Int ChooseInnovationInterceptTarget(Vector2Int playerCell)
    {
        Vector2Int direction = new Vector2Int(
            Mathf.RoundToInt(pacStudent.FacingDirection.x),
            Mathf.RoundToInt(-pacStudent.FacingDirection.y));
        if (direction == Vector2Int.zero)
        {
            direction = Vector2Int.right;
        }

        int sign = ghostNumber == 1 ? 1 : -1;
        foreach (int distance in new[] { 3, 2 })
        {
            Vector2Int desired = playerCell + direction * (distance * sign);
            Vector2Int target = FindNearestInnovationTarget(desired);
            if (target != playerCell)
            {
                return target;
            }
        }

        return playerCell;
    }

    private Dictionary<Vector2Int, int> BuildInnovationDistanceField(Vector2Int requestedTarget)
    {
        Vector2Int target = FindNearestInnovationTarget(requestedTarget);
        var distances = new Dictionary<Vector2Int, int>();
        var pending = new Queue<Vector2Int>();
        distances[target] = 0;
        pending.Enqueue(target);

        while (pending.Count > 0)
        {
            Vector2Int cell = pending.Dequeue();
            int distance = distances[cell] + 1;
            foreach (Vector2Int neighbour in GetLegalNeighbours(cell, false))
            {
                if (distances.ContainsKey(neighbour))
                {
                    continue;
                }

                distances[neighbour] = distance;
                pending.Enqueue(neighbour);
            }
        }

        return distances;
    }

    private Vector2Int FindNearestInnovationTarget(Vector2Int requestedTarget)
    {
        if (IsInnovationTargetCell(requestedTarget))
        {
            return requestedTarget;
        }

        var visited = new HashSet<Vector2Int>();
        var pending = new Queue<Vector2Int>();
        visited.Add(requestedTarget);
        pending.Enqueue(requestedTarget);
        while (pending.Count > 0)
        {
            Vector2Int cell = pending.Dequeue();
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighbour = cell + direction;
                if (!levelGenerator.InBounds(neighbour) || !visited.Add(neighbour))
                {
                    continue;
                }

                if (IsInnovationTargetCell(neighbour))
                {
                    return neighbour;
                }

                if (levelGenerator.IsWalkable(neighbour.x, neighbour.y))
                {
                    pending.Enqueue(neighbour);
                }
            }
        }

        return requestedTarget;
    }

    private bool IsInnovationTargetCell(Vector2Int cell)
    {
        return levelGenerator.InBounds(cell)
            && levelGenerator.IsWalkable(cell.x, cell.y)
            && !levelGenerator.IsGhostSpawn(cell)
            && !(cell.y == levelGenerator.Rows / 2 && (cell.x == 0 || cell.x == levelGenerator.Columns - 1));
    }

    private int GridDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private Vector2Int ChooseClockwise(Vector2Int from, List<Vector2Int> preferred, List<Vector2Int> all)
    {
        Vector2Int direction = currentDirection == Vector3.zero
            ? Vector2Int.right
            : new Vector2Int(Mathf.RoundToInt(currentDirection.x), Mathf.RoundToInt(-currentDirection.y));
        Vector2Int left = new Vector2Int(direction.y, -direction.x);
        Vector2Int right = new Vector2Int(-direction.y, direction.x);
        Vector2Int straight = from + direction;
        Vector2Int leftTarget = from + left;
        Vector2Int rightTarget = from + right;

        Vector2Int[] order = { leftTarget, straight, rightTarget };
        foreach (Vector2Int candidate in order)
        {
            if (preferred.Contains(candidate))
            {
                return candidate;
            }
        }

        return all[0];
    }

    private Vector2Int CurrentGridDirection()
    {
        if (currentDirection == Vector3.zero)
        {
            return Vector2Int.zero;
        }

        return new Vector2Int(
            Mathf.RoundToInt(currentDirection.x),
            Mathf.RoundToInt(-currentDirection.y));
    }

    private bool TryGetOuterLoopStep(Vector2Int from, Vector2Int incomingDirection, out Vector2Int nextStep)
    {
        GhostPathState start = new GhostPathState(from, incomingDirection);
        var pending = new Queue<GhostPathState>();
        var previous = new Dictionary<GhostPathState, GhostPathState>();
        pending.Enqueue(start);
        previous[start] = start;

        while (pending.Count > 0)
        {
            GhostPathState state = pending.Dequeue();
            if (state.cell == OuterLoopCorner && state.direction == OuterLoopEntryDirection)
            {
                GhostPathState first = state;
                while (!previous[first].Equals(start))
                {
                    first = previous[first];
                }

                nextStep = first.cell;
                return first.cell != from;
            }

            List<Vector2Int> neighbours = GetLegalNeighbours(state.cell, false);
            List<Vector2Int> nonReverse = neighbours.FindAll(candidate =>
                state.direction == Vector2Int.zero || candidate - state.cell != -state.direction);
            if (nonReverse.Count > 0)
            {
                neighbours = nonReverse;
            }

            foreach (Vector2Int neighbour in neighbours)
            {
                GhostPathState next = new GhostPathState(neighbour, neighbour - state.cell);
                if (previous.ContainsKey(next))
                {
                    continue;
                }

                previous[next] = state;
                pending.Enqueue(next);
            }
        }

        nextStep = from;
        return false;
    }

    private List<Vector2Int> GetLegalNeighbours(Vector2Int from, bool leavingSpawn)
    {
        var result = new List<Vector2Int>(4);
        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int target = from + direction;
            if (!levelGenerator.IsGhostStepAllowed(from, target, leavingSpawn))
            {
                continue;
            }

            if (!leavingSpawn && levelGenerator.IsGhostSpawn(target))
            {
                continue;
            }

            result.Add(target);
        }
        return result;
    }

    private Vector2Int FindFallbackNeighbour(Vector2Int from)
    {
        List<Vector2Int> candidates = GetLegalNeighbours(from, levelGenerator.IsGhostSpawn(from));
        return candidates.Count > 0 ? candidates[0] : from;
    }

    private bool IsReverse(Vector2Int from, Vector2Int target)
    {
        if (currentDirection == Vector3.zero)
        {
            return false;
        }

        Vector2Int previous = new Vector2Int(
            Mathf.RoundToInt(currentDirection.x),
            Mathf.RoundToInt(-currentDirection.y));
        return target - from == -previous;
    }

    private void SetDirectionAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat("MoveX", currentDirection.x);
        animator.SetFloat("MoveY", currentDirection.y);
    }

    private float GetMoveSpeed()
    {
        float playerSpeed = pacStudent != null ? pacStudent.MoveSpeed : 5f;
        if (currentState != GhostState.Normal)
        {
            return 0.45f * playerSpeed;
        }

        return (IsInnovationNormalMode() && ghostNumber == 4 ? 0.75f : 0.9f) * playerSpeed;
    }

    private GhostState StateForScaredTimer()
    {
        float remaining = uiManager != null ? uiManager.GetScaredTimeRemaining() : 0f;
        if (remaining <= 0f) return GhostState.Normal;
        if (remaining <= 3f) return GhostState.Recovering;
        return GhostState.Scared;
    }

    private bool IsRoundRunning()
    {
        return uiManager == null || uiManager.IsRoundRunning;
    }

    private void RefreshGhostMusic()
    {
        AudioController audio = uiManager != null ? uiManager.AudioController : FindFirstObjectByType<AudioController>();
        if (audio != null)
        {
            audio.RefreshGhostMusic();
        }
    }

}
