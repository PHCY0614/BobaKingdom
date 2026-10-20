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
    private Coroutine movementRoutine;
    private Coroutine deadRoutine;
    private bool ghostFourOnOuterLoop;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private Vector3 currentDirection;
    private Vector3 targetPosition;

    public GhostState currentState = GhostState.Normal;

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
        ApplyState(currentState);
    }

    private void Update()
    {
        if (currentState == GhostState.Dead || movementRoutine != null || !IsRoundRunning())
        {
            return;
        }

        BeginNextMove();
    }

    public void SetState(GhostState newState)
    {
        if (currentState == GhostState.Dead && newState != GhostState.Dead)
        {
            return;
        }

        ApplyState(newState);
    }

    public void HandleEaten(UIManager eatenByUiManager)
    {
        if (currentState == GhostState.Dead)
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

    public void ResetPosition()
    {
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
                travelled = Mathf.Min(distance, travelled + GetMoveSpeed() * Time.deltaTime);
            }
            transform.position = Vector3.Lerp(from, to, distance <= 0f ? 1f : travelled / distance);
            yield return null;
        }

        transform.position = to;
        currentPosition = to;
        movementRoutine = null;
        SetDirectionAnimation();
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
        return (currentState == GhostState.Normal ? 0.9f : 0.45f) * playerSpeed;
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
