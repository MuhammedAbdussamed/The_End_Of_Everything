using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A support tower that deploys three stationary guards. Its upgrade levels affect
/// guards only; the valid guard-placement radius never grows.
/// </summary>
public class CastleTower : TowerBase
{
    private const int GuardCount = 3;
    private const float BaseGuardHealth = 30f;
    private const float BaseGuardDamage = 10f;
    private const float GuardAttackSpeed = 1f;
    private const float MinimumGuardSpacing = 1.6f;

    [SerializeField, Min(0f)] private float guardRespawnDelay = 12.5f;

    private readonly List<GuardUnit> guards = new();
    private Material guardMaterial;
    private bool shuttingDown;

    public IReadOnlyList<GuardUnit> Guards => guards;
    public float GuardHealth => BaseGuardHealth * GuardMultiplier;
    public float GuardDamage => BaseGuardDamage * GuardMultiplier;
    public float GuardRespawnDelay => guardRespawnDelay;
    public override float TowerRange => towerData?.TowerRange ?? 0f;

    private float GuardMultiplier => Mathf.Pow(1.5f, TowerLevel - 1);

    protected override void Awake()
    {
        base.Awake();
        Upgraded += HandleUpgraded;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (guards.Count == 0) CreateGuards();
    }

    protected override void OnDestroy()
    {
        shuttingDown = true;
        StopAllCoroutines();
        Upgraded -= HandleUpgraded;
        foreach (GuardUnit guard in guards)
            if (guard != null) Destroy(guard.gameObject);
        guards.Clear();
        if (guardMaterial != null) Destroy(guardMaterial);
        base.OnDestroy();
    }

    public bool TrySetGuardPosition(GuardUnit guard, Vector3 requestedPosition, bool teleport = false)
    {
        if (guard == null || !guards.Contains(guard) || !guard.isActiveAndEnabled) return false;

        Vector3 center = transform.position;
        Vector3 offset = requestedPosition - center;
        offset.y = 0f;
        float usableRadius = Mathf.Max(0.5f, TowerRange - 0.8f);
        if (offset.sqrMagnitude > usableRadius * usableRadius)
        {
            Vector3 clamped = center + offset.normalized * usableRadius;
            requestedPosition.x = clamped.x;
            requestedPosition.z = clamped.z;
        }

        // A click beside the route snaps to the nearest walkable path point, but never beyond the castle's radius.
        if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit navHit, usableRadius, NavMesh.AllAreas)) return false;
        Vector3 placement = navHit.position;
        Vector3 placementOffset = placement - center;
        placementOffset.y = 0f;
        if (placementOffset.sqrMagnitude > usableRadius * usableRadius) return false;
        placement = FindUnoccupiedPosition(guard, placement, center, usableRadius);
        return guard.SetGuardPosition(placement, teleport);
    }

    public bool TrySetGuardsFormation(Vector3 requestedCenter, bool teleport = false)
    {
        if (guards.Count == 0) return false;
        Vector3[] offsets =
        {
            new Vector3(0f, 0f, 1.5f),
            new Vector3(-1.3f, 0f, -0.8f),
            new Vector3(1.3f, 0f, -0.8f)
        };
        bool movedAny = false;
        for (int index = 0; index < guards.Count && index < offsets.Length; index++)
        {
            GuardUnit guard = guards[index];
            if (guard != null && guard.isActiveAndEnabled)
                movedAny |= TrySetGuardPosition(guard, requestedCenter + offsets[index], teleport);
        }
        return movedAny;
    }

    public void ShowGuardBubbles(bool visible)
    {
        foreach (GuardUnit guard in guards)
            if (guard != null) guard.ShowSelectionBubble(visible);
    }

    public void NotifyGuardDied(GuardUnit guard, Vector3 assignedPosition)
    {
        if (shuttingDown || guard == null) return;
        int slot = guards.IndexOf(guard);
        if (slot < 0) return;
        guards[slot] = null;
        StartCoroutine(RespawnGuard(slot, assignedPosition));
    }

    private void CreateGuards()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            guardMaterial = new Material(shader)
            {
                color = new Color(0.24f, 0.42f, 0.72f),
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };

        for (int index = 0; index < GuardCount; index++)
        {
            float angle = index * Mathf.PI * 2f / GuardCount;
            Vector3 requested = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
            guards.Add(CreateGuard(index, requested));
            GuardUnit guard = guards[index];
            TrySetGuardPosition(guard, requested, true);
        }

        Vector3 formationCenter = transform.position;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit centerHit, TowerRange, NavMesh.AllAreas))
            formationCenter = centerHit.position;
        TrySetGuardsFormation(formationCenter, true);
    }

    private GuardUnit CreateGuard(int slot, Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit spawnHit, Mathf.Max(1f, TowerRange), NavMesh.AllAreas))
            position = spawnHit.position;
        GameObject guardObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        guardObject.name = $"Kale Askeri {slot + 1}";
        guardObject.transform.position = position;
        guardObject.transform.localScale = new Vector3(0.72f, 1f, 0.72f);
        Renderer renderer = guardObject.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = guardMaterial;
        GuardUnit guard = guardObject.AddComponent<GuardUnit>();
        guard.Configure(this, GuardHealth, GuardDamage, GuardAttackSpeed);
        return guard;
    }

    private IEnumerator RespawnGuard(int slot, Vector3 assignedPosition)
    {
        if (guardRespawnDelay > 0f) yield return new WaitForSeconds(guardRespawnDelay);
        if (shuttingDown || !isActiveAndEnabled || slot < 0 || slot >= guards.Count || guards[slot] != null)
            yield break;

        Vector3 spawnPosition = transform.position;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit spawnHit, Mathf.Max(1f, TowerRange), NavMesh.AllAreas))
            spawnPosition = spawnHit.position;

        GuardUnit replacement = CreateGuard(slot, spawnPosition);
        guards[slot] = replacement;
        TrySetGuardPosition(replacement, spawnPosition, true);
        TrySetGuardPosition(replacement, assignedPosition);
    }

    private Vector3 FindUnoccupiedPosition(GuardUnit movingGuard, Vector3 candidate, Vector3 center, float radius)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            bool overlaps = false;
            foreach (GuardUnit other in guards)
            {
                if (other == null || other == movingGuard || !other.isActiveAndEnabled) continue;
                Vector3 separation = candidate - other.transform.position;
                separation.y = 0f;
                if (separation.sqrMagnitude < MinimumGuardSpacing * MinimumGuardSpacing)
                {
                    overlaps = true;
                    break;
                }
            }
            if (!overlaps) return candidate;

            float angle = attempt * Mathf.PI * 2f / 12f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * MinimumGuardSpacing;
            Vector3 trial = candidate + offset;
            Vector3 radial = trial - center;
            radial.y = 0f;
            if (radial.sqrMagnitude > radius * radius)
            {
                Vector3 clamped = center + radial.normalized * radius;
                trial.x = clamped.x;
                trial.z = clamped.z;
            }
            if (NavMesh.SamplePosition(trial, out NavMeshHit hit, 1f, NavMesh.AllAreas)) candidate = hit.position;
        }
        return candidate;
    }

    private void HandleUpgraded(TowerBase tower)
    {
        foreach (GuardUnit guard in guards)
            if (guard != null) guard.Configure(this, GuardHealth, GuardDamage, GuardAttackSpeed);
    }
}
