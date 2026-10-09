using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Invincibility (i-frames)")]
    [SerializeField] private float invincibilityDuration = 0.8f;
    private float lastDamageTime = -999f;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 3f;

    [Header("UI")]
    [SerializeField] private GameOverUI gameOverUI;
    // Events - UI and other systems subscribe to these
    public event Action<int, int> OnHealthChanged;   // (current, max)
    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Called by enemies. Kept SendMessage-compatible AND directly callable.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        // i-frame check
        if (Time.time - lastDamageTime < invincibilityDuration) return;
        lastDamageTime = Time.time;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealed?.Invoke();
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (gameOverUI != null)
            gameOverUI.ShowGameOver();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}