using System.Collections;
using TMPro;                       // remove if using legacy Text
using UnityEngine;
using UnityEngine.UI;

public class RhinoHealthUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private GameObject healthBarRoot;      // the world-space canvas
    [SerializeField] private Image healthFill;              // the Filled image

    [Header("Slain Text")]
    [SerializeField] private CanvasGroup slainTextGroup;    // on the screen-space text
    [SerializeField] private float slainDisplayTime = 2f;
    [SerializeField] private float slainFadeTime = 1f;

    [Header("References")]
    [SerializeField] private RhinoAI rhino;

    private int maxHealth;
    private int currentHealth;
    private bool hasBeenSeen;
    private bool isDead;

    void Awake()
    {
        if (rhino == null) rhino = GetComponent<RhinoAI>();

        // Hide everything at start
        if (healthBarRoot != null) healthBarRoot.SetActive(false);
        if (slainTextGroup != null) slainTextGroup.alpha = 0f;
    }

    void Start()
    {
        // Initialize from RhinoAI's max health
        maxHealth = rhino != null ? rhino.maxHealth : 100;
        currentHealth = maxHealth;
        UpdateFill();
    }

    /// <summary>
    /// Called by RhinoDetector when the player enters detectRange.
    /// </summary>
    public void ShowHealthBar()
    {
        if (isDead) return;
        if (hasBeenSeen) return;    // only show once, if you prefer

        hasBeenSeen = true;
        if (healthBarRoot != null) healthBarRoot.SetActive(true);
    }

    /// <summary>
    /// Call this whenever the rhino takes damage.
    /// </summary>
    public void SetHealth(int current, int max)
    {
        currentHealth = Mathf.Clamp(current, 0, max);
        maxHealth = max;
        UpdateFill();

        // Show the bar if it got hit before being detected
        if (!hasBeenSeen && current < max)
            ShowHealthBar();
    }

    /// <summary>
    /// Call this from RhinoAI.Die().
    /// </summary>
    public void OnRhinoDied()
    {
        if (isDead) return;
        isDead = true;

        // Hide the health bar immediately
        if (healthBarRoot != null) healthBarRoot.SetActive(false);

        // Show the "Enemy Slain" text
        if (slainTextGroup != null)
            StartCoroutine(SlainRoutine());
    }

    private void UpdateFill()
    {
        if (healthFill != null)
            healthFill.fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }

    private IEnumerator SlainRoutine()
    {
        // Fade in
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            slainTextGroup.alpha = Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        slainTextGroup.alpha = 1f;

        // Hold
        yield return new WaitForSeconds(slainDisplayTime);

        // Fade out
        t = 0f;
        while (t < slainFadeTime)
        {
            t += Time.deltaTime;
            slainTextGroup.alpha = 1f - Mathf.Clamp01(t / slainFadeTime);
            yield return null;
        }
        slainTextGroup.alpha = 0f;
    }
}