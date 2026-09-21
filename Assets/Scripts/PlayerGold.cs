using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGold : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingGold = 300;

    public int CurrentGold { get; private set; }
    public event Action Changed;

    private void Awake()
    {
        // Older scene data may deserialize a newly-added integer as zero.
        // This game always starts with enough gold to build a first defence.
        startingGold = Mathf.Max(300, startingGold);
        CurrentGold = startingGold;
    }

    public bool CanAfford(int amount) => amount >= 0 && CurrentGold >= amount;

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        CurrentGold += amount;
        Changed?.Invoke();
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0 || !CanAfford(amount)) return false;
        CurrentGold -= amount;
        Changed?.Invoke();
        return true;
    }
}
