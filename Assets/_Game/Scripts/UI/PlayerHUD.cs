using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;

    private RectTransform healthFillRect;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        ValidateAuthoredView();
        healthFillRect = healthFill != null ? healthFill.rectTransform : null;
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += UpdateHealth;
            UpdateHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealth;
        }
    }

    private void UpdateHealth(float currentHealth, float maxHealth)
    {
        float normalizedHealth = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        if (healthFill != null)
        {
            healthFill.fillAmount = normalizedHealth;
        }

        if (healthFillRect != null)
        {
            healthFillRect.localScale = new Vector3(normalizedHealth, 1f, 1f);
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth:0} / {maxHealth:0}";
        }
    }

    private void ValidateAuthoredView()
    {
        if (healthFill == null || healthText == null)
        {
            Debug.LogWarning("PlayerHUD requires authored healthFill and healthText references.", this);
        }
    }
}
