using UnityEngine;

[CreateAssetMenu(fileName = "BomberTowerData", menuName = "Tower Defence/Bomber Tower Data")]
public class BomberTowerData : TowerData
{
    [Header("Explosion")]
    [SerializeField, Min(0.1f)] private float blastRadius = 3f;

    public float BlastRadius => blastRadius;
}
