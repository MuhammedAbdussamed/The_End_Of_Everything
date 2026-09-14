using UnityEngine;

[DisallowMultipleComponent]
public class PathWaypoint : MonoBehaviour
{
    [SerializeField, Min(0f)] private float radius = 0.8f;

    public float Radius => radius;

    /// <summary>Bu noktanın yatay dairesi içinde her düşman için ayrı bir hedef seçer.</summary>
    public Vector3 GetRandomPosition()
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 previous = transform.position + Vector3.right * radius;
        for (int segment = 1; segment <= 32; segment++)
        {
            float angle = segment * Mathf.PI * 2f / 32f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawSphere(transform.position, 0.08f);
    }
}
