using UnityEngine;
using UnityEngine.AI;

/// <summary>A castle defender that chases nearby enemies and returns to its guard point.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class GuardUnit : MonoBehaviour
{
    private const float DetectionRadius = 4.25f;
    private const float AttackRadius = 1.25f;
    private const float GuardPositionStoppingDistance = 0.1f;
    private static readonly Collider[] NearbyColliders = new Collider[32];

    private float maxHealth;
    private float currentHealth;
    private float damage;
    private float attackSpeed;
    private float attackTimer;
    private GameObject selectionBubble;
    private Material bubbleMaterial;
    private NavMeshAgent agent;
    private PathEnemy target;
    private Vector3 guardPosition;
    private bool hasGuardPosition;
    private float nextTargetSearchTime;

    public CastleTower Castle { get; private set; }
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float Damage => damage;
    public bool IsAlive => currentHealth > 0f;
    public Vector3 GuardPosition => hasGuardPosition ? guardPosition : transform.position;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = 3.75f;
        agent.acceleration = 18f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = GuardPositionStoppingDistance;
        agent.radius = 0.32f;
        agent.height = 1.8f;
        agent.baseOffset = 1f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        CreateSelectionBubble();
    }

    private void OnDestroy()
    {
        if (bubbleMaterial != null) Destroy(bubbleMaterial);
    }

    public void Configure(CastleTower owner, float health, float attackDamage, float attacksPerSecond)
    {
        float healthRatio = maxHealth > 0f ? currentHealth / maxHealth : 1f;
        Castle = owner;
        maxHealth = health;
        currentHealth = Mathf.Clamp(maxHealth * healthRatio, 0f, maxHealth);
        if (currentHealth <= 0f) currentHealth = maxHealth;
        damage = attackDamage;
        attackSpeed = attacksPerSecond;
    }

    public bool SetGuardPosition(Vector3 position, bool teleport = false)
    {
        target = null;
        if (agent == null || !agent.isActiveAndEnabled) return false;

        if (!agent.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit currentHit, 2f, NavMesh.AllAreas)
                && !NavMesh.SamplePosition(position, out currentHit, 2f, NavMesh.AllAreas)) return false;
            transform.position = currentHit.position;
            if (!agent.Warp(currentHit.position)) return false;
        }

        if (teleport)
        {
            if (!agent.Warp(position)) return false;
            guardPosition = position;
            hasGuardPosition = true;
            agent.isStopped = true;
            agent.ResetPath();
            return true;
        }

        var path = new NavMeshPath();
        if (!agent.CalculatePath(position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        guardPosition = position;
        hasGuardPosition = true;
        agent.stoppingDistance = GuardPositionStoppingDistance;
        agent.isStopped = false;
        return agent.SetPath(path);
    }

    public void ShowSelectionBubble(bool visible)
    {
        if (selectionBubble != null) selectionBubble.SetActive(visible && IsAlive);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth > 0f) return;

        ShowSelectionBubble(false);
        CastleTower owner = Castle;
        Castle = null;
        if (owner != null) owner.NotifyGuardDied(this, GuardPosition);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!IsAlive || attackSpeed <= 0f) return;
        if (!IsValidTarget(target)) target = null;
        if (target == null && Time.time >= nextTargetSearchTime)
        {
            nextTargetSearchTime = Time.time + 0.2f;
            target = FindNearestEnemy();
        }

        if (target != null)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > AttackRadius * AttackRadius)
            {
                MoveTo(target.transform.position, AttackRadius * 0.8f);
                return;
            }

            StopMoving();
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;
            target.Stats?.TakeDamage(damage, TowerData.TowerDamageType.Physical);
            attackTimer = 1f / attackSpeed;
            return;
        }

        if (hasGuardPosition && HorizontalSqrDistance(transform.position, guardPosition) > 0.12f)
            MoveTo(guardPosition, GuardPositionStoppingDistance);
        else
            StopMoving();
    }

    private PathEnemy FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, DetectionRadius, NearbyColliders, ~0, QueryTriggerInteraction.Ignore);
        PathEnemy closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            PathEnemy enemy = NearbyColliders[index].GetComponentInParent<PathEnemy>();
            if (!IsValidTarget(enemy)) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private bool IsValidTarget(PathEnemy enemy)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.Stats == null || enemy.Stats.IsDead || enemy.Stats.IsWaiting)
            return false;
        if (Castle == null) return true;
        return HorizontalSqrDistance(enemy.transform.position, Castle.transform.position)
            <= Castle.TowerRange * Castle.TowerRange;
    }

    private void MoveTo(Vector3 position, float stoppingDistance)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(position);
    }

    private void StopMoving()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private static float HorizontalSqrDistance(Vector3 first, Vector3 second)
    {
        Vector3 offset = first - second;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private void CreateSelectionBubble()
    {
        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = "Seçim Baloncuğu";
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        bubble.transform.localScale = Vector3.one * 0.85f;
        SphereCollider bubbleCollider = bubble.GetComponent<SphereCollider>();
        if (bubbleCollider != null) bubbleCollider.isTrigger = true;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            bubbleMaterial = new Material(shader)
            {
                color = new Color(1f, 0.82f, 0.22f),
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };
            Renderer renderer = bubble.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = bubbleMaterial;
        }
        selectionBubble = bubble;
        selectionBubble.SetActive(false);
    }
}
