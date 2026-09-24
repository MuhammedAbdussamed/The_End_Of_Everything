using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class CastleGuardTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private Scene scene;
    private float previousTimeScale;

    [UnitySetUp]
    public IEnumerator LoadSceneAndBuildCastle()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        scene = SceneManager.GetActiveScene();
        yield return null;

        BuildSite site = Object.FindObjectsByType<BuildSite>(FindObjectsSortMode.None).First();
        PlayerGold gold = Object.FindFirstObjectByType<PlayerGold>();
        Assert.That(site.TryBuild(BuildSite.TowerKind.Castle, gold), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Castle guard test cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator DeadGuardRespawnsFromItsCastleAfterTheConfiguredDelay()
    {
        CastleTower castle = Object.FindFirstObjectByType<CastleTower>();
        Assert.That(castle, Is.Not.Null);
        Assert.That(castle.Guards.Count, Is.EqualTo(3));

        typeof(CastleTower).GetField("guardRespawnDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(castle, 0.05f);
        GuardUnit fallen = castle.Guards[0];
        fallen.TakeDamage(fallen.MaxHealth);
        yield return null;

        Assert.That(castle.Guards[0], Is.Null);
        yield return new WaitForSeconds(0.1f);

        GuardUnit replacement = castle.Guards[0];
        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement, Is.Not.SameAs(fallen));
        Assert.That(replacement.Castle, Is.SameAs(castle));
        Assert.That(replacement.CurrentHealth, Is.EqualTo(replacement.MaxHealth));
    }

    [UnityTest]
    public IEnumerator FlagButtonEntersPlacementAndCanMoveASelectedGuard()
    {
        CastleTower castle = Object.FindFirstObjectByType<CastleTower>();
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        foreach (PathEnemy enemy in Object.FindObjectsByType<PathEnemy>(FindObjectsSortMode.None))
            enemy.gameObject.SetActive(false);
        hud.OpenTowerPanel(castle);

        Button flagButton = hud.transform.Find("Tower Upgrade Panel/Guard Placement")?.GetComponent<Button>();
        Assert.That(flagButton, Is.Not.Null);
        flagButton.onClick.Invoke();
        Assert.That(hud.GuardPlacementActive, Is.True);

        GuardUnit guard = castle.Guards[0];
        NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
        Assert.That(agent.isOnNavMesh, Is.True, "Castle guards must be bound to the baked NavMesh before receiving orders.");

        Vector3 start = guard.transform.position;
        Vector3 destination = FindDifferentWalkablePoint(castle, agent, start);
        Assert.That(HorizontalDistance(start, destination), Is.GreaterThan(1f));
        Assert.That(hud.TrySelectGuard(guard), Is.True);
        Assert.That(hud.TryPlaceSelectedGuard(destination), Is.True);

        Vector3 assignedPosition = guard.GuardPosition;
        Assert.That(HorizontalDistance(start, assignedPosition), Is.GreaterThan(1f),
            $"Requested {destination}, but the castle assigned {assignedPosition} from start {start}.");
        float timeout = Time.time + 2f;
        while (Time.time < timeout && HorizontalDistance(guard.transform.position, assignedPosition) > 0.25f)
            yield return null;

        Assert.That(HorizontalDistance(guard.transform.position, start), Is.GreaterThan(0.75f));
        Assert.That(HorizontalDistance(guard.transform.position, assignedPosition), Is.LessThan(0.35f));
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private static Vector3 FindDifferentWalkablePoint(CastleTower castle, NavMeshAgent agent, Vector3 start)
    {
        float radius = castle.TowerRange - 1f;
        Vector3 best = start;
        float bestDistance = 0f;
        for (float x = -radius; x <= radius; x += 0.5f)
        {
            for (float z = -radius; z <= radius; z += 0.5f)
            {
                Vector3 probe = castle.transform.position + new Vector3(x, 0f, z);
                probe.y = start.y - agent.baseOffset;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)
                    || HorizontalDistance(hit.position, castle.transform.position) >= castle.TowerRange - 0.8f)
                    continue;

                var path = new NavMeshPath();
                float distance = HorizontalDistance(hit.position, start);
                if (distance > bestDistance && agent.CalculatePath(hit.position, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    best = hit.position;
                    bestDistance = distance;
                }
            }
        }

        Assert.That(bestDistance, Is.GreaterThan(1.5f),
            "The castle needs a second reachable NavMesh point inside its placement radius.");
        return best;
    }
}
