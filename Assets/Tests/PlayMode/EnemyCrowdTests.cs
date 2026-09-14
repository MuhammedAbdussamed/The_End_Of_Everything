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
    public IEnumerator TwentyEnemiesFinishTheRouteWhileBothTowersKeepFiring()
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
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                camera.enabled = false;
            yield return null;

            PathEnemy[] enemies = Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.InstanceID);
            TowerBase[] towers = Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(20), "Run the actual scene formation.");
            Assert.That(towers.Length, Is.EqualTo(2));
            Assert.That(enemies.Select(e => e.CurrentTarget).Distinct().Count(), Is.EqualTo(20));
            int[] previousIndices = new int[enemies.Length];
            var targetsSeen = towers.ToDictionary(t => t, t => new HashSet<PathEnemy>());
            var shotsSeen = towers.ToDictionary(t => t, t => new HashSet<TowerProjectile>());
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
                    if (enemy.HasReachedDestination) continue;
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
                        float speed = tower is ArcherTower ? 18f : 8f;
                        Assert.That(shot.Key.GetComponent<Rigidbody>().linearVelocity.magnitude,
                            Is.EqualTo(speed).Within(0.01f));
                        targetsSeen[tower].Add(shot.Value);
                        shotsSeen[tower].Add(shot.Key);
                    }
                }
            }

            string unfinished = string.Join(", ", enemies.Where(e => !e.HasReachedDestination)
                .Select(e => e.name + " at waypoint " + e.CurrentWaypointIndex));
            Assert.That(enemies.Count(e => e.HasReachedDestination), Is.EqualTo(20), "Unfinished: " + unfinished);
            Assert.That(enemies.All(e => !e.gameObject.activeSelf), Is.True, "Finished enemies must not obstruct followers.");
            foreach (TowerBase tower in towers)
            {
                Assert.That(targetsSeen[tower].Count, Is.GreaterThan(1), tower.name + " failed to switch targets.");
                Assert.That(shotsSeen[tower].Count, Is.GreaterThan(1), tower.name + " failed to use its projectile pool.");
            }
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
