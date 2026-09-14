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

public class PathEnemyWaypointTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private Scene testScene;
    private PathEnemy enemy;
    private NavMeshAgent agent;
    private PathWaypoint[] waypoints;
    private float previousTimeScale;
    private SimulationMode previousSimulationMode;
    private Random.State previousRandomState;

    [UnitySetUp]
    public IEnumerator LoadSampleScene()
    {
        previousTimeScale = Time.timeScale;
        previousSimulationMode = Physics.simulationMode;
        previousRandomState = Random.state;
        // Start and path calculation still run while paused, so no waypoint is passed
        // before the test has begun observing real NavMeshAgent movement.
        Time.timeScale = 0f;
        Physics.simulationMode = SimulationMode.FixedUpdate;
        Random.InitState(14092026);
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        testScene = SceneManager.GetActiveScene();
        EnemyTestScene.UseOneIndependentEnemy();
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.enabled = false;
        foreach (TowerBase tower in Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None))
            tower.enabled = false;
        yield return null;

        enemy = Object.FindFirstObjectByType<PathEnemy>();
        Assert.That(enemy, Is.Not.Null, "SampleScene must contain the enemy cube.");
        // Route unit scenarios use one enemy; the crowd test covers all scene enemies.
        foreach (PathEnemy other in Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None))
            if (other != enemy) other.gameObject.SetActive(false);
        agent = enemy.GetComponent<NavMeshAgent>();
        GameObject waypointRoot = GameObject.Find("Path Waypoints");
        Assert.That(waypointRoot, Is.Not.Null, "The route must be editable through scene waypoint objects.");
        waypoints = waypointRoot.GetComponentsInChildren<PathWaypoint>();
        Assert.That(agent.isOnNavMesh, Is.True);
        Assert.That(enemy.CurrentWaypointIndex, Is.Zero, "The enemy must begin with the first waypoint.");
    }

    [UnityTearDown]
    public IEnumerator UnloadSampleScene()
    {
        try
        {
            if (testScene.IsValid() && testScene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Waypoint test cleanup"));
                yield return SceneManager.UnloadSceneAsync(testScene);
            }
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Physics.simulationMode = previousSimulationMode;
            Random.state = previousRandomState;
        }
    }

    [Test]
    public void SceneContainsOrderedEmptyWaypointsOnThePath()
    {
        Assert.That(waypoints.Length, Is.GreaterThan(1));
        Assert.That(enemy.WaypointCount, Is.EqualTo(waypoints.Length));
        Assert.That(waypoints[0].transform.parent.childCount, Is.EqualTo(waypoints.Length));

        Vector3 previous = ProjectToPath(agent.transform.position);
        foreach (PathWaypoint waypoint in waypoints)
        {
            Assert.That(waypoint.GetComponent<Renderer>(), Is.Null, waypoint.name);
            Assert.That(waypoint.GetComponent<Collider>(), Is.Null, waypoint.name);
            Assert.That(waypoint.Radius, Is.GreaterThan(0f), waypoint.name);
            Vector3 point = ProjectToPath(waypoint.transform.position);
            Assert.That(FlatDistance(point, waypoint.transform.position), Is.LessThan(0.05f),
                waypoint.name + " must be placed on the walkable path.");
            AssertConnected(previous, point, waypoint.name);
            previous = point;
        }
    }

    [UnityTest]
    public IEnumerator MultipleEnemiesChooseIndependentTargetsWithoutRebuildingSharedNavMesh()
    {
        Component surface = GetSurface(enemy);
        Object sharedNavMeshData = GetNavMeshData(surface);
        Assert.That(sharedNavMeshData, Is.Not.Null);
        Vector3 originalTarget = enemy.CurrentTarget;
        var enemies = new List<PathEnemy> { enemy };
        for (int index = 0; index < 3; index++)
        {
            PathEnemy clone = Object.Instantiate(enemy, enemy.transform.position, Quaternion.identity);
            clone.name = "Waypoint test enemy " + (index + 2);
            enemies.Add(clone);
        }
        yield return null;
        yield return null;

        Assert.That(GetNavMeshData(surface), Is.SameAs(sharedNavMeshData),
            "Spawning enemies must reuse the already built scene NavMesh.");
        Assert.That(enemy.CurrentTarget, Is.EqualTo(originalTarget),
            "Another enemy must not overwrite this enemy's chosen target.");
        foreach (PathEnemy candidate in enemies)
        {
            Assert.That(GetSurface(candidate), Is.SameAs(surface));
            Assert.That(candidate.WaypointCount, Is.EqualTo(waypoints.Length));
            Assert.That(candidate.CurrentWaypointIndex, Is.Zero);
            Assert.That(candidate.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
            Assert.That(FlatDistance(candidate.CurrentTarget, waypoints[0].transform.position),
                Is.LessThanOrEqualTo(waypoints[0].Radius + 0.05f), candidate.name);
        }
        Assert.That(enemies.Select(candidate => candidate.CurrentTarget).Distinct().Count(),
            Is.EqualTo(enemies.Count), "Each enemy needs its own random point around the shared waypoint.");
    }

    [UnityTest]
    public IEnumerator ChosenTargetStaysFixedWhileEnemyWalksTowardIt()
    {
        Vector3 initialPosition = enemy.transform.position;
        Vector3 target = enemy.CurrentTarget;
        int waypointIndex = enemy.CurrentWaypointIndex;
        float deadline = Time.realtimeSinceStartup + 10f;
        Time.timeScale = 1f;
        int observedFrames = 0;
        while (FlatDistance(initialPosition, enemy.transform.position) < 0.4f &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            observedFrames++;
            Assert.That(enemy.CurrentWaypointIndex, Is.EqualTo(waypointIndex));
            Assert.That(enemy.CurrentTarget, Is.EqualTo(target),
                "Randomizing every Update makes enemies continually change direction.");
            Assert.That(agent.autoBraking, Is.False, "Intermediate points must allow continuous movement.");
        }
        Assert.That(observedFrames, Is.GreaterThan(0));
        Assert.That(FlatDistance(initialPosition, enemy.transform.position), Is.GreaterThanOrEqualTo(0.4f),
            "The real NavMeshAgent must move toward its stable target.");
    }

    [UnityTest]
    public IEnumerator EnemyTraversesEveryWaypointInOrderAndDisappearsAtTheLastWaypoint()
    {
        Assert.That(agent.speed, Is.EqualTo(5f).Within(0.001f), "Use the speed configured on the NavMeshAgent.");
        // Completion must depend on the last waypoint, even without a separate endpoint object.
        Object.Destroy(GameObject.Find("Path End"));
        var visitedIndices = new List<int> { enemy.CurrentWaypointIndex };
        int previousIndex = enemy.CurrentWaypointIndex;
        Vector3 previousTarget = enemy.CurrentTarget;
        float deadline = Time.realtimeSinceStartup + 40f;
        Time.timeScale = 4f;

        while (!enemy.HasReachedDestination && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (!enemy.HasReachedDestination)
                Assert.That(agent.isOnNavMesh, Is.True, "The enemy left the walkable path.");
            Vector3 projected = ProjectToPath(enemy.transform.position);
            Assert.That(FlatDistance(projected, enemy.transform.position), Is.LessThan(0.1f),
                "Movement must stay on the path through its corner.");

            int currentIndex = enemy.CurrentWaypointIndex;
            if (currentIndex != previousIndex)
            {
                Assert.That(currentIndex, Is.EqualTo(previousIndex + 1), "A route point was skipped or revisited.");
                Assert.That(FlatDistance(enemy.transform.position, previousTarget), Is.LessThan(1.5f),
                    "The enemy advanced without approaching its sampled waypoint.");
                if (currentIndex < enemy.WaypointCount)
                    visitedIndices.Add(currentIndex);
                previousIndex = currentIndex;
                previousTarget = enemy.CurrentTarget;
            }
            else
            {
                Assert.That(enemy.CurrentTarget, Is.EqualTo(previousTarget),
                    "The sampled target must remain fixed until the waypoint is reached.");
            }
            if (currentIndex < enemy.WaypointCount)
                Assert.That(agent.autoBraking, Is.EqualTo(currentIndex == enemy.WaypointCount - 1));
        }

        Assert.That(enemy.HasReachedDestination, Is.True, "Enemy failed to finish the route within 40 real seconds.");
        Assert.That(enemy.gameObject.activeSelf, Is.False, "Finished enemies must clear the route.");
        Assert.That(visitedIndices, Is.EqualTo(Enumerable.Range(0, waypoints.Length).ToArray()));
        Assert.That(agent.autoBraking, Is.True, "Only the final waypoint should brake the agent.");
        Assert.That(enemy.CurrentWaypointIndex, Is.EqualTo(waypoints.Length));
        Assert.That(FlatDistance(enemy.CurrentTarget, waypoints.Last().transform.position),
            Is.LessThanOrEqualTo(waypoints.Last().Radius + 0.05f));
        Assert.That(FlatDistance(enemy.transform.position, enemy.CurrentTarget),
            Is.LessThanOrEqualTo(enemy.WaypointReachDistance + 0.1f));
        Vector3 stoppedPosition = enemy.transform.position;
        yield return new WaitForSeconds(0.5f);
        Assert.That(FlatDistance(stoppedPosition, enemy.transform.position), Is.LessThan(0.1f),
            "The enemy must remain inactive at the final waypoint.");
    }

    private Vector3 ProjectToPath(Vector3 point)
    {
        Assert.That(NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, agent.areaMask), Is.True,
            "No walkable path near " + point);
        return hit.position;
    }

    private void AssertConnected(Vector3 from, Vector3 to, string label)
    {
        var path = new NavMeshPath();
        Assert.That(NavMesh.CalculatePath(from, to, agent.areaMask, path), Is.True, label);
        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), label + " is unreachable.");
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private static Component GetSurface(PathEnemy candidate) => (Component)typeof(PathEnemy)
        .GetField("pathSurface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(candidate);

    private static Object GetNavMeshData(Component surface) =>
        (Object)surface.GetType().GetProperty("navMeshData").GetValue(surface);
}
