using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Async scene loading screen with progress bar and messages.
/// </summary>
public class KartWarsLoadingScreen : MonoBehaviour
{
    public static KartWarsLoadingScreen Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Image loadingIcon;

    [Header("Settings")]
    [SerializeField] private float minimumLoadTime = 1f;
    [SerializeField] private float iconRotationSpeed = 180f;
    [SerializeField] private bool smoothProgress = true;

    [Header("Tips")]
    [SerializeField] private string[] loadingTips = new string[]
    {
        "Use drift to take corners faster!",
        "Boomerangs return to you after being thrown.",
        "Mines arm after a short delay.",
        "Collect weapon pickups to arm yourself!",
        "Watch out for falling off the track!",
        "Press SPACE to respawn after being eliminated.",
        "Use the spectator mode to watch other players."
    };

    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private bool isLoading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Hide();
    }

    /// <summary>
    /// Load a scene asynchronously with loading screen
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    /// <summary>
    /// Load a scene by build index
    /// </summary>
    public void LoadScene(int sceneIndex)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneAsync(sceneIndex));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        Show();

        float startTime = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        yield return StartCoroutine(TrackProgress(operation, startTime));

        operation.allowSceneActivation = true;

        // Wait for scene to fully load
        while (!operation.isDone)
        {
            yield return null;
        }

        Hide();
    }

    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        Show();

        float startTime = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        operation.allowSceneActivation = false;

        yield return StartCoroutine(TrackProgress(operation, startTime));

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        Hide();
    }

    private IEnumerator TrackProgress(AsyncOperation operation, float startTime)
    {
        while (!operation.isDone)
        {
            // Progress goes from 0 to 0.9 while loading
            targetProgress = Mathf.Clamp01(operation.progress / 0.9f);

            if (smoothProgress)
            {
                currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.unscaledDeltaTime * 2f);
            }
            else
            {
                currentProgress = targetProgress;
            }

            UpdateUI(currentProgress);

            // Rotate loading icon
            if (loadingIcon != null)
            {
                loadingIcon.transform.Rotate(0, 0, -iconRotationSpeed * Time.unscaledDeltaTime);
            }

            // Check if loading is complete and minimum time has passed
            if (operation.progress >= 0.9f)
            {
                float elapsedTime = Time.unscaledTime - startTime;
                if (elapsedTime >= minimumLoadTime)
                {
                    // Final progress update
                    currentProgress = 1f;
                    UpdateUI(1f);
                    yield return new WaitForSecondsRealtime(0.2f);
                    break;
                }
            }

            yield return null;
        }
    }

    private void UpdateUI(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }
    }

    private void Show()
    {
        isLoading = true;
        currentProgress = 0f;
        targetProgress = 0f;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        UpdateUI(0f);
        ShowRandomTip();
    }

    private void Hide()
    {
        isLoading = false;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    private void ShowRandomTip()
    {
        if (tipText != null && loadingTips.Length > 0)
        {
            tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
        }
    }

    /// <summary>
    /// Set a custom loading message
    /// </summary>
    public void SetLoadingMessage(string message)
    {
        if (tipText != null)
        {
            tipText.text = message;
        }
    }
}
