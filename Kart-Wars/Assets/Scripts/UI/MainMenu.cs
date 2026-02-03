using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple main menu with play and quit functionality.
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

    [Header("Audio")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip menuMusic;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
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
    }

    /// <summary>
    /// Start the game / go to lobby
    /// </summary>
    public void PlayGame()
    {
        PlayClickSound();

        string sceneName = useLobby ? lobbySceneName : gameSceneName;
        
        // Check if we have loading screen
        var loadingScreen = FindAnyObjectByType<KartWarsLoadingScreen>();
        if (loadingScreen != null)
        {
            loadingScreen.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
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

    private void HideAllPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    private void PlayClickSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }
}
