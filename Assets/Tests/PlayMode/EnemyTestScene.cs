using UnityEngine;

public static class EnemyTestScene
{
    public static void UseOneIndependentEnemy()
    {
        WaveController waves = Object.FindFirstObjectByType<WaveController>();
        if (waves == null) return;
        Enemy template = waves.EnemyTemplate;
        waves.enabled = false;
        foreach (PathEnemy existing in Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None))
            Object.DestroyImmediate(existing.gameObject);
        Enemy enemy = Object.Instantiate(template, template.transform.position, template.transform.rotation);
        enemy.gameObject.name = "Combat Test Enemy";
        enemy.gameObject.SetActive(true);
    }
}
