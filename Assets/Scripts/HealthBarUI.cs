using UnityEngine;
using UnityEngine.UI;
using TMPro; // Remove if using legacy Text

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI healthText;   // Optional

    [Header("Smoothing")]
    [SerializeField] private bool smooth = true;
    [SerializeField] private float smoothSpeed = 6f;

    [Header("UI")]
    [SerializeField] private GameOverUI gameOverUI;

    private float targetFill = 1f;

    void OnEnable()
    {
        if (playerHealth == null)
        {
            // Auto-find the player if not assigned
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerHealth = go.GetComponent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealth;
            // Initialize immediately with current values
            UpdateHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealth;
    }

    void Update()
    {
        if (smooth && fillImage != null)
        {
            fillImage.fillAmount = Mathf.Lerp(
                fillImage.fillAmount, targetFill, Time.deltaTime * smoothSpeed);

            if (Mathf.Abs(fillImage.fillAmount - targetFill) < 0.001f)
                fillImage.fillAmount = targetFill;
        }
    }

    private void UpdateHealth(int current, int max)
    {
        targetFill = max > 0 ? (float)current / max : 0f;

        if (!smooth && fillImage != null)
            fillImage.fillAmount = targetFill;

        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }

}