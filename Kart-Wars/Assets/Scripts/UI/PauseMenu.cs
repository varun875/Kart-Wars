using UnityEngine;
using Mirror;

/// <summary>
/// Pause menu UI and input toggling.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private bool disableInMultiplayer = false;

    [Header("Audio")]
    [SerializeField] private AudioClip pauseSound;
    [SerializeField] private AudioClip unpauseSound;

    private bool isPaused = false;
    private AudioSource audioSource;

    public bool IsPaused => isPaused;

    public static PauseMenu Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        Resume();
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    /// <summary>
    /// Pause the game
    /// </summary>
    public void Pause()
    {
        // Check if we should pause in multiplayer
        if (disableInMultiplayer && NetworkClient.isConnected)
        {
            return;
        }

        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        // Only freeze time in single player
        if (!NetworkClient.isConnected)
        {
            Time.timeScale = 0f;
        }

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Disable player input
        DisablePlayerInput();

        // Play sound
        if (pauseSound != null)
        {
            audioSource.PlayOneShot(pauseSound);
        }
    }

    /// <summary>
    /// Resume the game
    /// </summary>
    public void Resume()
    {
        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        Time.timeScale = 1f;

        // Hide cursor (for gameplay)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Enable player input
        EnablePlayerInput();

        // Play sound
        if (unpauseSound != null)
        {
            audioSource.PlayOneShot(unpauseSound);
        }
    }

    /// <summary>
    /// Open settings sub-panel
    /// </summary>
    public void OpenSettings()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Close settings and return to pause menu
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    /// <summary>
    /// Quit to main menu
    /// </summary>
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkClient.isConnected)
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
            }
            else
            {
                NetworkManager.singleton.StopClient();
            }
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Quit the application
    /// </summary>
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private void DisablePlayerInput()
    {
        if (NetworkClient.localPlayer != null)
        {
            var kart = NetworkClient.localPlayer.GetComponent<MirrorKartController>();
            if (kart != null)
            {
                kart.DisableControls();
            }
        }
    }

    private void EnablePlayerInput()
    {
        if (NetworkClient.localPlayer != null)
        {
            var kart = NetworkClient.localPlayer.GetComponent<MirrorKartController>();
            if (kart != null)
            {
                var health = NetworkClient.localPlayer.GetComponent<PlayerHealth>();
                if (health == null || !health.IsDead)
                {
                    kart.EnableControls();
                }
            }
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
