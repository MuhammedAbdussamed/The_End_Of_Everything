using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class EnemySceneTests
{
    [UnityTest]
    public IEnumerator WalkingSceneEnemyTakesRealProjectileDamage()
    {
        float previousTimeScale = Time.timeScale;
        Random.State previousRandom = Random.state;
        Scene scene = default;
        try
        {
            Time.timeScale = 0f;
            Random.InitState(20260915);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SampleScene.unity");
#endif
            scene = SceneManager.GetActiveScene();
            EnemyTestScene.UseOneIndependentEnemy();
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            yield return null;
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(1));
            Enemy enemy = enemies[0];
            PathEnemy route = enemy.GetComponent<PathEnemy>();
            float startingHealth = enemy.CurrentHealth;
            float deadline = Time.realtimeSinceStartup + 35f;
            Time.timeScale = 2f;
            while (enemy.gameObject.activeSelf && enemy.CurrentHealth == startingHealth
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemy.CurrentHealth, Is.LessThan(startingHealth),
                "The current scene enemy must take a real projectile hit while walking, without being repositioned by the test.");
            Assert.That(route.CurrentWaypointIndex, Is.GreaterThan(0));
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Enemy scene test cleanup"));
                SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = previousTimeScale;
            Random.state = previousRandom;
        }
    }
}
