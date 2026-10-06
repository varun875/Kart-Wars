using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

/// <summary>
/// Handles joining a game via Relay and Lobby.
/// Takes a lobby code or relay join code, retrieves relay data, and starts NGO client.
/// </summary>
public class ClientManager : MonoBehaviour
{
    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Fired when the client successfully connects to the host.
    /// </summary>
    public static event Action<string> OnClientConnected;

    /// <summary>
    /// The last room / relay code used to join a game (sanitized).
    /// </summary>
    public static string LastJoinedRoomCode { get; private set; }

    /// <summary>
    /// Message explaining why the client was disconnected (shown on Main Menu).
    /// </summary>
    public static string DisconnectReason { get; set; }

    private string _currentLobbyCode;
    private string _currentLobbyId;

    /// <summary>
    /// Joins a game using the provided room code (strips dashes like XXX-XXX).
    /// Supports both direct Relay join codes (6 alphanumeric chars) and Lobby codes.
    /// </summary>
    public async Task JoinGameAsync(string roomCode)
    {
        try
        {
            await HostManager.EnsureServicesInitializedAsync();

            if (string.IsNullOrWhiteSpace(roomCode))
                throw new ArgumentException("Room code cannot be null or empty");

            // Strip dash and clean formatting (Requirement 6: strip dash when joining)
            string cleanCode = roomCode.Replace("-", "").Trim().ToUpperInvariant();
            LastJoinedRoomCode = cleanCode;

            if (debugLogs) Debug.Log($"[ClientManager] Attempting to join with sanitized code: {cleanCode}");

            // If it's a 6-character code, try direct Relay join first
            if (cleanCode.Length == 6)
            {
                try
                {
                    if (debugLogs) Debug.Log($"[ClientManager] Attempting direct Relay connection with code: {cleanCode}");
                    await JoinRelayGameAsync(cleanCode);
                    return;
                }
                catch (Exception relayEx)
                {
                    if (debugLogs) Debug.LogWarning($"[ClientManager] Direct relay join failed ({relayEx.Message}), trying Lobby fallback...");
                }
            }

            // Fallback: Join lobby by code
            Lobby lobby = await JoinLobbyByCodeAsync(cleanCode);
            _currentLobbyCode = lobby.LobbyCode;
            _currentLobbyId = lobby.Id;

            if (debugLogs) Debug.Log($"[ClientManager] Joined lobby ID: {lobby.Id}");

            // Get relay join code from lobby data
            string relayJoinCode = GetRelayJoinCodeFromLobby(lobby);

            if (string.IsNullOrEmpty(relayJoinCode))
                throw new Exception("Relay join code not found in lobby data");

            if (debugLogs) Debug.Log($"[ClientManager] Got relay code from lobby: {relayJoinCode}");

            // Setup UnityTransport with relay (DTLS)
            await SetupUnityTransportRelayAsync(relayJoinCode);

            // Start NGO client
            StartClientConnection(cleanCode);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientManager] Error joining game: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Directly connects to a Relay host using the given 6-character join code.
    /// Strips any dashes present.
    /// </summary>
    public async Task JoinRelayGameAsync(string relayJoinCode)
    {
        try
        {
            await HostManager.EnsureServicesInitializedAsync();

            if (string.IsNullOrWhiteSpace(relayJoinCode))
                throw new ArgumentException("Relay join code cannot be null or empty");

            string cleanCode = relayJoinCode.Replace("-", "").Trim().ToUpperInvariant();
            LastJoinedRoomCode = cleanCode;

            await SetupUnityTransportRelayAsync(cleanCode);
            StartClientConnection(cleanCode);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientManager] Failed to join Relay game: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Starts the NGO client connection if not already running.
    /// </summary>
    private void StartClientConnection(string code)
    {
        if (NetworkManager.Singleton == null)
            throw new Exception("NetworkManager.Singleton is null");

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            if (NetworkManager.Singleton.StartClient())
            {
                if (debugLogs) Debug.Log("[ClientManager] NGO Client started successfully");
                OnClientConnected?.Invoke(code);
            }
            else
            {
                throw new Exception("Failed to start NGO client");
            }
        }
    }

    /// <summary>
    /// Joins a lobby using its code.
    /// </summary>
    private async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
    {
        try
        {
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            return lobby;
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError($"[ClientManager] Failed to join lobby: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Extracts relay join code from lobby data.
    /// </summary>
    private string GetRelayJoinCodeFromLobby(Lobby lobby)
    {
        try
        {
            if (lobby?.Data != null && lobby.Data.ContainsKey("RelayJoinCode"))
                return lobby.Data["RelayJoinCode"].Value;

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientManager] Error retrieving relay code: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Configures UnityTransport with Relay join allocation using DTLS.
    /// </summary>
    private async Task SetupUnityTransportRelayAsync(string relayJoinCode)
    {
        try
        {
            string cleanCode = relayJoinCode.Replace("-", "").Trim().ToUpperInvariant();

            JoinAllocation joinAllocation = await RelayService.Instance
                .JoinAllocationAsync(cleanCode);

            var transport = NetworkManagerHelper.EnsureTransport();

            if (transport == null)
                throw new Exception("UnityTransport could not be initialized or found on NetworkManager");

            // Convert join allocation to RelayServerData with DTLS protocol
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            if (debugLogs) Debug.Log("[ClientManager] Relay transport configured successfully (dtls)");
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError($"[ClientManager] Relay join failed: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientManager] Transport setup failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Returns current lobby code.
    /// </summary>
    public string GetLobbyCode() => _currentLobbyCode;

    /// <summary>
    /// Returns current lobby ID.
    /// </summary>
    public string GetLobbyId() => _currentLobbyId;
}