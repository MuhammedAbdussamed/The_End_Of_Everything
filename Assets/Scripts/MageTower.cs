using UnityEngine; // 1

[DisallowMultipleComponent] // 1
public class MageTower : TowerBase // 1
{
    [Header("Projectile Settings")] // 1
    [SerializeField] private float projectileVelocity = 8f; // 1

    /// <summary>Büyücü projectile hızını ortak fırlatma sistemine verir.</summary> // 1
    protected override float GetProjectileVelocity() => projectileVelocity; // 1
}
