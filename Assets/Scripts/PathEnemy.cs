using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Enemy))]
public class PathEnemy : MonoBehaviour
{
    [SerializeField] private NavMeshSurface pathSurface;
    [SerializeField] private Transform waypointRoot;
    [SerializeField, Min(0.05f)] private float waypointReachDistance = 0.4f;
    [SerializeField, Min(0.05f)] private float navMeshSampleDistance = 0.75f;

    private NavMeshAgent agent;
    private PathWaypoint[] waypoints;
    private NavMeshPath candidatePath;
    private NavMeshQueryFilter queryFilter;
    private bool hasMovementTarget;
    private Enemy enemyStats;

    public int WaypointCount => waypoints?.Length ?? 0;
    public int CurrentWaypointIndex { get; private set; }
    public Vector3 CurrentTarget { get; private set; }
    public bool HasReachedDestination { get; private set; }
    public float WaypointReachDistance => waypointReachDistance;
    public NavMeshAgent Agent => agent;
    public Enemy Stats => enemyStats;

    public void ConfigureRoute(NavMeshSurface surface, Transform routeRoot, PathWaypoint[] route)
    {
        pathSurface = surface;
        waypointRoot = routeRoot;
        waypoints = route;
    }

    /// <summary>Ortak NavMesh'i hazırlar ve sıralı patika noktalarını takip etmeye başlar.</summary>
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyStats = GetComponent<Enemy>();
        pathSurface ??= FindFirstObjectByType<NavMeshSurface>();
        waypointRoot ??= GameObject.Find("Path Waypoints")?.transform;

        if (pathSurface == null || waypointRoot == null)
        {
            Debug.LogError("PathEnemy requires a NavMeshSurface and Path Waypoints.", this);
            return;
        }

        // Hierarchy order is the route order; each enemy chooses its own offsets.
        waypoints ??= waypointRoot.GetComponentsInChildren<PathWaypoint>();
        if (waypoints.Length == 0)
        {
            Debug.LogError("Path Waypoints must contain at least one PathWaypoint.", this);
            return;
        }

        // All enemies share this surface. Spawning another enemy must not rebuild it.
        if (pathSurface.navMeshData == null) pathSurface.BuildNavMesh();

        queryFilter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        candidatePath = new NavMeshPath();
        // Enemy hitboxes and agents may overlap; the NavMesh still confines them to the path.
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, queryFilter))
        {
            agent.Warp(hit.position + Vector3.up * agent.baseOffset);
        }

        if (agent.isOnNavMesh)
        {
            MoveToNextWaypoint();
        }
        else
        {
            Debug.LogError("PathEnemy could not reach the path NavMesh.", this);
        }
    }

    private void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh
            || !hasMovementTarget || agent.pathPending) return;

        float reachDistance = Mathf.Max(waypointReachDistance, agent.stoppingDistance);
        if (float.IsInfinity(agent.remainingDistance) || agent.remainingDistance > reachDistance) return;

        CurrentWaypointIndex++;
        if (CurrentWaypointIndex >= WaypointCount)
        {
            HasReachedDestination = true;
            hasMovementTarget = false;
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            // Finished enemies must leave navigation and tower targeting so followers can pass.
            gameObject.SetActive(false);
            return;
        }

        MoveToNextWaypoint();
    }

    private void MoveToNextWaypoint()
    {
        agent.autoBraking = CurrentWaypointIndex == WaypointCount - 1;

        PathWaypoint waypoint = waypoints[CurrentWaypointIndex];
        if (waypoint != null)
        {
            // Reject off-path or unreachable samples; keep a chosen target until arrival.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (TrySetTarget(waypoint.GetRandomPosition(), waypoint)) return;
            }

            if (TrySetTarget(waypoint.transform.position, waypoint)) return;
        }

        StopAtInvalidWaypoint();
    }

    private bool TrySetTarget(Vector3 position, PathWaypoint waypoint)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, queryFilter)) return false;

        if (waypoint != null)
        {
            Vector3 offset = hit.position - waypoint.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > waypoint.Radius * waypoint.Radius + 0.001f) return false;
        }

        if (!agent.CalculatePath(hit.position, candidatePath)
            || candidatePath.status != NavMeshPathStatus.PathComplete) return false;
        if (!agent.SetPath(candidatePath)) return false;

        CurrentTarget = hit.position;
        hasMovementTarget = true;
        agent.isStopped = false;
        return true;
    }

    private void StopAtInvalidWaypoint()
    {
        hasMovementTarget = false;
        agent.isStopped = true;
        Debug.LogError($"{name}: No reachable point at route index {CurrentWaypointIndex}. Check the waypoint position and radius.", this);
    }
}
