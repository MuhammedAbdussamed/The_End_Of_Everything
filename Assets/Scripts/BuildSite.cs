using UnityEngine;

[DisallowMultipleComponent]
public class BuildSite : MonoBehaviour
{
    public enum TowerKind { Archer, Mage, Bomber }

    public static readonly Color NormalColor = new(0.62f, 0.48f, 0.34f, 1f);
    public static readonly Color HighlightColor = new(0.92f, 0.84f, 0.70f, 1f);

    private TowerBuildSystem buildSystem;
    private TowerBase tower;
    private Renderer markerRenderer;
    private MaterialPropertyBlock colorBlock;
    private int colorProperty;
    private Vector3 normalScale;

    public bool IsEmpty => tower == null;
    public TowerBase Tower => tower;

    public void Initialize(TowerBuildSystem owner)
    {
        buildSystem = owner;
        markerRenderer = GetComponent<Renderer>();
        Material material = markerRenderer != null ? markerRenderer.sharedMaterial : null;
        if (material != null)
        {
            colorProperty = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            colorBlock = new MaterialPropertyBlock();
        }
        normalScale = transform.localScale;
    }

    public bool TryBuild(TowerKind kind, PlayerGold gold)
    {
        if (!IsEmpty || buildSystem == null || gold == null) return false;
        int cost = buildSystem.GetBuildCost(kind);
        if (!gold.TrySpendGold(cost)) return false;

        tower = buildSystem.CreateTower(kind, this);
        if (tower == null)
        {
            gold.AddGold(cost);
            return false;
        }

        SetMarkerVisible(false);
        return true;
    }

    public void ClearTower(TowerBase soldTower)
    {
        if (tower != soldTower) return;
        tower = null;
        SetMarkerVisible(true);
        Destroy(soldTower.gameObject);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (!IsEmpty) return;
        transform.localScale = highlighted
            ? new Vector3(normalScale.x * 1.08f, normalScale.y, normalScale.z * 1.08f)
            : normalScale;
        if (markerRenderer == null || colorBlock == null) return;
        markerRenderer.GetPropertyBlock(colorBlock);
        colorBlock.SetColor(colorProperty, highlighted ? HighlightColor : NormalColor);
        markerRenderer.SetPropertyBlock(colorBlock);
    }

    private void SetMarkerVisible(bool visible)
    {
        if (markerRenderer != null) markerRenderer.enabled = visible;
        Collider markerCollider = GetComponent<Collider>();
        if (markerCollider != null) markerCollider.enabled = visible;
        if (visible) SetHighlighted(false);
    }
}
