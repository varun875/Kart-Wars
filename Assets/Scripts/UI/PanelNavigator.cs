using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls panel navigation (Main, Host, Join) in the host/join menu scene.
/// Host Panel buttons and Relay lifecycle are managed exclusively by HostGameUI.
/// </summary>
public class PanelNavigator : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject overlay;
    public GameObject hostPanel;
    public GameObject joinPanel;

    [Header("Host Panel UI (Managed by HostGameUI)")]
    public TextMeshProUGUI roomCodeText;
    public Button startHostButton; // Kept for serialized inspector compatibility
    public Button copyCodeButton;

    [Header("Join Panel UI")]
    public TMP_InputField roomCodeInput;
    public Button connectButton;
    public TextMeshProUGUI statusText;

    [Header("Managers")]
    public HostManager hostManager;
    public ClientManager clientManager;

    private TextMeshProUGUI _connectButtonLabel;
    private string _originalConnectButtonText = "Join Game";
    private Coroutine _connectingAnimationCoroutine;

    private void Awake()
    {
        NetworkManagerHelper.EnsureTransport();
        EnsureHostGameUI();
        EnsureJoinUIReferences();
    }

    private void Start()
    {
        ShowMain();

        WireMainPanelButtons();
        WireHostPanelButtons();
        WireJoinPanelButtons();

        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectClicked);

        ClientManager.OnClientConnected += OnClientConnected;
        HookNetworkManagerCallbacks();
    }

    private void WireMainPanelButtons()
    {
        if (mainPanel == null) return;
        Button[] buttons = mainPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name.ToLowerInvariant();
            if (bName.Contains("host"))
            {
                btn.onClick.AddListener(ShowHost);
            }
            else if (bName.Contains("join"))
            {
                btn.onClick.AddListener(ShowJoin);
            }
        }
    }

    private void WireHostPanelButtons()
    {
        if (hostPanel == null) return;

        // Auto-detect copy code button if not explicitly assigned
        if (copyCodeButton == null)
        {
            Button[] buttons = hostPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string bName = btn.gameObject.name.ToLowerInvariant();
                if (bName.Contains("copy") || bName.Contains("clipboard"))
                {
                    copyCodeButton = btn;
                    break;
                }
            }
        }

        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.RemoveListener(CopyRoomCode);
            copyCodeButton.onClick.AddListener(CopyRoomCode);
        }
    }

    private void WireJoinPanelButtons()
    {
        if (joinPanel == null) return;
        Button[] buttons = joinPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name.ToLowerInvariant();
            if (bName.Contains("back"))
            {
                btn.onClick.AddListener(ShowMain);
            }
        }
    }

    /// <summary>
    /// Copies the active room join code to the player's clipboard.
    /// Provides immediate visual feedback by updating button text.
    /// </summary>
    public void CopyRoomCode()
    {
        string code = hostManager != null ? hostManager.GetRelayJoinCode() : null;

        if (string.IsNullOrEmpty(code) && roomCodeText != null)
        {
            string displayed = roomCodeText.text.Trim();
            if (!string.IsNullOrEmpty(displayed) && !displayed.Equals("GENERATING...", StringComparison.OrdinalIgnoreCase))
            {
                code = HostManager.SanitizeJoinCode(displayed);
            }
        }

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[PanelNavigator] No room code available to copy yet!");
            SetStatus("No code available to copy yet!", Color.yellow);
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        Debug.Log($"[PanelNavigator] Copied join code '{code}' to system clipboard.");
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

    private void OnDestroy()
    {
        if (connectButton != null)
            connectButton.onClick.RemoveListener(OnConnectClicked);

        ClientManager.OnClientConnected -= OnClientConnected;
        UnhookNetworkManagerCallbacks();
    }

    private void HookNetworkManagerCallbacks()
    {
        UnhookNetworkManagerCallbacks();
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkClientConnected;
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkClientDisconnected;
        }
    }

    private void UnhookNetworkManagerCallbacks()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
        }
    }

    private void OnNetworkClientConnected(ulong clientId)
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId)
        {
            StopConnectingAnimation();
            if (_connectButtonLabel != null) _connectButtonLabel.text = "CONNECTED!";
            SetStatus("Connected! Loading match...", Color.green);
        }
    }

    private void OnNetworkClientDisconnected(ulong clientId)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        if (clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId || 
            clientId == Unity.Netcode.NetworkManager.ServerClientId)
        {
            StopConnectingAnimation();
            RestoreConnectButton();

            string reason = Unity.Netcode.NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                reason = "Host disconnected or room does not exist.";
            }

            SetStatus($"Connection failed: {reason}", Color.red);
        }
    }

    /// <summary>
    /// Ensures JoinPanel UI references are hooked and creates or discovers statusText if missing.
    /// </summary>
    private void EnsureJoinUIReferences()
    {
        if (connectButton != null)
        {
            _connectButtonLabel = connectButton.GetComponentInChildren<TextMeshProUGUI>();
            if (_connectButtonLabel != null && !string.IsNullOrEmpty(_connectButtonLabel.text))
            {
                _originalConnectButtonText = _connectButtonLabel.text;
            }
        }

        if (statusText != null) return;

        if (joinPanel != null)
        {
            var tmps = joinPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                string tName = t.gameObject.name.ToLowerInvariant();
                if (tName.Contains("status") || tName.Contains("info") || tName.Contains("message"))
                {
                    statusText = t;
                    return;
                }
            }

            // Dynamically instantiate JoinStatusText under joinPanel
            GameObject statusObj = new GameObject("JoinStatusText");
            statusObj.transform.SetParent(joinPanel.transform, false);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 26;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.yellow;
            statusText.text = "";

            if (_connectButtonLabel != null && _connectButtonLabel.fontAsset != null)
            {
                statusText.fontAsset = _connectButtonLabel.fontAsset;
            }

            RectTransform rt = statusText.rectTransform;
            if (connectButton != null)
            {
                RectTransform btnRt = connectButton.GetComponent<RectTransform>();
                rt.anchorMin = btnRt.anchorMin;
                rt.anchorMax = btnRt.anchorMax;
                rt.pivot = btnRt.pivot;
                rt.anchoredPosition = new Vector2(btnRt.anchoredPosition.x, btnRt.anchoredPosition.y - 70f);
                rt.sizeDelta = new Vector2(500f, 50f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -120f);
                rt.sizeDelta = new Vector2(500f, 50f);
            }
        }
    }

    /// <summary>
    /// Ensures HostPanel has HostGameUI attached so that HostGameUI exclusively
    /// owns all Host panel buttons (Back, Copy Code, Host, and RoomCode text).
    /// </summary>
    private void EnsureHostGameUI()
    {
        if (hostPanel != null)
        {
            HostGameUI hostUI = hostPanel.GetComponent<HostGameUI>();
            if (hostUI == null)
            {
                hostPanel.AddComponent<HostGameUI>();
            }
        }
    }

    // ── PANEL NAVIGATION ─────────────────────────

    public void ShowMain()
    {
        StopConnectingAnimation();
        RestoreConnectButton();
        if (overlay != null) overlay.SetActive(false);
        if (hostPanel != null) hostPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
    }

    public void ShowHost()
    {
        StopConnectingAnimation();
        RestoreConnectButton();
        if (overlay != null) overlay.SetActive(true);
        if (joinPanel != null) joinPanel.SetActive(false);

        // Activating hostPanel triggers HostGameUI.OnEnable() to allocate Relay and wire buttons
        if (hostPanel != null)
        {
            EnsureHostGameUI();
            hostPanel.SetActive(true);
        }
    }

    public void ShowJoin()
    {
        EnsureJoinUIReferences();
        StopConnectingAnimation();
        RestoreConnectButton();
        SetStatus("", Color.white);

        if (overlay != null) overlay.SetActive(true);
        if (hostPanel != null) hostPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(true);
    }

    // ── JOIN FLOW ────────────────────────────────

    public async void OnConnectClicked()
    {
        if (_connectingAnimationCoroutine != null) return;

        EnsureJoinUIReferences();

        if (clientManager == null)
        {
            clientManager = FindFirstObjectByType<ClientManager>();
            if (clientManager == null)
                clientManager = gameObject.AddComponent<ClientManager>();
        }

        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a room code!", Color.red);
            return;
        }

        string cleanCode = HostManager.SanitizeJoinCode(code);

        HookNetworkManagerCallbacks();
        StartConnectingAnimation(cleanCode);

        try
        {
            await clientManager.JoinGameAsync(cleanCode);
        }
        catch (Exception e)
        {
            StopConnectingAnimation();
            RestoreConnectButton();
            SetStatus($"Join failed: {e.Message}", Color.red);
        }
    }

    private void OnClientConnected(string code)
    {
        SetStatus("Connected! Loading game...", Color.green);
        if (_connectButtonLabel != null) _connectButtonLabel.text = "CONNECTED!";
    }

    private void StartConnectingAnimation(string code)
    {
        StopConnectingAnimation();
        if (connectButton != null) connectButton.interactable = false;
        _connectingAnimationCoroutine = StartCoroutine(ConnectingAnimationRoutine(code));
    }

    private void StopConnectingAnimation()
    {
        if (_connectingAnimationCoroutine != null)
        {
            StopCoroutine(_connectingAnimationCoroutine);
            _connectingAnimationCoroutine = null;
        }
    }

    private void RestoreConnectButton()
    {
        if (connectButton != null) connectButton.interactable = true;
        if (_connectButtonLabel != null && !string.IsNullOrEmpty(_originalConnectButtonText))
        {
            _connectButtonLabel.text = _originalConnectButtonText;
        }
    }

    private System.Collections.IEnumerator ConnectingAnimationRoutine(string code)
    {
        string[] dots = new string[] { "", ".", "..", "..." };
        int idx = 0;
        float elapsed = 0f;

        while (true)
        {
            string currentDots = dots[idx];
            if (_connectButtonLabel != null)
            {
                _connectButtonLabel.text = $"CONNECTING{currentDots}";
            }

            SetStatus($"Connecting to {code}{currentDots}", Color.yellow);

            idx = (idx + 1) % dots.Length;
            elapsed += 0.35f;

            if (elapsed > 20f)
            {
                StopConnectingAnimation();
                RestoreConnectButton();
                SetStatus("Connection timed out. Check room code or host status.", Color.red);
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.35f);
        }
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