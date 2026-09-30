using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelNavigator : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject overlay;
    public GameObject hostPanel;
    public GameObject joinPanel;

    [Header("Host Panel UI")]
    public TextMeshProUGUI roomCodeText;
    public Button startHostButton;

    [Header("Join Panel UI")]
    public TMP_InputField roomCodeInput;
    public Button connectButton;
    public TextMeshProUGUI statusText;

    [Header("Managers")]
    public HostManager hostManager;
    public ClientManager clientManager;

    void Start()
    {
        ShowMain();

        startHostButton.onClick.AddListener(OnStartHostClicked);
        connectButton.onClick.AddListener(OnConnectClicked);

        HostManager.OnHostCreated += OnHostCreated;
        ClientManager.OnClientConnected += OnClientConnected;
    }

    void OnDestroy()
    {
        HostManager.OnHostCreated -= OnHostCreated;
        ClientManager.OnClientConnected -= OnClientConnected;
    }

    // ── PANEL NAVIGATION ─────────────────────────

    public void ShowMain()
    {
        overlay.SetActive(false);
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
    }

    public void ShowHost()
    {
        overlay.SetActive(true);
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);
    }

    public void ShowJoin()
    {
        overlay.SetActive(true);
        hostPanel.SetActive(false);
        joinPanel.SetActive(true);
    }

    // ── HOST ─────────────────────────────────────

    public async void OnStartHostClicked()
    {
        if (hostManager == null)
        {
            SetStatus("HostManager not assigned!", Color.red);
            return;
        }

        SetStatus("Creating room...", Color.yellow);

        try
        {
            await hostManager.InitializeHostAsync();
        }
        catch (System.Exception e)
        {
            SetStatus($"Failed: {e.Message}", Color.red);
        }
    }

    private void OnHostCreated(string lobbyCode, string relayCode)
    {
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code:\n{lobbyCode}";

        SetStatus("Hosting! Share your code.", Color.green);
    }

    // ── JOIN ─────────────────────────────────────

    public async void OnConnectClicked()
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

        SetStatus("Connecting...", Color.yellow);

        try
        {
            await clientManager.JoinGameAsync(code);
        }
        catch (System.Exception e)
        {
            SetStatus($"Failed: {e.Message}", Color.red);
        }
    }

    private void OnClientConnected(string lobbyCode)
    {
        SetStatus("Connected! Loading game...", Color.green);
    }

    // ── HELPERS ──────────────────────────────────

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"[PanelNavigator] {message}");
    }
}