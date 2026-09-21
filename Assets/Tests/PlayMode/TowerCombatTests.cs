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
        EnemyTestScene.UseOneIndependentEnemy();
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.enabled = false;
        // Let PathEnemy.Start build its real NavMesh before taking control of movement.
        yield return null;
        enemy = Object.FindFirstObjectByType<PathEnemy>();
        Assert.That(enemy, Is.Not.Null, "SampleScene must contain the enemy cube.");
        Assert.That(Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None).Length, Is.EqualTo(1),
            "The current scene must contain only the one remaining enemy.");
        Assert.That(enemy.GetComponent<Enemy>(), Is.Not.Null);
        // Keep focused combat cases isolated; the crowd test exercises the full formation.
        foreach (PathEnemy other in Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None))
            if (other != enemy) other.gameObject.SetActive(false);
        enemy.GetComponent<NavMeshAgent>().enabled = false;
        enemyBody = enemy.GetComponent<Rigidbody>();
        Assert.That(enemyBody, Is.Not.Null);
        Assert.That(enemyBody.isKinematic, Is.True, "The moving enemy needs its trigger Rigidbody.");
        Assert.That(enemy.isActiveAndEnabled, Is.True);
        BuildSite[] sites = Object.FindObjectsByType<BuildSite>(FindObjectsSortMode.None);
        PlayerGold gold = Object.FindFirstObjectByType<PlayerGold>();
        Assert.That(sites.Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(sites[0].TryBuild(BuildSite.TowerKind.Archer, gold), Is.True);
        Assert.That(sites[1].TryBuild(BuildSite.TowerKind.Mage, gold), Is.True);
        Assert.That(sites[2].TryBuild(BuildSite.TowerKind.Bomber, gold), Is.True);
        towers = Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
        Assert.That(towers.Length, Is.EqualTo(3));
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
    public void ConstructedTowersHaveIndependentPoolsAndPreserveConfiguredColliderRadius()
    {
        var pools = new HashSet<Transform>();
        foreach (TowerBase tower in towers)
        {
            Assert.That(tower.Data, Is.Not.Null, tower.name);
            Assert.That(tower.TowerRange, Is.EqualTo(tower is ArcherTower ? 3.5f : tower is MageTower ? 3f : 3.25f).Within(0.001f));
            Transform pool = GetPool(tower);
            Assert.That(pool, Is.Not.Null, tower.name + " needs its own projectile pool.");
            Assert.That(pools.Add(pool), Is.True, tower.name + " must not share another tower's pool.");
            Assert.That(GetProjectiles(tower).Length, Is.GreaterThan(0), tower.name);
            Assert.That(GetProjectiles(tower).All(projectile => !projectile.gameObject.activeSelf), Is.True);

            SphereCollider rangeCollider = tower.GetComponent<SphereCollider>();
            Assert.That(rangeCollider.isTrigger, Is.True, tower.name);
            Assert.That(rangeCollider.radius,
                Is.EqualTo(tower.TowerRange).Within(0.001f), tower.name + " Inspector Radius must not be divided by scale.");
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
    public IEnumerator MortarRisesAboveTheTowerThenFallsAndDamagesItsTarget()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        yield return IsolateTower(bomber);
        MoveEnemy(bomber.transform.position + Vector3.right * (WorldRadius(bomber) * 0.75f));
        SimulateStep();
        yield return WaitForLaunch(bomber);
        TowerProjectile projectile = GetActiveProjectile(bomber);
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        Assert.That(projectile.IsMortar, Is.True);
        Assert.That(body.useGravity, Is.True);
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        float startHeight = body.position.y;
        float highest = startHeight;
        bool fell = false;
        for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++)
        {
            SimulateStep();
            if (!projectile.gameObject.activeSelf) break;
            highest = Mathf.Max(highest, body.position.y);
            fell |= body.linearVelocity.y < 0f;
        }
        Assert.That(highest, Is.GreaterThan(startHeight + 3f), "Mortar must visibly arc over the path.");
        Assert.That(fell, Is.True);
        Assert.That(projectile.gameObject.activeSelf, Is.False);
        Assert.That(enemy.GetComponent<Enemy>().CurrentHealth, Is.EqualTo(65f));
        Assert.That(projectile.IsMortar, Is.False);
        Assert.That(body.useGravity, Is.False, "Pool return must clear the ballistic state.");
    }

    [Test]
    public void BomberDataHasIntermediateRangeHigherDamageAndSlowerFireRate()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        Assert.That(bomber.Data, Is.InstanceOf<BomberTowerData>());
        Assert.That(bomber.BlastRadius, Is.EqualTo(3f));
        Assert.That(bomber.DamageType, Is.EqualTo(TowerData.TowerDamageType.Physical));
        Assert.That(GetProjectiles(bomber).Length, Is.EqualTo(8));
        Assert.That(1f / bomber.TowerAttackSpeed, Is.EqualTo(2.222222f).Within(0.0001f));
        foreach (TowerBase other in towers.Where(t => t != bomber))
        {
            Assert.That(bomber.TowerDamage, Is.GreaterThan(other.TowerDamage));
            Assert.That(bomber.TowerAttackSpeed, Is.LessThan(other.TowerAttackSpeed));
        }
        Assert.That(bomber.TowerRange, Is.GreaterThan(towers.OfType<MageTower>().Single().TowerRange));
        Assert.That(bomber.TowerRange, Is.LessThan(towers.OfType<ArcherTower>().Single().TowerRange));
    }

    [UnityTest]
    public IEnumerator BombExplosionHitsDenseGroupOncePerEnemyAndExcludesOutsideWaitingAndOtherLayers()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        yield return IsolateTower(bomber);
        Vector3 targetPosition = bomber.transform.position + Vector3.right * (WorldRadius(bomber) * 0.75f);
        MoveEnemy(targetPosition);
        var nearby = new List<Enemy>();
        // More than 16 colliders exercises a saturated query buffer, including compound colliders.
        for (int i = 0; i < 32; i++)
        {
            Enemy splashTarget = CreateSplashEnemy(targetPosition + Vector3.forward * (0.8f + i * 0.04f));
            var extraCollider = new GameObject("Extra enemy collider");
            extraCollider.layer = splashTarget.gameObject.layer;
            extraCollider.transform.SetParent(splashTarget.transform, false);
            extraCollider.AddComponent<BoxCollider>();
            nearby.Add(splashTarget);
        }
        Enemy outside = CreateSplashEnemy(targetPosition + Vector3.forward * (bomber.BlastRadius + 1.25f));
        Enemy waiting = CreateSplashEnemy(targetPosition + Vector3.back);
        waiting.SetWaiting(true);
        Enemy otherLayer = CreateSplashEnemy(targetPosition + Vector3.forward);
        otherLayer.gameObject.layer = LayerMask.NameToLayer("Default");
        Enemy lethal = CreateSplashEnemy(targetPosition + Vector3.back * 1.5f);
        lethal.TakeDamage(lethal.MaxHealth - bomber.TowerDamage, TowerData.TowerDamageType.Magic);
        SimulateStep();
        yield return WaitForLaunch(bomber);
        TowerProjectile projectile = GetActiveProjectile(bomber);
        for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++) SimulateStep();

        Assert.That(projectile.gameObject.activeSelf, Is.False);
        Assert.That(enemy.GetComponent<Enemy>().CurrentHealth, Is.EqualTo(65f));
        foreach (Enemy splashTarget in nearby)
        {
            Assert.That(splashTarget.CurrentHealth, Is.EqualTo(65f), "Every enemy must receive exactly one splash hit.");
            Assert.That(splashTarget.LastDamageType, Is.EqualTo(TowerData.TowerDamageType.Physical));
        }
        Assert.That(outside.CurrentHealth, Is.EqualTo(100f));
        Assert.That(waiting.CurrentHealth, Is.EqualTo(100f));
        Assert.That(otherLayer.CurrentHealth, Is.EqualTo(100f));
        Assert.That(lethal.IsDead, Is.True);
        Assert.That(lethal.gameObject.activeSelf, Is.False);
        bomber.ProjectileHit(projectile, enemy);
        Assert.That(nearby.All(e => e.CurrentHealth == 65f), Is.True, "Duplicate callbacks must not explode twice.");
        BombExplosionVisual visual = Object.FindFirstObjectByType<BombExplosionVisual>();
        Assert.That(visual, Is.Not.Null);
        Assert.That(Vector3.Distance(visual.transform.position, targetPosition), Is.LessThan(1f), "Explosion must use the impact position before the bomb returns to the pool.");
        LineRenderer ring = visual.GetComponent<LineRenderer>();
        Assert.That(ring.GetPosition(0).magnitude, Is.EqualTo(bomber.BlastRadius).Within(0.001f));
        yield return new WaitForSeconds(0.4f);
        Assert.That(visual.gameObject.activeSelf, Is.False, "The explosion visual must return to its inactive state.");
    }

    [UnityTest]
    public IEnumerator MortarFliesOverAnEnemyStandingBetweenTowerAndLandingPoint()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        yield return IsolateTower(bomber);
        MoveEnemy(bomber.transform.position + Vector3.right * (WorldRadius(bomber) * 0.9f));
        Enemy blocker = CreateSplashEnemy(bomber.transform.position + Vector3.right * 4f);
        PathEnemy blockerPath = blocker.gameObject.AddComponent<PathEnemy>();
        Enemy adjacent = CreateSplashEnemy(blocker.transform.position + Vector3.forward * 1.5f);
        // Run launch and physical collision in the same frame, before the stationary blocker starts navigation.
        bool launched = (bool)typeof(TowerBase).GetMethod("LaunchProjectile", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(bomber, new object[] { enemy });
        Assert.That(launched, Is.True);
        TowerProjectile projectile = GetActiveProjectile(bomber);
        for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++) SimulateStep();
        blockerPath.enabled = false;
        Assert.That(projectile.gameObject.activeSelf, Is.False);
        Assert.That(blocker.CurrentHealth, Is.EqualTo(100f));
        Assert.That(adjacent.CurrentHealth, Is.EqualTo(100f));
        Assert.That(enemy.GetComponent<Enemy>().CurrentHealth, Is.EqualTo(65f), "The shell must land at the distant target after passing above the blocker.");
    }

    [UnityTest]
    public IEnumerator MortarStillExplodesOnThePathAfterItsOriginalTargetDies()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        yield return IsolateTower(bomber);
        Vector3 landing = new Vector3(bomber.transform.position.x, 0.68f, 4.5f);
        MoveEnemy(landing);
        Enemy nearby = CreateSplashEnemy(landing + Vector3.right * 1.5f);
        SimulateStep();
        yield return WaitForLaunch(bomber);
        TowerProjectile projectile = GetActiveProjectile(bomber);
        enemy.GetComponent<Enemy>().TakeDamage(999f, TowerData.TowerDamageType.Physical);
        yield return null;
        Assert.That(projectile.gameObject.activeSelf, Is.True, "A launched shell must not vanish when another tower kills its target.");
        for (int step = 0; step < 200 && projectile.gameObject.activeSelf; step++)
        {
            SimulateStep();
            yield return null;
        }
        Assert.That(projectile.gameObject.activeSelf, Is.False, "The landing point must detonate a shell even on a path made of render-only geometry.");
        Assert.That(nearby.CurrentHealth, Is.EqualTo(65f));
        bomber.ProjectileGroundImpact(projectile);
        Assert.That(nearby.CurrentHealth, Is.EqualTo(65f), "Ground callbacks must not detonate the same shell twice.");
    }

    [UnityTest]
    public IEnumerator BomberWaitsMoreThanTwoSecondsBetweenShots()
    {
        BomberTower bomber = towers.OfType<BomberTower>().Single();
        yield return IsolateTower(bomber);
        MoveEnemy(bomber.transform.position + Vector3.right * (WorldRadius(bomber) * 0.75f));
        SimulateStep();
        yield return WaitForLaunch(bomber);
        // Physics is paused, so both shots stay in flight and the pool reveals the actual firing interval.
        float launchedAt = Time.time;
        while (Time.time - launchedAt < 2.05f)
        {
            Assert.That(GetProjectiles(bomber).Count(p => p.gameObject.activeSelf), Is.EqualTo(1));
            yield return null;
        }
        float deadline = Time.realtimeSinceStartup + 1f;
        while (GetProjectiles(bomber).Count(p => p.gameObject.activeSelf) < 2 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(GetProjectiles(bomber).Count(p => p.gameObject.activeSelf), Is.EqualTo(2));
        Assert.That(Time.time - launchedAt, Is.InRange(2.15f, 2.45f));
    }

    [UnityTest]
    public IEnumerator MageHitSlowsThenSmoothlyRestoresEnemySpeedInHalfASecond()
    {
        MageTower mage = towers.OfType<MageTower>().Single();
        yield return IsolateTower(mage);
        MoveEnemy(mage.transform.position + Vector3.right * (WorldRadius(mage) * 0.75f));
        SimulateStep();
        yield return WaitForLaunch(mage);

        Enemy stats = enemy.GetComponent<Enemy>();
        float normalSpeed = stats.MovementSpeed;
        float healthBeforeHit = stats.CurrentHealth;
        for (int step = 0; step < 150 && stats.CurrentHealth >= healthBeforeHit; step++) SimulateStep();

        mage.enabled = false;
        float initialSlowedSpeed = stats.CurrentMovementSpeed;
        Assert.That(stats.IsSlowed, Is.True);
        Assert.That(initialSlowedSpeed, Is.EqualTo(normalSpeed * 0.45f).Within(0.01f));

        yield return new WaitForSeconds(0.25f);
        Assert.That(stats.CurrentMovementSpeed, Is.GreaterThan(initialSlowedSpeed));
        Assert.That(stats.CurrentMovementSpeed, Is.LessThan(normalSpeed));

        yield return new WaitForSeconds(0.3f);
        Assert.That(stats.IsSlowed, Is.False);
        Assert.That(stats.CurrentMovementSpeed, Is.EqualTo(normalSpeed).Within(0.01f));
    }

    private static Enemy CreateSplashEnemy(Vector3 position)
    {
        var gameObject = new GameObject("Splash Test Enemy");
        gameObject.SetActive(false);
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        gameObject.transform.position = position;
        gameObject.AddComponent<NavMeshAgent>().enabled = false;
        gameObject.AddComponent<BoxCollider>();
        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Enemy stats = gameObject.AddComponent<Enemy>();
        gameObject.SetActive(true);
        return stats;
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
            MoveEnemy(tower.transform.position + Vector3.right * (WorldRadius(tower) + 0.25f));
            SimulateStep();
            yield return null;
            SimulateStep();
            yield return null;
            Assert.That(probe.EnterCount, Is.EqualTo(1), tower.name + " must already have a trigger contact.");
            Assert.That(tower.EnemiesInRange, Is.Empty, tower.name);
            Assert.That(GetActiveProjectile(tower), Is.Null, tower.name + " fired outside its data range.");

            MoveEnemy(tower.transform.position + Vector3.right * (WorldRadius(tower) - 0.25f));
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
            MoveEnemy(tower.transform.position + Vector3.right * (WorldRadius(tower) * 0.75f));
            SimulateStep();
            yield return WaitForLaunch(tower);
            TowerProjectile projectile = GetActiveProjectile(tower);
            Rigidbody body = projectile.GetComponent<Rigidbody>();
            Transform pool = projectile.transform.parent;
            Enemy stats = enemy.GetComponent<Enemy>();
            float healthBeforeHit = stats.CurrentHealth;

            // Advance real physics until the projectile collides with the real enemy collider.
            for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++)
                SimulateStep();

            Assert.That(projectile.gameObject.activeSelf, Is.False, tower.name + " projectile failed to recycle on hit.");
            Assert.That(stats.CurrentHealth, Is.EqualTo(healthBeforeHit - tower.TowerDamage).Within(0.001f));
            Assert.That(stats.LastDamageType, Is.EqualTo(tower.DamageType));
            tower.ProjectileHit(projectile, enemy);
            Assert.That(stats.CurrentHealth, Is.EqualTo(healthBeforeHit - tower.TowerDamage).Within(0.001f),
                "A duplicate trigger callback must not apply the same hit twice.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(projectile.transform.parent, Is.SameAs(pool));
            Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            // Reset is intentionally repeated to exercise already-kinematic pooled bodies.
            projectile.Initialize();
            projectile.Initialize();

            yield return WaitForLaunch(tower);
            Assert.That(GetActiveProjectile(tower), Is.SameAs(projectile), tower.name + " should reuse its first free slot.");
            Assert.That(body.isKinematic, Is.False);
            if (tower is BomberTower)
            {
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.linearVelocity.y, Is.GreaterThan(0f), "Reused bombs must start a fresh upward arc.");
            }
            else Assert.That(body.linearVelocity.magnitude,
                Is.EqualTo(ExpectedSpeed(tower)).Within(0.001f), tower.name + " reused projectile velocity");
        }
    }

    private IEnumerator VerifyLaunchAndMotion(TowerBase tower, float speed)
    {
        yield return IsolateTower(tower);
        MoveEnemy(tower.transform.position + Vector3.right * (WorldRadius(tower) * 0.75f));
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

    [UnityTest]
    public IEnumerator LethalProjectileHitClearsHealthTargetAndProjectile()
    {
        TowerBase tower = towers.Single(t => t is ArcherTower);
        yield return IsolateTower(tower);
        Enemy stats = enemy.GetComponent<Enemy>();
        stats.TakeDamage(stats.CurrentHealth - 1f, TowerData.TowerDamageType.Magic);
        MoveEnemy(tower.transform.position + Vector3.right * (WorldRadius(tower) * 0.75f));
        SimulateStep();
        yield return WaitForLaunch(tower);
        TowerProjectile projectile = GetActiveProjectile(tower);
        for (int step = 0; step < 150 && projectile.gameObject.activeSelf; step++) SimulateStep();
        Assert.That(stats.CurrentHealth, Is.Zero);
        Assert.That(stats.IsDead, Is.True);
        Assert.That(enemy.gameObject.activeSelf, Is.False);
        Assert.That(projectile.gameObject.activeSelf, Is.False);
        stats.TakeDamage(100f, TowerData.TowerDamageType.Magic);
        Assert.That(stats.CurrentHealth, Is.Zero, "Health must not become negative after death.");
        yield return null;
        Assert.That(tower.EnemiesInRange, Is.Empty);
        Assert.That(GetActiveProjectile(tower), Is.Null);
    }

    [UnityTest]
    public IEnumerator EnemyStatsRemainAvailableWithoutHealthLogs()
    {
        Enemy stats = enemy.GetComponent<Enemy>();
        Assert.That(stats.CurrentHealth, Is.EqualTo(stats.MaxHealth));
        Assert.That(enemy.GetComponent<NavMeshAgent>().speed, Is.EqualTo(stats.MovementSpeed));
        Assert.That(stats.AttackDamage, Is.EqualTo(10f));
        Assert.That(stats.AttackSpeed, Is.EqualTo(1f));
        Assert.That(stats.DamageType, Is.EqualTo(TowerData.TowerDamageType.Physical));
        stats.TakeDamage(10f, TowerData.TowerDamageType.Physical);
        var loggedMessages = new List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (!message.StartsWith("[Enemy Health] " + enemy.gameObject.name + ": ")) return;
            loggedMessages.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            yield return new WaitForSeconds(2.1f);
            Assert.That(loggedMessages, Is.Empty, "Health belongs in the UI, not in repeating console logs.");
            Assert.That(stats.CurrentHealth, Is.EqualTo(90f));
        }
        finally
        {
            Application.logMessageReceived -= capture;
        }
    }

    [UnityTest]
    public IEnumerator DamageBrieflyChangesOnlyTheHitEnemyColorAndRestoresIt()
    {
        Enemy stats = enemy.GetComponent<Enemy>();
        Renderer renderer = enemy.GetComponent<Renderer>();
        Material shared = renderer.sharedMaterial;
        int property = Shader.PropertyToID("_BaseColor");
        Color original = shared.GetColor(property);
        var untouched = GameObject.CreatePrimitive(PrimitiveType.Cube);
        untouched.transform.position = OutsideAllRanges + Vector3.right * 5f;
        Renderer otherRenderer = untouched.GetComponent<Renderer>();
        otherRenderer.sharedMaterial = shared;
        stats.TakeDamage(1f, TowerData.TowerDamageType.Physical);
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.That(block.GetColor(property), Is.Not.EqualTo(original));
        Assert.That(block.GetColor(property).r, Is.InRange(original.r, 1f));
        Assert.That(shared.GetColor(property), Is.EqualTo(original), "Never recolor the shared enemy material.");
        otherRenderer.GetPropertyBlock(block);
        Assert.That(block.isEmpty, Is.True, "Unhurt enemies sharing the material must keep their own appearance.");
        yield return new WaitForSeconds(0.1f);
        stats.TakeDamage(1f, TowerData.TowerDamageType.Magic);
        yield return new WaitForSeconds(0.1f);
        renderer.GetPropertyBlock(block);
        Assert.That(block.isEmpty, Is.False, "Another hit must restart the flash.");
        yield return new WaitForSeconds(0.15f);
        renderer.GetPropertyBlock(block);
        Assert.That(block.isEmpty, Is.True, "The original renderer state must return after the flash.");
        stats.TakeDamage(1f, TowerData.TowerDamageType.Physical);
        enemy.gameObject.SetActive(false);
        renderer.GetPropertyBlock(block);
        Assert.That(block.isEmpty, Is.True, "Disabling an enemy must not leave a hit tint on it.");
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

    private static float WorldRadius(TowerBase tower)
    {
        Vector3 scale = tower.transform.lossyScale;
        return tower.GetComponent<SphereCollider>().radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

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
