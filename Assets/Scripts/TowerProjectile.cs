using UnityEngine; // 1

[RequireComponent(typeof(Rigidbody))] // 1
[RequireComponent(typeof(SphereCollider))] // 1
public class TowerProjectile : MonoBehaviour // 1
{
    private const float MaxLifetime = 5f; // 1
    private Rigidbody body; // 1
    private TowerBase owner; // 1
    private Vector3 initialLocalPosition; // 1
    private Quaternion initialLocalRotation; // 1
    private Vector3 initialLocalScale; // 1
    private bool initialized; // 1
    private bool launched; // 1
    private float remainingLifetime; // 1

    /// <summary>Projectile'ın fizik ve başlangıç değerlerini kaydedip havuza hazırlar.</summary> // 1
    public void Initialize() // 1
    {
        ResetToPool(); // 1
    }

    /// <summary>Projectile'ı hedef yönünde Rigidbody hızıyla ateşler.</summary> // 1
    public void Launch(TowerBase projectileOwner, Vector3 direction, float speed) // 1
    {
        CacheInitialState(); // 1
        if (body == null) return; // 1
        owner = projectileOwner; // 1
        launched = true; // 1
        remainingLifetime = MaxLifetime; // 1
        body.isKinematic = false; // 1
        body.useGravity = false; // 1
        body.detectCollisions = true; // 1
        body.position = transform.position; // 1
        body.rotation = transform.rotation; // 1
        body.linearVelocity = direction.normalized * speed; // 1
        body.angularVelocity = Vector3.zero; // 1
        body.WakeUp(); // 1
    }

    /// <summary>Hedefe ulaşamayan projectile'ı güvenli bir süre sonunda geri döndürür.</summary> // 1
    private void Update() // 1
    {
        if (!launched) return; // 1
        remainingLifetime -= Time.deltaTime; // 1
        if (owner == null || remainingLifetime <= 0f) // 1
        {
            if (owner != null) owner.RecycleProjectile(this); // 1
            else ResetToPool(); // 1
        }
    }

    /// <summary>Projectile'ı fizik, konum ve hesapları sıfırlanmış şekilde eski yerine koyar.</summary> // 1
    public void ResetToPool() // 1
    {
        CacheInitialState(); // 1
        launched = false; // 1
        remainingLifetime = 0f; // 1
        owner = null; // 1
        if (body != null) // 1
        {
            if (!body.isKinematic) // 1
            {
                body.linearVelocity = Vector3.zero; // 1
                body.angularVelocity = Vector3.zero; // 1
            }
            body.isKinematic = true; // 1
            body.useGravity = false; // 1
            body.detectCollisions = true; // 1
            body.Sleep(); // 1
        }
        transform.localPosition = initialLocalPosition; // 1
        transform.localRotation = initialLocalRotation; // 1
        transform.localScale = initialLocalScale; // 1
        if (body != null) // 1
        {
            body.position = transform.position; // 1
            body.rotation = transform.rotation; // 1
        }
        gameObject.SetActive(false); // 1
    }

    /// <summary>Projectile bir düşmana çarptığında kendi kulesine geri bildirim gönderir.</summary> // 1
    private void OnTriggerEnter(Collider other) // 1
    {
        if (!launched) return; // 1
        PathEnemy enemy = other.GetComponentInParent<PathEnemy>(); // 1
        if (enemy != null && owner != null) owner.ProjectileHit(this, enemy); // 1
    }

    /// <summary>Havuz konumunu yalnızca bir kez kaydeder ve projectile collider'ını hazırlar.</summary> // 1
    private void CacheInitialState() // 1
    {
        if (body == null) body = GetComponent<Rigidbody>(); // 1
        SphereCollider projectileCollider = GetComponent<SphereCollider>(); // 1
        if (projectileCollider != null) projectileCollider.isTrigger = true; // 1
        if (initialized) return; // 1

        initialLocalPosition = transform.localPosition; // 1
        initialLocalRotation = transform.localRotation; // 1
        initialLocalScale = transform.localScale; // 1
        initialized = true; // 1
    }
}
