using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class EnemyCrowdTests
{
    [UnityTest]
    public IEnumerator SixtyEnemiesFinishTheRouteAndDespawnAtTheLastWaypoint()
    {
        yield return RunCrowd(false);
    }

    [UnityTest]
    public IEnumerator SixtyOverlappingEnemiesCanWalkFromTheSamePosition()
    {
        yield return RunCrowd(true);
    }

    private IEnumerator RunCrowd(bool overlapAtStart)
    {
        float previousTimeScale = Time.timeScale;
        SimulationMode previousSimulation = Physics.simulationMode;
        Random.State previousRandom = Random.state;
        Scene scene = default;
        try
        {
            Time.timeScale = 0f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Random.InitState(20260914);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SampleScene.unity");
#endif
            scene = SceneManager.GetActiveScene();
            EnemyTestScene.UseOneIndependentEnemy();
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                camera.enabled = false;
            yield return null;

            PathEnemy template = Object.FindFirstObjectByType<PathEnemy>();
            Assert.That(Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            foreach (TowerBase tower in Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None))
                tower.enabled = false; // This fixture isolates navigation from lethal tower damage.
            template.GetComponent<Enemy>().enabled = false;
            Vector3 spawn = template.transform.position;
            var crowd = new List<PathEnemy> { template };
            // Extra enemies exist only in this test, never in the saved single-enemy scene.
            for (int i = 1; i < 60; i++)
            {
                Vector3 candidate = spawn;
                if (!overlapAtStart) candidate += new Vector3((i % 10 - 4.5f) * 0.4f, 0f, (i / 10) * 0.4f);
                NavMeshAgent templateAgent = template.GetComponent<NavMeshAgent>();
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, templateAgent.areaMask))
                    candidate = hit.position + Vector3.up * templateAgent.baseOffset;
                PathEnemy clone = Object.Instantiate(template, candidate, template.transform.rotation);
                clone.name = "Crowd test enemy " + i;
                crowd.Add(clone);
            }
            yield return null;
            yield return null;
            PathEnemy[] enemies = crowd.ToArray();
            TowerBase[] towers = Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(60), "Generate a temporary crowd from the current scene enemy.");
            Assert.That(towers.Length, Is.EqualTo(3));
            Assert.That(enemies.Select(e => e.CurrentTarget).Distinct().Count(), Is.EqualTo(60));
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(Physics.GetIgnoreLayerCollision(enemyLayer, enemyLayer), Is.True,
                "Enemy hitboxes must not collide with each other.");
            Assert.That(Physics.GetIgnoreLayerCollision(enemyLayer, LayerMask.NameToLayer("Projectile")), Is.False);
            Assert.That(Physics.GetIgnoreLayerCollision(enemyLayer, LayerMask.NameToLayer("Default")), Is.False);
            foreach (PathEnemy enemy in enemies)
            {
                Assert.That(enemy.gameObject.layer, Is.EqualTo(enemyLayer));
                NavMeshAgent navAgent = enemy.GetComponent<NavMeshAgent>();
                Assert.That(navAgent.obstacleAvoidanceType, Is.EqualTo(ObstacleAvoidanceType.NoObstacleAvoidance));
                Assert.That(navAgent.speed, Is.EqualTo(5f).Within(0.001f));
                if (overlapAtStart)
                {
                    Assert.That(navAgent.Warp(enemies[0].transform.position), Is.True);
                    Assert.That(navAgent.SetDestination(enemy.CurrentTarget), Is.True);
                }
            }
            int[] previousIndices = new int[enemies.Length];
            var activeField = typeof(TowerBase).GetField("activeProjectiles", BindingFlags.Instance | BindingFlags.NonPublic);
            var poolField = typeof(TowerBase).GetField("projectilePool", BindingFlags.Instance | BindingFlags.NonPublic);
            float deadline = Time.realtimeSinceStartup + 45f;
            Time.timeScale = 3f;

            while (enemies.Any(e => !e.HasReachedDestination) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                for (int i = 0; i < enemies.Length; i++)
                {
                    PathEnemy enemy = enemies[i];
                    int index = enemy.CurrentWaypointIndex;
                    Assert.That(index, Is.InRange(previousIndices[i], previousIndices[i] + 1), enemy.name + " skipped/revisited a waypoint.");
                    previousIndices[i] = index;
                    if (enemy.HasReachedDestination)
                    {
                        Assert.That(index, Is.EqualTo(enemy.WaypointCount));
                        Vector3 finalOffset = enemy.transform.position - enemy.CurrentTarget;
                        finalOffset.y = 0f;
                        Assert.That(finalOffset.magnitude, Is.LessThanOrEqualTo(enemy.WaypointReachDistance + 0.1f));
                        Assert.That(enemy.gameObject.activeSelf, Is.False);
                        continue;
                    }
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    Assert.That(agent.isOnNavMesh, Is.True, enemy.name);
                    Assert.That(NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit hit, 1f, agent.areaMask), Is.True);
                    Vector2 offset = new Vector2(hit.position.x - enemy.transform.position.x, hit.position.z - enemy.transform.position.z);
                    Assert.That(offset.magnitude, Is.LessThan(0.1f), enemy.name + " left the path.");
                }

                foreach (TowerBase tower in towers)
                {
                    Assert.That(tower.EnemiesInRange.Distinct().Count(), Is.EqualTo(tower.EnemiesInRange.Count));
                    var active = (Dictionary<TowerProjectile, PathEnemy>)activeField.GetValue(tower);
                    Transform pool = (Transform)poolField.GetValue(tower);
                    Assert.That(active.Count, Is.LessThanOrEqualTo(pool.childCount));
                    foreach (var shot in active)
                    {
                        Assert.That(shot.Key, Is.Not.Null);
                        Assert.That(shot.Value, Is.Not.Null);
                        Assert.That(shot.Key.transform.parent, Is.SameAs(pool), "Projectile ownership crossed pools.");
                        Assert.That(shot.Key.gameObject.activeSelf, Is.True);
                        if (tower is BomberTower) Assert.That(shot.Key.GetComponent<Rigidbody>().useGravity, Is.True);
                        else Assert.That(shot.Key.GetComponent<Rigidbody>().linearVelocity.magnitude,
                            Is.EqualTo(tower is ArcherTower ? 18f : 8f).Within(0.01f));
                    }
                }
            }

            string unfinished = string.Join(", ", enemies.Where(e => !e.HasReachedDestination)
                .Select(e => e.name + " at waypoint " + e.CurrentWaypointIndex));
            Assert.That(enemies.Count(e => e.HasReachedDestination), Is.EqualTo(60), "Unfinished: " + unfinished);
            Assert.That(enemies.All(e => !e.gameObject.activeSelf), Is.True, "Finished enemies must not obstruct followers.");
            // Navigation is tested with towers disabled so enemies can finish the route.
            // TowerCombatTests verifies acquisition, hits and pooling with enemies placed in range.
            yield return new WaitForSeconds(5.2f);
            foreach (TowerBase tower in towers)
            {
                Assert.That(tower.EnemiesInRange, Is.Empty);
                Assert.That((Dictionary<TowerProjectile, PathEnemy>)activeField.GetValue(tower), Is.Empty);
            }
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Crowd test cleanup"));
                SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = previousTimeScale;
            Physics.simulationMode = previousSimulation;
            Random.state = previousRandom;
        }
    }
}
