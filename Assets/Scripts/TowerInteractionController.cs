using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TowerInteractionController : MonoBehaviour
{
    [SerializeField] private GameHud hud;
    [SerializeField, Range(0f, 1f)] private float hoverBrightness = 0.18f;

    private Camera mainCamera;
    private TowerBase hoveredTower;
    private Renderer[] hoveredRenderers;
    private MaterialPropertyBlock[] originalBlocks;

    public TowerBase HoveredTower => hoveredTower;

    private void Awake()
    {
        if (hud == null) hud = GetComponent<GameHud>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        bool mouseDown = Input.GetMouseButtonDown(0);
        if (mouseDown && hud != null && hud.TryHandleTowerPanelClick(Input.mousePosition))
        {
            SetHoveredTower(null);
            return;
        }

        bool overUi = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            || (hud != null && hud.IsPointerOverTowerPanel(Input.mousePosition));
        TowerBase tower = overUi || hud == null || hud.ResultVisible ? null : FindTowerAtScreenPoint(mainCamera, Input.mousePosition);
        SetHoveredTower(tower);

        if (!mouseDown || hud == null) return;
        if (tower != null) hud.OpenTowerPanel(tower);
        else if (!overUi) hud.CloseTowerPanel();
    }

    public static TowerBase FindTowerAtScreenPoint(Camera camera, Vector2 screenPoint)
    {
        if (camera == null) return null;
        RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPoint), 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            TowerBase tower = hit.collider.GetComponentInParent<TowerBase>();
            if (tower != null && tower.isActiveAndEnabled) return tower;
        }
        return null;
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

    private void OnDisable() => RestoreHoverColors();
}
