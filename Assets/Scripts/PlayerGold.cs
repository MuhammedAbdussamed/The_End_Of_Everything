using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGold : MonoBehaviour
{
    public int CurrentGold { get; private set; }
    public event Action Changed;

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
