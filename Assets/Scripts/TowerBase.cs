using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Level 2 Upgrade")]
    [SerializeField, Min(1f)] private float levelTwoRangeMultiplier = 1.25f;
    [SerializeField, Min(1f)] private float levelTwoAttackSpeedMultiplier = 1.15f;
    [SerializeField, Min(1f)] private float levelTwoDamageMultiplier = 1.25f;

    [Header("Level 3 Upgrade")]
    [SerializeField, Min(1f)] private float levelThreeRangeMultiplier = 1.55f;
    [SerializeField, Min(1f)] private float levelThreeAttackSpeedMultiplier = 1.35f;
    [SerializeField, Min(1f)] private float levelThreeDamageMultiplier = 1.65f;

    private readonly List<PathEnemy> enemiesInRange = new List<PathEnemy>();
    private readonly Dictionary<TowerProjectile, PathEnemy> activeProjectiles = new Dictionary<TowerProjectile, PathEnemy>(); // 1
    private readonly List<TowerProjectile> completedProjectiles = new List<TowerProjectile>(); // 1
    private float attackTimer; // 1
    private int currentLevel;
    private BuildSite buildSite;
    private SphereCollider rangeCollider;
    private Collider[] towerColliders;
    private TowerProjectile[] projectiles = Array.Empty<TowerProjectile>();

    public TowerData Data => towerData;

    public IReadOnlyList<PathEnemy> EnemiesInRange => enemiesInRange;

    public float TowerRange => (towerData?.TowerRange ?? 0f) * CurrentMultiplier(levelTwoRangeMultiplier, levelThreeRangeMultiplier);

    public int TowerLevel => currentLevel > 0 ? currentLevel : towerData?.TowerLevel ?? 1;

    public float TowerAttackSpeed => (towerData?.TowerAttackSpeed ?? 0f) * CurrentMultiplier(levelTwoAttackSpeedMultiplier, levelThreeAttackSpeedMultiplier);

    public float TowerDamage => (towerData?.TowerDamage ?? 0f) * CurrentMultiplier(levelTwoDamageMultiplier, levelThreeDamageMultiplier);

    public int BuildCost => this switch
    {
        BomberTower => 120,
        MageTower => 90,
        _ => 60
    };

    public int UpgradeCost => TowerLevel switch
    {
        1 when this is BomberTower => 95,
        1 when this is MageTower => 70,
        1 => 45,
        2 when this is BomberTower => 145,
        2 when this is MageTower => 110,
        2 => 75,
        _ => 0
    };

    public int SellValue => Mathf.RoundToInt(BuildCost * 0.75f);

    public bool CanUpgrade => TowerLevel < 3;

    public TowerData.TowerDamageType DamageType => towerData?.DamageType ?? TowerData.TowerDamageType.Physical;

    public event Action<TowerBase> Upgraded;
    public SphereCollider RangeCollider
    {
        get
        {
            CacheComponents();
            return rangeCollider;
        }
    }

    public void ConfigureRuntime(TowerData data, Transform pool)
    {
        towerData = data;
        projectilePool = pool;
        currentLevel = Mathf.Clamp(towerData?.TowerLevel ?? 1, 1, 3);
    }

    /// <summary>Alt kulelerin kendi projectile hızını ortak fırlatma sistemine vermesini sağlar.</summary> // 1
    protected virtual float GetProjectileVelocity() => 10f; // 1

    protected virtual void Awake() // 1
    {
        CacheComponents();
        currentLevel = Mathf.Clamp(towerData?.TowerLevel ?? 1, 1, 3);
        SyncRangeCollider(); // 1
        InitializeProjectilePool(); // 1
    }

    public bool TryUpgrade(PlayerGold playerGold)
    {
        if (!CanUpgrade || playerGold == null || !playerGold.TrySpendGold(UpgradeCost)) return false;
        currentLevel++;
        SyncRangeCollider();
        Upgraded?.Invoke(this);
        return true;
    }

    public void InitializeConstruction(BuildSite owner) => buildSite = owner;

    public bool TrySell(PlayerGold playerGold)
    {
        if (buildSite == null || playerGold == null) return false;
        playerGold.AddGold(SellValue);
        buildSite.ClearTower(this);
        return true;
    }

    private float CurrentMultiplier(float levelTwoMultiplier, float levelThreeMultiplier) =>
        TowerLevel >= 3 ? levelThreeMultiplier : TowerLevel >= 2 ? levelTwoMultiplier : 1f;

    protected virtual void OnValidate()
    {
        CacheComponents();
        SyncRangeCollider();
    }

    protected virtual void OnEnable()
    {
        CacheComponents();
        foreach (Collider towerCollider in towerColliders) towerCollider.enabled = true;
        SyncRangeCollider();
    }

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

        projectiles = projectilePool.GetComponentsInChildren<TowerProjectile>(true);
        for (int i = 0; i < projectiles.Length; i++) // 1
        {
            TowerProjectile projectile = projectiles[i];
            if (projectile != null && !projectile.gameObject.activeSelf) projectile.Initialize(); // 1
        }
    }

    /// <summary>Havuzdaki boş projectile nesnesini hedefe doğru fırlatır.</summary> // 1
    private bool LaunchProjectile(PathEnemy target) // 1
    {
        TowerProjectile projectile = GetAvailableProjectile(); // 1
        if (projectile == null || target == null) return false; // 1

        float projectileSpeed = GetProjectileVelocity();
        Vector3 direction = GetInterceptDirection(target, projectileSpeed);
        if (direction == Vector3.zero) return false; // 1

        activeProjectiles[projectile] = target; // 1
        projectile.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(direction)); // 1
        projectile.gameObject.SetActive(true); // 1
        LaunchProjectileMotion(projectile, target, direction, projectileSpeed);
        return true; // 1
    }

    protected virtual void LaunchProjectileMotion(TowerProjectile projectile, PathEnemy target, Vector3 direction, float speed) =>
        projectile.Launch(this, direction, speed);

    private Vector3 GetInterceptDirection(PathEnemy target, float projectileSpeed)
    {
        Vector3 offset = target.transform.position - transform.position;
        NavMeshAgent targetAgent = target.Agent;
        if (targetAgent == null || !targetAgent.isActiveAndEnabled || !targetAgent.isOnNavMesh)
            return offset.normalized;

        // Lead a moving target while keeping the projectile's own constant velocity.
        Vector3 targetVelocity = targetAgent.velocity;
        float a = targetVelocity.sqrMagnitude - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(offset, targetVelocity);
        float c = offset.sqrMagnitude;
        float interceptTime = float.PositiveInfinity;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (b < -0.0001f) interceptTime = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                float root = Mathf.Sqrt(discriminant);
                float first = (-b - root) / (2f * a);
                float second = (-b + root) / (2f * a);
                if (first > 0f) interceptTime = first;
                if (second > 0f) interceptTime = Mathf.Min(interceptTime, second);
            }
        }
        if (!float.IsInfinity(interceptTime)) offset += targetVelocity * interceptTime;
        return offset.normalized;
    }

    /// <summary>Havuzda kullanılmayan ilk projectile nesnesini bulur.</summary> // 1
    private TowerProjectile GetAvailableProjectile() // 1
    {
        if (projectilePool == null) return null; // 1

        for (int i = 0; i < projectiles.Length; i++) // 1
        {
            TowerProjectile projectile = projectiles[i]; // 1
            if (projectile != null && !projectile.gameObject.activeSelf) return projectile; // 1
        }

        return null; // 1
    }

    /// <summary>Çarpan projectile nesnesini kendi havuzuna geri koyar.</summary> // 1
    public void ProjectileHit(TowerProjectile projectile, PathEnemy target) // 1
    {
        if (target == null || !activeProjectiles.TryGetValue(projectile, out PathEnemy currentTarget)
            || !CanHitEnemy(currentTarget, target)) return;
        Vector3 impactPosition = projectile.transform.position;
        RecycleProjectile(projectile); // 1
        ApplyProjectileDamage(target, impactPosition);
    }

    protected virtual bool CanHitEnemy(PathEnemy intendedTarget, PathEnemy hitEnemy) => intendedTarget == hitEnemy;

    protected virtual void ApplyProjectileDamage(PathEnemy target, Vector3 impactPosition) =>
        target?.Stats?.TakeDamage(TowerDamage, DamageType);

    public void ProjectileGroundImpact(TowerProjectile projectile)
    {
        if (projectile == null || !projectile.IsMortar || !activeProjectiles.ContainsKey(projectile)) return;
        Vector3 impactPosition = projectile.transform.position;
        RecycleProjectile(projectile);
        ApplyProjectileDamage(null, impactPosition);
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
            if (!activeProjectile.Key.IsMortar && (activeProjectile.Value == null || !activeProjectile.Value.isActiveAndEnabled))
                completedProjectiles.Add(activeProjectile.Key);
        }

        foreach (TowerProjectile projectile in completedProjectiles) // 1
        {
            RecycleProjectile(projectile); // 1
        }
    }

    private bool IsEnemyInRange(PathEnemy enemy)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || TowerRange <= 0f) return false;
        Enemy stats = enemy.Stats;
        if (stats != null && (stats.IsWaiting || stats.IsDead)) return false;

        Vector3 worldScale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        float worldRadius = rangeCollider.radius * largestScale;
        Vector3 worldCenter = transform.TransformPoint(rangeCollider.center);
        return (enemy.transform.position - worldCenter).sqrMagnitude <= worldRadius * worldRadius;
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

    protected virtual void OnDestroy()
    {
        if (towerData != null && (towerData.hideFlags & HideFlags.DontSave) != 0)
            Destroy(towerData);
    }

    private void SyncRangeCollider()
    {
        CacheComponents();
        if (rangeCollider == null) return;
        // Tower data defines the Inspector Radius; Unity applies the Transform scale.
        rangeCollider.radius = TowerRange;
        rangeCollider.isTrigger = true;
    }

    private void CacheComponents()
    {
        if (rangeCollider == null) TryGetComponent(out rangeCollider);
        if (towerColliders == null || towerColliders.Length == 0) towerColliders = GetComponents<Collider>();
    }
}
