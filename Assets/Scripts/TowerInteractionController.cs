using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TowerInteractionController : MonoBehaviour
{
    [SerializeField] private GameHud hud;
    [SerializeField, Range(0f, 1f)] private float hoverBrightness = 0.18f;

    private Camera mainCamera;
    private TowerBase hoveredTower;
    private BuildSite hoveredBuildSite;
    private Renderer[] hoveredRenderers;
    private MaterialPropertyBlock[] originalBlocks;
    private TowerBase selectedTower;
    private LineRenderer rangeIndicator;
    private Material rangeMaterial;
    private Vector3 lastRangeCenter;
    private float lastRangeRadius = -1f;

    private const int RangeSegments = 72;
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[64];

    public TowerBase HoveredTower => hoveredTower;

    private void Awake()
    {
        if (hud == null) hud = GetComponent<GameHud>();
        if (GetComponent<TowerBuildSystem>() == null) gameObject.AddComponent<TowerBuildSystem>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        bool mouseDown = Input.GetMouseButtonDown(0);
        if (mouseDown && hud != null && hud.TryHandleInteractionPanelClick(Input.mousePosition))
        {
            SetHoveredTower(null);
            UpdateRangeIndicator();
            return;
        }

        bool overUi = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            || (hud != null && hud.IsPointerOverTowerPanel(Input.mousePosition));
        TowerBase tower = null;
        BuildSite buildSite = null;
        if (!overUi && hud != null && !hud.ResultVisible)
            FindTargetsAtScreenPoint(mainCamera, Input.mousePosition, out tower, out buildSite);
        SetHoveredTower(tower);
        SetHoveredBuildSite(buildSite);

        UpdateRangeIndicator();

        if (!mouseDown || hud == null) return;
        if (tower != null)
        {
            hud.OpenTowerPanel(tower);
            SelectTower(tower);
        }
        else if (buildSite != null)
        {
            hud.OpenBuildPanel(buildSite);
            SelectTower(null);
        }
        else if (!overUi)
        {
            hud.CloseTowerPanel();
            hud.CloseBuildPanel();
            SelectTower(null);
        }
    }

    public static BuildSite FindBuildSiteAtScreenPoint(Camera camera, Vector2 screenPoint)
    {
        FindTargetsAtScreenPoint(camera, screenPoint, out _, out BuildSite site);
        return site;
    }

    public static TowerBase FindTowerAtScreenPoint(Camera camera, Vector2 screenPoint)
    {
        FindTargetsAtScreenPoint(camera, screenPoint, out TowerBase tower, out _);
        return tower;
    }

    private static void FindTargetsAtScreenPoint(Camera camera, Vector2 screenPoint,
        out TowerBase nearestTower, out BuildSite nearestSite)
    {
        nearestTower = null;
        nearestSite = null;
        if (camera == null) return;

        float towerDistance = float.PositiveInfinity;
        float siteDistance = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(camera.ScreenPointToRay(screenPoint), RaycastHits, 1000f, ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = RaycastHits[i];
            if (hit.distance < towerDistance && hit.collider.TryGetComponent(out TowerBase tower)
                && tower.isActiveAndEnabled)
            {
                nearestTower = tower;
                towerDistance = hit.distance;
            }
            if (hit.distance < siteDistance && hit.collider.TryGetComponent(out BuildSite site)
                && site.isActiveAndEnabled && site.IsEmpty)
            {
                nearestSite = site;
                siteDistance = hit.distance;
            }
        }
    }

    private void SetHoveredTower(TowerBase tower)
    {
        if (tower == hoveredTower) return;
        RestoreHoverColors();
        hoveredTower = tower;
        if (hoveredTower == null) return;

        hoveredRenderers = hoveredTower.GetComponentsInChildren<Renderer>(true);
        originalBlocks = new MaterialPropertyBlock[hoveredRenderers.Length];
        for (int i = 0; i < hoveredRenderers.Length; i++)
        {
            Renderer renderer = hoveredRenderers[i];
            Material material = renderer.sharedMaterial;
            if (material == null) continue;
            int property = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            if (!material.HasProperty(property)) continue;
            var original = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original);
            Color color = original.HasColor(property) ? original.GetColor(property) : material.GetColor(property);
            var highlighted = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(highlighted);
            Color brighter = Color.Lerp(color, Color.white, hoverBrightness);
            brighter.a = color.a;
            highlighted.SetColor(property, brighter);
            renderer.SetPropertyBlock(highlighted);
            originalBlocks[i] = original;
        }
    }

    private void RestoreHoverColors()
    {
        if (hoveredRenderers != null)
        {
            for (int i = 0; i < hoveredRenderers.Length; i++)
                if (hoveredRenderers[i] != null && originalBlocks[i] != null)
                    hoveredRenderers[i].SetPropertyBlock(originalBlocks[i].isEmpty ? null : originalBlocks[i]);
        }
        hoveredTower = null;
        hoveredRenderers = null;
        originalBlocks = null;
    }

    private void SetHoveredBuildSite(BuildSite site)
    {
        if (site == hoveredBuildSite) return;
        if (hoveredBuildSite != null) hoveredBuildSite.SetHighlighted(false);
        hoveredBuildSite = site;
        if (hoveredBuildSite != null) hoveredBuildSite.SetHighlighted(true);
    }

    private void SelectTower(TowerBase tower)
    {
        selectedTower = tower;
        lastRangeRadius = -1f;
        UpdateRangeIndicator();
    }

    private void UpdateRangeIndicator()
    {
        if (hud != null && (!hud.UpgradePanelVisible || hud.SelectedTower != selectedTower))
            selectedTower = null;
        if (selectedTower == null || hud == null || hud.ResultVisible)
        {
            if (rangeIndicator != null) rangeIndicator.gameObject.SetActive(false);
            return;
        }

        EnsureRangeIndicator();
        SphereCollider rangeCollider = selectedTower.RangeCollider;
        if (rangeCollider == null)
        {
            rangeIndicator.gameObject.SetActive(false);
            return;
        }

        Vector3 scale = selectedTower.transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float radius = rangeCollider.radius * largestScale;
        Vector3 center = selectedTower.transform.TransformPoint(rangeCollider.center);
        center.y = 0.22f;

        if (!rangeIndicator.gameObject.activeSelf) rangeIndicator.gameObject.SetActive(true);
        if (Mathf.Approximately(lastRangeRadius, radius) && (lastRangeCenter - center).sqrMagnitude < 0.0001f) return;
        lastRangeRadius = radius;
        lastRangeCenter = center;
        for (int i = 0; i < RangeSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RangeSegments;
            rangeIndicator.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    private void EnsureRangeIndicator()
    {
        if (rangeIndicator != null) return;
        GameObject indicator = new GameObject("Seçili Kule Menzili");
        indicator.transform.SetParent(transform, false);
        rangeIndicator = indicator.AddComponent<LineRenderer>();
        rangeIndicator.useWorldSpace = true;
        rangeIndicator.loop = true;
        rangeIndicator.positionCount = RangeSegments;
        rangeIndicator.widthMultiplier = 0.14f;
        rangeIndicator.numCornerVertices = 3;
        rangeIndicator.numCapVertices = 3;
        rangeIndicator.startColor = new Color(1f, 0.84f, 0.38f, 0.95f);
        rangeIndicator.endColor = rangeIndicator.startColor;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            rangeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            rangeIndicator.sharedMaterial = rangeMaterial;
        }
        rangeIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeIndicator.receiveShadows = false;
    }

    private void OnDisable()
    {
        RestoreHoverColors();
        SetHoveredBuildSite(null);
        SelectTower(null);
    }

    private void OnDestroy()
    {
        if (rangeMaterial != null) Destroy(rangeMaterial);
    }
}
