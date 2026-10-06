using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Exclusive controller for the Host Game screen/panel.
/// Manages the Back button, Copy Code button, Host button, and Room Code TMP text.
/// Integrates Unity Relay hosting with Netcode for GameObjects (NGO).
/// </summary>
public class HostGameUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Back button: Shuts down NetworkManager if listening and returns to Main Menu / Panel.")]
    [SerializeField] private Button backButton;

    [Tooltip("Copy Code button: Copies the raw join code to the clipboard.")]
    [SerializeField] private Button copyCodeButton;

    [Tooltip("Host button: Starts NGO host and loads the game scene via NetworkManager.SceneManager.")]
    [SerializeField] private Button hostButton;

    [Tooltip("Room Code text (TMP): Displays the code formatted as XXX-XXX.")]
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Tooltip("Optional status message text.")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Configuration")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool createLobby = true;
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    [Header("Managers (Optional)")]
    [SerializeField] private HostManager hostManager;

    private string _rawJoinCode = "";
    private bool _isAllocating = false;
    private bool _isHostingSuccess = false;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        _isHostingSuccess = false;
        BindButtonListeners();

        // Clear any previous stale code before generating a fresh one
        ResetHostState();

        StartRelayInitialization();
    }

    private void OnDisable()
    {
        UnbindButtonListeners();

        // If we successfully started hosting and transitioned to the game scene,
        // do NOT reset the host state or delete the lobby!
        if (_isHostingSuccess)
        {
            return;
        }

        // Only reset if backed out or cancelled
        ResetHostState();
    }

    /// <summary>
    /// Resets the current join code and instructs HostManager to clean up.
    /// Ensures reopening the screen always generates a fresh room code.
    /// </summary>
    public void ResetHostState()
    {
        _rawJoinCode = "";

        if (hostManager != null)
        {
            hostManager.ResetHostData();
        }

        if (hostButton != null)
        {
            hostButton.interactable = false;
        }

        SetRoomCodeDisplay("");
    }

    /// <summary>
    /// Binds button click events exclusively to this script.
    /// </summary>
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCodeButtonClicked);

        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostButtonClicked);
    }

    /// <summary>
    /// Removes button click events to avoid duplicates.
    /// </summary>
    private void UnbindButtonListeners()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackButtonClicked);

        if (copyCodeButton != null)
            copyCodeButton.onClick.RemoveListener(OnCopyCodeButtonClicked);

        if (hostButton != null)
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
    }

    /// <summary>
    /// Called when the screen opens: initializes services, creates Relay allocation,
    /// sets transport (dtls), displays code as XXX-XXX, and enables Host button once ready.
    /// </summary>
    public async void StartRelayInitialization()
    {
        if (_isAllocating) return;

        // Disable Host button until the code is ready (Requirement 7)
        if (hostButton != null)
            hostButton.interactable = false;

        SetRoomCodeDisplay("GENERATING...");
        SetStatus("Allocating Relay room (DTLS)...", Color.yellow);

        _isAllocating = true;

        try
        {
            EnsureHostManager();

            // Step 1 & 2: Init Unity Services, sign in anonymously, create Relay allocation (max 4 players),
            // get join code, and set transport via UnityTransport.SetRelayServerData (dtls).
            _rawJoinCode = await hostManager.CreateRelayHostAllocationAsync(maxPlayers, createLobby);

            if (string.IsNullOrEmpty(_rawJoinCode))
            {
                throw new Exception("Received empty Relay join code from service.");
            }

            // Step 6: Display the code as XXX-XXX
            string formattedCode = HostManager.FormatJoinCode(_rawJoinCode);
            SetRoomCodeDisplay(formattedCode);
            SetStatus("Room ready! Share your code.", Color.green);

            // Step 7: Enable the Host button now that code is ready
            if (hostButton != null)
                hostButton.interactable = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostGameUI] Error during Relay initialization: {ex.Message}\n{ex.StackTrace}");
            SetRoomCodeDisplay("FAILED");
            SetStatus($"Error: {ex.Message}", Color.red);

            // Keep host button disabled on error
            if (hostButton != null)
                hostButton.interactable = false;
        }
        finally
        {
            _isAllocating = false;
        }
    }

    /// <summary>
    /// Copy Code button: copies the raw join code to clipboard (Requirement 3).
    /// </summary>
    public void OnCopyCodeButtonClicked()
    {
        if (string.IsNullOrEmpty(_rawJoinCode))
        {
            SetStatus("No code available to copy yet!", Color.yellow);
            return;
        }

        GUIUtility.systemCopyBuffer = _rawJoinCode;
        Debug.Log($"[HostGameUI] Copied join code '{_rawJoinCode}' to clipboard.");
        SetStatus("Code copied to clipboard!", Color.cyan);
        StartCoroutine(CopyButtonFeedbackRoutine());
    }

    private System.Collections.IEnumerator CopyButtonFeedbackRoutine()
    {
        if (copyCodeButton == null) yield break;
        var label = copyCodeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) yield break;

        string originalText = label.text;
        label.text = "COPIED!";
        yield return new WaitForSecondsRealtime(1.5f);
        if (label != null) label.text = originalText;
    }

    /// <summary>
    /// Host button: StartHost(), then load the game scene using NetworkManager.SceneManager (Requirement 4).
    /// Automatically handles stale/expired allocations.
    /// </summary>
    public void OnHostButtonClicked()
    {
        EnsureHostManager();

        // Expired allocation check: if the room code sat unused for too long, regenerate
        if (string.IsNullOrEmpty(_rawJoinCode) || (hostManager != null && hostManager.IsAllocationStale(300f)))
        {
            Debug.LogWarning("[HostGameUI] Allocation is expired or missing. Refreshing room...");
            SetStatus("Allocation expired. Generating new code...", Color.yellow);
            StartRelayInitialization();
            return;
        }

        if (hostButton != null)
            hostButton.interactable = false;

        SetStatus("Starting host...", Color.yellow);

        try
        {
            bool success = hostManager != null && hostManager.StartHostAndLoadGameScene(gameSceneName);

            if (!success)
            {
                // Fallback direct attempt if hostManager call failed
                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
                {
                    if (NetworkManager.Singleton.StartHost())
                    {
                        if (NetworkManager.Singleton.SceneManager != null)
                        {
                            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                            success = true;
                        }
                    }
                }
            }

            if (success)
            {
                // Mark hosting success so OnDisable does not reset state or delete the lobby
                _isHostingSuccess = true;
                SetStatus("Host started! Loading game...", Color.green);
            }
            else
            {
                _isHostingSuccess = false;
                Debug.LogWarning("[HostGameUI] StartHost() failed (allocation may have expired). Refreshing room...");
                SetStatus("Host failed (allocation expired). Generating fresh code...", Color.red);
                StartRelayInitialization();
            }
        }
        catch (Exception ex)
        {
            _isHostingSuccess = false;
            Debug.LogError($"[HostGameUI] Failed to start host: {ex.Message}\n{ex.StackTrace}");
            SetStatus("Failed to start host. Generating fresh code...", Color.red);
            StartRelayInitialization();
        }
    }

    /// <summary>
    /// Back button: Shutdown() if listening, cleans up host state, then returns to main menu/panel (Requirement 5).
    /// </summary>
    public void OnBackButtonClicked()
    {
        Debug.Log("[HostGameUI] Back button clicked — returning to Main Menu/Panel");

        _isHostingSuccess = false;

        // Clean up any pending host allocation / lobby
        ResetHostState();

        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            Debug.Log("[HostGameUI] Shutting down active NetworkManager connection...");
            NetworkManager.Singleton.Shutdown();
        }

        // Return to main panel if in multi-panel scene, else load Main Menu scene
        var navigator = FindFirstObjectByType<PanelNavigator>();
        if (navigator != null && navigator.mainPanel != null)
        {
            navigator.ShowMain();
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void EnsureHostManager()
    {
        NetworkManagerHelper.EnsureTransport();

        if (hostManager == null)
        {
            hostManager = HostManager.Instance != null 
                ? HostManager.Instance 
                : FindFirstObjectByType<HostManager>();

            if (hostManager == null)
            {
                var go = new GameObject("HostManager");
                hostManager = go.AddComponent<HostManager>();
            }
        }
    }

    private void SetRoomCodeDisplay(string text)
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = text;
        }
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"[HostGameUI] {message}");
    }

    /// <summary>
    /// Fallback auto-detection of UI components so the script functions immediately
    /// even without modifying serialized properties in scene files.
    /// Emits clear console warnings if expected components are not found.
    /// </summary>
    private void AutoFindReferences()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name.ToLowerInvariant();

            if (backButton == null && bName.Contains("back"))
            {
                backButton = btn;
            }
            else if (copyCodeButton == null && (bName.Contains("copy") || bName.Contains("clipboard")))
            {
                copyCodeButton = btn;
            }
            else if (hostButton == null && bName.Contains("host") && !bName.Contains("back"))
            {
                hostButton = btn;
            }
        }

        if (roomCodeText == null)
        {
            TextMeshProUGUI[] tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmps)
            {
                string tName = tmp.gameObject.name.ToLowerInvariant();
                if (tName.Contains("roomcode") || tName.Contains("code"))
                {
                    roomCodeText = tmp;
                    break;
                }
            }
        }

        EnsureHostManager();

        // Detailed console warnings for any missing references
        if (backButton == null)
            Debug.LogWarning("[HostGameUI] 'Back Button' was not found! Please assign it in Inspector or ensure a child button contains 'Back'.");

        if (copyCodeButton == null)
            Debug.LogWarning("[HostGameUI] 'Copy Code Button' was not found! Please assign it in Inspector or ensure a child button is named 'ClipBoard' or contains 'Copy'.");

        if (hostButton == null)
            Debug.LogWarning("[HostGameUI] 'Host Button' was not found! Please assign it in Inspector or ensure a child button contains 'Host'.");

        if (roomCodeText == null)
            Debug.LogWarning("[HostGameUI] 'Room Code Text' (TMP) was not found! Please assign it in Inspector or ensure a child TMP text contains 'RoomCode' or 'Code'.");
    }
}
