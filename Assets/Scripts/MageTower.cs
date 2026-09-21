using UnityEngine; // 1

[DisallowMultipleComponent] // 1
public class MageTower : TowerBase // 1
{
    [Header("Projectile Settings")] // 1
    [SerializeField] private float projectileVelocity = 8f; // 1

    [Header("Slow On Hit")]
    [SerializeField, Min(0.05f)] private float slowDuration = 0.5f;
    [SerializeField, Range(0.05f, 1f)] private float initialSpeedMultiplier = 0.45f;

    /// <summary>Büyücü projectile hızını ortak fırlatma sistemine verir.</summary> // 1
    protected override float GetProjectileVelocity() => projectileVelocity; // 1

    protected override void ApplyProjectileDamage(PathEnemy target, Vector3 impactPosition)
    {
        base.ApplyProjectileDamage(target, impactPosition);
        Enemy enemy = target != null ? target.Stats : null;
        if (enemy != null && !enemy.IsDead)
            enemy.ApplyDecayingSlow(Mathf.Max(0.5f, slowDuration), initialSpeedMultiplier > 0f ? initialSpeedMultiplier : 0.45f);
    }
}
