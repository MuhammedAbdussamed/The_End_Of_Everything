using UnityEngine;

[CreateAssetMenu(fileName = "BomberTowerData", menuName = "Tower Defence/Bomber Tower Data")]
public class BomberTowerData : TowerData
{
    [Header("Explosion")]
    [SerializeField, Min(0.1f)] private float blastRadius = 3f;

    public float BlastRadius => blastRadius;

    public void ConfigureRuntime(float range, int level, float attackSpeed, float damage,
        TowerDamageType type, float radius)
    {
        base.ConfigureRuntime(range, level, attackSpeed, damage, type);
        blastRadius = Mathf.Max(0.1f, radius);
    }
}
