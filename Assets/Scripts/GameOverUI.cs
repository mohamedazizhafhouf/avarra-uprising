using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The dark full-screen image (CanvasGroup recommended).")]
    [SerializeField] private CanvasGroup darkBackground;

    [Tooltip("The 'Game Over' text.")]
    [SerializeField] private CanvasGroup gameOverText;

    [Header("Timing")]
    [Tooltip("How long the fade-in takes (seconds).")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("Delay after fade completes before returning to menu.")]
    [SerializeField] private float delayBeforeMenu = 5f;

    [Header("Scene")]
    [Tooltip("Exact name of the main menu scene (must be in Build Settings).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Optional")]
    [Tooltip("If true, time will be frozen while the Game Over screen is showing.")]
    [SerializeField] private bool pauseGameDuringGameOver = true;

    private bool isShowing;

    /// <summary>
    /// Call this from PlayerHealth's OnDeath event, or any other trigger.
    /// </summary>
    public void ShowGameOver()
    {
        if (isShowing) return;
        isShowing = true;

        if (pauseGameDuringGameOver)
            Time.timeScale = 0f;

        // Hide text initially, show background fully transparent
        if (darkBackground != null)
        {
            darkBackground.alpha = 0f;
            darkBackground.gameObject.SetActive(true);
        }
        if (gameOverText != null)
        {
            gameOverText.alpha = 0f;
            gameOverText.gameObject.SetActive(true);
        }

        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // Use unscaled time so this works even if Time.timeScale = 0
        float t = 0f;

        // Fade in dark background
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            if (darkBackground != null)
                darkBackground.alpha = a;
            yield return null;
        }
        if (darkBackground != null)
            darkBackground.alpha = 1f;

        // Small pause, then fade in text
        yield return new WaitForSecondsRealtime(0.2f);

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            if (gameOverText != null)
                gameOverText.alpha = a;
            yield return null;
        }
        if (gameOverText != null)
            gameOverText.alpha = 1f;

        // Wait the required delay, then load the menu
        yield return new WaitForSecondsRealtime(delayBeforeMenu);

        // Restore time before changing scenes (important!)
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}