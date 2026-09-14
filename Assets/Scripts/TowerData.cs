using UnityEngine;

/// <summary>
/// Shared base values for a family of towers. Create one asset per tower archetype
/// and assign it to a TowerBase component.
/// </summary>
[CreateAssetMenu(fileName = "TowerData", menuName = "Tower Defence/Tower Data")]
public class TowerData : ScriptableObject
{
    /// <summary>
    /// Damage categories supported by towers.
    /// </summary>
    public enum TowerDamageType
    {
        Physical,
        Magic
    }

    [Header("Tower Stats")]
    [SerializeField, Min(0.1f)] private float towerRange = 5f;
    [SerializeField, Min(1)] private int towerLevel = 1;
    [SerializeField, Min(0.01f)] private float towerAttackSpeed = 1f;
    [SerializeField, Min(0f)] private float towerDamage = 10f;
    [SerializeField] private TowerDamageType damageType = TowerDamageType.Physical;

    public float TowerRange => towerRange;

    public int TowerLevel => Mathf.Max(1, towerLevel);

    public float TowerAttackSpeed => towerAttackSpeed;

    public float TowerDamage => towerDamage;

    public TowerDamageType DamageType => damageType;
}
