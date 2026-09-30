using System;
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
/// Takes a lobby code, retrieves relay join code, and starts NGO client.
/// </summary>
public class ClientManager : MonoBehaviour
{
    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Fired when the client successfully connects to the host.
    /// </summary>
    public static event Action<string> OnClientConnected;

    private string _currentLobbyCode;
    private string _currentLobbyId;

    /// <summary>
    /// Joins a game using the provided lobby code.
    /// </summary>
    public async System.Threading.Tasks.Task JoinGameAsync(string lobbyCode)
    {
        try
        {
            if (!GameManager.IsAuthenticated())
                throw new Exception("Player is not authenticated. Ensure GameManager initialized first.");

            if (string.IsNullOrWhiteSpace(lobbyCode))
                throw new ArgumentException("Lobby code cannot be null or empty");

            if (debugLogs) Debug.Log($"[ClientManager] Attempting to join lobby: {lobbyCode}");

            // Step 1: Join lobby by code
            Lobby lobby = await JoinLobbyByCodeAsync(lobbyCode);
            _currentLobbyCode = lobby.LobbyCode;
            _currentLobbyId = lobby.Id;

            if (debugLogs) Debug.Log($"[ClientManager] Joined lobby ID: {lobby.Id}");

            // Step 2: Get relay join code from lobby data
            string relayJoinCode = GetRelayJoinCodeFromLobby(lobby);

            if (string.IsNullOrEmpty(relayJoinCode))
                throw new Exception("Relay join code not found in lobby data");

            if (debugLogs) Debug.Log($"[ClientManager] Got relay code: {relayJoinCode}");

            // Step 3: Setup UnityTransport with relay
            await SetupUnityTransportRelayAsync(relayJoinCode);

            // Step 4: Start NGO client
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
            {
                if (NetworkManager.Singleton.StartClient())
                {
                    if (debugLogs) Debug.Log("[ClientManager] NGO Client started successfully");
                    OnClientConnected?.Invoke(_currentLobbyCode);
                }
                else
                {
                    throw new Exception("Failed to start NGO client");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientManager] Error joining game: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Joins a lobby using its code.
    /// </summary>
    private async System.Threading.Tasks.Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
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
    /// Configures UnityTransport with Relay join allocation.
    /// </summary>
    private async System.Threading.Tasks.Task SetupUnityTransportRelayAsync(string relayJoinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance
                .JoinAllocationAsync(relayJoinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager");

            transport.SetRelayServerData(new RelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                true
            ));

            if (debugLogs) Debug.Log("[ClientManager] Relay transport configured successfully");
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