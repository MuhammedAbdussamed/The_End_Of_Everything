using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDefeated => CurrentHealth <= 0f;
    public event Action Changed;

    private void Awake() => CurrentHealth = maxHealth;

    public void TakeDamage(float amount)
    {
        if (IsDefeated || amount <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        Changed?.Invoke();
    }
}
