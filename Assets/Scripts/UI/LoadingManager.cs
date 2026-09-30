using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class LoadingSceneManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Settings")]
    public float minLoadingTime = 0.5f;
    public float sliderLerpSpeed = 8f;

    private const string TARGET_SCENE = "Main Menu";

    private AsyncOperation asyncLoad;
    private float loadingStartTime;
    private bool hasSlider;
    private bool hasLoadingText;
    private bool hasStatusText;
    private float targetProgress = 0f;

    void Start()
    {
        LogDebug($"LoadingSceneManager starting — target: '{TARGET_SCENE}'");

        // Cache null checks once
        hasSlider = loadingSlider != null;
        hasLoadingText = loadingText != null;
        hasStatusText = statusText != null;

        if (!hasSlider)
        {
            Debug.LogError("❌ loadingSlider is NULL! Drag your Slider into the Inspector slot!");
        }
        else
        {
            loadingSlider.minValue = 0f;
            loadingSlider.maxValue = 1f;
            loadingSlider.value = 0f;
            LogDebug("Slider initialized");
        }

        // Correct scene name lookup by iterating build settings
        bool sceneFound = false;
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        LogDebug($"Scenes in Build Settings ({sceneCount} total):");
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            LogDebug($"   [{i}] {sceneName}");

            if (sceneName == TARGET_SCENE)
            {
                sceneFound = true;
                LogDebug($"✅ Scene '{TARGET_SCENE}' found at build index {i}");
            }
        }

        if (!sceneFound)
        {
            Debug.LogError($"❌ Scene '{TARGET_SCENE}' NOT found in Build Settings!");
            Debug.LogError("Fix: File → Build Settings → Add your scenes");
            HandleLoadFailure();
            return;
        }

        loadingStartTime = Time.time;
        StartCoroutine(LoadSceneAsync());
    }

    void Update()
    {
        if (hasSlider)
        {
            loadingSlider.value = Mathf.MoveTowards(loadingSlider.value, targetProgress, Time.deltaTime * sliderLerpSpeed * 0.1f);
        }
    }

    IEnumerator LoadSceneAsync()
    {
        LogDebug("LoadSceneAsync coroutine started");

        asyncLoad = SceneManager.LoadSceneAsync(TARGET_SCENE);

        if (asyncLoad == null)
        {
            Debug.LogError($"❌ SceneManager.LoadSceneAsync returned NULL for '{TARGET_SCENE}'");
            HandleLoadFailure();
            yield break;
        }

        asyncLoad.allowSceneActivation = false;
        LogDebug("allowSceneActivation set to false");

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            targetProgress = progress;

            LogDebug($"Loading: raw={asyncLoad.progress:F2}, normalized={progress:F2}");

            // Update text
            if (hasLoadingText)
                loadingText.text = $"Loading... {(int)(progress * 100)}%";

            // Update status text
            if (hasStatusText)
            {
                if (progress < 0.3f) statusText.text = "Loading...";
                else if (progress < 0.6f) statusText.text = "Preparing...";
                else if (progress < 0.9f) statusText.text = "Finalizing...";
                else statusText.text = "Ready!";
            }

            // Activate scene when ready and min time has elapsed
            bool timeElapsed = (Time.time - loadingStartTime) >= minLoadingTime;
            if (asyncLoad.progress >= 0.9f && timeElapsed)
            {
                targetProgress = 1f;

                // Wait until bar visually fills to 1.0 before switching
                while (hasSlider && loadingSlider.value < 1f)
                {
                    yield return null;
                }

                LogDebug("Activating scene...");
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        LogDebug("Scene loading complete!");
    }

    private void HandleLoadFailure()
    {
        Debug.LogError("❌ Load failed — falling back to scene index 0");
        if (SceneManager.sceneCountInBuildSettings > 0)
            SceneManager.LoadScene(0);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogDebug(string message)
    {
        Debug.Log($"[LoadingSceneManager] {message}");
    }
}