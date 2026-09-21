using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class TowerUpgradeTests
{
    private Scene scene;
    private float previousTimeScale;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/SampleScene.unity");
#endif
        scene = SceneManager.GetActiveScene();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Tower upgrade cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator BuildUpgradeToLevelThreeAndSellRestoresTheSite()
    {
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        PlayerGold gold = Object.FindFirstObjectByType<PlayerGold>();
        BuildSite site = Object.FindObjectsByType<BuildSite>(FindObjectsSortMode.None).First();
        Assert.That(hud, Is.Not.Null);
        Assert.That(gold, Is.Not.Null);
        Assert.That(gold.CurrentGold, Is.EqualTo(300));
        Assert.That(site.IsEmpty, Is.True);

        hud.OpenBuildPanel(site);
        Assert.That(hud.BuildPanelVisible, Is.True);
        hud.BuildSelected(BuildSite.TowerKind.Archer);
        Assert.That(site.IsEmpty, Is.False);
        Assert.That(hud.BuildPanelVisible, Is.False);
        Assert.That(gold.CurrentGold, Is.EqualTo(240));

        ArcherTower archer = site.Tower as ArcherTower;
        Assert.That(archer, Is.Not.Null);

        float baseRange = archer.Data.TowerRange;
        float baseDamage = archer.Data.TowerDamage;
        float baseSpeed = archer.Data.TowerAttackSpeed;
        hud.OpenTowerPanel(archer);
        Assert.That(hud.UpgradePanelVisible, Is.True);
        Assert.That(hud.SelectedTower, Is.SameAs(archer));
        Assert.That(hud.UpgradeButtonLabel, Is.EqualTo("Yükselt  •  45 Gold"));

        Button upgrade = hud.transform.Find("Tower Upgrade Panel/Upgrade")?.GetComponent<Button>();
        Assert.That(upgrade, Is.Not.Null);
        ClickUpgradeButtonAtItsRenderedPosition(hud, upgrade);
        yield return null;
        Assert.That(gold.CurrentGold, Is.EqualTo(195));
        Assert.That(archer.TowerLevel, Is.EqualTo(2));
        Assert.That(archer.TowerDamage, Is.EqualTo(baseDamage * 1.25f).Within(0.001f));
        Assert.That(archer.TowerAttackSpeed, Is.EqualTo(baseSpeed * 1.15f).Within(0.001f));
        Assert.That(archer.TowerRange, Is.EqualTo(baseRange * 1.25f).Within(0.001f));
        Assert.That(archer.GetComponent<SphereCollider>().radius, Is.EqualTo(archer.TowerRange).Within(0.001f));
        Assert.That(hud.UpgradeButtonLabel, Is.EqualTo("Yükselt  •  75 Gold"));

        ClickUpgradeButtonAtItsRenderedPosition(hud, upgrade);
        yield return null;
        Assert.That(gold.CurrentGold, Is.EqualTo(120));
        Assert.That(archer.TowerLevel, Is.EqualTo(3));
        Assert.That(archer.TowerDamage, Is.EqualTo(baseDamage * 1.65f).Within(0.001f));
        Assert.That(archer.TowerAttackSpeed, Is.EqualTo(baseSpeed * 1.35f).Within(0.001f));
        Assert.That(archer.TowerRange, Is.EqualTo(baseRange * 1.55f).Within(0.001f));
        Assert.That(hud.UpgradeButtonLabel, Is.EqualTo("Maksimum seviye"));
        Assert.That(upgrade.interactable, Is.False);

        Assert.That(archer.Data.TowerRange, Is.EqualTo(baseRange), "Runtime upgrades must not modify the shared tower data asset.");
        hud.SellSelectedTower();
        yield return null;
        Assert.That(gold.CurrentGold, Is.EqualTo(165), "Selling refunds 75% of the 60 gold construction price.");
        Assert.That(site.IsEmpty, Is.True);
        Assert.That(site.GetComponent<Renderer>().enabled, Is.True);
        Assert.That(site.GetComponent<Collider>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator TowerRaycastUsesTheVisibleColliderAndHoverRestoresTheMaterialColor()
    {
        PlayerGold gold = Object.FindFirstObjectByType<PlayerGold>();
        BuildSite site = Object.FindObjectsByType<BuildSite>(FindObjectsSortMode.None).First();
        Assert.That(site.TryBuild(BuildSite.TowerKind.Archer, gold), Is.True);
        ArcherTower archer = site.Tower as ArcherTower;
        TowerInteractionController interaction = Object.FindFirstObjectByType<TowerInteractionController>();
        Camera camera = Camera.main;
        Physics.SyncTransforms();
        Vector3 screenPoint = camera.WorldToScreenPoint(archer.GetComponent<BoxCollider>().bounds.center);
        Assert.That(TowerInteractionController.FindTowerAtScreenPoint(camera, screenPoint), Is.SameAs(archer));

        Renderer renderer = archer.GetComponent<Renderer>();
        Material material = renderer.sharedMaterial;
        int colorProperty = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
        var before = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(before);
        Color original = before.HasColor(colorProperty) ? before.GetColor(colorProperty) : material.GetColor(colorProperty);

        MethodInfo setHovered = typeof(TowerInteractionController).GetMethod("SetHoveredTower", BindingFlags.Instance | BindingFlags.NonPublic);
        setHovered.Invoke(interaction, new object[] { archer });
        var highlighted = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(highlighted);
        Assert.That(highlighted.GetColor(colorProperty).grayscale, Is.GreaterThan(original.grayscale));

        setHovered.Invoke(interaction, new object[] { null });
        var restored = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(restored);
        Color restoredColor = restored.HasColor(colorProperty) ? restored.GetColor(colorProperty) : material.GetColor(colorProperty);
        Assert.That(restoredColor, Is.EqualTo(original));
        yield return null;
    }

    private static void ClickUpgradeButtonAtItsRenderedPosition(GameHud hud, Button expectedButton)
    {
        GraphicRaycaster raycaster = hud.GetComponent<GraphicRaycaster>();
        Assert.That(raycaster, Is.Not.Null, "The HUD needs a GraphicRaycaster for mouse clicks to reach buttons.");
        Canvas.ForceUpdateCanvases();

        RectTransform rect = expectedButton.GetComponent<RectTransform>();
        Vector2 renderedPoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Canvas canvas = hud.GetComponent<Canvas>();
        Vector2 inputPoint = new Vector2(renderedPoint.x * Screen.width / canvas.pixelRect.width,
            renderedPoint.y * Screen.height / canvas.pixelRect.height);
        Assert.That(hud.TryHandleTowerPanelClick(inputPoint), Is.True,
            "The rendered upgrade button must accept the Game View mouse coordinate.");
    }
}
