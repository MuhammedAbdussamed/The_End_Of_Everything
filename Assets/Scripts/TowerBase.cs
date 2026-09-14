using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Common parent component for every tower in the game.
/// Put tower-specific behaviour in subclasses and keep shared stats here.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class TowerBase : MonoBehaviour
{
    [Header("Shared Tower Data")]
    [SerializeField] protected TowerData towerData;

    [Header("Projectile Pool")] // 1
    [SerializeField] private Transform projectilePool; // 1

    private readonly List<PathEnemy> enemiesInRange = new List<PathEnemy>();
    private readonly Dictionary<TowerProjectile, PathEnemy> activeProjectiles = new Dictionary<TowerProjectile, PathEnemy>(); // 1
    private readonly List<TowerProjectile> completedProjectiles = new List<TowerProjectile>(); // 1
    private float attackTimer; // 1

    public TowerData Data => towerData;

    public IReadOnlyList<PathEnemy> EnemiesInRange => enemiesInRange;

    public float TowerRange => towerData?.TowerRange ?? 0f;

    public int TowerLevel => towerData?.TowerLevel ?? 1;

    public float TowerAttackSpeed => towerData?.TowerAttackSpeed ?? 0f;

    public float TowerDamage => towerData?.TowerDamage ?? 0f;

    public TowerData.TowerDamageType DamageType => towerData?.DamageType ?? TowerData.TowerDamageType.Physical;

    /// <summary>Alt kulelerin kendi projectile hızını ortak fırlatma sistemine vermesini sağlar.</summary> // 1
    protected virtual float GetProjectileVelocity() => 10f; // 1

    protected virtual void Awake() // 1
    {
        SyncRangeCollider(); // 1
        InitializeProjectilePool(); // 1
    }

    protected virtual void OnValidate() => SyncRangeCollider();

    /// <summary>İlk düşmana saldırır ve aktif projectile nesnelerini hareket ettirir.</summary> // 1
    protected virtual void Update() // 1
    {
        RemoveInvalidEnemies(); // 1

        if (projectilePool == null || TowerAttackSpeed <= 0f || enemiesInRange.Count == 0) return; // 1

        attackTimer -= Time.deltaTime; // 1
        if (attackTimer > 0f) return; // 1

        if (LaunchProjectile(enemiesInRange[0])) // 1
        {
            attackTimer = 1f / TowerAttackSpeed; // 1
        }
    }

    /// <summary>Kule menziline giren düşmanı giriş sırasına göre listeye ekler.</summary>
    protected virtual void OnTriggerEnter(Collider other) // 1
    {
        TryAddEnemy(other);
    }

    // A collider can touch the trigger before the enemy's center is in range.
    // Keep checking that overlap so entering the actual range acquires a target.
    protected virtual void OnTriggerStay(Collider other)
    {
        TryAddEnemy(other);
    }

    private void TryAddEnemy(Collider other)
    {
        if (!isActiveAndEnabled) return;
        PathEnemy enemy = other.GetComponentInParent<PathEnemy>();
        if (IsEnemyInRange(enemy) && !enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }
    }

    /// <summary>Kule menzilinden çıkan düşmanı listeden kaldırır.</summary>
    protected virtual void OnTriggerExit(Collider other) // 1
    {
        PathEnemy enemy = other.GetComponentInParent<PathEnemy>();
        if (enemy != null)
        {
            enemiesInRange.Remove(enemy);
        }
    }

    /// <summary>Projectile havuzundaki nesneleri başlangıçta pasif hale getirir.</summary> // 1
    private void InitializeProjectilePool() // 1
    {
        if (projectilePool == null)
        {
            Debug.LogWarning($"{name}: Assign a projectile pool to enable tower attacks.", this);
            return;
        }

        for (int i = 0; i < projectilePool.childCount; i++) // 1
        {
            TowerProjectile projectile = projectilePool.GetChild(i).GetComponent<TowerProjectile>(); // 1
            projectile?.Initialize(); // 1
        }
    }

    /// <summary>Havuzdaki boş projectile nesnesini hedefe doğru fırlatır.</summary> // 1
    private bool LaunchProjectile(PathEnemy target) // 1
    {
        TowerProjectile projectile = GetAvailableProjectile(); // 1
        if (projectile == null || target == null) return false; // 1

        Vector3 direction = (target.transform.position - transform.position).normalized; // 1
        if (direction == Vector3.zero) return false; // 1

        activeProjectiles[projectile] = target; // 1
        projectile.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(direction)); // 1
        projectile.gameObject.SetActive(true); // 1
        projectile.Launch(this, direction, GetProjectileVelocity()); // 1
        return true; // 1
    }

    /// <summary>Havuzda kullanılmayan ilk projectile nesnesini bulur.</summary> // 1
    private TowerProjectile GetAvailableProjectile() // 1
    {
        if (projectilePool == null) return null; // 1

        for (int i = 0; i < projectilePool.childCount; i++) // 1
        {
            TowerProjectile projectile = projectilePool.GetChild(i).GetComponent<TowerProjectile>(); // 1
            if (projectile != null && !projectile.gameObject.activeSelf) return projectile; // 1
        }

        return null; // 1
    }

    /// <summary>Çarpan projectile nesnesini kendi havuzuna geri koyar.</summary> // 1
    public void ProjectileHit(TowerProjectile projectile, PathEnemy target) // 1
    {
        if (!activeProjectiles.TryGetValue(projectile, out PathEnemy currentTarget) || currentTarget != target) return; // 1
        RecycleProjectile(projectile); // 1
    }

    /// <summary>Projectile nesnesini fizik değerleri sıfırlanmış halde havuza gönderir.</summary> // 1
    public void RecycleProjectile(TowerProjectile projectile) // 1
    {
        if (projectile == null) return; // 1
        activeProjectiles.Remove(projectile); // 1
        projectile.ResetToPool(); // 1
    }

    /// <summary>Yok edilen veya kendi menzilinden çıkan düşmanları listeden temizler.</summary> // 1
    private void RemoveInvalidEnemies() // 1
    {
        for (int i = enemiesInRange.Count - 1; i >= 0; i--) // 1
        {
            PathEnemy enemy = enemiesInRange[i]; // 1
            if (!IsEnemyInRange(enemy)) enemiesInRange.RemoveAt(i);
        }

        completedProjectiles.Clear(); // 1
        foreach (KeyValuePair<TowerProjectile, PathEnemy> activeProjectile in activeProjectiles) // 1
        {
            if (activeProjectile.Value == null || !activeProjectile.Value.isActiveAndEnabled)
                completedProjectiles.Add(activeProjectile.Key);
        }

        foreach (TowerProjectile projectile in completedProjectiles) // 1
        {
            RecycleProjectile(projectile); // 1
        }
    }

    private bool IsEnemyInRange(PathEnemy enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && TowerRange > 0f
            && (enemy.transform.position - transform.position).sqrMagnitude <= TowerRange * TowerRange;
    }

    /// <summary>Kule kapanırken kendi aktif projectile nesnelerini havuza iade eder.</summary> // 1
    protected virtual void OnDisable() // 1
    {
        foreach (TowerProjectile projectile in activeProjectiles.Keys) // 1
        {
            if (projectile != null) projectile.ResetToPool();
        }

        activeProjectiles.Clear(); // 1
        enemiesInRange.Clear();
        attackTimer = 0f;
    }

    private void SyncRangeCollider()
    {
        SphereCollider rangeCollider = GetComponent<SphereCollider>();
        Vector3 worldScale = transform.lossyScale; // 1
        float largestScale = Mathf.Max(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        rangeCollider.radius = largestScale > 0f ? TowerRange / largestScale : TowerRange;
        rangeCollider.isTrigger = true;
    }
}
