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

public class TowerCombatTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const float PhysicsStep = 0.02f;
    private static readonly Vector3 OutsideAllRanges = new Vector3(100f, 1.5f, 100f);
    private readonly List<string> kinematicWarnings = new List<string>();
    private SimulationMode previousSimulationMode;
    private float previousTimeScale;
    private Scene testScene;
    private PathEnemy enemy;
    private Rigidbody enemyBody;
    private TowerBase[] towers;

    [UnitySetUp]
    public IEnumerator LoadSampleScene()
    {
        kinematicWarnings.Clear();
        Application.logMessageReceived += CaptureKinematicWarnings;
        previousSimulationMode = Physics.simulationMode;
        previousTimeScale = Time.timeScale;
        Physics.simulationMode = SimulationMode.Script;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        testScene = SceneManager.GetActiveScene();
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.enabled = false;
        // Let PathEnemy.Start build its real NavMesh before taking control of movement.
        yield return null;
        enemy = Object.FindFirstObjectByType<PathEnemy>();
        Assert.That(enemy, Is.Not.Null, "SampleScene must contain the enemy cube.");
        // Keep focused combat cases isolated; the crowd test exercises the full formation.
        foreach (PathEnemy other in Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None))
            if (other != enemy) other.gameObject.SetActive(false);
        enemy.GetComponent<NavMeshAgent>().enabled = false;
        enemyBody = enemy.GetComponent<Rigidbody>();
        Assert.That(enemyBody, Is.Not.Null);
        Assert.That(enemyBody.isKinematic, Is.True, "The moving enemy needs its trigger Rigidbody.");
        Assert.That(enemy.isActiveAndEnabled, Is.True);
        towers = Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
        Assert.That(towers.Length, Is.EqualTo(2));
        MoveEnemy(OutsideAllRanges);
        SimulateStep();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator UnloadSampleScene()
    {
        try
        {
            if (testScene.IsValid() && testScene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Tower combat test cleanup"));
                yield return SceneManager.UnloadSceneAsync(testScene);
            }
        }
        finally
        {
            Physics.simulationMode = previousSimulationMode;
            Time.timeScale = previousTimeScale;
            Application.logMessageReceived -= CaptureKinematicWarnings;
        }
        Assert.That(kinematicWarnings, Is.Empty, string.Join("\n", kinematicWarnings));
    }

    [Test]
    public void SceneTowersHaveIndependentPoolsAndCorrectWorldRange()
    {
        var pools = new HashSet<Transform>();
        foreach (TowerBase tower in towers)
        {
            Assert.That(tower.Data, Is.Not.Null, tower.name);
            Transform pool = GetPool(tower);
            Assert.That(pool, Is.Not.Null, tower.name + " needs its own projectile pool.");
            Assert.That(pools.Add(pool), Is.True, tower.name + " must not share another tower's pool.");
            Assert.That(GetProjectiles(tower).Length, Is.GreaterThan(0), tower.name);
            Assert.That(GetProjectiles(tower).All(projectile => !projectile.gameObject.activeSelf), Is.True);

            SphereCollider rangeCollider = tower.GetComponent<SphereCollider>();
            Vector3 scale = tower.transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Assert.That(rangeCollider.isTrigger, Is.True, tower.name);
            Assert.That(rangeCollider.radius * largestScale,
                Is.EqualTo(tower.TowerRange).Within(0.001f), tower.name + " world trigger radius");
        }
    }

    [UnityTest]
    public IEnumerator EnemyOutsideAllRangesDoesNotCauseFiring()
    {
        for (int frame = 0; frame < 5; frame++)
        {
            SimulateStep();
            yield return null;
            foreach (TowerBase tower in towers)
            {
                Assert.That(tower.EnemiesInRange, Is.Empty, tower.name);
                Assert.That(GetActiveProjectile(tower), Is.Null, tower.name);
            }
        }
    }

    [UnityTest]
    public IEnumerator ArcherAcquiresEnemyAndMovesProjectileAt18UnitsPerSecond()
    {
        yield return VerifyLaunchAndMotion(towers.Single(tower => tower is ArcherTower), 18f);
    }

    [UnityTest]
    public IEnumerator MageAcquiresEnemyAndMovesProjectileAt8UnitsPerSecond()
    {
        yield return VerifyLaunchAndMotion(towers.Single(tower => tower is MageTower), 8f);
    }

    [UnityTest]
    public IEnumerator OverlappingEnemyIsAcquiredWhenItsCenterEntersRangeWithoutReentry()
    {
        TowerRangeContactProbe probe = enemy.gameObject.AddComponent<TowerRangeContactProbe>();
        foreach (TowerBase tower in towers)
        {
            yield return IsolateTower(tower);
            probe.WatchedCollider = tower.GetComponent<SphereCollider>();
            probe.EnterCount = 0;
            probe.ExitCount = 0;

            // The cube is one unit wide: its near face overlaps while its center is out of range.
            MoveEnemy(tower.transform.position + Vector3.right * (tower.TowerRange + 0.25f));
            SimulateStep();
            yield return null;
            SimulateStep();
            yield return null;
            Assert.That(probe.EnterCount, Is.EqualTo(1), tower.name + " must already have a trigger contact.");
            Assert.That(tower.EnemiesInRange, Is.Empty, tower.name);
            Assert.That(GetActiveProjectile(tower), Is.Null, tower.name + " fired outside its data range.");

            MoveEnemy(tower.transform.position + Vector3.right * (tower.TowerRange - 0.25f));
            SimulateStep();
            yield return WaitForLaunch(tower);
            Assert.That(tower.EnemiesInRange, Does.Contain(enemy), tower.name);
            Assert.That(probe.EnterCount, Is.EqualTo(1), "Acquisition must use the existing overlap.");
            Assert.That(probe.ExitCount, Is.Zero, "The enemy must not need to exit and re-enter.");
        }
    }

    [UnityTest]
    public IEnumerator ProjectileHitReturnsToPoolAndTheSameProjectileCanFireAgain()
    {
        foreach (TowerBase tower in towers)
        {
            yield return IsolateTower(tower);
            MoveEnemy(tower.transform.position + Vector3.right * (tower.TowerRange * 0.75f));
            SimulateStep();
            yield return WaitForLaunch(tower);
            TowerProjectile projectile = GetActiveProjectile(tower);
            Rigidbody body = projectile.GetComponent<Rigidbody>();
            Transform pool = projectile.transform.parent;

            // Advance real physics until the projectile collides with the real enemy collider.
            for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++)
                SimulateStep();

            Assert.That(projectile.gameObject.activeSelf, Is.False, tower.name + " projectile failed to recycle on hit.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(projectile.transform.parent, Is.SameAs(pool));
            Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            // Reset is intentionally repeated to exercise already-kinematic pooled bodies.
            projectile.Initialize();
            projectile.Initialize();

            yield return WaitForLaunch(tower);
            Assert.That(GetActiveProjectile(tower), Is.SameAs(projectile), tower.name + " should reuse its first free slot.");
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.linearVelocity.magnitude,
                Is.EqualTo(ExpectedSpeed(tower)).Within(0.001f), tower.name + " reused projectile velocity");
        }
    }

    private IEnumerator VerifyLaunchAndMotion(TowerBase tower, float speed)
    {
        yield return IsolateTower(tower);
        MoveEnemy(tower.transform.position + Vector3.right * (tower.TowerRange * 0.75f));
        SimulateStep();
        yield return WaitForLaunch(tower);
        Assert.That(tower.EnemiesInRange, Does.Contain(enemy));
        TowerProjectile projectile = GetActiveProjectile(tower);
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        Assert.That(body.isKinematic, Is.False);
        Assert.That(body.useGravity, Is.False);
        Assert.That(body.linearVelocity.magnitude, Is.EqualTo(speed).Within(0.001f));
        Vector3 start = body.position;
        SimulateStep();
        Assert.That(Vector3.Distance(start, body.position), Is.EqualTo(speed * PhysicsStep).Within(0.005f));
        Assert.That(Vector3.Dot(body.position - start, enemy.transform.position - start), Is.GreaterThan(0f));
    }

    private IEnumerator IsolateTower(TowerBase target)
    {
        foreach (TowerBase tower in towers)
            tower.enabled = tower == target;
        MoveEnemy(OutsideAllRanges);
        SimulateStep();
        yield return null;
    }

    private IEnumerator WaitForLaunch(TowerBase tower)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (GetActiveProjectile(tower) == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(GetActiveProjectile(tower), Is.Not.Null, tower.name + " did not launch a projectile within three seconds.");
    }

    private void MoveEnemy(Vector3 position)
    {
        enemyBody.position = position;
        enemy.transform.position = position;
        Physics.SyncTransforms();
    }

    private static void SimulateStep()
    {
        Physics.SyncTransforms();
        Physics.Simulate(PhysicsStep);
    }

    private static Transform GetPool(TowerBase tower) => (Transform)typeof(TowerBase)
        .GetField("projectilePool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tower);

    private static TowerProjectile[] GetProjectiles(TowerBase tower)
    {
        Transform pool = GetPool(tower);
        Assert.That(pool, Is.Not.Null, tower.name + " has no assigned projectile pool.");
        return pool.GetComponentsInChildren<TowerProjectile>(true);
    }

    private static TowerProjectile GetActiveProjectile(TowerBase tower) =>
        GetProjectiles(tower).FirstOrDefault(projectile => projectile.gameObject.activeSelf);

    private static float ExpectedSpeed(TowerBase tower)
    {
        if (tower is ArcherTower)
            return 18f;
        if (tower is MageTower)
            return 8f;
        throw new System.ArgumentException("Unexpected tower type in SampleScene.", nameof(tower));
    }

    private void CaptureKinematicWarnings(string message, string stackTrace, LogType type)
    {
        if ((type == LogType.Warning || type == LogType.Error) &&
            message.IndexOf("kinematic", System.StringComparison.OrdinalIgnoreCase) >= 0)
            kinematicWarnings.Add(message);
    }
}
