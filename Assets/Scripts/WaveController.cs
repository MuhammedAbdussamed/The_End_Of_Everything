using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class WaveController : MonoBehaviour
{
    public enum WaveState { Preparing, Active, Completed, Defeated }

    [SerializeField] private Enemy enemyTemplate;
    [SerializeField] private Transform waitingPoint;
    [SerializeField] private NavMeshSurface pathSurface;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerGold playerGold;
    [SerializeField] private int[] waveSizes = { 4, 8, 12 };
    [SerializeField] private int[] largeEnemiesPerWave = { 0, 2, 4 };
    [SerializeField, Min(0f)] private float preparationTime = 3f;
    [SerializeField, Min(0f)] private float releaseInterval = 0.6f;
    [SerializeField, Min(0.1f)] private float formationSpacing = 1.5f;

    private readonly List<Enemy> activeEnemies = new List<Enemy>();
    private Transform waypointRoot;
    private PathWaypoint[] sharedWaypoints;
    public Enemy EnemyTemplate => enemyTemplate;
    public Transform WaitingPoint => waitingPoint;
    public IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;
    public int CurrentWave { get; private set; }
    public int TotalWaves => waveSizes.Length;
    public int RemainingEnemies => activeEnemies.Count;
    public WaveState State { get; private set; } = WaveState.Preparing;
    public event Action Changed;

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Changed += OnHealthChanged;
    }

    private void OnHealthChanged()
    {
        if (!playerHealth.IsDefeated || State == WaveState.Completed || State == WaveState.Defeated) return;
        StopAllCoroutines();
        SetState(WaveState.Defeated);
        foreach (Enemy enemy in activeEnemies)
        {
            if (enemy == null) continue;
            enemy.SetWaiting(true);
            enemy.GetComponent<PathEnemy>().enabled = false;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
        foreach (TowerBase tower in FindObjectsByType<TowerBase>(FindObjectsSortMode.None))
            tower.enabled = false;
    }

    private IEnumerator Start()
    {
        if (enemyTemplate == null || waitingPoint == null || pathSurface == null || playerHealth == null || playerGold == null)
        {
            Debug.LogError("WaveController needs an enemy template, waiting point, path surface, player health and gold.", this);
            yield break;
        }
        enemyTemplate.gameObject.SetActive(false);
        if (pathSurface.navMeshData == null) pathSurface.BuildNavMesh();
        waypointRoot = GameObject.Find("Path Waypoints")?.transform;
        sharedWaypoints = waypointRoot != null ? waypointRoot.GetComponentsInChildren<PathWaypoint>() : null;
        WaitForSeconds preparationDelay = new WaitForSeconds(preparationTime);
        WaitForSeconds releaseDelay = new WaitForSeconds(releaseInterval);

        for (int waveIndex = 0; waveIndex < waveSizes.Length; waveIndex++)
        {
            if (playerHealth.IsDefeated) break;
            CurrentWave = waveIndex + 1;
            int count = Mathf.Max(0, waveSizes[waveIndex]);
            int largeCount = waveIndex < largeEnemiesPerWave.Length ? Mathf.Clamp(largeEnemiesPerWave[waveIndex], 0, count) : 0;
            Enemy[] wave = PrepareWave(count, largeCount);
            SetState(WaveState.Preparing);
            yield return preparationDelay;
            if (playerHealth.IsDefeated) break;

            SetState(WaveState.Active);
            for (int i = 0; i < wave.Length; i++)
            {
                if (playerHealth.IsDefeated) break;
                if (wave[i] != null && wave[i].gameObject.activeSelf) ReleaseEnemy(wave[i]);
                if (i + 1 < wave.Length) yield return releaseDelay;
            }
            while (activeEnemies.Count > 0 && !playerHealth.IsDefeated) yield return null;
        }
        SetState(playerHealth.IsDefeated ? WaveState.Defeated : WaveState.Completed);
    }

    private Enemy[] PrepareWave(int count, int largeCount)
    {
        var wave = new Enemy[count];
        int columns = Mathf.Min(5, count);
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3((i % columns - (columns - 1) * 0.5f) * formationSpacing,
                0f, -(i / columns) * formationSpacing);
            Enemy enemy = Instantiate(enemyTemplate, waitingPoint.TransformPoint(offset), waitingPoint.rotation, transform);
            enemy.gameObject.name = $"Enemy Wave {CurrentWave} - {i + 1}";
            PathEnemy pathEnemy = enemy.GetComponent<PathEnemy>();
            pathEnemy.ConfigureRoute(pathSurface, waypointRoot, sharedWaypoints);
            pathEnemy.enabled = false;
            enemy.Agent.enabled = false;
            if ((i + 1) * largeCount / count > i * largeCount / count)
            {
                enemy.ApplyLargeVariant();
                enemy.gameObject.name += " (Large)";
            }
            enemy.SetWaiting(true);
            enemy.Removed += OnEnemyRemoved;
            activeEnemies.Add(enemy);
            enemy.gameObject.SetActive(true);
            wave[i] = enemy;
        }
        return wave;
    }

    private void ReleaseEnemy(Enemy enemy)
    {
        NavMeshAgent agent = enemy.Agent;
        agent.enabled = true;
        enemy.SetWaiting(false);
        enemy.GetComponent<PathEnemy>().enabled = true;
    }

    private void OnEnemyRemoved(Enemy enemy)
    {
        if (!activeEnemies.Remove(enemy)) return;
        enemy.Removed -= OnEnemyRemoved;
        if (State != WaveState.Completed && State != WaveState.Defeated)
        {
            if (enemy.IsDead) playerGold.AddGold(enemy.GoldReward);
            else if (enemy.GetComponent<PathEnemy>().HasReachedDestination)
                playerHealth.TakeDamage(enemy.BaseDamage);
        }
        Changed?.Invoke();
        Destroy(enemy.gameObject);
    }

    private void SetState(WaveState state)
    {
        if (State == WaveState.Completed || State == WaveState.Defeated) return;
        State = state;
        Changed?.Invoke();
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
        StopAllCoroutines();
        foreach (Enemy enemy in activeEnemies)
            if (enemy != null) enemy.Removed -= OnEnemyRemoved;
        activeEnemies.Clear();
    }
}
