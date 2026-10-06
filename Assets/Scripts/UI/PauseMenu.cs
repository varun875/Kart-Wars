using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Pause menu UI, input toggling, room code display, and multiplayer Leave/Quit handling.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const string DefaultMainMenuScene = "Main Menu";

    [Header("UI References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TextMeshProUGUI roomCodeText;

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
        UpdateRoomCodeDisplay();
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
    /// Updates the room code UI text formatted as XXX-XXX.
    /// </summary>
    public void UpdateRoomCodeDisplay()
    {
        string rawCode = GetCurrentRoomCode();
        if (string.IsNullOrEmpty(rawCode)) return;

        string formatted = HostManager.FormatJoinCode(rawCode);

        // Auto-discover text component if not explicitly assigned
        if (roomCodeText == null && pausePanel != null)
        {
            var texts = pausePanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name.ToLowerInvariant().Contains("code") || t.gameObject.name.ToLowerInvariant().Contains("room"))
                {
                    roomCodeText = t;
                    break;
                }
            }

            // Fallback: reuse or create a small HUD text if not found
            if (roomCodeText == null)
            {
                Transform existing = pausePanel.transform.Find("RoomCodeDisplay");
                if (existing != null)
                {
                    roomCodeText = existing.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    GameObject codeObj = new GameObject("RoomCodeDisplay", typeof(RectTransform), typeof(TextMeshProUGUI));
                    codeObj.transform.SetParent(pausePanel.transform, false);
                    roomCodeText = codeObj.GetComponent<TextMeshProUGUI>();
                    roomCodeText.fontSize = 20;
                    roomCodeText.alignment = TextAlignmentOptions.TopRight;
                    roomCodeText.color = new Color(1f, 1f, 1f, 0.85f);
                    RectTransform rt = codeObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-20, -20);
                    rt.sizeDelta = new Vector2(300, 40);
                }
            }
        }

        if (roomCodeText != null)
        {
            roomCodeText.text = $"Room Code: {formatted}";
            roomCodeText.gameObject.SetActive(true);
        }
    }

    private string GetCurrentRoomCode()
    {
        if (HostManager.Instance != null && !string.IsNullOrEmpty(HostManager.Instance.GetRelayJoinCode()))
        {
            return HostManager.Instance.GetRelayJoinCode();
        }

        if (!string.IsNullOrEmpty(ClientManager.LastJoinedRoomCode))
        {
            return ClientManager.LastJoinedRoomCode;
        }

        if (InGameNetworkManager.Instance != null && !string.IsNullOrEmpty(InGameNetworkManager.Instance.RoomCode))
        {
            return InGameNetworkManager.Instance.RoomCode;
        }

        return string.Empty;
    }

    /// <summary>
    /// Pause the game
    /// </summary>
    public void Pause()
    {
        bool isMultiplayer = NetworkManager.Singleton != null && 
            (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);

        if (disableInMultiplayer && isMultiplayer)
        {
            return;
        }

        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        UpdateRoomCodeDisplay();

        // Only freeze time in true single player offline mode
        if (!isMultiplayer)
        {
            Time.timeScale = 0f;
        }

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Disable player input
        DisablePlayerInput();

        // Play sound
        if (pauseSound != null && audioSource != null)
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

        // Hide cursor for gameplay
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Enable player input
        EnablePlayerInput();

        // Play sound
        if (unpauseSound != null && audioSource != null)
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
    /// In-game Leave button: client does Shutdown() and goes to Main Menu.
    /// Host deletes the lobby, stops the heartbeat, shuts down, and goes to Main Menu (Requirement 6).
    /// </summary>
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                Debug.Log("[PauseMenu] Host leaving: deleting lobby, stopping heartbeat, and shutting down...");
                if (HostManager.Instance != null)
                {
                    HostManager.Instance.ResetHostData();
                }
            }
            else
            {
                Debug.Log("[PauseMenu] Client leaving: shutting down NGO connection...");
            }

            if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        SceneManager.LoadScene(DefaultMainMenuScene);
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
        var localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer != null)
        {
            var kart = localPlayer.GetComponent<MirrorKartController>();
            if (kart != null)
            {
                kart.DisableControls();
            }
        }
    }

    private void EnablePlayerInput()
    {
        var localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer != null)
        {
            var kart = localPlayer.GetComponent<MirrorKartController>();
            if (kart != null)
            {
                var health = localPlayer.GetComponent<PlayerHealth>();
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
