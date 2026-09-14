using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float movementSpeed = 5f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0f)] private float baseDamage = 10f;
    [SerializeField, Min(0)] private int goldReward = 10;
    [SerializeField, Min(0.01f)] private float attackSpeed = 1f;
    [SerializeField] private TowerData.TowerDamageType damageType = TowerData.TowerDamageType.Physical;

    [Header("Damage Feedback")]
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float hitFlashStrength = 0.35f;

    private sealed class HitRenderer
    {
        public Renderer Renderer;
        public int ColorProperty;
        public Color OriginalColor;
        public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
        public readonly MaterialPropertyBlock Flash = new MaterialPropertyBlock();
    }
    private HitRenderer[] hitRenderers;
    private float hitFlashRemaining;

    [Header("Runtime Health")]
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float MovementSpeed => movementSpeed;
    public float AttackDamage => attackDamage;
    public float BaseDamage => baseDamage;
    public int GoldReward => goldReward;
    public float AttackSpeed => attackSpeed;
    public TowerData.TowerDamageType DamageType => damageType;
    public TowerData.TowerDamageType LastDamageType { get; private set; }
    public bool IsDead => currentHealth <= 0f;
    public bool IsWaiting { get; private set; }
    public bool IsLarge { get; private set; }
    public event Action<Enemy> Removed;

    private void Awake()
    {
        currentHealth = maxHealth;
        SyncMovementSpeed();
        CacheHitRenderers();
    }

    private void OnDisable()
    {
        RestoreHitColors();
        Removed?.Invoke(this);
    }

    private void Update()
    {
        if (hitFlashRemaining <= 0f) return;
        hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime);
        if (hitFlashRemaining == 0f) RestoreHitColors();
        else ApplyHitColors(hitFlashStrength * hitFlashRemaining / hitFlashDuration);
    }

    private void CacheHitRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        hitRenderers = new HitRenderer[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            if (material == null) continue;
            int property = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            if (!material.HasProperty(property)) continue;
            var entry = new HitRenderer { Renderer = renderers[i], ColorProperty = property };
            entry.Renderer.GetPropertyBlock(entry.Original);
            entry.Renderer.GetPropertyBlock(entry.Flash);
            entry.OriginalColor = entry.Original.HasColor(property) ? entry.Original.GetColor(property) : material.GetColor(property);
            hitRenderers[i] = entry;
        }
    }

    private void ApplyHitColors(float strength)
    {
        if (hitRenderers == null) return;
        foreach (HitRenderer entry in hitRenderers)
        {
            if (entry == null || entry.Renderer == null) continue;
            Color flash = Color.Lerp(entry.OriginalColor, Color.white, strength);
            flash.a = entry.OriginalColor.a;
            entry.Flash.SetColor(entry.ColorProperty, flash);
            entry.Renderer.SetPropertyBlock(entry.Flash);
        }
    }

    private void RestoreHitColors()
    {
        hitFlashRemaining = 0f;
        if (hitRenderers == null) return;
        foreach (HitRenderer entry in hitRenderers)
            if (entry != null && entry.Renderer != null)
                entry.Renderer.SetPropertyBlock(entry.Original.isEmpty ? null : entry.Original);
    }

    public void SetWaiting(bool waiting) => IsWaiting = waiting;

    public void ApplyLargeVariant()
    {
        if (IsLarge) return;
        IsLarge = true;
        maxHealth *= 2f;
        currentHealth = maxHealth;
        movementSpeed *= 0.6f;
        baseDamage *= 2f;
        goldReward *= 2;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        float previousWorldOffset = agent.baseOffset * transform.lossyScale.y;
        transform.localScale *= 1.5f;
        // The agent already applies Transform scale; only lift the inactive staging pose here.
        transform.position += Vector3.up * (agent.baseOffset * transform.lossyScale.y - previousWorldOffset);
        SyncMovementSpeed();
    }

    private void OnValidate() => SyncMovementSpeed();

    private void SyncMovementSpeed()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = movementSpeed;
    }

    public void TakeDamage(float amount, TowerData.TowerDamageType incomingDamageType)
    {
        if (!isActiveAndEnabled || IsDead || IsWaiting || amount <= 0f) return;

        LastDamageType = incomingDamageType;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        hitFlashRemaining = hitFlashDuration;
        ApplyHitColors(hitFlashStrength);
        if (!IsDead) return;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        gameObject.SetActive(false);
    }

}
