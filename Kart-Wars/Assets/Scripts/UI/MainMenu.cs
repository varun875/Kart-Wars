using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu with Simple Rotating Mode - displays a spinning kart model
/// Optimized with async loading and background thread priority
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private bool useLobby = true;

    [Header("UI References")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject loadingPanel;

    [Header("Audio")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip menuMusic;

    [Header("Simple Rotating Mode")]
    [SerializeField] private GameObject rotatingKartModel;
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private Transform rotationCenter;
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float bobSpeed = 1.5f;

    [Header("Performance Settings")]
    [SerializeField] private ThreadPriority loadingPriority = ThreadPriority.Low;
    [SerializeField] private int targetFrameRate = 60;

    private AudioSource audioSource;
    private AsyncOperation asyncLoadOperation;
    private bool isLoading = false;
    private float bobTimer = 0f;
    private Vector3 kartBasePosition;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Set background loading priority to keep rotation smooth
        Application.backgroundLoadingPriority = loadingPriority;

        // Set target framerate for consistent rotation
        Application.targetFrameRate = targetFrameRate;

        // Store base position for bobbing
        if (rotatingKartModel != null)
        {
            kartBasePosition = rotatingKartModel.transform.position;
        }
    }

    private void Start()
    {
        // Play menu music
        if (menuMusic != null && audioSource != null)
        {
            audioSource.clip = menuMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Ensure cursor is visible
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Show main panel
        ShowMainPanel();

        // Start rotation if model exists
        if (rotatingKartModel == null)
        {
            Debug.LogWarning("[MainMenu] Rotating kart model not assigned. Assign a kart FBX model in the inspector.");
        }
    }

    private void Update()
    {
        // Simple Rotating Mode - Smooth rotation and bobbing
        if (rotatingKartModel != null && !isLoading)
        {
            // Rotate around Y axis
            rotatingKartModel.transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            // Bobbing motion for visual interest
            bobTimer += Time.deltaTime * bobSpeed;
            float yOffset = Mathf.Sin(bobTimer) * bobHeight;
            
            if (rotationCenter != null)
            {
                rotatingKartModel.transform.position = rotationCenter.position + Vector3.up * yOffset;
            }
            else
            {
                rotatingKartModel.transform.position = kartBasePosition + Vector3.up * yOffset;
            }
        }

        // Update loading progress if async loading
        if (isLoading && asyncLoadOperation != null)
        {
            UpdateLoadingProgress();
        }
    }

    private void UpdateLoadingProgress()
    {
        if (asyncLoadOperation != null)
        {
            float progress = Mathf.Clamp01(asyncLoadOperation.progress / 0.9f);
            UpdateLoadingUI(progress);
        }
    }

    private void UpdateLoadingUI(float progress)
    {
        // Update loading progress UI here
        Debug.Log($"[MainMenu] Loading progress: {progress:P0}");
    }

    /// <summary>
    /// Start the game / go to lobby with async loading
    /// </summary>
    public void PlayGame()
    {
        PlayClickSound();

        if (isLoading) return;

        string sceneName = useLobby ? lobbySceneName : gameSceneName;
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    /// <summary>
    /// Async scene loading with smooth rotation maintained
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        isLoading = true;

        // Show loading panel
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        HideAllPanels();

        // Set low priority for background loading to keep FPS smooth
        Application.backgroundLoadingPriority = loadingPriority;

        // Start async load
        asyncLoadOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncLoadOperation.allowSceneActivation = false;

        Debug.Log($"[MainMenu] Async loading started: {sceneName}");

        // Wait until scene is loaded (90%)
        while (asyncLoadOperation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(asyncLoadOperation.progress / 0.9f);
            UpdateLoadingUI(progress);
            yield return null; // Maintain frame rate for rotation
        }

        // Scene is loaded, allow activation
        UpdateLoadingUI(1f);
        
        // Small delay to show 100% completion
        yield return new WaitForSeconds(0.3f);

        asyncLoadOperation.allowSceneActivation = true;

        Debug.Log($"[MainMenu] Scene loaded: {sceneName}");
    }

    /// <summary>
    /// Open options panel
    /// </summary>
    public void OpenOptions()
    {
        PlayClickSound();
        HideAllPanels();
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Open credits panel
    /// </summary>
    public void OpenCredits()
    {
        PlayClickSound();
        HideAllPanels();
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Return to main panel
    /// </summary>
    public void ShowMainPanel()
    {
        PlayClickSound();
        HideAllPanels();
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Quit the application
    /// </summary>
    public void QuitGame()
    {
        PlayClickSound();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    /// <summary>
    /// Set rotation speed dynamically
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    /// <summary>
    /// Toggle rotating model on/off
    /// </summary>
    public void ToggleRotation(bool enabled)
    {
        if (rotatingKartModel != null)
        {
            rotatingKartModel.SetActive(enabled);
        }
    }

    private void HideAllPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void PlayClickSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    private void OnDestroy()
    {
        // Reset background loading priority when leaving menu
        Application.backgroundLoadingPriority = ThreadPriority.Normal;
    }
}
