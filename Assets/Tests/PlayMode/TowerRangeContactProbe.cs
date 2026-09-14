using UnityEngine;

// Records actual physics callbacks so the boundary regression cannot accidentally pass via re-entry.
public class TowerRangeContactProbe : MonoBehaviour
{
    public Collider WatchedCollider;
    public int EnterCount;
    public int ExitCount;

    private void OnTriggerEnter(Collider other)
    {
        if (other == WatchedCollider) EnterCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == WatchedCollider) ExitCount++;
    }
}
