using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    public event Action<float, float> HealthChanged;
    public event Action Died;

    [SerializeField, Min(1f)] private float maxHealth = 100f;

    private float currentHealth;
    private float equipmentMaxHealthBonus;
    private bool isDead;
    private PlayerMovement movement;
    private PlayerAim aim;
    private PlayerWeapon weapon;
    private Rigidbody body;
    [SerializeField] private RunSessionController runSessionController;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth + equipmentMaxHealthBonus;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = MaxHealth;
        movement = GetComponent<PlayerMovement>();
        aim = GetComponent<PlayerAim>();
        weapon = GetComponent<PlayerWeapon>();
        body = GetComponent<Rigidbody>();

        if (runSessionController == null)
        {
            runSessionController = FindFirstObjectByType<RunSessionController>();
        }
    }

    private void Start()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"Player took {damage:0.#} damage. HP: {currentHealth:0.#} / {MaxHealth:0.#}", this);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void SetEquipmentMaxHealthBonus(float bonus)
    {
        float previousMaxHealth = MaxHealth;
        equipmentMaxHealthBonus = Mathf.Max(0f, bonus);
        float newMaxHealth = MaxHealth;

        if (!isDead)
        {
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, newMaxHealth - previousMaxHealth), 0f, newMaxHealth);
            HealthChanged?.Invoke(currentHealth, newMaxHealth);
        }
    }

    public void RestoreHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, MaxHealth);
        isDead = currentHealth <= 0f;
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (movement != null)
        {
            movement.enabled = false;
        }

        if (aim != null)
        {
            aim.enabled = false;
        }

        if (weapon != null)
        {
            weapon.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        runSessionController?.FailRun();
        Died?.Invoke();
        Debug.Log("Player died.", this);
    }
}
