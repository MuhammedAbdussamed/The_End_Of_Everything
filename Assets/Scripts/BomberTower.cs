using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BomberTower : TowerBase
{
    [Header("Bomb Settings")]
    [SerializeField, Min(0.1f)] private float projectileVelocity = 14f;
    [SerializeField, Min(0.5f)] private float arcHeight = 4f;
    [SerializeField, Min(0.05f)] private float maxLeadTime = 0.75f;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 3f;
    [SerializeField] private BombExplosionVisual explosionVisual;

    private Collider[] hitBuffer = new Collider[16];
    private readonly HashSet<Enemy> explosionTargets = new HashSet<Enemy>();
    private int enemyMask;

    public float BlastRadius => (Data as BomberTowerData)?.BlastRadius ?? 0f;
    public float ProjectileVelocity => projectileVelocity;

    public void ConfigureExplosionVisual(BombExplosionVisual visual) => explosionVisual = visual;

    protected override void Awake()
    {
        base.Awake();
        enemyMask = LayerMask.GetMask("Enemy");
    }

    protected override float GetProjectileVelocity() => projectileVelocity;

    protected override PathEnemy SelectTarget(IReadOnlyList<PathEnemy> candidates)
    {
        PathEnemy fallback = null;
        PathEnemy best = null;
        int bestWaypoint = -1;
        float bestRemainingDistance = float.PositiveInfinity;

        foreach (PathEnemy candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.HasReachedDestination) continue;

            Enemy enemy = candidate.Stats != null ? candidate.Stats : candidate.GetComponent<Enemy>();
            if (!IsDamageable(enemy)) continue;
            if (fallback == null) fallback = candidate;

            NavMeshAgent agent = candidate.Agent;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;

            float remainingDistance = agent.pathPending || float.IsInfinity(agent.remainingDistance)
                ? float.PositiveInfinity
                : agent.remainingDistance;
            if (best == null || candidate.CurrentWaypointIndex > bestWaypoint ||
                (candidate.CurrentWaypointIndex == bestWaypoint && remainingDistance < bestRemainingDistance))
            {
                best = candidate;
                bestWaypoint = candidate.CurrentWaypointIndex;
                bestRemainingDistance = remainingDistance;
            }
        }

        // Disabled agents are used by combat tests and can also occur briefly during spawning.
        return best != null ? best : fallback;
    }

    protected override void LaunchProjectileMotion(TowerProjectile projectile, PathEnemy target, Vector3 direction, float speed)
    {
        Vector3 start = projectile.transform.position;
        Vector3 currentPosition = target.transform.position;
        Vector3 destination = currentPosition;
        NavMeshAgent agent = target.Agent;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            float leadTime = Mathf.Min(GetFlightTime(start, currentPosition, speed), maxLeadTime);
            Vector3 predictedPosition = currentPosition + agent.velocity * leadTime;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            if (NavMesh.SamplePosition(predictedPosition, out NavMeshHit predictedHit, navMeshSampleDistance, filter))
                destination = predictedHit.position;
            else if (NavMesh.SamplePosition(currentPosition, out NavMeshHit currentHit, 1f, filter))
                destination = currentHit.position;
        }
        else
        {
            destination.y -= agent != null ? agent.baseOffset : 0.5f;
        }

        float flightTime = GetFlightTime(start, destination, speed);
        projectile.LaunchMortar(this, destination, flightTime);
    }

    private float GetFlightTime(Vector3 start, Vector3 destination, float speed)
    {
        float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
        float apex = Mathf.Max(start.y, destination.y) + arcHeight;
        float timeToRise = Mathf.Sqrt(2f * (apex - start.y) / gravity);
        float timeToFall = Mathf.Sqrt(2f * (apex - destination.y) / gravity);
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(destination - start, Vector3.up);
        return Mathf.Max(timeToRise + timeToFall, horizontalOffset.magnitude / Mathf.Max(0.1f, speed));
    }

    protected override bool CanHitEnemy(PathEnemy intendedTarget, PathEnemy hitEnemy)
    {
        Enemy enemy = hitEnemy.Stats;
        return hitEnemy.isActiveAndEnabled && IsDamageable(enemy);
    }

    protected override void ApplyProjectileDamage(PathEnemy target, Vector3 impactPosition)
    {
        explosionTargets.Clear();
        Enemy directTarget = target != null ? target.Stats : null;
        if (IsDamageable(directTarget)) explosionTargets.Add(directTarget);

        int count;
        // Grow and repeat a saturated query so dense groups never lose splash hits.
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(impactPosition, BlastRadius, hitBuffer,
                enemyMask, QueryTriggerInteraction.Collide);
            if (count < hitBuffer.Length) break;
            Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
        }
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hitBuffer[i].GetComponentInParent<Enemy>();
            if (IsDamageable(enemy)) explosionTargets.Add(enemy);
            hitBuffer[i] = null;
        }
        foreach (Enemy enemy in explosionTargets)
            if (enemy != null) enemy.TakeDamage(TowerDamage, DamageType);

        explosionVisual?.Show(impactPosition, BlastRadius);
    }

    private static bool IsDamageable(Enemy enemy) =>
        enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead && !enemy.IsWaiting;
}
