using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// NGO host/join UI flow with room code system.
/// Works with HostManager and ClientManager.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject connectingPanel;

    [Header("UI Elements")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI roomCodeDisplay;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button cancelButton;

    [Header("Managers")]
    [SerializeField] private HostManager hostManager;
    [SerializeField] private ClientManager clientManager;

    private string playerName = "Player";

    private void Start()
    {
        ShowMenuPanel();

        if (playerNameInput != null)
        {
            playerNameInput.text = "Racer" + Random.Range(1, 1000);
            playerNameInput.onValueChanged.AddListener(OnPlayerNameChanged);
        }

        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostClicked);
        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        HostManager.OnHostCreated += OnHostCreated;
        ClientManager.OnClientConnected += OnClientConnected;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedCallback;
        }
    }

    private void OnDestroy()
    {
        HostManager.OnHostCreated -= OnHostCreated;
        ClientManager.OnClientConnected -= OnClientConnected;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedCallback;
        }
    }

    private void OnPlayerNameChanged(string newName)
    {
        playerName = string.IsNullOrEmpty(newName) ? "Player" : newName;
    }

    // ── HOST ─────────────────────────────────────

    public async void OnHostClicked()
    {
        if (hostManager == null)
        {
            SetStatus("HostManager not assigned!", Color.red);
            return;
        }

        ShowConnectingPanel("Creating room...");

        try
        {
            await hostManager.InitializeHostAsync();
        }
        catch (System.Exception e)
        {
            SetStatus($"Host failed: {e.Message}", Color.red);
            ShowMenuPanel();
        }
    }

    private void OnHostCreated(string lobbyCode, string relayJoinCode)
    {
        if (roomCodeDisplay != null)
            roomCodeDisplay.text = $"Room Code: {lobbyCode}";

        SetStatus("Hosting! Share your room code.", Color.green);

        if (connectingPanel != null) connectingPanel.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // ── JOIN ─────────────────────────────────────

    public async void OnJoinClicked()
    {
        if (clientManager == null)
        {
            SetStatus("ClientManager not assigned!", Color.red);
            return;
        }

        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a room code!", Color.red);
            return;
        }

        ShowConnectingPanel($"Joining room {code}...");

        try
        {
            await clientManager.JoinGameAsync(code);
        }
        catch (System.Exception e)
        {
            SetStatus($"Join failed: {e.Message}", Color.red);
            ShowMenuPanel();
        }
    }

    private void OnClientConnected(string lobbyCode)
    {
        SetStatus("Connected! Loading game...", Color.green);
    }

    // ── CANCEL ───────────────────────────────────

    public void OnCancelClicked()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        ShowMenuPanel();
        SetStatus("Disconnected", Color.white);
    }

    // ── NGO CALLBACKS ────────────────────────────

    private void OnClientConnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SetStatus(
                $"Player connected. Total: {NetworkManager.Singleton.ConnectedClients.Count}",
                Color.green
            );
        }
    }

    private void OnClientDisconnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            ShowMenuPanel();
            SetStatus("Disconnected from server", Color.red);
        }
        else
        {
            SetStatus(
                $"Player disconnected. Total: {NetworkManager.Singleton.ConnectedClients.Count}",
                Color.yellow
            );
        }
    }

    // ── UI HELPERS ───────────────────────────────

    private void ShowMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (connectingPanel != null) connectingPanel.SetActive(false);
    }

    private void ShowConnectingPanel(string message)
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (connectingPanel != null) connectingPanel.SetActive(true);
        SetStatus(message, Color.yellow);
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"[MainMenuManager] {message}");
    }

    private void SetStatus(string message) => SetStatus(message, Color.white);

    private void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
    }
}