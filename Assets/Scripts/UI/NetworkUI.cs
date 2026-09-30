using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Controls the networking UI including Main Menu, Lobby, and player count display.
/// Manages host creation, client joining, and lobby interaction buttons.
/// </summary>
public class NetworkUI : MonoBehaviour
{
    [Header("UI Panel References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Main Menu UI Elements")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Lobby UI Elements")]
    [SerializeField] private TextMeshProUGUI lobbyCodeDisplay;
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private Button submitJoinButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI playerCountDisplay;
    [SerializeField] private TextMeshProUGUI playerListDisplay;

    [Header("Dependencies")]
    [SerializeField] private HostManager hostManager;
    [SerializeField] private ClientManager clientManager;
    [SerializeField] private LobbyManager lobbyManager;

    [SerializeField] private bool debugLogs = true;

    private void OnEnable()
    {
        HostManager.OnHostCreated += HandleHostCreated;
        ClientManager.OnClientConnected += HandleClientConnected;
        LobbyManager.OnPlayerCountChanged += HandlePlayerCountChanged;
        LobbyManager.OnLobbyReady += HandleLobbyReady;
    }

    private void OnDisable()
    {
        HostManager.OnHostCreated -= HandleHostCreated;
        ClientManager.OnClientConnected -= HandleClientConnected;
        LobbyManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
        LobbyManager.OnLobbyReady -= HandleLobbyReady;
    }

    private void Start()
    {
        InitializeUI();
    }

    /// <summary>
    /// Initializes all UI elements and event listeners.
    /// </summary>
    private void InitializeUI()
    {
        try
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostButtonClicked);

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinButtonClicked);

            if (submitJoinButton != null)
                submitJoinButton.onClick.AddListener(OnSubmitJoinClicked);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveButtonClicked);

            ShowMainMenu();

            if (debugLogs) Debug.Log("[NetworkUI] UI initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error initializing UI: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles host button click - initiates hosting.
    /// </summary>
    private async void OnHostButtonClicked()
    {
        try
        {
            if (hostManager == null)
                throw new InvalidOperationException("HostManager reference is missing");

            if (debugLogs) Debug.Log("[NetworkUI] Host button clicked");

            UpdateStatusText("Creating host...");
            hostButton.interactable = false;
            joinButton.interactable = false;

            await hostManager.InitializeHostAsync();

            if (debugLogs) Debug.Log("[NetworkUI] Host creation initiated");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error creating host: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");
            hostButton.interactable = true;
            joinButton.interactable = true;
        }
    }

    /// <summary>
    /// Handles join button click - shows join input panel.
    /// </summary>
    private void OnJoinButtonClicked()
    {
        try
        {
            if (debugLogs) Debug.Log("[NetworkUI] Join button clicked");
            ShowJoinPanel();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error showing join panel: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles submit join button click - attempts to join lobby.
    /// </summary>
    private async void OnSubmitJoinClicked()
    {
        try
        {
            if (clientManager == null)
                throw new InvalidOperationException("ClientManager reference is missing");

            string lobbyCode = joinCodeInput.text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                UpdateStatusText("Please enter a lobby code");
                return;
            }

            if (debugLogs) Debug.Log($"[NetworkUI] Joining lobby with code: {lobbyCode}");

            UpdateStatusText("Joining lobby...");
            submitJoinButton.interactable = false;

            await clientManager.JoinGameAsync(lobbyCode);

            if (debugLogs) Debug.Log("[NetworkUI] Client join initiated");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error joining lobby: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");
            submitJoinButton.interactable = true;
        }
    }

    /// <summary>
    /// Handles leave button click - disconnects from lobby.
    /// </summary>
    private void OnLeaveButtonClicked()
    {
        try
        {
            if (debugLogs) Debug.Log("[NetworkUI] Leave button clicked");

            if (leaveButton != null)
                leaveButton.interactable = false;

            UpdateStatusText("Leaving lobby...");
            DisconnectFromNetwork();
            HandleLobbyDisconnected();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error leaving lobby: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");

            if (leaveButton != null)
                leaveButton.interactable = true;
        }
    }

    private void DisconnectFromNetwork()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            if (debugLogs) Debug.Log("[NetworkUI] Network shutdown complete");
        }
    }

    /// <summary>
    /// Shows the main menu panel and hides lobby panel.
    /// </summary>
    private void ShowMainMenu()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (hostButton != null)
            hostButton.interactable = true;

        if (joinButton != null)
            joinButton.interactable = true;
    }

    /// <summary>
    /// Shows the join code input panel.
    /// </summary>
    private void ShowJoinPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);

        if (lobbyCodeDisplay != null)
            lobbyCodeDisplay.text = "Enter Code to Join:";

        if (joinCodeInput != null)
        {
            joinCodeInput.text = string.Empty;
            joinCodeInput.ActivateInputField();
        }

        if (submitJoinButton != null)
            submitJoinButton.interactable = true;

        RefreshLobbyDisplay(0);
    }

    /// <summary>
    /// Shows the lobby panel when host or client successfully connects.
    /// </summary>
    private void ShowLobbyPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);

        if (leaveButton != null)
            leaveButton.interactable = true;
    }

    /// <summary>
    /// Handles the OnHostCreated event.
    /// </summary>
    private void HandleHostCreated(string lobbyCode, string relayJoinCode)
    {
        try
        {
            if (debugLogs) Debug.Log($"[NetworkUI] Host created. Lobby Code: {lobbyCode}");

            ShowLobbyPanel();

            if (lobbyCodeDisplay != null)
                lobbyCodeDisplay.text = $"Lobby Code: <color=yellow>{lobbyCode}</color>";

            RefreshLobbyDisplay();
            UpdateStatusText("Hosting. Waiting for players...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error handling host created: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the OnClientConnected event.
    /// </summary>
    private void HandleClientConnected(string lobbyCode)
    {
        try
        {
            if (debugLogs) Debug.Log($"[NetworkUI] Client connected to lobby: {lobbyCode}");

            ShowLobbyPanel();

            if (lobbyCodeDisplay != null)
                lobbyCodeDisplay.text = $"Connected to: <color=yellow>{lobbyCode}</color>";

            RefreshLobbyDisplay();
            UpdateStatusText("Connected to lobby. Waiting for game...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error handling client connected: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles lobby player-count updates from LobbyManager.
    /// </summary>
    private void HandlePlayerCountChanged(int playerCount)
    {
        try
        {
            RefreshLobbyDisplay(playerCount);
            if (debugLogs) Debug.Log($"[NetworkUI] Lobby updated. Players: {playerCount}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error updating lobby display: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles lobby full/ready state from LobbyManager.
    /// </summary>
    private void HandleLobbyReady()
    {
        UpdateStatusText("Lobby is full. Ready to start!");
    }

    private void RefreshLobbyDisplay(int? knownPlayerCount = null)
    {
        int playerCount = knownPlayerCount ?? (lobbyManager != null ? lobbyManager.GetPlayerCount() : 0);
        int maxPlayers = lobbyManager != null ? lobbyManager.maxPlayers : 4;

        if (playerCountDisplay != null)
            playerCountDisplay.text = $"Players: {playerCount}/{maxPlayers}";

        if (playerListDisplay == null)
            return;

        string playerList = "Connected Players:\n";
        if (lobbyManager == null)
        {
            playerListDisplay.text = playerList + "- Waiting for players...";
            return;
        }

        var playerNames = lobbyManager.GetPlayerNames();
        if (playerNames.Count == 0)
        {
            playerListDisplay.text = playerList + "- Waiting for players...";
            return;
        }

        foreach (var playerName in playerNames)
        {
            playerList += $"- {playerName}\n";
        }

        playerListDisplay.text = playerList;
    }

    /// <summary>
    /// Handles disconnection from lobby.
    /// </summary>
    private void HandleLobbyDisconnected()
    {
        try
        {
            if (debugLogs) Debug.Log("[NetworkUI] Disconnected from lobby");

            ShowMainMenu();
            RefreshLobbyDisplay(0);
            UpdateStatusText("Disconnected from lobby");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkUI] Error handling lobby disconnected: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the status text display.
    /// </summary>
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            if (debugLogs) Debug.Log($"[NetworkUI] Status: {message}");
        }
    }
}
