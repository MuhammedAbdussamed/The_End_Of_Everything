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

public class WaveFlowTests
{
    private Scene scene;
    private WaveController waves;
    private PlayerHealth health;
    private GameHud hud;
    private float previousTimeScale;
    private Random.State previousRandom;
    private SimulationMode previousPhysics;

    [TestCase(100f, 3)]
    [TestCase(75f, 3)]
    [TestCase(74.9f, 2)]
    [TestCase(50f, 2)]
    [TestCase(49.9f, 1)]
    [TestCase(0f, 1)]
    public void WinStarsFollowRemainingHealth(float health, int expectedStars)
    {
        Assert.That(GameHud.CalculateWinStars(health), Is.EqualTo(expectedStars));
    }

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        previousTimeScale = Time.timeScale;
        previousRandom = Random.state;
        previousPhysics = Physics.simulationMode;
        Time.timeScale = 0f;
        Physics.simulationMode = SimulationMode.FixedUpdate;
        Random.InitState(20260916);
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/SampleScene.unity");
#endif
        scene = SceneManager.GetActiveScene();
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
        foreach (TowerBase tower in Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None)) tower.enabled = false;
        yield return null;
        waves = Object.FindFirstObjectByType<WaveController>();
        health = Object.FindFirstObjectByType<PlayerHealth>();
        hud = Object.FindFirstObjectByType<GameHud>();
        Assert.That(waves, Is.Not.Null);
        Assert.That(health, Is.Not.Null);
        Assert.That(hud, Is.Not.Null);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Wave test cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        Time.timeScale = previousTimeScale;
        Random.state = previousRandom;
        Physics.simulationMode = previousPhysics;
    }

    [UnityTest]
    public IEnumerator PlatformStagesEnemiesAndEveryWaveWaitsForItsLastEnemy()
    {
        Assert.That(waves.TotalWaves, Is.EqualTo(3));
        Assert.That(waves.CurrentWave, Is.EqualTo(1));
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Preparing));
        Assert.That(hud.WaveLabel, Is.EqualTo("1 / 3"));
        Assert.That(hud.HealthLabel, Is.EqualTo("100 / 100"));
        Assert.That(hud.GoldLabel, Is.EqualTo("300"));
        Assert.That(hud.ResultVisible, Is.False);
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(4));
        Assert.That(waves.ActiveEnemies.All(enemy => enemy.IsWaiting), Is.True);
        Enemy waiting = waves.ActiveEnemies.First();
        Assert.That(waiting.IsWaiting, Is.True);
        Assert.That(waiting.GetComponent<PathEnemy>().enabled, Is.False);
        Assert.That(waiting.GetComponent<NavMeshAgent>().enabled, Is.False);
        Bounds platform = GameObject.Find("Enemy Waiting Platform").GetComponent<Renderer>().bounds;
        Assert.That(waiting.transform.position.x, Is.InRange(platform.min.x, platform.max.x));
        Assert.That(waiting.transform.position.z, Is.InRange(platform.min.z, platform.max.z));
        Vector3 waitingPosition = waiting.transform.position;
        yield return null;
        yield return null;
        Assert.That(waiting.transform.position, Is.EqualTo(waitingPosition));
        waiting.TakeDamage(999f, TowerData.TowerDamageType.Physical);
        Assert.That(waiting.CurrentHealth, Is.EqualTo(waiting.MaxHealth), "Waiting enemies must not be attacked before release.");

        Time.timeScale = 5f;
        int[] counts = { 4, 8, 12 };
        for (int wave = 1; wave <= 3; wave++)
        {
            yield return WaitForWave(wave);
            Assert.That(waves.RemainingEnemies, Is.EqualTo(counts[wave - 1]));
            Assert.That(hud.WaveLabel, Is.EqualTo($"{wave} / 3"));
            Assert.That(waves.ActiveEnemies.Count(e => e.IsLarge), Is.EqualTo((wave - 1) * 2));
            foreach (Enemy spawned in waves.ActiveEnemies)
            {
                float healthMultiplier = spawned.IsLarge ? 2f : 1f;
                float speedMultiplier = spawned.IsLarge ? 0.6f : 1f;
                float sizeMultiplier = spawned.IsLarge ? 1.5f : 1f;
                Assert.That(spawned.MaxHealth, Is.EqualTo(waves.EnemyTemplate.MaxHealth * healthMultiplier));
                Assert.That(spawned.CurrentHealth, Is.EqualTo(spawned.MaxHealth));
                Assert.That(spawned.MovementSpeed, Is.EqualTo(waves.EnemyTemplate.MovementSpeed * speedMultiplier).Within(0.001f));
                Assert.That(spawned.GetComponent<NavMeshAgent>().speed, Is.EqualTo(spawned.MovementSpeed));
                Assert.That(spawned.BaseDamage, Is.EqualTo(waves.EnemyTemplate.BaseDamage * healthMultiplier));
                Assert.That(spawned.GoldReward, Is.EqualTo(spawned.IsLarge ? 20 : 10));
                Assert.That(spawned.AttackDamage, Is.EqualTo(waves.EnemyTemplate.AttackDamage));
                Assert.That(spawned.transform.localScale, Is.EqualTo(waves.EnemyTemplate.transform.localScale * sizeMultiplier));
                Assert.That(spawned.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(platform.max.y).Within(0.12f),
                    "Both enemy sizes must stand on top of the waiting platform.");
            }
            yield return WaitForRelease();
            yield return null;
            Enemy[] enemies = waves.ActiveEnemies.ToArray();
            foreach (Enemy spawned in enemies)
            {
                Assert.That(NavMesh.SamplePosition(spawned.transform.position, out NavMeshHit ground, 3f, NavMesh.AllAreas), Is.True);
                Assert.That(spawned.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(ground.position.y).Within(0.15f),
                    "Larger enemies must stay grounded while their agent walks.");
            }
            foreach (Enemy enemy in enemies.Take(enemies.Length - 1)) enemy.TakeDamage(999f, TowerData.TowerDamageType.Physical);
            Assert.That(waves.RemainingEnemies, Is.EqualTo(1));
            yield return new WaitForSeconds(0.5f);
            Assert.That(waves.CurrentWave, Is.EqualTo(wave), "A surviving enemy must block the next wave.");
            Assert.That(hud.ResultVisible, Is.False);
            enemies.Last().TakeDamage(999f, TowerData.TowerDamageType.Magic);
            int goldAfterKill = Object.FindFirstObjectByType<PlayerGold>().CurrentGold;
            enemies.Last().TakeDamage(999f, TowerData.TowerDamageType.Magic);
            Assert.That(Object.FindFirstObjectByType<PlayerGold>().CurrentGold, Is.EqualTo(goldAfterKill), "Repeated hits must not award gold twice.");
            Assert.That(hud.GoldLabel, Is.EqualTo(goldAfterKill.ToString()), "Gold HUD must update immediately.");
        }
        yield return null;
        yield return null;
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Completed));
        Assert.That(waves.CurrentWave, Is.EqualTo(3));
        Assert.That(waves.RemainingEnemies, Is.Zero);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f), "Killed enemies must not damage the player.");
        Assert.That(hud.WaveLabel, Is.EqualTo("3 / 3"));
        Assert.That(hud.GoldLabel, Is.EqualTo("600"));
        Assert.That(hud.ResultVisible, Is.True);
        Assert.That(hud.ResultLabel, Is.EqualTo("KAZANDIN"));
        Assert.That(hud.ResultDetailsLabel, Does.Contain("★  ★  ★"));
    }

    [UnityTest]
    public IEnumerator EscapingEnemiesFinishAllThreeWavesAndReducePlayerHealth()
    {
        // Give this isolated test enough health to observe all 24 escapes (300 total base damage).
        typeof(PlayerHealth).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 500f);
        typeof(PlayerHealth).GetProperty("CurrentHealth").SetValue(health, 500f);
        Time.timeScale = 6f;
        int[] counts = { 4, 8, 12 };
        float expectedHealth = 500f;
        for (int wave = 1; wave <= 3; wave++)
        {
            yield return WaitForWave(wave);
            Assert.That(waves.RemainingEnemies, Is.EqualTo(counts[wave - 1]));
            expectedHealth -= waves.ActiveEnemies.Sum(e => e.BaseDamage);
            foreach (Enemy spawned in waves.ActiveEnemies)
                typeof(Enemy).GetField("attackDamage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawned, 999f);
            yield return WaitForWaveToFinish(wave);
            Assert.That(health.CurrentHealth, Is.EqualTo(expectedHealth), "Escapes must use BaseDamage even when AttackDamage is 999.");
            Assert.That(hud.HealthLabel, Is.EqualTo($"{expectedHealth:0.#} / 500"));
        }
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Completed));
        Assert.That(waves.RemainingEnemies, Is.Zero);
        Assert.That(health.CurrentHealth, Is.EqualTo(200f));
        Assert.That(hud.WaveLabel, Is.EqualTo("3 / 3"));
        Assert.That(hud.GoldLabel, Is.EqualTo("300"), "Escaped enemies must not award gold.");
        Assert.That(hud.ResultVisible, Is.True);
        Assert.That(hud.ResultLabel, Is.EqualTo("KAZANDIN"));
    }

    [UnityTest]
    public IEnumerator KillsAndEscapesTogetherCompleteAWave()
    {
        typeof(PlayerHealth).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 400f);
        typeof(PlayerHealth).GetProperty("CurrentHealth").SetValue(health, 400f);
        Time.timeScale = 6f;
        for (int wave = 1; wave <= 3; wave++)
        {
            yield return WaitForWave(wave);
            yield return WaitForRelease();
            int killCount = wave == 3 ? 3 : 1;
            foreach (Enemy enemy in waves.ActiveEnemies.Take(killCount).ToArray())
                enemy.TakeDamage(999f, TowerData.TowerDamageType.Physical);
            yield return WaitForWaveToFinish(wave);
        }
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Completed));
        Assert.That(health.CurrentHealth, Is.EqualTo(160f));
        Assert.That(hud.HealthLabel, Is.EqualTo("160 / 400"));
        Assert.That(hud.GoldLabel, Is.EqualTo("360"));
    }

    [UnityTest]
    public IEnumerator LosingDuringPreparationStopsReleaseAndShowsLoseImmediately()
    {
        Enemy waiting = waves.ActiveEnemies.First();
        health.TakeDamage(1000f);
        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Defeated));
        Assert.That(hud.ResultVisible, Is.True);
        Assert.That(hud.ResultLabel, Is.EqualTo("KAYBETTİN"));
        Assert.That(hud.HealthLabel, Is.EqualTo("0 / 100"));
        Time.timeScale = 10f;
        yield return new WaitForSeconds(5f);
        Assert.That(waves.CurrentWave, Is.EqualTo(1));
        Assert.That(waiting.IsWaiting, Is.True);
        Assert.That(waiting.GetComponent<NavMeshAgent>().enabled, Is.False);
        waiting.TakeDamage(999f, TowerData.TowerDamageType.Physical);
        Assert.That(hud.GoldLabel, Is.EqualTo("300"));
        Assert.That(hud.ResultLabel, Is.EqualTo("KAYBETTİN"));
    }

    [UnityTest]
    public IEnumerator FatalLastEscapeShowsLoseInsteadOfWin()
    {
        Time.timeScale = 8f;
        for (int wave = 1; wave <= 3; wave++)
        {
            yield return WaitForWave(wave);
            yield return WaitForRelease();
            Enemy[] enemies = waves.ActiveEnemies.ToArray();
            int killCount = wave == 3 ? enemies.Length - 1 : enemies.Length;
            foreach (Enemy enemy in enemies.Take(killCount)) enemy.TakeDamage(999f, TowerData.TowerDamageType.Physical);
        }
        Enemy last = waves.ActiveEnemies.Single();
        health.TakeDamage(health.CurrentHealth - last.BaseDamage);
        float deadline = Time.realtimeSinceStartup + 35f;
        while (waves.State != WaveController.WaveState.Defeated && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Defeated));
        Assert.That(waves.RemainingEnemies, Is.Zero);
        Assert.That(hud.GoldLabel, Is.EqualTo("580"));
        Assert.That(hud.ResultVisible, Is.True);
        Assert.That(hud.ResultLabel, Is.EqualTo("KAYBETTİN"));
        yield return null;
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Defeated));
    }

    private IEnumerator WaitForWave(int wave)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (waves.CurrentWave < wave && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves.CurrentWave, Is.EqualTo(wave));
    }

    private IEnumerator WaitForRelease()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((waves.State == WaveController.WaveState.Preparing || waves.ActiveEnemies.Any(e => e.IsWaiting))
            && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves.State, Is.EqualTo(WaveController.WaveState.Active));
        Assert.That(waves.ActiveEnemies.All(e => !e.IsWaiting), Is.True);
    }

    private IEnumerator WaitForWaveToFinish(int wave)
    {
        float deadline = Time.realtimeSinceStartup + 35f;
        while (waves.CurrentWave == wave && waves.State != WaveController.WaveState.Completed && waves.State != WaveController.WaveState.Defeated
            && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves.CurrentWave > wave || waves.State == WaveController.WaveState.Completed, Is.True,
            "The wave did not finish after all enemies should have reached the final waypoint.");
    }
}
