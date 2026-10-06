using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

/// <summary>
/// Handles creating a Relay allocation, setting up a Lobby, and starting the NGO host.
/// Manages up to 4 players max.
/// </summary>
public class HostManager : MonoBehaviour
{
    private const int DefaultMaxPlayers = 4;
    private const string DefaultGameScene = "Game";
    private const string DefaultMainMenuScene = "Main Menu";

    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Fired when host is successfully created.
    /// Returns lobbyCode and relayJoinCode.
    /// </summary>
    public static event Action<string, string> OnHostCreated;

    public static HostManager Instance { get; private set; }

    private string _currentLobbyId;
    private string _currentLobbyCode;
    private string _currentRelayJoinCode;
    private float _allocationTime;
    private Coroutine _heartbeatCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ensure this GameObject is at root so DontDestroyOnLoad works without warnings
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        ResetHostData();
    }

    /// <summary>
    /// Ensures Unity Services are initialized and authenticated anonymously.
    /// </summary>
    public static async Task EnsureServicesInitializedAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    /// <summary>
    /// Creates a Relay allocation for up to maxPlayers (host + clients),
    /// configures UnityTransport with DTLS, optionally creates a UGS Lobby, and returns the raw join code.
    /// Does not start the NGO host yet so the UI can display the code first.
    /// </summary>
    public async Task<string> CreateRelayHostAllocationAsync(int maxPlayers = DefaultMaxPlayers, bool createLobby = true)
    {
        try
        {
            // Clear any prior stale host/lobby data first
            ResetHostData();

            await EnsureServicesInitializedAsync();

            if (debugLogs) Debug.Log($"[HostManager] Creating Relay allocation for {maxPlayers} players...");

            // maxConnections is the number of connecting clients (total players - 1)
            int maxConnections = Mathf.Max(1, maxPlayers - 1);
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            _currentRelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            _allocationTime = Time.realtimeSinceStartup;

            if (debugLogs) Debug.Log($"[HostManager] Relay allocation created. Join Code: {_currentRelayJoinCode}");

            SetupUnityTransportRelay(allocation);

            // Create Lobby if requested (enables lobby list discovery and fallback joining)
            if (createLobby)
            {
                try
                {
                    await CreateLobbyAsync(_currentRelayJoinCode);
                    if (!string.IsNullOrEmpty(_currentLobbyId))
                    {
                        StartLobbyHeartbeat(_currentLobbyId);
                    }
                }
                catch (Exception lobbyEx)
                {
                    Debug.LogWarning($"[HostManager] Lobby creation skipped/failed: {lobbyEx.Message}. Proceeding with Relay only.");
                }
            }

            return _currentRelayJoinCode;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Failed to create Relay allocation: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Checks if the current Relay allocation has been sitting unused for longer than maxAgeSeconds.
    /// Relay allocations can expire if left unhosted for a prolonged period.
    /// </summary>
    public bool IsAllocationStale(float maxAgeSeconds = 300f)
    {
        if (string.IsNullOrEmpty(_currentRelayJoinCode)) return true;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return false;
        return (Time.realtimeSinceStartup - _allocationTime) > maxAgeSeconds;
    }

    /// <summary>
    /// Resets all current host data, stops lobby heartbeats, and deletes any active lobby.
    /// Call when backing out of the host screen or closing the panel.
    /// </summary>
    public void ResetHostData()
    {
        StopLobbyHeartbeat();

        if (!string.IsNullOrEmpty(_currentLobbyId))
        {
            string lobbyToDelete = _currentLobbyId;
            _currentLobbyId = null;
            _currentLobbyCode = null;

            // Fire and forget delete so UI transition isn't blocked
            _ = Task.Run(async () =>
            {
                try
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyToDelete);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HostManager] Could not delete lobby {lobbyToDelete}: {ex.Message}");
                }
            });
        }

        _currentRelayJoinCode = null;
        _allocationTime = 0f;
    }

    private void StartLobbyHeartbeat(string lobbyId)
    {
        StopLobbyHeartbeat();
        _heartbeatCoroutine = StartCoroutine(LobbyHeartbeatCoroutine(lobbyId));
    }

    private void StopLobbyHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    private System.Collections.IEnumerator LobbyHeartbeatCoroutine(string lobbyId, float waitTimeSeconds = 15f)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (!string.IsNullOrEmpty(_currentLobbyId) && _currentLobbyId == lobbyId)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    /// <summary>
    /// Starts the NGO host and loads the target game scene using NetworkManager.SceneManager.
    /// </summary>
    public bool StartHostAndLoadGameScene(string sceneName = DefaultGameScene)
    {
        try
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[HostManager] NetworkManager.Singleton is null! Ensure NetworkManager is present in scene.");
                return false;
            }

            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
            {
                if (NetworkManager.Singleton.StartHost())
                {
                    if (debugLogs) Debug.Log("[HostManager] NGO Host started successfully");
                    OnHostCreated?.Invoke(_currentLobbyCode, _currentRelayJoinCode);

                    if (NetworkManager.Singleton.SceneManager != null)
                    {
                        if (debugLogs) Debug.Log($"[HostManager] Loading scene '{sceneName}' via NetworkManager.SceneManager...");
                        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                    }
                    else
                    {
                        Debug.LogWarning("[HostManager] NetworkManager.SceneManager is null. Falling back to SceneManager.LoadScene.");
                        SceneManager.LoadScene(sceneName);
                    }
                    return true;
                }
                else
                {
                    Debug.LogError("[HostManager] Failed to start NGO host");
                    return false;
                }
            }
            else
            {
                if (debugLogs) Debug.Log("[HostManager] NetworkManager is already running as host/client.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Error starting host and loading scene: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Shuts down NetworkManager if listening, then loads the main menu scene.
    /// </summary>
    public static void ShutdownAndReturnToMainMenu(string mainMenuScene = DefaultMainMenuScene)
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>
    /// Formats a raw join code into the XXX-XXX display pattern.
    /// </summary>
    public static string FormatJoinCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        string clean = code.Replace("-", "").Trim().ToUpperInvariant();

        if (clean.Length == 6)
        {
            return $"{clean.Substring(0, 3)}-{clean.Substring(3)}";
        }
        else if (clean.Length > 3)
        {
            int mid = clean.Length / 2;
            return $"{clean.Substring(0, mid)}-{clean.Substring(mid)}";
        }
        return clean;
    }

    /// <summary>
    /// Strips dashes and whitespace from join codes for joining.
    /// </summary>
    public static string SanitizeJoinCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        return code.Replace("-", "").Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Initializes the host by creating a Relay allocation and Lobby (legacy / full flow).
    /// </summary>
    public async Task InitializeHostAsync()
    {
        try
        {
            await EnsureServicesInitializedAsync();

            if (debugLogs) Debug.Log("[HostManager] Starting host initialization...");

            // Step 1: Create Relay allocation
            Allocation allocation = await CreateRelayAllocationAsync();
            _currentRelayJoinCode = await RelayService.Instance
                .GetJoinCodeAsync(allocation.AllocationId);

            if (debugLogs) Debug.Log($"[HostManager] Relay created. Join Code: {_currentRelayJoinCode}");

            // Step 2: Create Lobby with relay data
            await CreateLobbyAsync(_currentRelayJoinCode);

            if (debugLogs) Debug.Log($"[HostManager] Lobby created. Code: {_currentLobbyCode}");

            // Step 3: Setup UnityTransport with relay data (DTLS)
            SetupUnityTransportRelay(allocation);

            // Step 4: Start NGO host
            StartHostAndLoadGameScene(DefaultGameScene);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Error initializing host: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Creates a Relay allocation for multiplayer.
    /// </summary>
    private async Task<Allocation> CreateRelayAllocationAsync()
    {
        try
        {
            int maxConnections = Mathf.Max(1, DefaultMaxPlayers - 1);
            Allocation allocation = await RelayService.Instance
                .CreateAllocationAsync(maxConnections);
            return allocation;
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError($"[HostManager] Failed to create Relay allocation: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Creates a lobby with relay join code stored in data.
    /// </summary>
    private async Task CreateLobbyAsync(string relayJoinCode)
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "RelayJoinCode",
                        new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode)
                    }
                }
            };

            string playerId = GameManager.GetPlayerId();
            string lobbyName = string.IsNullOrEmpty(playerId) || playerId.Length < 6 
                ? "KartWars-Room" 
                : $"KartWars-{playerId[..6]}";

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                DefaultMaxPlayers,
                options
            );

            _currentLobbyId = lobby.Id;
            _currentLobbyCode = lobby.LobbyCode;
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError($"[HostManager] Failed to create lobby: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Configures UnityTransport with Relay connection data using DTLS.
    /// </summary>
    private void SetupUnityTransportRelay(Allocation allocation)
    {
        try
        {
            var transport = NetworkManagerHelper.EnsureTransport();

            if (transport == null)
                throw new Exception("UnityTransport could not be initialized or found on NetworkManager");

            // Convert allocation to RelayServerData with DTLS protocol
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            if (debugLogs) Debug.Log("[HostManager] UnityTransport configured with Relay data (dtls)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Failed to setup transport: {ex.Message}");
            throw;
        }
    }

    /// <summary>Returns current lobby code.</summary>
    public string GetLobbyCode() => _currentLobbyCode;

    /// <summary>Returns current relay join code.</summary>
    public string GetRelayJoinCode() => _currentRelayJoinCode;

    /// <summary>Returns current lobby ID.</summary>
    public string GetLobbyId() => _currentLobbyId;

    /// <summary>
    /// Locks the lobby (IsLocked = true) so players cannot join mid-match (Requirement 4).
    /// </summary>
    public async Task<bool> LockLobbyAsync()
    {
        if (string.IsNullOrEmpty(_currentLobbyId))
        {
            if (debugLogs) Debug.Log("[HostManager] No active lobby to lock.");
            return false;
        }

        try
        {
            var updateOptions = new UpdateLobbyOptions
            {
                IsLocked = true
            };
            await LobbyService.Instance.UpdateLobbyAsync(_currentLobbyId, updateOptions);
            Debug.Log("[HostManager] Lobby locked.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HostManager] Failed to lock lobby: {ex.Message}");
            return false;
        }
    }
}