using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Mirror host/join UI flow with status text and IP input.
/// </summary>
public class MainMenuManager : NetworkManager
{
    [Header("UI Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject connectingPanel;

    [Header("UI Elements")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button cancelButton;

    [Header("Settings")]
    [SerializeField] private string defaultIP = "localhost";
    [SerializeField] private string gameSceneName = "GameScene";

    private string playerName = "Player";

    private new void Start()
    {
        ShowMenuPanel();

        if (ipAddressInput != null)
        {
            ipAddressInput.text = defaultIP;
        }

        if (playerNameInput != null)
        {
            playerNameInput.text = "Player" + Random.Range(1, 1000);
            playerNameInput.onValueChanged.AddListener(OnPlayerNameChanged);
        }

        // Button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(HostGame);
        if (joinButton != null)
            joinButton.onClick.AddListener(JoinGame);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelConnection);
    }

    private void OnPlayerNameChanged(string newName)
    {
        playerName = string.IsNullOrEmpty(newName) ? "Player" : newName;
    }

    /// <summary>
    /// Host a new game
    /// </summary>
    public void HostGame()
    {
        ShowConnectingPanel("Starting server...");

        try
        {
            StartHost();
            SetStatus("Server started. Waiting for players...");
        }
        catch (System.Exception e)
        {
            SetStatus($"Failed to start server: {e.Message}");
            ShowMenuPanel();
        }
    }

    /// <summary>
    /// Join an existing game
    /// </summary>
    public void JoinGame()
    {
        string ip = ipAddressInput != null ? ipAddressInput.text : defaultIP;
        
        if (string.IsNullOrEmpty(ip))
        {
            SetStatus("Please enter an IP address");
            return;
        }

        ShowConnectingPanel($"Connecting to {ip}...");
        networkAddress = ip;

        try
        {
            StartClient();
        }
        catch (System.Exception e)
        {
            SetStatus($"Failed to connect: {e.Message}");
            ShowMenuPanel();
        }
    }

    /// <summary>
    /// Cancel current connection attempt
    /// </summary>
    public void CancelConnection()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            StopClient();
        }
        else if (NetworkServer.active)
        {
            StopServer();
        }

        ShowMenuPanel();
        SetStatus("Disconnected");
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        SetStatus($"Player connected. Total: {NetworkServer.connections.Count}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        SetStatus($"Player disconnected. Total: {NetworkServer.connections.Count}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        SetStatus("Connected to server!");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        ShowMenuPanel();
        SetStatus("Disconnected from server");
    }

    public override void OnClientError(TransportError error, string reason)
    {
        base.OnClientError(error, reason);
        ShowMenuPanel();
        SetStatus($"Connection error: {reason}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Spawn player at a random spawn point
        Transform startPos = GetStartPosition();
        GameObject player = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        NetworkServer.AddPlayerForConnection(conn, player);

        // Register player with game master
        if (MirrorGameMaster.Instance != null)
        {
            MirrorGameMaster.Instance.RegisterPlayer(player.GetComponent<NetworkIdentity>(), playerName);
        }
    }

    private void ShowMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (connectingPanel != null) connectingPanel.SetActive(false);
    }

    private void ShowConnectingPanel(string message)
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (connectingPanel != null) connectingPanel.SetActive(true);
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[MainMenuManager] {message}");
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        
        if (NetworkServer.active)
        {
            StopHost();
        }
    }
}
