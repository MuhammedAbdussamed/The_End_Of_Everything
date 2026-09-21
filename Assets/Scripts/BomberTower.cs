using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BomberTower : TowerBase
{
    [Header("Bomb Settings")]
    [SerializeField, Min(0.1f)] private float projectileVelocity = 12f;
    [SerializeField, Min(0.5f)] private float arcHeight = 4f;
    [SerializeField] private BombExplosionVisual explosionVisual;

    private Collider[] hitBuffer = new Collider[16];
    private readonly HashSet<Enemy> explosionTargets = new HashSet<Enemy>();
    private int enemyMask;

    public float BlastRadius => (Data as BomberTowerData)?.BlastRadius ?? 0f;

    public void ConfigureExplosionVisual(BombExplosionVisual visual) => explosionVisual = visual;

    protected override void Awake()
    {
        base.Awake();
        enemyMask = LayerMask.GetMask("Enemy");
    }

    protected override float GetProjectileVelocity() => projectileVelocity;

    protected override void LaunchProjectileMotion(TowerProjectile projectile, PathEnemy target, Vector3 direction, float speed)
    {
        Vector3 start = projectile.transform.position;
        Vector3 destination = target.transform.position;
        NavMeshAgent agent = target.Agent;
        Vector3 targetVelocity = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        float flightTime = GetFlightTime(start, destination, speed);
        // Lead moving enemies; the landing point stays fixed once the shell leaves the tower.
        for (int i = 0; i < 3; i++)
        {
            destination = target.transform.position + targetVelocity * flightTime;
            flightTime = GetFlightTime(start, destination, speed);
        }
        // The current path uses render meshes for navigation and does not require physics colliders.
        if (NavMesh.SamplePosition(destination, out NavMeshHit ground, BlastRadius, NavMesh.AllAreas))
            destination.y = ground.position.y;
        else destination.y -= agent != null ? agent.baseOffset : 0.5f;
        flightTime = GetFlightTime(start, destination, speed);
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
