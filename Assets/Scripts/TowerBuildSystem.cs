using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TowerBuildSystem : MonoBehaviour
{
    private readonly List<BuildSite> sites = new();
    private readonly Dictionary<BuildSite.TowerKind, Material> towerMaterials = new();
    private readonly Dictionary<BuildSite.TowerKind, Transform> projectilePools = new();
    private Transform siteRoot;
    private Material siteMaterial;
    private BombExplosionVisual explosionVisual;

    private static readonly Vector3[] SitePositions =
    {
        new(-10f, 0.16f, -23f),
        new(-25f, 0.16f, -15f),
        new(-10f, 0.16f, -7f),
        new(-25f, 0.16f, 1f),
        new(-9f, 0.16f, 12f),
        new(3f, 0.16f, -3f),
        new(12f, 0.16f, 12f),
        new(26f, 0.16f, -3f)
    };

    public const float SiteDiameter = 2.7f;
    public const float SiteRadius = SiteDiameter * 0.5f;
    public static IReadOnlyList<Vector3> PreviewPositions => SitePositions;
    public IReadOnlyList<BuildSite> Sites => sites;

    private void Awake()
    {
        DisableLegacyStarterTowers();
        CacheSharedSceneReferences();
        CreateSites();
    }

    public int GetBuildCost(BuildSite.TowerKind kind) => kind switch
    {
        BuildSite.TowerKind.Bomber => 120,
        BuildSite.TowerKind.Mage => 90,
        _ => 60
    };

    public TowerBase CreateTower(BuildSite.TowerKind kind, BuildSite site)
    {
        if (site == null) return null;

        GameObject towerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        towerObject.SetActive(false);
        towerObject.name = kind switch
        {
            BuildSite.TowerKind.Archer => "Okçu Kulesi",
            BuildSite.TowerKind.Mage => "Büyücü Kulesi",
            _ => "Bombacı Kulesi"
        };
        towerObject.transform.SetPositionAndRotation(
            new Vector3(site.transform.position.x, 1.5f, site.transform.position.z), Quaternion.identity);
        towerObject.transform.localScale = new Vector3(2.5f, 4f, 2.5f);

        Renderer renderer = towerObject.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = GetTowerMaterial(kind);

        projectilePools.TryGetValue(kind, out Transform projectilePool);
        TowerBase tower = kind switch
        {
            BuildSite.TowerKind.Archer => towerObject.AddComponent<ArcherTower>(),
            BuildSite.TowerKind.Mage => towerObject.AddComponent<MageTower>(),
            _ => towerObject.AddComponent<BomberTower>()
        };
        TowerData data = CreateRuntimeData(kind);
        tower.ConfigureRuntime(data, projectilePool);
        if (tower is BomberTower bomber)
            bomber.ConfigureExplosionVisual(explosionVisual);
        tower.InitializeConstruction(site);
        towerObject.SetActive(true);
        return tower;
    }

    private static TowerData CreateRuntimeData(BuildSite.TowerKind kind)
    {
        if (kind == BuildSite.TowerKind.Bomber)
        {
            BomberTowerData data = ScriptableObject.CreateInstance<BomberTowerData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.ConfigureRuntime(3.25f, 1, 0.45f, 35f, TowerData.TowerDamageType.Physical, 3f);
            return data;
        }

        TowerData standardData = ScriptableObject.CreateInstance<TowerData>();
        standardData.hideFlags = HideFlags.HideAndDontSave;
        if (kind == BuildSite.TowerKind.Mage)
            // Mage fires once every 1.5 seconds, with a heavier magic hit.
            standardData.ConfigureRuntime(3f, 1, 1f / 1.5f, 28f, TowerData.TowerDamageType.Magic);
        else
            standardData.ConfigureRuntime(3.5f, 1, 3f, 8f, TowerData.TowerDamageType.Physical);
        return standardData;
    }

    private void CacheSharedSceneReferences()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            BuildSite.TowerKind? kind = candidate.name switch
            {
                "Archer Projectile Pool" => BuildSite.TowerKind.Archer,
                "Mage Projectile Pool" => BuildSite.TowerKind.Mage,
                "Bomber Projectile Pool" => BuildSite.TowerKind.Bomber,
                _ => null
            };
            if (kind.HasValue) projectilePools[kind.Value] = candidate;
        }
        explosionVisual = FindFirstObjectByType<BombExplosionVisual>(FindObjectsInactive.Include);
    }

    private Material GetTowerMaterial(BuildSite.TowerKind kind)
    {
        if (towerMaterials.TryGetValue(kind, out Material material) && material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        Color color = kind switch
        {
            BuildSite.TowerKind.Archer => new Color(0.15f, 0.48f, 0.38f),
            BuildSite.TowerKind.Mage => new Color(0.28f, 0.31f, 0.66f),
            _ => new Color(0.64f, 0.35f, 0.14f)
        };
        material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
        towerMaterials[kind] = material;
        return material;
    }

    private static void DisableLegacyStarterTowers()
    {
        foreach (TowerBase tower in FindObjectsByType<TowerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            tower.gameObject.SetActive(false);
    }

    private void CreateSites()
    {
        siteRoot = new GameObject("İnşa Alanları").transform;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            siteMaterial = new Material(shader)
            {
                color = BuildSite.NormalColor,
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };
        foreach (Vector3 position in SitePositions)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Boş İnşa Alanı";
            marker.transform.SetParent(siteRoot, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(SiteDiameter, 0.08f, SiteDiameter);
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = siteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            BuildSite site = marker.AddComponent<BuildSite>();
            site.Initialize(this);
            sites.Add(site);
        }
    }

    private void OnDestroy()
    {
        if (siteRoot != null) Destroy(siteRoot.gameObject);
        foreach (Material material in towerMaterials.Values)
            if (material != null) Destroy(material);
        towerMaterials.Clear();
        if (siteMaterial != null) Destroy(siteMaterial);
    }
}
