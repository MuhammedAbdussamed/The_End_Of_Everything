using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BombExplosionVisual : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.3f;
    private readonly Vector3[] ringPoints = new Vector3[48];
    private float remainingTime;

    public void Show(Vector3 position, float radius)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        transform.localScale = Vector3.one;
        LineRenderer ring = GetComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = ringPoints.Length;
        for (int i = 0; i < ringPoints.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / ringPoints.Length;
            ringPoints[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }
        ring.SetPositions(ringPoints);
        remainingTime = duration;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f) gameObject.SetActive(false);
    }
}
